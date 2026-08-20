#include "host.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <unistd.h>
#include <wayland-server-core.h>

#include "xdg-shell-server-protocol.h"
#include "xdg-decoration-server-protocol.h"

// Versions the player actually needs. Each is a measured floor, not a guess.
//
//   wl_compositor 4  SDL binds min(3, version) and calls set_buffer_scale, a v3 request.
//                    libdecor, if it ever loads, calls damage_buffer, which is v4. Higher
//                    would oblige us to implement wl_surface.offset.
//   wl_output     2  SDL binds this at a hard-coded 2 with no clamp, and builds its whole
//                    display list from the v2-only done event. Advertise 1 and the player
//                    dies with "invalid version for global wl_output".
//   wl_seat       5  SDL binds min(5, version). The global must exist even with no
//                    capabilities: without it the player segfaults inside SDL_Init, before
//                    it has printed anything at all.
//   xdg_wm_base   3  SDL binds min(3, version). Its Wayland backend has no wl_shell
//                    fallback, so this is the only way a window gets a role.
#define HOST_COMPOSITOR_VERSION 4
#define HOST_OUTPUT_VERSION     2
#define HOST_SEAT_VERSION       5
#define HOST_XDG_VERSION        3

static struct {
    struct wl_display    *display;
    struct wl_event_loop *loop;
    const char           *socket;
    int32_t               out_w, out_h;
    int                   clients;

    // The player's window, once it has made one. There is only ever one.
    struct wl_resource *surface;
    struct wl_resource *xdg_surface;
    struct wl_resource *xdg_toplevel;
    struct wl_resource *pending_buffer;   // attached, not yet committed
    struct wl_resource *frame_callback;   // owed a done event at the next commit
    unsigned            commits;

    // The latest frame, copied out of the player's buffer so the buffer can go straight
    // back. Keeping the client's buffer instead would stall it on its own pool.
    uint8_t  *frame;
    size_t    frame_cap;
    int32_t   fw, fh;
    uint32_t  fstride;
    unsigned  fseq;

    struct wl_list pointers;    // wl_pointer resources
    struct wl_list keyboards;   // wl_keyboard resources
    bool           pointer_in;  // the pointer is currently over the player's surface
    bool           keyboard_in;
    int            keymap_fd;
    uint32_t       keymap_size, keymap_format;
} H;

static uint32_t now_ms(void)
{
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint32_t)(ts.tv_sec * 1000 + ts.tv_nsec / 1000000);
}

// ------------------------------------------------------------------ wl_surface

static void surface_destroy(struct wl_client *c, struct wl_resource *r)
{ (void)c; wl_resource_destroy(r); }

static void surface_attach(struct wl_client *c, struct wl_resource *r,
                           struct wl_resource *buffer, int32_t x, int32_t y)
{
    (void)c; (void)r; (void)x; (void)y;
    H.pending_buffer = buffer;   // double-buffered state: nothing happens until commit
}

static void surface_damage(struct wl_client *c, struct wl_resource *r,
                           int32_t x, int32_t y, int32_t w, int32_t h)
{ (void)c; (void)r; (void)x; (void)y; (void)w; (void)h; }

static void surface_frame(struct wl_client *c, struct wl_resource *r, uint32_t id)
{
    (void)r;
    // The player throttles on this. A callback that is never answered halves its frame
    // rate, and with a non-zero EGL swap interval would stop it dead.
    struct wl_resource *cb = wl_resource_create(c, &wl_callback_interface, 1, id);
    if (!cb) { wl_client_post_no_memory(c); return; }
    H.frame_callback = cb;
}

static void surface_set_opaque_region(struct wl_client *c, struct wl_resource *r,
                                      struct wl_resource *region)
{ (void)c; (void)r; (void)region; }

static void surface_set_input_region(struct wl_client *c, struct wl_resource *r,
                                     struct wl_resource *region)
{ (void)c; (void)r; (void)region; }

static void surface_commit(struct wl_client *c, struct wl_resource *r)
{
    (void)c; (void)r;
    H.commits++;

    if (H.pending_buffer) {
        struct wl_shm_buffer *shm = wl_shm_buffer_get(H.pending_buffer);
        if (shm) {
            int32_t  w      = wl_shm_buffer_get_width(shm);
            int32_t  h      = wl_shm_buffer_get_height(shm);
            int32_t  stride = wl_shm_buffer_get_stride(shm);
            uint32_t fmt    = wl_shm_buffer_get_format(shm);

            static bool said;
            if (!said) {
                said = true;
                printf("[host] first frame from the player: %dx%d, stride %d, format 0x%x\n",
                       w, h, stride, fmt);
            }

            size_t need = (size_t)stride * (size_t)h;
            if (H.frame_cap < need) {
                uint8_t *grown = realloc(H.frame, need);
                if (grown) { H.frame = grown; H.frame_cap = need; }
            }
            if (H.frame && H.frame_cap >= need) {
                // begin_access installs the SIGBUS handler that protects us from a client
                // which truncates its pool while we are reading it.
                wl_shm_buffer_begin_access(shm);
                const uint8_t *src = wl_shm_buffer_get_data(shm);

                if (fmt == WL_SHM_FORMAT_ARGB8888) {
                    // Premultiply on the way past.
                    //
                    // Wayland defines ARGB8888 as premultiplied, and the player does not
                    // premultiply: its camera clears to alpha 0 and its interface is drawn
                    // with ordinary alpha blending, so what lands here is straight alpha.
                    // Copied verbatim, every partly transparent pixel of the overlay comes
                    // out too bright -- the washed-out edge that is easy to see and easy to
                    // mistake for a compositing bug somewhere else.
                    for (int32_t y = 0; y < h; y++) {
                        const uint8_t *s8 = src + (size_t)y * stride;
                        uint8_t       *d8 = H.frame + (size_t)y * stride;
                        for (int32_t x = 0; x < w; x++) {
                            unsigned a = s8[3];
                            d8[0] = (uint8_t)(s8[0] * a / 255);
                            d8[1] = (uint8_t)(s8[1] * a / 255);
                            d8[2] = (uint8_t)(s8[2] * a / 255);
                            d8[3] = (uint8_t)a;
                            s8 += 4; d8 += 4;
                        }
                    }
                } else {
                    // XRGB8888 and anything else: opaque, nothing to premultiply.
                    memcpy(H.frame, src, need);
                }
                wl_shm_buffer_end_access(shm);

                H.fw = w; H.fh = h; H.fstride = (uint32_t)stride;
                H.fseq++;
            }
        } else {
            static bool warned;
            if (!warned) {
                warned = true;
                fprintf(stderr, "[host] the player attached a buffer that is not wl_shm. "
                                "Only shared memory is handled today.\n");
            }
        }

        // Released as soon as it has been copied. Holding a client's buffer stalls it on
        // its own pool, which presents as a frame-rate collapse with no cause attached.
        wl_buffer_send_release(H.pending_buffer);
        H.pending_buffer = NULL;
    }

    if (H.frame_callback) {
        wl_callback_send_done(H.frame_callback, now_ms());
        wl_resource_destroy(H.frame_callback);
        H.frame_callback = NULL;
    }
}

static void surface_set_buffer_transform(struct wl_client *c, struct wl_resource *r, int32_t t)
{ (void)c; (void)r; (void)t; }

static void surface_set_buffer_scale(struct wl_client *c, struct wl_resource *r, int32_t s)
{ (void)c; (void)r; (void)s; }

static void surface_damage_buffer(struct wl_client *c, struct wl_resource *r,
                                  int32_t x, int32_t y, int32_t w, int32_t h)
{ (void)c; (void)r; (void)x; (void)y; (void)w; (void)h; }

static const struct wl_surface_interface surface_impl = {
    .destroy              = surface_destroy,
    .attach               = surface_attach,
    .damage               = surface_damage,
    .frame                = surface_frame,
    .set_opaque_region    = surface_set_opaque_region,
    .set_input_region     = surface_set_input_region,
    .commit               = surface_commit,
    .set_buffer_transform = surface_set_buffer_transform,
    .set_buffer_scale     = surface_set_buffer_scale,
    .damage_buffer        = surface_damage_buffer,
};

// ------------------------------------------------------------------ wl_region

static void region_destroy(struct wl_client *c, struct wl_resource *r)
{ (void)c; wl_resource_destroy(r); }
static void region_add(struct wl_client *c, struct wl_resource *r,
                       int32_t x, int32_t y, int32_t w, int32_t h)
{ (void)c; (void)r; (void)x; (void)y; (void)w; (void)h; }
static void region_subtract(struct wl_client *c, struct wl_resource *r,
                            int32_t x, int32_t y, int32_t w, int32_t h)
{ (void)c; (void)r; (void)x; (void)y; (void)w; (void)h; }

static const struct wl_region_interface region_impl = {
    region_destroy, region_add, region_subtract
};

// ------------------------------------------------------------------ wl_compositor

static void compositor_create_surface(struct wl_client *c, struct wl_resource *r, uint32_t id)
{
    struct wl_resource *s = wl_resource_create(c, &wl_surface_interface,
                                               wl_resource_get_version(r), id);
    if (!s) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(s, &surface_impl, NULL, NULL);
}

static void compositor_create_region(struct wl_client *c, struct wl_resource *r, uint32_t id)
{
    (void)r;
    struct wl_resource *reg = wl_resource_create(c, &wl_region_interface, 1, id);
    if (!reg) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(reg, &region_impl, NULL, NULL);
}

static const struct wl_compositor_interface compositor_impl = {
    compositor_create_surface, compositor_create_region
};

static void bind_compositor(struct wl_client *c, void *data, uint32_t version, uint32_t id)
{
    (void)data;
    struct wl_resource *r = wl_resource_create(c, &wl_compositor_interface, (int)version, id);
    if (!r) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(r, &compositor_impl, NULL, NULL);
}

// ------------------------------------------------------------------ wl_output

static void bind_output(struct wl_client *c, void *data, uint32_t version, uint32_t id)
{
    (void)data;
    struct wl_resource *r = wl_resource_create(c, &wl_output_interface, (int)version, id);
    if (!r) { wl_client_post_no_memory(c); return; }

    int32_t w = H.out_w > 0 ? H.out_w : 1920;
    int32_t h = H.out_h > 0 ? H.out_h : 1080;

    // Sent from inside bind, not deferred. SDL performs exactly two roundtrips during video
    // init and takes its display list from whatever has arrived by the end of the second; a
    // done event even slightly late produces "The video driver did not add any displays" and
    // a player that reports its desktop as 0 x 0.
    wl_output_send_geometry(r, 0, 0, w, h, WL_OUTPUT_SUBPIXEL_UNKNOWN,
                            "VIP-Sim", "overlay", WL_OUTPUT_TRANSFORM_NORMAL);
    wl_output_send_mode(r, WL_OUTPUT_MODE_CURRENT | WL_OUTPUT_MODE_PREFERRED, w, h, 60000);
    if (version >= 2) {
        // Scale 1, deliberately: SDL divides the reported size by this, so a 2 here hands
        // the player a half-size desktop and therefore a half-size overlay.
        wl_output_send_scale(r, 1);
        wl_output_send_done(r);
    }
}

// ------------------------------------------------------------------ wl_seat

// The player's input devices. A list rather than a single resource because a client may
// legitimately ask for the same device more than once, and a stale pointer here would be a
// use-after-free on the next event rather than a missing click.
static void input_resource_destroyed(struct wl_resource *r)
{
    wl_list_remove(wl_resource_get_link(r));
}

static void pointer_set_cursor(struct wl_client *c, struct wl_resource *r, uint32_t serial,
                               struct wl_resource *surface, int32_t hx, int32_t hy)
{
    (void)c; (void)r; (void)serial; (void)surface; (void)hx; (void)hy;
    // The cursor the user sees is drawn by their own compositor over the overlay. A cursor
    // set in here would be one nobody can see, on a surface nobody composites.
}
static void pointer_release(struct wl_client *c, struct wl_resource *r)
{ (void)c; wl_resource_destroy(r); }

static const struct wl_pointer_interface pointer_impl = { pointer_set_cursor, pointer_release };

static void keyboard_release(struct wl_client *c, struct wl_resource *r)
{ (void)c; wl_resource_destroy(r); }

static const struct wl_keyboard_interface keyboard_impl = { keyboard_release };

static void seat_get_pointer(struct wl_client *c, struct wl_resource *r, uint32_t id)
{
    struct wl_resource *p = wl_resource_create(c, &wl_pointer_interface,
                                               wl_resource_get_version(r), id);
    if (!p) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(p, &pointer_impl, NULL, input_resource_destroyed);
    wl_list_insert(&H.pointers, wl_resource_get_link(p));
}

static void seat_get_keyboard(struct wl_client *c, struct wl_resource *r, uint32_t id)
{
    struct wl_resource *k = wl_resource_create(c, &wl_keyboard_interface,
                                               wl_resource_get_version(r), id);
    if (!k) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(k, &keyboard_impl, NULL, input_resource_destroyed);
    wl_list_insert(&H.keyboards, wl_resource_get_link(k));

    // The keymap has to go out now: SDL reads it as soon as it has the device, and a
    // keyboard with no keymap produces keycodes it cannot turn into characters.
    if (H.keymap_fd >= 0)
        wl_keyboard_send_keymap(k, H.keymap_format, H.keymap_fd, H.keymap_size);
    else
        wl_keyboard_send_keymap(k, WL_KEYBOARD_KEYMAP_FORMAT_NO_KEYMAP, -1, 0);

    if (wl_resource_get_version(k) >= 4)
        wl_keyboard_send_repeat_info(k, 25, 600);
}

static void seat_get_touch(struct wl_client *c, struct wl_resource *r, uint32_t id)
{
    struct wl_resource *t = wl_resource_create(c, &wl_touch_interface,
                                               wl_resource_get_version(r), id);
    if (t) wl_resource_set_implementation(t, NULL, NULL, NULL);
}
static void seat_release(struct wl_client *c, struct wl_resource *r)
{ (void)c; wl_resource_destroy(r); }

static const struct wl_seat_interface seat_impl = {
    seat_get_pointer, seat_get_keyboard, seat_get_touch, seat_release
};

static void bind_seat(struct wl_client *c, void *data, uint32_t version, uint32_t id)
{
    (void)data;
    struct wl_resource *r = wl_resource_create(c, &wl_seat_interface, (int)version, id);
    if (!r) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(r, &seat_impl, NULL, NULL);

    // A pointer and a keyboard, both driven by what the overlay receives from the user's
    // own compositor. Nothing here opens a device: this seat has no hardware behind it and
    // reports only what is forwarded to it.
    wl_seat_send_capabilities(r, WL_SEAT_CAPABILITY_POINTER | WL_SEAT_CAPABILITY_KEYBOARD);
    if (version >= 2) wl_seat_send_name(r, "vipsim");
}

// ------------------------------------------------------------------ xdg_shell

static void toplevel_destroy(struct wl_client *c, struct wl_resource *r)
{ (void)c; wl_resource_destroy(r); }
static void toplevel_set_parent(struct wl_client *c, struct wl_resource *r, struct wl_resource *p)
{ (void)c; (void)r; (void)p; }
static void toplevel_set_title(struct wl_client *c, struct wl_resource *r, const char *t)
{ (void)c; (void)r; (void)t; }
static void toplevel_set_app_id(struct wl_client *c, struct wl_resource *r, const char *a)
{ (void)c; (void)r; (void)a; }
static void toplevel_show_window_menu(struct wl_client *c, struct wl_resource *r,
                                      struct wl_resource *seat, uint32_t serial,
                                      int32_t x, int32_t y)
{ (void)c; (void)r; (void)seat; (void)serial; (void)x; (void)y; }
static void toplevel_move(struct wl_client *c, struct wl_resource *r,
                          struct wl_resource *seat, uint32_t serial)
{ (void)c; (void)r; (void)seat; (void)serial; }
static void toplevel_resize(struct wl_client *c, struct wl_resource *r,
                            struct wl_resource *seat, uint32_t serial, uint32_t edges)
{ (void)c; (void)r; (void)seat; (void)serial; (void)edges; }
static void toplevel_set_max_size(struct wl_client *c, struct wl_resource *r, int32_t w, int32_t h)
{ (void)c; (void)r; (void)w; (void)h; }
static void toplevel_set_min_size(struct wl_client *c, struct wl_resource *r, int32_t w, int32_t h)
{ (void)c; (void)r; (void)w; (void)h; }
static void toplevel_set_maximized(struct wl_client *c, struct wl_resource *r)
{ (void)c; (void)r; }
static void toplevel_unset_maximized(struct wl_client *c, struct wl_resource *r)
{ (void)c; (void)r; }
static void toplevel_set_fullscreen(struct wl_client *c, struct wl_resource *r,
                                    struct wl_resource *output)
{ (void)c; (void)r; (void)output; }
static void toplevel_unset_fullscreen(struct wl_client *c, struct wl_resource *r)
{ (void)c; (void)r; }
static void toplevel_set_minimized(struct wl_client *c, struct wl_resource *r)
{ (void)c; (void)r; }

static const struct xdg_toplevel_interface toplevel_impl = {
    toplevel_destroy, toplevel_set_parent, toplevel_set_title, toplevel_set_app_id,
    toplevel_show_window_menu, toplevel_move, toplevel_resize, toplevel_set_max_size,
    toplevel_set_min_size, toplevel_set_maximized, toplevel_unset_maximized,
    toplevel_set_fullscreen, toplevel_unset_fullscreen, toplevel_set_minimized,
};

static void xdg_surface_destroy(struct wl_client *c, struct wl_resource *r)
{ (void)c; wl_resource_destroy(r); }

static void xdg_surface_get_toplevel(struct wl_client *c, struct wl_resource *r, uint32_t id)
{
    struct wl_resource *t = wl_resource_create(c, &xdg_toplevel_interface,
                                               wl_resource_get_version(r), id);
    if (!t) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(t, &toplevel_impl, NULL, NULL);
    H.xdg_toplevel = t;

    int32_t w = H.out_w > 0 ? H.out_w : 1920;
    int32_t h = H.out_h > 0 ? H.out_h : 1080;

    // The first configure goes out now, before the client has committed anything. SDL waits
    // for it in an untimed loop, so a host that waits for a buffer and a client that waits
    // for a configure simply stop, with no error printed on either side.
    //
    // ACTIVATED and nothing else -- not FULLSCREEN, however tempting as a way to keep
    // decorations away. TransparentWindow.RestoreOverlayGeometry forces windowed mode on
    // Linux, and the two would argue about it for the life of the process.
    struct wl_array states;
    wl_array_init(&states);
    uint32_t *st = wl_array_add(&states, sizeof *st);
    if (st) *st = XDG_TOPLEVEL_STATE_ACTIVATED;
    xdg_toplevel_send_configure(t, w, h, &states);
    wl_array_release(&states);

    xdg_surface_send_configure(r, wl_display_next_serial(H.display));
    printf("[host] the player asked for a toplevel; configured %dx%d.\n", w, h);
}

static void xdg_surface_get_popup(struct wl_client *c, struct wl_resource *r, uint32_t id,
                                  struct wl_resource *parent, struct wl_resource *positioner)
{
    (void)r; (void)parent; (void)positioner;
    struct wl_resource *p = wl_resource_create(c, &xdg_popup_interface, 1, id);
    if (p) wl_resource_set_implementation(p, NULL, NULL, NULL);
}

static void xdg_surface_set_window_geometry(struct wl_client *c, struct wl_resource *r,
                                            int32_t x, int32_t y, int32_t w, int32_t h)
{
    (void)c; (void)r;
    static bool said;
    if (!said) {
        said = true;
        // Worth one line: a non-zero origin here is the fingerprint of client-side
        // decorations, which advertising the decoration manager is meant to prevent.
        printf("[host] window geometry %d,%d %dx%d%s\n", x, y, w, h,
               (x || y) ? "  <-- offset, so something is drawing decorations" : "");
    }
}

static void xdg_surface_ack_configure(struct wl_client *c, struct wl_resource *r, uint32_t serial)
{ (void)c; (void)r; (void)serial; }

static const struct xdg_surface_interface xdg_surface_impl = {
    xdg_surface_destroy, xdg_surface_get_toplevel, xdg_surface_get_popup,
    xdg_surface_set_window_geometry, xdg_surface_ack_configure,
};

static void wm_base_destroy(struct wl_client *c, struct wl_resource *r)
{ (void)c; wl_resource_destroy(r); }

static void wm_base_create_positioner(struct wl_client *c, struct wl_resource *r, uint32_t id)
{
    (void)r;
    struct wl_resource *p = wl_resource_create(c, &xdg_positioner_interface, 1, id);
    if (p) wl_resource_set_implementation(p, NULL, NULL, NULL);
}

static void wm_base_get_xdg_surface(struct wl_client *c, struct wl_resource *r, uint32_t id,
                                    struct wl_resource *surface)
{
    struct wl_resource *x = wl_resource_create(c, &xdg_surface_interface,
                                               wl_resource_get_version(r), id);
    if (!x) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(x, &xdg_surface_impl, NULL, NULL);
    H.xdg_surface = x;

    // The window's surface, identified by the role rather than by being the first one
    // created: SDL also makes surfaces for the cursor, and input aimed at one of those
    // would go nowhere the user can see.
    H.surface = surface;
}

static void wm_base_pong(struct wl_client *c, struct wl_resource *r, uint32_t serial)
{ (void)c; (void)r; (void)serial; }

static const struct xdg_wm_base_interface wm_base_impl = {
    wm_base_destroy, wm_base_create_positioner, wm_base_get_xdg_surface, wm_base_pong
};

static void bind_wm_base(struct wl_client *c, void *data, uint32_t version, uint32_t id)
{
    (void)data;
    struct wl_resource *r = wl_resource_create(c, &xdg_wm_base_interface, (int)version, id);
    if (!r) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(r, &wm_base_impl, NULL, NULL);
}

// ------------------------------------------------------------------ decorations

static void decoration_destroy(struct wl_client *c, struct wl_resource *r)
{ (void)c; wl_resource_destroy(r); }
static void decoration_set_mode(struct wl_client *c, struct wl_resource *r, uint32_t mode)
{ (void)c; (void)r; (void)mode; }
static void decoration_unset_mode(struct wl_client *c, struct wl_resource *r)
{ (void)c; (void)r; }

static const struct zxdg_toplevel_decoration_v1_interface decoration_impl = {
    decoration_destroy, decoration_set_mode, decoration_unset_mode
};

static void deco_manager_destroy(struct wl_client *c, struct wl_resource *r)
{ (void)c; wl_resource_destroy(r); }

static void deco_manager_get_decoration(struct wl_client *c, struct wl_resource *r, uint32_t id,
                                        struct wl_resource *toplevel)
{
    (void)r; (void)toplevel;
    struct wl_resource *d = wl_resource_create(c, &zxdg_toplevel_decoration_v1_interface, 1, id);
    if (!d) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(d, &decoration_impl, NULL, NULL);

    // Server-side, meaning the client draws no decorations -- and neither do we, because
    // this window is never shown to anybody. That is the whole reason for advertising this
    // global: its mere presence stops SDL loading libdecor, which would otherwise wrap the
    // player in a client-drawn titlebar and two extra subsurfaces.
    zxdg_toplevel_decoration_v1_send_configure(d, ZXDG_TOPLEVEL_DECORATION_V1_MODE_SERVER_SIDE);
}

static const struct zxdg_decoration_manager_v1_interface deco_manager_impl = {
    deco_manager_destroy, deco_manager_get_decoration
};

static void bind_deco_manager(struct wl_client *c, void *data, uint32_t version, uint32_t id)
{
    (void)data;
    struct wl_resource *r = wl_resource_create(c, &zxdg_decoration_manager_v1_interface,
                                               (int)version, id);
    if (!r) { wl_client_post_no_memory(c); return; }
    wl_resource_set_implementation(r, &deco_manager_impl, NULL, NULL);
}

// ------------------------------------------------------------------ lifecycle

static void on_client_created(struct wl_listener *l, void *data)
{
    (void)l; (void)data;
    H.clients++;
    printf("[host] client connected (%d total)\n", H.clients);
}

static struct wl_listener g_client_listener = { .notify = on_client_created };

const char *vipsim_host_start(void)
{
    H.display = wl_display_create();
    if (!H.display) {
        fprintf(stderr, "[host] could not create the Wayland display.\n");
        return NULL;
    }
    H.loop = wl_display_get_event_loop(H.display);
    wl_list_init(&H.pointers);
    wl_list_init(&H.keyboards);
    H.keymap_fd = -1;

    // A socket named for this process, not "wayland-N".
    //
    // add_socket_auto would take the first free wayland-N, and on a desktop that is a name
    // other software looks for -- a stray VIP-Sim would start collecting other people's
    // clients. Naming it after the pid keeps it obviously ours and lets more than one
    // VIP-Sim run at once.
    static char name[64];
    snprintf(name, sizeof name, "vipsim-%d", (int)getpid());
    if (wl_display_add_socket(H.display, name) != 0) {
        fprintf(stderr, "[host] could not bind the socket '%s' in XDG_RUNTIME_DIR=%s\n",
                name, getenv("XDG_RUNTIME_DIR") ? getenv("XDG_RUNTIME_DIR") : "(unset)");
        wl_display_destroy(H.display);
        H.display = NULL;
        return NULL;
    }
    H.socket = name;

    // libwayland-server implements wl_shm for us: the global, the pool, the buffers, the
    // mmap, and the SIGBUS handling for a client that truncates a pool underneath us. It
    // also emits ARGB8888 and XRGB8888 on bind, which is exactly the pair Mesa wants before
    // it will initialise EGL. Hand-rolling it would be a mistake -- wl_shm_buffer_get only
    // recognises buffers made by this implementation.
    wl_display_init_shm(H.display);

    wl_global_create(H.display, &wl_compositor_interface, HOST_COMPOSITOR_VERSION,
                     NULL, bind_compositor);
    wl_global_create(H.display, &wl_output_interface, HOST_OUTPUT_VERSION, NULL, bind_output);
    wl_global_create(H.display, &wl_seat_interface, HOST_SEAT_VERSION, NULL, bind_seat);
    wl_global_create(H.display, &xdg_wm_base_interface, HOST_XDG_VERSION, NULL, bind_wm_base);
    wl_global_create(H.display, &zxdg_decoration_manager_v1_interface, 1, NULL,
                     bind_deco_manager);

    wl_display_add_client_created_listener(H.display, &g_client_listener);

    printf("[host] serving on %s (XDG_RUNTIME_DIR=%s)\n", name,
           getenv("XDG_RUNTIME_DIR") ? getenv("XDG_RUNTIME_DIR") : "(unset)");
    return name;
}

int vipsim_host_fd(void)
{
    return H.loop ? wl_event_loop_get_fd(H.loop) : -1;
}

void vipsim_host_dispatch(void)
{
    if (!H.loop) return;
    wl_event_loop_dispatch(H.loop, 0);   // 0 = do not block
    wl_display_flush_clients(H.display);
}

void vipsim_host_flush(void)
{
    if (H.display) wl_display_flush_clients(H.display);
}

void vipsim_host_set_output_size(int32_t width, int32_t height)
{
    H.out_w = width;
    H.out_h = height;
}

bool vipsim_host_has_client(void)
{
    return H.clients > 0;
}

unsigned vipsim_host_commits(void)
{
    return H.commits;
}

unsigned vipsim_host_frame(const void **data, int32_t *w, int32_t *h, uint32_t *stride)
{
    if (!H.frame || H.fseq == 0) return 0;
    if (data)   *data   = H.frame;
    if (w)      *w      = H.fw;
    if (h)      *h      = H.fh;
    if (stride) *stride = H.fstride;
    return H.fseq;
}

void vipsim_host_stop(void)
{
    if (!H.display) return;
    wl_display_destroy_clients(H.display);
    wl_display_destroy(H.display);
    H.display = NULL;
    H.loop = NULL;
    free(H.frame);
    H.frame = NULL;
    H.frame_cap = 0;
}
// ------------------------------------------------------------------ input

// The player has no input devices of its own. It sits inside a compositor with no hardware
// behind it, so everything below is the overlay handing on what the user's real compositor
// delivered to the layer surface. The layer surface covers the whole output and the player's
// window is the same size, so surface coordinates pass through unchanged.

void vipsim_host_set_keymap(int fd, uint32_t size, uint32_t format)
{
    if (H.keymap_fd >= 0) close(H.keymap_fd);
    H.keymap_fd = fd;            // ours to close from here on
    H.keymap_size = size;
    H.keymap_format = format;

    // Forwarded verbatim rather than rebuilt. The user's keymap is whatever their compositor
    // decided it is, and handing the same description straight through means the player
    // agrees with the rest of their desktop about what the keys mean -- including layouts
    // this code has never heard of.
    struct wl_resource *k;
    wl_resource_for_each(k, &H.keyboards)
        wl_keyboard_send_keymap(k, format, fd, size);
}

void vipsim_host_pointer_motion(uint32_t time, double x, double y)
{
    if (!H.surface) return;
    struct wl_resource *p;
    uint32_t   serial = wl_display_next_serial(H.display);
    wl_fixed_t fx = wl_fixed_from_double(x), fy = wl_fixed_from_double(y);
    bool       entering = !H.pointer_in;

    static bool said;
    if (!said) {
        said = true;
        printf("[host] forwarding pointer to the player; %s pointer resources, surface %s\n",
               wl_list_empty(&H.pointers) ? "NO" : "have", H.surface ? "present" : "MISSING");
    }

    wl_resource_for_each(p, &H.pointers) {
        if (entering) wl_pointer_send_enter(p, serial, H.surface, fx, fy);
        wl_pointer_send_motion(p, time, fx, fy);
        if (wl_resource_get_version(p) >= WL_POINTER_FRAME_SINCE_VERSION)
            wl_pointer_send_frame(p);
    }
    H.pointer_in = true;
}

void vipsim_host_pointer_leave(void)
{
    if (!H.pointer_in || !H.surface) return;
    struct wl_resource *p;
    uint32_t serial = wl_display_next_serial(H.display);
    wl_resource_for_each(p, &H.pointers) {
        wl_pointer_send_leave(p, serial, H.surface);
        if (wl_resource_get_version(p) >= WL_POINTER_FRAME_SINCE_VERSION)
            wl_pointer_send_frame(p);
    }
    H.pointer_in = false;
}

void vipsim_host_pointer_button(uint32_t time, uint32_t button, uint32_t state)
{
    struct wl_resource *p;
    uint32_t serial = wl_display_next_serial(H.display);
    wl_resource_for_each(p, &H.pointers) {
        wl_pointer_send_button(p, serial, time, button, state);
        if (wl_resource_get_version(p) >= WL_POINTER_FRAME_SINCE_VERSION)
            wl_pointer_send_frame(p);
    }
}

void vipsim_host_pointer_axis(uint32_t time, uint32_t axis, double value)
{
    struct wl_resource *p;
    wl_resource_for_each(p, &H.pointers) {
        wl_pointer_send_axis(p, time, axis, wl_fixed_from_double(value));
        if (wl_resource_get_version(p) >= WL_POINTER_FRAME_SINCE_VERSION)
            wl_pointer_send_frame(p);
    }
}

void vipsim_host_keyboard_focus(bool focused)
{
    if (!H.surface || H.keyboard_in == focused) return;
    struct wl_resource *k;
    uint32_t serial = wl_display_next_serial(H.display);
    struct wl_array keys;
    wl_array_init(&keys);

    wl_resource_for_each(k, &H.keyboards) {
        if (focused) wl_keyboard_send_enter(k, serial, H.surface, &keys);
        else         wl_keyboard_send_leave(k, serial, H.surface);
    }
    wl_array_release(&keys);
    H.keyboard_in = focused;
}

void vipsim_host_key(uint32_t time, uint32_t key, uint32_t state)
{
    struct wl_resource *k;
    uint32_t serial = wl_display_next_serial(H.display);
    wl_resource_for_each(k, &H.keyboards)
        wl_keyboard_send_key(k, serial, time, key, state);
}

void vipsim_host_modifiers(uint32_t depressed, uint32_t latched, uint32_t locked, uint32_t group)
{
    struct wl_resource *k;
    uint32_t serial = wl_display_next_serial(H.display);
    wl_resource_for_each(k, &H.keyboards)
        wl_keyboard_send_modifiers(k, serial, depressed, latched, locked, group);
}

// ------------------------------------------------------------------ self-test

// Prove the socket is usable before the player is launched.
//
// Mesa's EGL fails identically -- eglInitialize returning EGL_NOT_INITIALIZED, with nothing
// in the message naming shm or formats -- whether wl_shm is absent entirely or present but
// silent about its formats. Unity then reports only "Failed to create valid graphics
// context", which is the least informative sentence in the whole failure. Connecting to our
// own socket first and checking turns both into a named error at the point where it can
// still be acted on.

#include <poll.h>
#include <wayland-client.h>

static struct {
    struct wl_shm *shm;
    int            formats;
} P;

static void probe_format(void *d, struct wl_shm *shm, uint32_t format)
{
    (void)d; (void)shm; (void)format;
    P.formats++;
}
static const struct wl_shm_listener probe_shm_listener = { probe_format };

static void probe_global(void *d, struct wl_registry *reg, uint32_t name,
                         const char *iface, uint32_t version)
{
    (void)d; (void)version;
    if (!strcmp(iface, "wl_shm") && !P.shm) {
        P.shm = wl_registry_bind(reg, name, &wl_shm_interface, 1);
        wl_shm_add_listener(P.shm, &probe_shm_listener, NULL);
    }
}
static void probe_global_remove(void *d, struct wl_registry *r, uint32_t name)
{ (void)d; (void)r; (void)name; }

static const struct wl_registry_listener probe_registry_listener = {
    probe_global, probe_global_remove
};

bool vipsim_host_selftest(void)
{
    if (!H.display) return false;

    struct wl_display *c = wl_display_connect(H.socket);
    if (!c) {
        fprintf(stderr, "[host] self-test: cannot connect to my own socket '%s'.\n", H.socket);
        return false;
    }

    struct wl_registry *reg = wl_display_get_registry(c);
    wl_registry_add_listener(reg, &probe_registry_listener, NULL);

    // Pumped by hand rather than with wl_display_roundtrip, which would deadlock: the server
    // that has to answer this client is this process, and it is not running yet.
    for (int i = 0; i < 50 && !(P.shm && P.formats); i++) {
        wl_display_flush(c);
        vipsim_host_dispatch();
        struct pollfd pfd = { wl_display_get_fd(c), POLLIN, 0 };
        if (poll(&pfd, 1, 20) > 0 && (pfd.revents & POLLIN))
            wl_display_dispatch(c);
        else
            wl_display_dispatch_pending(c);
    }

    bool ok = P.shm && P.formats;
    if (ok)
        printf("[host] self-test: wl_shm present, %d pixel formats offered.\n", P.formats);
    else
        fprintf(stderr, "[host] self-test FAILED: wl_shm %s, %d formats. The player's EGL "
                        "would fail with EGL_NOT_INITIALIZED and say nothing about why.\n",
                P.shm ? "present" : "MISSING", P.formats);

    wl_registry_destroy(reg);
    wl_display_disconnect(c);
    return ok;
}

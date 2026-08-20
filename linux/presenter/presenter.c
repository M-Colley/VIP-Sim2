// VIP-Sim Wayland presenter.
//
// Owns the overlay surface that Unity cannot own itself: a Wayland surface's role is
// fixed at creation and Unity's SDL window is an xdg_toplevel, so the overlay has to be a
// separate process holding a zwlr_layer_surface_v1. See docs/LINUX_PORT.md.
//
// Phase 1 proved the three things the design rests on -- a full-output layer surface,
// per-pixel alpha reaching the compositor, and click-through via an empty input region.
// This is Phase 3: the same surface, now fed real frames from VIP-Sim over shared memory,
// with the input region driven by the producer so the panel can be made interactive.
//
// Without a producer it draws the Phase 1 alpha test pattern, so the binary is still
// useful on its own for checking a compositor.
//
// Build: ./build.sh      Run: ./build/vipsim-presenter [--test]

#define _GNU_SOURCE
#include <errno.h>
#include <fcntl.h>
#include <poll.h>
#include <stdbool.h>
#include <stdint.h>
#include "host.h"

#include <errno.h>
#include <poll.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <unistd.h>

#include <wayland-client.h>
#include "wlr-layer-shell-unstable-v1-client-protocol.h"
#include "linux-dmabuf-v1-client-protocol.h"
#include "vipsim_shm.h"

// ---------------------------------------------------------------- globals

static struct wl_compositor         *g_compositor;
static struct wl_shm                *g_shm;
static struct zwlr_layer_shell_v1   *g_layer_shell;
static struct zwp_linux_dmabuf_v1   *g_dmabuf;
static int                           g_dmabuf_entries;
static struct wl_surface            *g_surface;
static struct zwlr_layer_surface_v1 *g_layer_surface;

static int  g_width, g_height;
static bool g_configured;
static bool g_running = true;
static bool g_force_test_pattern;

// Producer segment, mapped read-only for as long as it exists.
static struct vipsim_shm_header *g_prod;
static unsigned g_last_host_seq;
static bool g_host_mode;
static void                     *g_prod_map;
static size_t                    g_prod_size;
static uint32_t                  g_last_seq = 0xFFFFFFFFu;
static int32_t                   g_panel[4] = {0, 0, 0, 0};
static bool                      g_panel_valid;

// Double buffering: the compositor may still be reading the previous frame.
struct buffer {
    struct wl_buffer *wl;
    void             *data;
    size_t            size;
    bool              busy;
};
static struct buffer g_buffers[2];

// ---------------------------------------------------------------- buffers

static int alloc_shm(size_t size)
{
    int fd = memfd_create("vipsim-presenter", MFD_CLOEXEC);
    if (fd < 0) { perror("memfd_create"); return -1; }
    if (ftruncate(fd, (off_t)size) < 0) { perror("ftruncate"); close(fd); return -1; }
    return fd;
}

static void buffer_release(void *data, struct wl_buffer *wl)
{
    (void)wl;
    ((struct buffer *)data)->busy = false;
}
static const struct wl_buffer_listener buffer_listener = { .release = buffer_release };

static bool make_buffers(int w, int h)
{
    size_t stride = (size_t)w * 4;
    size_t one = stride * (size_t)h;
    size_t total = one * 2;

    int fd = alloc_shm(total);
    if (fd < 0) return false;

    void *map = mmap(NULL, total, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
    if (map == MAP_FAILED) { perror("mmap"); close(fd); return false; }

    struct wl_shm_pool *pool = wl_shm_create_pool(g_shm, fd, (int32_t)total);
    for (int i = 0; i < 2; i++) {
        g_buffers[i].wl = wl_shm_pool_create_buffer(
            pool, (int32_t)(one * (size_t)i), w, h, (int32_t)stride, WL_SHM_FORMAT_ARGB8888);
        g_buffers[i].data = (char *)map + one * (size_t)i;
        g_buffers[i].size = one;
        g_buffers[i].busy = false;
        wl_buffer_add_listener(g_buffers[i].wl, &buffer_listener, &g_buffers[i]);
    }
    wl_shm_pool_destroy(pool);
    close(fd);
    return true;
}

static struct buffer *free_buffer(void)
{
    for (int i = 0; i < 2; i++)
        if (!g_buffers[i].busy) return &g_buffers[i];
    return NULL;   // both in flight; skip this frame rather than tear
}

// ---------------------------------------------------------------- content

/// Phase 1 pattern: five alpha bands over a colour ramp, inside a white frame.
/// Kept because it is the quickest way to tell whether a compositor honours per-pixel
/// alpha at all, independently of whether VIP-Sim is running.
static void draw_test_pattern(uint32_t *px, int w, int h)
{
    const uint8_t alphas[5] = { 255, 191, 128, 64, 0 };
    int band_h = h / 5 > 0 ? h / 5 : 1;

    for (int y = 0; y < h; y++) {
        int band = y / band_h; if (band > 4) band = 4;
        uint32_t a = alphas[band];
        for (int x = 0; x < w; x++) {
            uint32_t r = (uint32_t)(255.0 * x / (w > 1 ? w - 1 : 1));
            uint32_t g = (uint32_t)(255.0 * y / (h > 1 ? h - 1 : 1));
            uint32_t b = 200;
            // ARGB8888 is premultiplied; forgetting that gives a washed-out overlay
            // that looks almost right.
            px[y * w + x] = (a << 24) | ((r * a / 255) << 16) | ((g * a / 255) << 8) | (b * a / 255);
        }
    }
    for (int x = 0; x < w; x++) { px[x] = 0xFFFFFFFFu; px[(h - 1) * w + x] = 0xFFFFFFFFu; }
    for (int y = 0; y < h; y++) { px[y * w] = 0xFFFFFFFFu; px[y * w + w - 1] = 0xFFFFFFFFu; }
}

/// Copy the producer's frame, letterboxed if its size does not match the output.
static void copy_producer(uint32_t *dst, int dw, int dh)
{
    const struct vipsim_shm_header *h = g_prod;
    uint32_t sw = h->width, sh = h->height, stride = h->stride;
    const char *src = (const char *)g_prod_map + VIPSIM_PIXEL_OFFSET;

    memset(dst, 0, (size_t)dw * (size_t)dh * 4);
    if (sw == 0 || sh == 0) return;

    uint32_t cw = sw < (uint32_t)dw ? sw : (uint32_t)dw;
    uint32_t ch = sh < (uint32_t)dh ? sh : (uint32_t)dh;
    int ox = (dw - (int)cw) / 2, oy = (dh - (int)ch) / 2;

    for (uint32_t y = 0; y < ch; y++)
        memcpy(dst + (size_t)(oy + (int)y) * (size_t)dw + ox,
               src + (size_t)y * stride, (size_t)cw * 4);
}

/// Copy the frame the player committed to our own compositor, letterboxed to the output.
///
/// Same shape as copy_producer, and it will replace it: when the player renders into a
/// compositor we own, its buffer already is the frame, so the shared-memory transport, the
/// GPU readback and the vertical flip on the Unity side all become unnecessary work.
static void copy_host(uint32_t *dst, int dw, int dh)
{
    const void *src = NULL;
    int32_t sw = 0, sh = 0;
    uint32_t stride = 0;

    memset(dst, 0, (size_t)dw * (size_t)dh * 4);
    if (!vipsim_host_frame(&src, &sw, &sh, &stride) || sw <= 0 || sh <= 0) return;

    uint32_t cw = (uint32_t)sw < (uint32_t)dw ? (uint32_t)sw : (uint32_t)dw;
    uint32_t ch = (uint32_t)sh < (uint32_t)dh ? (uint32_t)sh : (uint32_t)dh;
    int ox = (dw - (int)cw) / 2, oy = (dh - (int)ch) / 2;

    for (uint32_t y = 0; y < ch; y++)
        memcpy(dst + (size_t)(oy + (int)y) * (size_t)dw + ox,
               (const char *)src + (size_t)y * stride, (size_t)cw * 4);
}

/// Everything outside the producer's panel rectangle stays click-through. An empty region
/// means the compositor never routes input here at all -- no per-event filtering and no
/// focus races, which is what makes this cleaner than the Windows implementation.
static void apply_input_region(void)
{
    struct wl_region *r = wl_compositor_create_region(g_compositor);

    // Only when we are hosting the player. Without the nested compositor the player still
    // has its own window underneath catching clicks, and an overlay that took them instead
    // would swallow them: there would be nowhere to forward them to.
    if (g_host_mode && g_panel[2] > 0 && g_panel[3] > 0)
        wl_region_add(r, g_panel[0], g_panel[1], g_panel[2], g_panel[3]);
    wl_surface_set_input_region(g_surface, r);
    wl_region_destroy(r);
}

// ---------------------------------------------------------------- frame loop

static void frame_done(void *data, struct wl_callback *cb, uint32_t time);
static const struct wl_callback_listener frame_listener = { .done = frame_done };

static void present(void)
{
    struct buffer *b = free_buffer();
    if (!b) return;

    // The player's own commit, when it is running inside our compositor, in preference to
    // the shared-memory transport. Both paths exist while the host is being brought up; the
    // transport goes once the host is the only way frames arrive.
    if (vipsim_host_frame(NULL, NULL, NULL, NULL)) {
        copy_host((uint32_t *)b->data, g_width, g_height);
    } else if (g_prod && !g_force_test_pattern) {
        copy_producer((uint32_t *)b->data, g_width, g_height);
    } else {
        draw_test_pattern((uint32_t *)b->data, g_width, g_height);
    }

    // The rectangle that should catch the mouse, whichever way the pixels arrived. This
    // lived inside the producer branch and so was skipped the moment frames started coming
    // from the host instead -- the overlay went on taking no input at all, which looks
    // exactly like input forwarding that does not work.
    if (g_prod) {
        int32_t p[4] = { g_prod->panel_x, g_prod->panel_y, g_prod->panel_w, g_prod->panel_h };
        if (!g_panel_valid || memcmp(p, g_panel, sizeof p) != 0) {
            memcpy(g_panel, p, sizeof p);
            g_panel_valid = true;
            apply_input_region();
            printf("[presenter] input region %s (%d,%d %dx%d)\n",
                   (p[2] > 0 && p[3] > 0) ? "= panel rect (interactive)" : "empty (click-through)",
                   p[0], p[1], p[2], p[3]);
        }
    }

    b->busy = true;
    wl_surface_attach(g_surface, b->wl, 0, 0);
    wl_surface_damage_buffer(g_surface, 0, 0, g_width, g_height);

    struct wl_callback *cb = wl_surface_frame(g_surface);
    wl_callback_add_listener(cb, &frame_listener, NULL);
    wl_surface_commit(g_surface);
}

static void frame_done(void *data, struct wl_callback *cb, uint32_t time)
{
    (void)data; (void)time;
    wl_callback_destroy(cb);

    if (g_prod && g_prod->quit) {
        printf("[presenter] producer asked to quit.\n");
        g_running = false;
        return;
    }

    // Only redraw when there is something new; otherwise ask for the next callback so the
    // loop keeps pace with the compositor rather than spinning.
    unsigned host_seq = vipsim_host_frame(NULL, NULL, NULL, NULL);
    if (host_seq) {
        // The player is rendering into our compositor: repaint when its picture changes.
        if (host_seq != g_last_host_seq) {
            g_last_host_seq = host_seq;
            present();
        } else {
            struct wl_callback *next = wl_surface_frame(g_surface);
            wl_callback_add_listener(next, &frame_listener, NULL);
            wl_surface_commit(g_surface);
        }
    } else if (!g_prod || g_force_test_pattern || g_prod->seq != g_last_seq) {
        if (g_prod) g_last_seq = g_prod->seq;
        present();
    } else {
        struct wl_callback *next = wl_surface_frame(g_surface);
        wl_callback_add_listener(next, &frame_listener, NULL);
        wl_surface_commit(g_surface);
    }
}

// ---------------------------------------------------------------- producer segment

static bool open_producer(void)
{
    int fd = shm_open(VIPSIM_SHM_NAME, O_RDONLY, 0);
    if (fd < 0) return false;

    struct vipsim_shm_header head;
    if (read(fd, &head, sizeof head) != (ssize_t)sizeof head) { close(fd); return false; }
    if (head.magic != VIPSIM_SHM_MAGIC || head.version != VIPSIM_SHM_VERSION) {
        fprintf(stderr, "[presenter] shared segment is not a VIP-Sim v%u frame buffer; ignoring.\n",
                VIPSIM_SHM_VERSION);
        close(fd);
        return false;
    }

    g_prod_size = (size_t)vipsim_shm_size(head.stride, head.height);
    g_prod_map = mmap(NULL, g_prod_size, PROT_READ, MAP_SHARED, fd, 0);
    close(fd);
    if (g_prod_map == MAP_FAILED) { g_prod_map = NULL; return false; }

    g_prod = (struct vipsim_shm_header *)g_prod_map;
    printf("[presenter] attached to VIP-Sim: %ux%u, stride %u\n",
           g_prod->width, g_prod->height, g_prod->stride);
    return true;
}

// ---------------------------------------------------------------- layer surface

static void on_configure(void *data, struct zwlr_layer_surface_v1 *s,
                         uint32_t serial, uint32_t w, uint32_t h)
{
    (void)data;
    zwlr_layer_surface_v1_ack_configure(s, serial);

    bool first = !g_configured;
    g_width = (int)w; g_height = (int)h;
    printf("[presenter] configured: %dx%d\n", g_width, g_height);

    // The player's world is exactly the size the real compositor gave us, so the two can
    // never disagree about it.
    vipsim_host_set_output_size(g_width, g_height);

    if (g_width <= 0 || g_height <= 0) {
        fprintf(stderr, "[presenter] compositor gave a zero size; cannot draw.\n");
        g_running = false;
        return;
    }
    if (first) {
        if (!make_buffers(g_width, g_height)) { g_running = false; return; }
        apply_input_region();          // click-through until a producer says otherwise
        g_configured = true;
        present();
    }
}

static void on_closed(void *data, struct zwlr_layer_surface_v1 *s)
{
    (void)data; (void)s;
    printf("[presenter] compositor closed the layer surface.\n");
    g_running = false;
}

static const struct zwlr_layer_surface_v1_listener layer_surface_listener = {
    .configure = on_configure,
    .closed    = on_closed,
};

// ---------------------------------------------------------------- dmabuf probe

// Phase 4 groundwork, and deliberately only that.
//
// Importing a GPU buffer needs Unity to export its texture as a dmabuf, which needs a real
// GPU and a real session. The container this was written in has neither: no /dev/dri, and
// no udmabuf module either, so a dmabuf cannot be created by any route at all. Writing the
// import path blind would put two hundred lines of protocol code in the tree that had
// never once run -- the same shape of mistake as an effect whose severity ships at zero.
//
// What is testable today is the negotiation: whether the compositor offers the protocol,
// at what version, and which formats it accepts. That is exactly what the import path has
// to be written against, and getting it wrong is the usual reason a first dmabuf attempt
// renders black.

static void dmabuf_format(void *data, struct zwp_linux_dmabuf_v1 *d, uint32_t format)
{
    (void)data; (void)d; (void)format;
    g_dmabuf_entries++;
}

static void dmabuf_modifier(void *data, struct zwp_linux_dmabuf_v1 *d, uint32_t format,
                            uint32_t hi, uint32_t lo)
{
    (void)data; (void)d;
    // ARGB8888 is what VIP-Sim produces, so report that one by name rather than count it.
    if (format == 0x34325241u /* DRM_FORMAT_ARGB8888 */)
        printf("[presenter] dmabuf: ARGB8888 accepted, modifier %08x:%08x\n", hi, lo);
    g_dmabuf_entries++;
}

static const struct zwp_linux_dmabuf_v1_listener dmabuf_listener = {
    .format = dmabuf_format,
    .modifier = dmabuf_modifier,
};

// ---------------------------------------------------------------- input

// Everything the user's compositor delivers to the layer surface is handed to the player.
//
// The overlay is the only thing on screen: the player's window lives in our own compositor
// and the user never sees it, so it can never be focused or clicked directly. Without this
// the simulator is a picture -- the toolbar, the effect list and the severity sliders would
// all be unreachable, which is the objection that sank every other way of getting the
// player's window off the screen.
//
// Coordinates pass through unchanged. The layer surface covers the whole output and the
// player's window is exactly that size, because the wl_output the player is told about is
// the one we configure from the layer surface.

static struct wl_seat     *g_seat;
static struct wl_pointer  *g_pointer;
static struct wl_keyboard *g_keyboard;

static void pointer_enter(void *d, struct wl_pointer *p, uint32_t serial,
                          struct wl_surface *surf, wl_fixed_t sx, wl_fixed_t sy)
{
    (void)d; (void)p; (void)serial; (void)surf;
    printf("[presenter] pointer entered the overlay at (%.0f,%.0f)\n",
           wl_fixed_to_double(sx), wl_fixed_to_double(sy));
    vipsim_host_pointer_motion(0, wl_fixed_to_double(sx), wl_fixed_to_double(sy));
}

static void pointer_leave(void *d, struct wl_pointer *p, uint32_t serial, struct wl_surface *surf)
{
    (void)d; (void)p; (void)serial; (void)surf;
    vipsim_host_pointer_leave();
}

static void pointer_motion(void *d, struct wl_pointer *p, uint32_t time,
                           wl_fixed_t sx, wl_fixed_t sy)
{
    (void)d; (void)p;
    vipsim_host_pointer_motion(time, wl_fixed_to_double(sx), wl_fixed_to_double(sy));
}

static void pointer_button(void *d, struct wl_pointer *p, uint32_t serial, uint32_t time,
                           uint32_t button, uint32_t state)
{
    (void)d; (void)p; (void)serial;
    // Once. The first click proves the whole chain -- the user's compositor routed it to
    // the overlay, and it is on its way to a player whose window is not on their screen.
    static bool said;
    if (!said) { said = true; printf("[presenter] first click forwarded to the player.\n"); }
    vipsim_host_pointer_button(time, button, state);
}

static void pointer_axis(void *d, struct wl_pointer *p, uint32_t time,
                         uint32_t axis, wl_fixed_t value)
{
    (void)d; (void)p;
    vipsim_host_pointer_axis(time, axis, wl_fixed_to_double(value));
}

static void pointer_noop_frame(void *d, struct wl_pointer *p) { (void)d; (void)p; }
static void pointer_noop_u32(void *d, struct wl_pointer *p, uint32_t a) { (void)d; (void)p; (void)a; }
static void pointer_noop_axis_stop(void *d, struct wl_pointer *p, uint32_t t, uint32_t a)
{ (void)d; (void)p; (void)t; (void)a; }
static void pointer_noop_axis_discrete(void *d, struct wl_pointer *p, uint32_t a, int32_t v)
{ (void)d; (void)p; (void)a; (void)v; }
static void pointer_noop_axis_value120(void *d, struct wl_pointer *p, uint32_t a, int32_t v)
{ (void)d; (void)p; (void)a; (void)v; }
static void pointer_noop_axis_direction(void *d, struct wl_pointer *p, uint32_t a, uint32_t v)
{ (void)d; (void)p; (void)a; (void)v; }

static const struct wl_pointer_listener pointer_listener = {
    .enter                   = pointer_enter,
    .leave                   = pointer_leave,
    .motion                  = pointer_motion,
    .button                  = pointer_button,
    .axis                    = pointer_axis,
    .frame                   = pointer_noop_frame,
    .axis_source             = pointer_noop_u32,
    .axis_stop               = pointer_noop_axis_stop,
    .axis_discrete           = pointer_noop_axis_discrete,
    .axis_value120           = pointer_noop_axis_value120,
    .axis_relative_direction = pointer_noop_axis_direction,
};

static void kb_keymap(void *d, struct wl_keyboard *k, uint32_t format, int32_t fd, uint32_t size)
{
    (void)d; (void)k;
    // Handed straight on rather than parsed. Ownership of the fd moves to the host.
    vipsim_host_set_keymap(fd, size, format);
}

static void kb_enter(void *d, struct wl_keyboard *k, uint32_t serial,
                     struct wl_surface *surf, struct wl_array *keys)
{
    (void)d; (void)k; (void)serial; (void)surf; (void)keys;
    vipsim_host_keyboard_focus(true);
}

static void kb_leave(void *d, struct wl_keyboard *k, uint32_t serial, struct wl_surface *surf)
{
    (void)d; (void)k; (void)serial; (void)surf;
    vipsim_host_keyboard_focus(false);
}

static void kb_key(void *d, struct wl_keyboard *k, uint32_t serial, uint32_t time,
                   uint32_t key, uint32_t state)
{
    (void)d; (void)k; (void)serial;
    vipsim_host_key(time, key, state);
}

static void kb_modifiers(void *d, struct wl_keyboard *k, uint32_t serial, uint32_t depressed,
                         uint32_t latched, uint32_t locked, uint32_t group)
{
    (void)d; (void)k; (void)serial;
    vipsim_host_modifiers(depressed, latched, locked, group);
}

static void kb_repeat_info(void *d, struct wl_keyboard *k, int32_t rate, int32_t delay)
{ (void)d; (void)k; (void)rate; (void)delay; }

static const struct wl_keyboard_listener keyboard_listener = {
    kb_keymap, kb_enter, kb_leave, kb_key, kb_modifiers, kb_repeat_info
};

static void seat_capabilities(void *d, struct wl_seat *seat, uint32_t caps)
{
    (void)d;
    if ((caps & WL_SEAT_CAPABILITY_POINTER) && !g_pointer) {
        g_pointer = wl_seat_get_pointer(seat);
        wl_pointer_add_listener(g_pointer, &pointer_listener, NULL);
    }
    if ((caps & WL_SEAT_CAPABILITY_KEYBOARD) && !g_keyboard) {
        g_keyboard = wl_seat_get_keyboard(seat);
        wl_keyboard_add_listener(g_keyboard, &keyboard_listener, NULL);
    }
    printf("[presenter] seat: %s%s\n",
           (caps & WL_SEAT_CAPABILITY_POINTER)  ? "pointer "  : "",
           (caps & WL_SEAT_CAPABILITY_KEYBOARD) ? "keyboard" : "");
}

static void seat_name(void *d, struct wl_seat *s, const char *n) { (void)d; (void)s; (void)n; }

static const struct wl_seat_listener seat_listener = { seat_capabilities, seat_name };

// ---------------------------------------------------------------- registry

static void on_global(void *data, struct wl_registry *reg, uint32_t name,
                      const char *iface, uint32_t version)
{
    (void)data;
    if (!strcmp(iface, wl_compositor_interface.name))
        g_compositor = wl_registry_bind(reg, name, &wl_compositor_interface, version < 4 ? version : 4);
    else if (!strcmp(iface, wl_shm_interface.name))
        g_shm = wl_registry_bind(reg, name, &wl_shm_interface, 1);
    else if (!strcmp(iface, zwlr_layer_shell_v1_interface.name)) {
        g_layer_shell = wl_registry_bind(reg, name, &zwlr_layer_shell_v1_interface, version < 4 ? version : 4);
        printf("[presenter] compositor offers zwlr_layer_shell_v1 v%u\n", version);
    }
    else if (!strcmp(iface, wl_seat_interface.name) && !g_seat) {
        // Only useful in host mode, but bound unconditionally: the layer surface's input
        // region is what decides whether any of these events ever arrive, and that is
        // already driven by the panel rectangle.
        uint32_t v = version < 7 ? version : 7;
        g_seat = wl_registry_bind(reg, name, &wl_seat_interface, v);
        wl_seat_add_listener(g_seat, &seat_listener, NULL);
    }
    else if (!strcmp(iface, zwp_linux_dmabuf_v1_interface.name)) {
        uint32_t v = version < 3 ? version : 3;   // v4+ moves formats to a feedback object
        g_dmabuf = wl_registry_bind(reg, name, &zwp_linux_dmabuf_v1_interface, v);
        if (v >= 3) zwp_linux_dmabuf_v1_add_listener(g_dmabuf, &dmabuf_listener, NULL);
        printf("[presenter] compositor offers zwp_linux_dmabuf_v1 v%u (bound v%u)\n", version, v);
    }
}
static void on_global_remove(void *d, struct wl_registry *r, uint32_t n) { (void)d; (void)r; (void)n; }
static const struct wl_registry_listener registry_listener = {
    .global = on_global, .global_remove = on_global_remove,
};

// ---------------------------------------------------------------- main

int main(int argc, char **argv)
{
    // Line-buffer stdout. VIP-Sim launches this process with its output on a pipe so the
    // messages reach the player log, and a pipe is block-buffered by default: everything
    // said here -- which protocols the compositor offers, what size it configured, whether
    // a producer attached -- sits in the buffer and is lost when the player kills us on
    // exit. The one case where these lines matter is the one where they never arrive.
    setvbuf(stdout, NULL, _IOLBF, 0);

    bool host_mode = false;
    for (int i = 1; i < argc; i++) {
        if (!strcmp(argv[i], "--test")) g_force_test_pattern = true;
        if (!strcmp(argv[i], "--host")) host_mode = true;
    }

    struct wl_display *display = wl_display_connect(NULL);
    if (!display) {
        fprintf(stderr, "[presenter] cannot connect to a Wayland compositor. WAYLAND_DISPLAY=%s\n",
                getenv("WAYLAND_DISPLAY") ? getenv("WAYLAND_DISPLAY") : "(unset)");
        return 1;
    }

    struct wl_registry *reg = wl_display_get_registry(display);
    wl_registry_add_listener(reg, &registry_listener, NULL);
    wl_display_roundtrip(display);

    if (!g_compositor || !g_shm) {
        fprintf(stderr, "[presenter] compositor is missing wl_compositor or wl_shm.\n");
        return 1;
    }
    if (!g_layer_shell) {
        // The expected outcome on GNOME and on WSLg, and the single most useful thing
        // this program can report. Not a crash -- an answer.
        fprintf(stderr,
            "[presenter] this compositor does NOT implement zwlr_layer_shell_v1.\n"
            "            VIP-Sim's overlay cannot run here. GNOME/Mutter does not implement\n"
            "            it, and neither does WSLg. Try KWin, Sway, Hyprland, labwc or niri.\n");
        return 2;
    }

    if (g_dmabuf) {
        wl_display_roundtrip(display);   // let the format/modifier events land
        printf("[presenter] dmabuf: %d format/modifier entries advertised. Zero-copy "
               "is not wired up; frames still go through wl_shm.\n", g_dmabuf_entries);
    } else {
        printf("[presenter] dmabuf: not offered here; wl_shm is the only path.\n");
    }

    if (!g_force_test_pattern && !open_producer())
        printf("[presenter] no VIP-Sim frames yet; showing the alpha test pattern.\n");

    g_surface = wl_compositor_create_surface(g_compositor);
    g_layer_surface = zwlr_layer_shell_v1_get_layer_surface(
        g_layer_shell, g_surface, NULL, ZWLR_LAYER_SHELL_V1_LAYER_OVERLAY, "vipsim");
    zwlr_layer_surface_v1_add_listener(g_layer_surface, &layer_surface_listener, NULL);
    zwlr_layer_surface_v1_set_anchor(g_layer_surface,
        ZWLR_LAYER_SURFACE_V1_ANCHOR_TOP | ZWLR_LAYER_SURFACE_V1_ANCHOR_BOTTOM |
        ZWLR_LAYER_SURFACE_V1_ANCHOR_LEFT | ZWLR_LAYER_SURFACE_V1_ANCHOR_RIGHT);
    zwlr_layer_surface_v1_set_exclusive_zone(g_layer_surface, -1);
    zwlr_layer_surface_v1_set_keyboard_interactivity(g_layer_surface, 0);
    wl_surface_commit(g_surface);

    g_host_mode = host_mode;
    if (host_mode) {
        if (!vipsim_host_start()) return 1;
        if (!vipsim_host_selftest()) return 1;
    }

    printf("[presenter] waiting for configure...\n");

    // Two Wayland connections in one thread: a client connection to the real compositor
    // and, in host mode, a server socket for the player. wl_display_dispatch blocks, which
    // would starve whichever of the two was quiet, so both are polled instead.
    //
    // The prepare_read / read_events dance is not optional even single-threaded: a plain
    // dispatch would consume events that arrived while the other display was being
    // serviced, and the cancel_read path is what keeps the queue consistent when poll
    // wakes for the other fd.
    while (g_running) {
        while (wl_display_prepare_read(display) != 0)
            wl_display_dispatch_pending(display);
        wl_display_flush(display);
        vipsim_host_flush();

        struct pollfd fds[2];
        int n = 0;
        fds[n].fd = wl_display_get_fd(display); fds[n].events = POLLIN; fds[n].revents = 0; n++;
        int hfd = vipsim_host_fd();
        if (hfd >= 0) { fds[n].fd = hfd; fds[n].events = POLLIN; fds[n].revents = 0; n++; }

        // A timeout rather than an indefinite wait, so a producer that starts later is
        // still noticed on a screen where nothing else is happening.
        int rc = poll(fds, (nfds_t)n, 100);
        if (rc < 0 && errno != EINTR) { wl_display_cancel_read(display); break; }

        if (fds[0].revents & POLLIN) {
            if (wl_display_read_events(display) != 0) break;
        } else {
            wl_display_cancel_read(display);
        }
        if (wl_display_dispatch_pending(display) < 0) break;

        // Unconditionally, not only when the fd is readable: an event loop also carries
        // timers and idle sources, and those fire from dispatch rather than from the fd.
        vipsim_host_dispatch();

        // If VIP-Sim starts after the presenter, pick it up without a restart.
        if (!g_prod && !g_force_test_pattern && open_producer()) g_last_seq = 0xFFFFFFFFu;
    }

    vipsim_host_stop();

    if (g_prod_map) munmap(g_prod_map, g_prod_size);
    wl_display_disconnect(display);
    return g_configured ? 0 : 1;
}

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
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <unistd.h>

#include <wayland-client.h>
#include "wlr-layer-shell-unstable-v1-client-protocol.h"
#include "vipsim_shm.h"

// ---------------------------------------------------------------- globals

static struct wl_compositor         *g_compositor;
static struct wl_shm                *g_shm;
static struct zwlr_layer_shell_v1   *g_layer_shell;
static struct wl_surface            *g_surface;
static struct zwlr_layer_surface_v1 *g_layer_surface;

static int  g_width, g_height;
static bool g_configured;
static bool g_running = true;
static bool g_force_test_pattern;

// Producer segment, mapped read-only for as long as it exists.
static struct vipsim_shm_header *g_prod;
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

/// Everything outside the producer's panel rectangle stays click-through. An empty region
/// means the compositor never routes input here at all -- no per-event filtering and no
/// focus races, which is what makes this cleaner than the Windows implementation.
static void apply_input_region(void)
{
    struct wl_region *r = wl_compositor_create_region(g_compositor);
    if (g_panel[2] > 0 && g_panel[3] > 0)
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

    if (g_prod && !g_force_test_pattern) {
        copy_producer((uint32_t *)b->data, g_width, g_height);

        int32_t p[4] = { g_prod->panel_x, g_prod->panel_y, g_prod->panel_w, g_prod->panel_h };
        if (!g_panel_valid || memcmp(p, g_panel, sizeof p) != 0) {
            memcpy(g_panel, p, sizeof p);
            g_panel_valid = true;
            apply_input_region();
            printf("[presenter] input region %s\n",
                   (p[2] > 0 && p[3] > 0) ? "= panel rect (interactive)" : "empty (click-through)");
        }
    } else {
        draw_test_pattern((uint32_t *)b->data, g_width, g_height);
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
    if (!g_prod || g_force_test_pattern || g_prod->seq != g_last_seq) {
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
}
static void on_global_remove(void *d, struct wl_registry *r, uint32_t n) { (void)d; (void)r; (void)n; }
static const struct wl_registry_listener registry_listener = {
    .global = on_global, .global_remove = on_global_remove,
};

// ---------------------------------------------------------------- main

int main(int argc, char **argv)
{
    for (int i = 1; i < argc; i++)
        if (!strcmp(argv[i], "--test")) g_force_test_pattern = true;

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

    printf("[presenter] waiting for configure...\n");
    while (g_running && wl_display_dispatch(display) != -1) {
        // If VIP-Sim starts after the presenter, pick it up without a restart.
        if (!g_prod && !g_force_test_pattern && open_producer()) g_last_seq = 0xFFFFFFFFu;
    }

    if (g_prod_map) munmap(g_prod_map, g_prod_size);
    wl_display_disconnect(display);
    return g_configured ? 0 : 1;
}

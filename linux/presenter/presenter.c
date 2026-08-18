// VIP-Sim Wayland presenter -- Phase 1 spike.
//
// Purpose: answer the three questions that decide whether the Wayland-native overlay in
// docs/LINUX_PORT.md is viable, before any effort is spent on frame transport.
//
//   1. Can we obtain a layer surface on the overlay layer, covering the whole output?
//   2. Does per-pixel alpha actually reach the compositor? Alpha is load-bearing in
//      VIP-Sim -- on Windows a wrong alpha channel looks exactly like a dead effect --
//      so this must be proven, not assumed.
//   3. Does an empty input region give click-through at the compositor level, without the
//      WS_EX_TRANSPARENT juggling and focus races the Windows build needs?
//
// It deliberately does NOT talk to Unity. A spike that also has to move frames cannot tell
// you which half is broken.
//
// The test pattern is chosen to make alpha legible by eye: four horizontal bands at 100%,
// 75%, 50% and 25% alpha over a colour ramp, plus a fully transparent band. If the
// compositor is honouring per-pixel alpha, whatever is behind the overlay shows through
// each band progressively more. If alpha is being ignored, the bands look uniform.
//
// Build: ./build.sh      Run: ./vipsim-presenter

#define _GNU_SOURCE
#include <errno.h>
#include <fcntl.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <unistd.h>

#include <wayland-client.h>
#include "wlr-layer-shell-unstable-v1-client-protocol.h"

static struct wl_compositor          *g_compositor;
static struct wl_shm                 *g_shm;
static struct zwlr_layer_shell_v1    *g_layer_shell;
static struct wl_surface             *g_surface;
static struct zwlr_layer_surface_v1  *g_layer_surface;

static int      g_width, g_height;
static int      g_configured;
static int      g_running = 1;
static uint32_t g_globals_seen;

// ---------------------------------------------------------------- shared memory buffer

// An anonymous file the compositor can map. memfd_create keeps it off the filesystem
// entirely, which avoids the /tmp fallback dance older examples need.
static int alloc_shm(size_t size)
{
    int fd = memfd_create("vipsim-presenter", MFD_CLOEXEC);
    if (fd < 0) { perror("memfd_create"); return -1; }
    if (ftruncate(fd, (off_t)size) < 0) { perror("ftruncate"); close(fd); return -1; }
    return fd;
}

/// Four alpha bands over a colour ramp, plus a fully transparent band.
/// ARGB8888 in Wayland is PREMULTIPLIED, so every colour channel is scaled by its own
/// alpha. Forgetting that is the classic way to get a washed-out overlay that looks
/// almost right -- the same class of mistake as the alpha squaring found on Windows.
static void draw_test_pattern(uint32_t *px, int w, int h)
{
    const uint8_t alphas[5] = { 255, 191, 128, 64, 0 };
    int band_h = h / 5;

    for (int y = 0; y < h; y++) {
        int band = y / (band_h > 0 ? band_h : 1);
        if (band > 4) band = 4;
        uint32_t a = alphas[band];

        for (int x = 0; x < w; x++) {
            // Colour ramp across the width so the bands are distinguishable in a photo.
            uint32_t r = (uint32_t)(255.0 * x / (w > 1 ? w - 1 : 1));
            uint32_t g = (uint32_t)(255.0 * y / (h > 1 ? h - 1 : 1));
            uint32_t b = 200;

            // Premultiply.
            r = r * a / 255;
            g = g * a / 255;
            b = b * a / 255;

            px[y * w + x] = (a << 24) | (r << 16) | (g << 8) | b;
        }
    }

    // A 2px opaque white frame, so the surface's true extent is unmistakable -- this is
    // what proves the layer surface really covers the whole output.
    for (int x = 0; x < w; x++) {
        px[x] = 0xFFFFFFFFu;
        px[(h - 1) * w + x] = 0xFFFFFFFFu;
    }
    for (int y = 0; y < h; y++) {
        px[y * w] = 0xFFFFFFFFu;
        px[y * w + (w - 1)] = 0xFFFFFFFFu;
    }
}

static struct wl_buffer *make_buffer(int w, int h)
{
    size_t stride = (size_t)w * 4;
    size_t size   = stride * (size_t)h;

    int fd = alloc_shm(size);
    if (fd < 0) return NULL;

    uint32_t *px = mmap(NULL, size, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
    if (px == MAP_FAILED) { perror("mmap"); close(fd); return NULL; }

    draw_test_pattern(px, w, h);

    struct wl_shm_pool *pool = wl_shm_create_pool(g_shm, fd, (int32_t)size);
    struct wl_buffer *buf = wl_shm_pool_create_buffer(
        pool, 0, w, h, (int32_t)stride, WL_SHM_FORMAT_ARGB8888);
    wl_shm_pool_destroy(pool);
    munmap(px, size);
    close(fd);
    return buf;
}

// ---------------------------------------------------------------- layer surface events

static void on_configure(void *data, struct zwlr_layer_surface_v1 *surface,
                         uint32_t serial, uint32_t w, uint32_t h)
{
    (void)data;
    zwlr_layer_surface_v1_ack_configure(surface, serial);

    g_width  = (int)w;
    g_height = (int)h;
    printf("[presenter] configured: %dx%d\n", g_width, g_height);

    if (g_width <= 0 || g_height <= 0) {
        fprintf(stderr, "[presenter] compositor gave a zero size; cannot draw.\n");
        g_running = 0;
        return;
    }

    struct wl_buffer *buf = make_buffer(g_width, g_height);
    if (!buf) { g_running = 0; return; }

    // Click-through, the Wayland way: an EMPTY input region means the compositor never
    // routes pointer or touch events to this surface at all. No per-event filtering, no
    // focus races -- the two things that make the Windows implementation delicate.
    struct wl_region *empty = wl_compositor_create_region(g_compositor);
    wl_surface_set_input_region(g_surface, empty);
    wl_region_destroy(empty);

    wl_surface_attach(g_surface, buf, 0, 0);
    wl_surface_damage_buffer(g_surface, 0, 0, g_width, g_height);
    wl_surface_commit(g_surface);

    g_configured = 1;
    printf("[presenter] drew alpha test pattern; input region is empty (click-through).\n");
}

static void on_closed(void *data, struct zwlr_layer_surface_v1 *surface)
{
    (void)data; (void)surface;
    printf("[presenter] compositor closed the layer surface.\n");
    g_running = 0;
}

static const struct zwlr_layer_surface_v1_listener layer_surface_listener = {
    .configure = on_configure,
    .closed    = on_closed,
};

// ---------------------------------------------------------------- registry

static void on_global(void *data, struct wl_registry *registry, uint32_t name,
                      const char *interface, uint32_t version)
{
    (void)data;
    if (strcmp(interface, wl_compositor_interface.name) == 0) {
        g_compositor = wl_registry_bind(registry, name, &wl_compositor_interface,
                                        version < 4 ? version : 4);
        g_globals_seen |= 1;
    } else if (strcmp(interface, wl_shm_interface.name) == 0) {
        g_shm = wl_registry_bind(registry, name, &wl_shm_interface, 1);
        g_globals_seen |= 2;
    } else if (strcmp(interface, zwlr_layer_shell_v1_interface.name) == 0) {
        g_layer_shell = wl_registry_bind(registry, name, &zwlr_layer_shell_v1_interface,
                                         version < 4 ? version : 4);
        g_globals_seen |= 4;
        printf("[presenter] compositor offers zwlr_layer_shell_v1 v%u\n", version);
    }
}

static void on_global_remove(void *d, struct wl_registry *r, uint32_t n)
{ (void)d; (void)r; (void)n; }

static const struct wl_registry_listener registry_listener = {
    .global        = on_global,
    .global_remove = on_global_remove,
};

// ---------------------------------------------------------------- main

int main(void)
{
    struct wl_display *display = wl_display_connect(NULL);
    if (!display) {
        fprintf(stderr,
            "[presenter] cannot connect to a Wayland compositor.\n"
            "            WAYLAND_DISPLAY=%s\n",
            getenv("WAYLAND_DISPLAY") ? getenv("WAYLAND_DISPLAY") : "(unset)");
        return 1;
    }

    struct wl_registry *registry = wl_display_get_registry(display);
    wl_registry_add_listener(registry, &registry_listener, NULL);
    wl_display_roundtrip(display);

    if (!g_compositor || !g_shm) {
        fprintf(stderr, "[presenter] compositor is missing wl_compositor or wl_shm.\n");
        return 1;
    }

    if (!g_layer_shell) {
        // The expected outcome on GNOME and on WSLg, and the single most important thing
        // this spike can report. Not an error in the program -- an answer.
        fprintf(stderr,
            "[presenter] this compositor does NOT implement zwlr_layer_shell_v1.\n"
            "            VIP-Sim's overlay cannot run here. Known: GNOME/Mutter does not\n"
            "            implement it (see docs/LINUX_PORT.md), and neither does WSLg.\n"
            "            Try KWin, Sway, Hyprland, labwc, niri or a nested Weston.\n");
        return 2;
    }

    g_surface = wl_compositor_create_surface(g_compositor);
    g_layer_surface = zwlr_layer_shell_v1_get_layer_surface(
        g_layer_shell, g_surface,
        NULL,                                       // NULL output: compositor chooses
        ZWLR_LAYER_SHELL_V1_LAYER_OVERLAY,          // above panels and normal windows
        "vipsim");

    zwlr_layer_surface_v1_add_listener(g_layer_surface, &layer_surface_listener, NULL);

    // Anchored to all four edges means "fill the output", and the compositor then sends
    // the real size in configure rather than us guessing it.
    zwlr_layer_surface_v1_set_anchor(g_layer_surface,
        ZWLR_LAYER_SURFACE_V1_ANCHOR_TOP    | ZWLR_LAYER_SURFACE_V1_ANCHOR_BOTTOM |
        ZWLR_LAYER_SURFACE_V1_ANCHOR_LEFT   | ZWLR_LAYER_SURFACE_V1_ANCHOR_RIGHT);

    // -1: ignore other surfaces' exclusive zones, i.e. cover panels and bars too.
    zwlr_layer_surface_v1_set_exclusive_zone(g_layer_surface, -1);

    // The overlay must never take the keyboard; VIP-Sim's panel does that on its own.
    zwlr_layer_surface_v1_set_keyboard_interactivity(g_layer_surface, 0);

    wl_surface_commit(g_surface);

    printf("[presenter] waiting for configure...\n");
    while (g_running && wl_display_dispatch(display) != -1) {
        if (g_configured) {
            // Nothing further to do in the spike: the compositor now owns the frame.
            // Stay alive so the result can be looked at.
        }
    }

    wl_display_disconnect(display);
    return g_configured ? 0 : 1;
}

// Producer side of the VIP-Sim frame transport: the library Unity loads.
//
// The C ABI mirrors the capture seam on the other platforms so the C# side stays uniform.
// Unity calls push() with the bytes from an AsyncGPUReadback; everything below is a memcpy
// into shared memory and an increment. No Wayland here at all -- the presenter owns the
// surface, this owns nothing but a buffer, and that separation is what lets either side be
// restarted without the other noticing.
//
// Built as libvipsim_present.so.

#define _GNU_SOURCE
#include <fcntl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <unistd.h>

#include "vipsim_shm.h"

static struct vipsim_shm_header *g_head;
static void   *g_map;
static size_t  g_size;
static char   *g_pixels;

#define EXPORT __attribute__((visibility("default")))

/// Create the segment. Returns 0 on success, negative on failure.
EXPORT int vipsim_present_open(int width, int height)
{
    if (width <= 0 || height <= 0) return -1;
    if (g_map) return 0;                       // already open

    uint32_t stride = (uint32_t)width * 4u;
    g_size = (size_t)vipsim_shm_size(stride, (uint32_t)height);

    // Unlink first: a segment left by a crashed run would otherwise be reused at the old
    // size, and the presenter would map a buffer that no longer matches the header.
    shm_unlink(VIPSIM_SHM_NAME);

    int fd = shm_open(VIPSIM_SHM_NAME, O_CREAT | O_RDWR | O_EXCL, 0600);
    if (fd < 0) { perror("shm_open"); return -2; }
    if (ftruncate(fd, (off_t)g_size) < 0) { perror("ftruncate"); close(fd); return -3; }

    g_map = mmap(NULL, g_size, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
    close(fd);
    if (g_map == MAP_FAILED) { g_map = NULL; perror("mmap"); return -4; }

    g_head = (struct vipsim_shm_header *)g_map;
    memset(g_head, 0, sizeof *g_head);
    g_head->width = (uint32_t)width;
    g_head->height = (uint32_t)height;
    g_head->stride = stride;
    g_head->version = VIPSIM_SHM_VERSION;
    // Magic last: until it is set the presenter refuses the segment, so it can never see
    // a half-initialised header.
    __sync_synchronize();
    g_head->magic = VIPSIM_SHM_MAGIC;

    g_pixels = (char *)g_map + VIPSIM_PIXEL_OFFSET;
    fprintf(stderr, "[vipsim_present] %dx%d segment ready at %s\n",
            width, height, VIPSIM_SHM_NAME);
    return 0;
}

/// Publish one frame. `src` is ARGB8888, premultiplied, `src_stride` bytes per row.
EXPORT int vipsim_present_push(const void *src, int src_stride)
{
    if (!g_head || !src) return -1;

    uint32_t stride = g_head->stride;
    uint32_t rows = g_head->height;
    if (src_stride == (int)stride) {
        memcpy(g_pixels, src, (size_t)stride * rows);
    } else {
        size_t n = (size_t)(src_stride < (int)stride ? src_stride : (int)stride);
        for (uint32_t y = 0; y < rows; y++)
            memcpy(g_pixels + (size_t)y * stride,
                   (const char *)src + (size_t)y * (size_t)src_stride, n);
    }

    // Pixels first, then the sequence: the presenter must never see a new number
    // pointing at a half-written frame.
    __sync_synchronize();
    g_head->seq++;
    return 0;
}

/// Publish one frame given Unity's RGBA32 bytes.
///
/// Unity hands back straight (non-premultiplied) RGBA; Wayland's ARGB8888 is
/// premultiplied and, on a little-endian machine, laid out as BGRA in memory. Doing that
/// conversion here rather than in C# keeps a per-pixel loop out of the managed heap --
/// at 4K that is eight million iterations a frame, which is not something to hand to the
/// garbage collector.
EXPORT int vipsim_present_push_rgba32(const void *src, int src_stride)
{
    if (!g_head || !src) return -1;

    const uint32_t w = g_head->width, h = g_head->height;
    const uint32_t dst_stride = g_head->stride;

    for (uint32_t y = 0; y < h; y++) {
        const unsigned char *s = (const unsigned char *)src + (size_t)y * (size_t)src_stride;
        unsigned char *d = (unsigned char *)g_pixels + (size_t)y * dst_stride;
        for (uint32_t x = 0; x < w; x++) {
            unsigned a = s[3];
            d[0] = (unsigned char)(s[2] * a / 255);   /* B */
            d[1] = (unsigned char)(s[1] * a / 255);   /* G */
            d[2] = (unsigned char)(s[0] * a / 255);   /* R */
            d[3] = (unsigned char)a;
            s += 4; d += 4;
        }
    }

    __sync_synchronize();
    g_head->seq++;
    return 0;
}

/// Set the rectangle that should receive input. Width or height <= 0 makes the whole
/// overlay click-through, which is the normal state; this is the Wayland equivalent of
/// infoState / tutorialState on the other platforms.
EXPORT void vipsim_present_set_panel(int x, int y, int w, int h)
{
    if (!g_head) return;
    g_head->panel_x = x; g_head->panel_y = y;
    g_head->panel_w = w; g_head->panel_h = h;
    __sync_synchronize();
}

/// Ask the presenter to exit, then drop the segment.
EXPORT void vipsim_present_close(void)
{
    if (g_head) { g_head->quit = 1; __sync_synchronize(); }
    if (g_map) { munmap(g_map, g_size); g_map = NULL; g_head = NULL; g_pixels = NULL; }
    shm_unlink(VIPSIM_SHM_NAME);
}

/// Frames published so far -- for the F8-style diagnostics on the C# side.
EXPORT unsigned vipsim_present_frame_count(void)
{
    return g_head ? g_head->seq : 0u;
}

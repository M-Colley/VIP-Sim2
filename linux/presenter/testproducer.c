// Stands in for VIP-Sim so the transport can be tested without Unity.
//
// Pushes a moving pattern through the same library Unity will use, so what is verified
// here is the real path: shared segment, sequence handshake, presenter pickup, and the
// input-region switch. Anything that works for this producer works for Unity, because
// Unity's only extra job is handing over the bytes.
//
// Usage: testproducer [width height frames]

#define _GNU_SOURCE
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

int vipsim_present_open(int width, int height);
int vipsim_present_push(const void *src, int src_stride);
void vipsim_present_set_panel(int x, int y, int w, int h);
void vipsim_present_close(void);
unsigned vipsim_present_frame_count(void);

int main(int argc, char **argv)
{
    int w = argc > 1 ? atoi(argv[1]) : 640;
    int h = argc > 2 ? atoi(argv[2]) : 360;
    int frames = argc > 3 ? atoi(argv[3]) : 120;

    if (vipsim_present_open(w, h) != 0) {
        fprintf(stderr, "testproducer: could not open the segment\n");
        return 1;
    }

    uint32_t *buf = malloc((size_t)w * (size_t)h * 4);
    if (!buf) return 1;

    // A panel rectangle in the corner, so the presenter's input-region switch is
    // exercised rather than merely compiled.
    vipsim_present_set_panel(w - 200, 0, 200, 120);

    for (int f = 0; f < frames; f++) {
        int cx = (int)((double)w * (0.5 + 0.35 * __builtin_sin(f * 0.12)));
        int cy = h / 2;
        int r = h / 4;

        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                int dx = x - cx, dy = y - cy;
                int inside = dx * dx + dy * dy < r * r;
                // Opaque disc on a half-transparent ground, so both the motion and the
                // alpha are visible in a screenshot.
                uint32_t a = inside ? 255u : 96u;
                uint32_t rr = inside ? 255u : (uint32_t)(255.0 * x / (w - 1));
                uint32_t gg = inside ? 158u : 40u;
                uint32_t bb = inside ? 41u  : (uint32_t)(255.0 * y / (h - 1));
                buf[y * w + x] = (a << 24) | ((rr * a / 255) << 16)
                               | ((gg * a / 255) << 8) | (bb * a / 255);
            }
        }

        vipsim_present_push(buf, w * 4);
        struct timespec ts = { 0, 16 * 1000 * 1000 };   // ~60 fps
        nanosleep(&ts, NULL);
    }

    printf("testproducer: pushed %u frames\n", vipsim_present_frame_count());
    vipsim_present_close();
    free(buf);
    return 0;
}

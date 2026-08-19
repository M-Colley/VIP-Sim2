// Exercise the capture plugin without Unity.
//
// On a desktop with a portal this opens the compositor's own picker and then reports the
// frames arriving. On a machine without one -- a server, a container, WSL -- it should say
// so plainly and exit, rather than hanging on a DBus call that will never be answered.
// That second behaviour is the one worth testing anywhere, because it is what a user with
// an unusual setup will meet.
//
// Usage: testcapture [seconds]

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

int         vipsim_capture_init(void);
int         vipsim_capture_start(void);
int         vipsim_capture_state(void);
const char *vipsim_capture_message(void);
int         vipsim_capture_frame_size(int *w, int *h);
unsigned    vipsim_capture_copy_frame(void *dst, int stride);
void        vipsim_capture_stop(void);

static const char *state_name(int s)
{
    switch (s) {
        case 0: return "idle";
        case 1: return "waiting for the user";
        case 2: return "streaming";
        case 3: return "failed";
        case 4: return "no portal";
        default: return "?";
    }
}

/// A frame of nothing. wlroots delivers these around a renegotiation, and they are worth
/// counting separately: a run that reports frames but shows black is a different fault
/// from one that reports no frames at all.
static int is_blank(const unsigned char *p, int w, int h)
{
    size_t n = (size_t)w * h * 4;
    for (size_t k = 0; k < n; k += 997)      // a coarse stride; blank frames are all zero
        if (p[k]) return 0;
    return 1;
}

int main(int argc, char **argv)
{
    int seconds = argc > 1 ? atoi(argv[1]) : 5;

    // Line-buffer stdout. Redirected to a file it is block-buffered by default, so if the
    // session tears this process down the results are lost while the library's stderr
    // messages survive -- which looks exactly like the capture loop never running.
    setvbuf(stdout, NULL, _IOLBF, 0);

    printf("init...\n");
    int rc = vipsim_capture_init();
    printf("  rc=%d  state=%s  message: %s\n", rc, state_name(vipsim_capture_state()),
           vipsim_capture_message());

    if (rc != 0) {
        printf("\nNo capture available here. That is the expected result without a\n"
               "desktop portal, and the point of this run is that it says so instead of\n"
               "hanging.\n");
        return 0;
    }

    printf("start (the portal picker should appear)...\n");
    rc = vipsim_capture_start();
    printf("  rc=%d  state=%s  message: %s\n", rc, state_name(vipsim_capture_state()),
           vipsim_capture_message());
    if (rc != 0) return 1;

    unsigned last = 0, frames = 0, blank = 0;
    unsigned char *buf = NULL, *keep = NULL;
    int w = 0, h = 0, have_keep = 0;

    for (int i = 0; i < seconds * 20; i++) {
        struct timespec ts = { 0, 50 * 1000 * 1000 };
        nanosleep(&ts, NULL);

        if (vipsim_capture_frame_size(&w, &h) != 0) continue;
        if (!buf) {
            buf  = malloc((size_t)w * h * 4);
            keep = malloc((size_t)w * h * 4);
            if (!buf || !keep) return 1;
            printf("  format settled: %dx%d\n", w, h);
        }
        unsigned seq = vipsim_capture_copy_frame(buf, w * 4);
        if (seq && seq != last) {
            last = seq;
            frames++;
            if (is_blank(buf, w, h)) {
                blank++;
            } else {
                memcpy(keep, buf, (size_t)w * h * 4);
                have_keep = 1;
            }
        }

        // Heartbeat, reporting the last frame that had anything in it. A frame counter
        // alone cannot tell live content from a buffer of zeros.
        if (i % 20 == 19) {
            printf("  t=%2ds  frames=%u (%u blank)  state=%-9s", (i + 1) / 20, frames,
                   blank, state_name(vipsim_capture_state()));
            if (have_keep) {
                const unsigned char *px = keep + (size_t)(h / 2) * w * 4 + (size_t)(w / 2) * 4;
                printf("  centre BGRA=%02x %02x %02x %02x\n", px[0], px[1], px[2], px[3]);
            } else {
                printf("  no non-blank frame yet\n");
            }
        }
    }

    printf("\n%u distinct frames in %d seconds (%dx%d), %u of them blank\n",
           frames, seconds, w, h, blank);

    // Say what was in the last frame that had content, and leave it on disk to be looked
    // at. The pixels are the proof; the frame count only shows the plumbing moved bytes.
    if (have_keep) {
        size_t n = (size_t)w * h * 4, nonzero = 0;
        unsigned long sum = 0;
        for (size_t k = 0; k < n; k++) { if (keep[k]) nonzero++; sum += keep[k]; }
        printf("last non-blank frame: %.1f%% non-zero bytes, mean %.1f\n",
               100.0 * nonzero / n, (double)sum / n);

        FILE *f = fopen("/tmp/vipsim-capture-live/frame.ppm", "wb");
        if (f) {
            fprintf(f, "P6\n%d %d\n255\n", w, h);
            for (size_t k = 0; k < (size_t)w * h; k++) {
                unsigned char rgb[3] = { keep[k*4+2], keep[k*4+1], keep[k*4+0] };  // BGRA -> RGB
                fwrite(rgb, 1, 3, f);
            }
            fclose(f);
            printf("wrote /tmp/vipsim-capture-live/frame.ppm\n");
        }
    }

    vipsim_capture_stop();
    free(buf);
    free(keep);
    return (frames > 0 && have_keep) ? 0 : 2;
}

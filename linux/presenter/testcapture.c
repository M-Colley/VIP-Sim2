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

int main(int argc, char **argv)
{
    int seconds = argc > 1 ? atoi(argv[1]) : 5;

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

    unsigned last = 0, frames = 0;
    void *buf = NULL;
    int w = 0, h = 0;

    for (int i = 0; i < seconds * 20; i++) {
        struct timespec ts = { 0, 50 * 1000 * 1000 };
        nanosleep(&ts, NULL);

        if (vipsim_capture_frame_size(&w, &h) != 0) continue;
        if (!buf) {
            buf = malloc((size_t)w * (size_t)h * 4);
            printf("  format settled: %dx%d\n", w, h);
        }
        unsigned seq = vipsim_capture_copy_frame(buf, w * 4);
        if (seq && seq != last) { last = seq; frames++; }
    }

    printf("\n%u distinct frames in %d seconds (%dx%d)\n", frames, seconds, w, h);
    vipsim_capture_stop();
    free(buf);
    return frames > 0 ? 0 : 2;
}

#include "host.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <wayland-server-core.h>

static struct {
    struct wl_display    *display;
    struct wl_event_loop *loop;
    const char           *socket;
    int32_t               out_w, out_h;
    int                   clients;
} H;

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

    // libwayland-server implements wl_shm for us: the global, the pool, the buffers and the
    // mmap. We only have to say which formats we accept. ARGB8888 and XRGB8888 are always
    // implied and must not be added explicitly.
    wl_display_init_shm(H.display);

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

void vipsim_host_stop(void)
{
    if (!H.display) return;
    wl_display_destroy_clients(H.display);
    wl_display_destroy(H.display);
    H.display = NULL;
    H.loop = NULL;
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

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

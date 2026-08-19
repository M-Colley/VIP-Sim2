// The presenter's second half: a Wayland server that the VIP-Sim player connects to.
//
// Why a compositor at all. On Wayland a surface's role is fixed when it is created, and the
// player's window is an xdg_toplevel created by SDL, so it can never become the layer
// surface the overlay needs. Everything else follows from that: the overlay is a separate
// process, and the player's own window is a second, visible, opaque, input-taking window
// that has no business being on the user's screen.
//
// Hiding it turned out to be the wrong problem. A toplevel cannot be minimised without
// losing frame callbacks, cannot be unmapped and keep rendering, and cannot refuse keyboard
// focus; and every way of making it invisible also makes it deaf, because it is the only
// thing on screen receiving the clicks that reach VIP-Sim's own panel.
//
// So the player is given a compositor of its own instead. Its toplevel is created in here,
// never reaches the user's compositor, and has nothing to hide, no decorations to suppress
// and no size to disagree about: the wl_output this server advertises is whatever size the
// real compositor configured the layer surface to be. The buffer the player attaches is the
// frame, and it goes straight onto the layer surface.
//
// One process, one thread, two displays: a client connection to the real compositor and a
// server socket for the player. presenter.c's poll loop services both.

#ifndef VIPSIM_HOST_H
#define VIPSIM_HOST_H

#include <stdbool.h>
#include <stdint.h>

/// Start the server and bind a socket. Returns the socket name, or NULL on failure.
/// The name is what the player must see in WAYLAND_DISPLAY.
const char *vipsim_host_start(void);

/// The file descriptor to poll. Readable means there is work for vipsim_host_dispatch.
int vipsim_host_fd(void);

/// Service whatever is pending. Non-blocking.
void vipsim_host_dispatch(void);

/// Push queued events out to clients. Call before blocking in poll, or clients hang
/// waiting for events that are sitting in a buffer on this side.
void vipsim_host_flush(void);

/// Tell the server how big the output is, i.e. what the real compositor configured the
/// layer surface to. The player sizes its window from this, which is what makes the size
/// mismatch structurally impossible rather than merely fixed.
void vipsim_host_set_output_size(int32_t width, int32_t height);

/// How many times the player has committed a frame. The plainest evidence that the two
/// halves are talking: it climbs, or the player is not rendering into our world.
unsigned vipsim_host_commits(void);

/// True once a client has connected.
bool vipsim_host_has_client(void);

/// Connect to our own socket and check the player will find what it needs. Returns false
/// with a named reason rather than letting Mesa fail namelessly later.
bool vipsim_host_selftest(void);

void vipsim_host_stop(void);

#endif

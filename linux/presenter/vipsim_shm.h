// Shared-memory contract between VIP-Sim and the Wayland presenter.
//
// Unity cannot own the overlay surface on Wayland -- a surface's role is fixed at
// creation and SDL makes a toplevel -- so the overlay is a separate process and the
// finished frames have to cross a process boundary. This header is the whole of that
// interface, deliberately: one struct, one segment, no protocol to get out of step.
//
// The producer (VIP-Sim) writes pixels, then bumps `seq`. The presenter watches `seq` and
// presents whatever it finds. A torn frame is possible in principle and harmless in
// practice -- the next one is 16ms away -- which is why there is no lock. Adding one
// would mean a hung producer could stall the compositor's callback loop, and a frozen
// overlay covering the desktop is a far worse failure than one sheared frame.
//
// v1 moves pixels through the CPU. That is the simple, correct version; the zero-copy
// dmabuf path is the follow-up, and it can reuse this header for control while carrying
// pixels out of band.

#ifndef VIPSIM_SHM_H
#define VIPSIM_SHM_H

#include <stdint.h>

#define VIPSIM_SHM_NAME  "/vipsim-frames"
#define VIPSIM_SHM_MAGIC 0x31535056u   /* "VPS1" little-endian */
#define VIPSIM_SHM_VERSION 1u

// Pixels start here, past the header, on a cache-line boundary.
#define VIPSIM_PIXEL_OFFSET 128u

struct vipsim_shm_header {
    uint32_t magic;         // VIPSIM_SHM_MAGIC, so a stale or foreign segment is refused
    uint32_t version;       // VIPSIM_SHM_VERSION
    uint32_t width;
    uint32_t height;
    uint32_t stride;        // bytes per row; ARGB8888, premultiplied
    uint32_t seq;           // producer increments after each completed frame

    // The rectangle that should receive input, in surface coordinates. Everything outside
    // it stays click-through. Width or height <= 0 means the whole surface is
    // click-through, which is the normal state -- this is the Wayland equivalent of the
    // infoState/tutorialState flags the other two platforms use to make the panel usable.
    int32_t panel_x;
    int32_t panel_y;
    int32_t panel_w;
    int32_t panel_h;

    uint32_t quit;          // producer sets this to ask the presenter to exit cleanly
    uint32_t reserved[5];
};

static inline uint64_t vipsim_shm_size(uint32_t stride, uint32_t height)
{
    return (uint64_t)VIPSIM_PIXEL_OFFSET + (uint64_t)stride * (uint64_t)height;
}

#endif /* VIPSIM_SHM_H */

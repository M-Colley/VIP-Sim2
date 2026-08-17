# Linux port — Wayland-native design

Status: **foundation**. The Unity side builds for `StandaloneLinux64` (first verified
August 2026 — the CI entry existed but had never actually compiled), the platform seam
logs an honest status at runtime, and this document pins the architecture. The two native
components are specified here and deliberately **not** written yet: they can only be
compiled and verified on a real Linux machine, and this project's history shows exactly
what shipping unverifiable code produces.

## The landscape this design is built for (verified August 2026)

- **GNOME 50 (March 2026) removed X11 sessions outright** — first major desktop to do so.
  Ubuntu 26.04 LTS and Fedora 44 ship it. X11 *applications* survive via XWayland; X11 as
  a session is ending. A Linux port that leans on X11 leans on a closing door.
- **`wlr-layer-shell` is the Wayland overlay mechanism**, supported by KWin 6.6, COSMIC,
  Sway, Hyprland, Wayfire, niri, Mir and the rest of the ecosystem — **but not by
  GNOME/Mutter** (verified against the live compositor matrix at wayland.app; the 2019
  requests, mutter#973 and gnome-shell#1141, remain open). GNOME adopts other ext
  protocols (background blur landed in 51), so this is a policy position, not inertia.
- **xdg-desktop-portal + PipeWire is the sanctioned capture path** on every desktop,
  GNOME included, with a compositor-owned consent picker.

## The one constraint that shapes everything

**Unity's own window cannot become a layer surface.** A Wayland surface's role
(`xdg_toplevel` vs `zwlr_layer_surface_v1`) is fixed at creation, and Unity's Linux
player creates its window through SDL as a toplevel (or as an X11 window under
XWayland). SDL has no layer-shell support (upstream issue libsdl-org/SDL#7262, open).
There is no plugin trick that re-roles an existing surface.

So the Windows/macOS shape — "take the engine's window and flip native flags on it" —
is impossible here. The Wayland-native overlay is a **separate presenter**: a small
native client that owns a layer surface, with Unity feeding it frames.

## Architecture

```
┌────────────────────────┐   frames    ┌──────────────────────────────┐
│ Unity player (hidden / │ ──────────► │ presenter (native C client)  │
│ offscreen rendering)   │             │  zwlr_layer_surface_v1       │
│  - effects pipeline    │   control   │  - layer: overlay            │
│  - gaze tracking       │ ◄────────── │  - anchored to all edges     │
│  - panel UI            │             │  - empty input region        │
└───────────┬────────────┘             └──────────────────────────────┘
            │ PipeWire stream
            ▼
┌────────────────────────┐
│ xdg-desktop-portal     │  (ScreenCast: user picks window/monitor
│  + PipeWire            │   in the compositor's own consent dialog)
└────────────────────────┘
```

### Component 1 — capture (`libvipsim_capture.so`)

Portal `org.freedesktop.portal.ScreenCast` over DBus → PipeWire stream → frames into a
Unity `Texture2D`. Works on every desktop including GNOME; the portal picker replaces
VIP-Sim's own window list on this platform (the compositor will not enumerate other
apps' windows for us — that is the Wayland security model working as intended, and it is
the same consent shape as macOS's Screen Recording permission).

C ABI (mirror of the MacCapture/DesktopCapture2 seam so the Unity side stays uniform):

```c
int  vipsim_capture_init(void);
int  vipsim_capture_start(void);            // opens the portal picker; async
int  vipsim_capture_frame_size(int* w, int* h);
int  vipsim_capture_copy_frame(void* rgba, int stride);  // v1: CPU copy
void vipsim_capture_stop(void);
```

v1 copies frames through CPU memory (simple, correct); v2 imports the PipeWire dmabuf
directly as a Unity external texture (zero-copy — required for 4K at full rate).

### Component 2 — presenter (`vipsim-presenter`)

Native Wayland client owning one `zwlr_layer_surface_v1`:

- layer **overlay**, anchored to all four edges, `exclusive_zone -1` (over panels);
- **empty input region → compositor-level click-through.** This is *cleaner* than
  Windows: no `WS_EX_TRANSPARENT` juggling, no focus races — the compositor simply
  never routes input to the surface;
- ARGB buffers, premultiplied alpha. Alpha is load-bearing here exactly as on Windows:
  the compositor composites what we hand it, and the F8 alpha probe remains the
  measurement tool of record;
- `keyboard_interactivity: none`; the panel becomes interactive by toggling the input
  region to the panel rect — the moral equivalent of `infoState`/`tutorialState`.

Frame transport Unity → presenter: v1 shared memory (`wl_shm`) fed by
`AsyncGPUReadback` (~500 MB/s at 4K30 — measurable, and acceptable to prove the
pipeline); v2 `zwp_linux_dmabuf_v1` GPU handoff.

### Open problem, stated rather than hidden: the global cursor

Wayland does not let clients read the global pointer — deliberately. Mouse-following
mode therefore has no clean native path. Candidates, in preference order:

1. **Webcam gaze tracking as the primary pointer source on Linux.** VIP-Sim is unusual
   in *having* a second pointer; on this platform it may simply be the default.
2. The Unity process is an XWayland client and can `XQueryPointer`; coordinates are
   usable on today's major compositors but not guaranteed by anything.
3. Compositor-specific interfaces (hyprctl etc.) — a maintenance treadmill; last resort.

### GNOME

No layer-shell → the presenter cannot run there. Options, decided later with data:
run the Unity window itself under XWayland with X11 overlay tricks (works today,
lives on borrowed time), or declare the overlay unsupported on GNOME while capture
(portal) still works. Revisit if GNOME's position moves.

## Phases, each gated on verification on a real Linux machine

1. **Alpha spike** (days): presenter skeleton alone — layer surface, ARGB test pattern,
   empty input region. Proves compositing + click-through on KWin and one wlroots
   compositor before anything is invested in transport.
2. Capture plugin v1 (portal + PipeWire, CPU frames).
3. Transport v1 (shm) wiring Unity → presenter; effects visible end-to-end.
4. dmabuf v2 for both directions; performance pass with the F11 benchmark.
5. Packaging: tarball/AppImage, `.desktop` file, `setup.sh` equivalent (the execute bit
   dies in transit exactly as it does for macOS); flip CI's Linux entry from
   experimental once the licence secrets exist.

## What exists in-repo today

- `VipSimBuild.BuildLinux()` — now actually exercised; the player builds.
- `TransparentWindow` logs a one-shot, truthful status on Linux (session type, Wayland/
  X11 displays) instead of the misleading Windows-flavoured acquisition error.
- Everything platform-neutral ports untouched: effects and shaders, gaze stack
  (MediaPipe ships `libmediapipe_c.so`), presets, tutorial, F1 panel, DisplaySwitcher,
  theme. uWindowCapture compiles but is inert on Linux; the window list stays empty
  until Component 1 replaces it behind the same `thereIsActiveWindow` gate.

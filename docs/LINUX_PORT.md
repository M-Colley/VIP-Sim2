# Linux port — Wayland-native design

Status: **foundation, runtime-verified**. The Unity side builds for `StandaloneLinux64`
(first verified August 2026 — the CI entry existed but had never actually compiled),
and the player has now **run on Linux** — under WSLg on the development machine: 30s
without a crash, OpenGL 4.5 via Mesa, and the platform seam reporting real environment
values (`wayland-0`, XWayland `:0`). Two expected gaps showed as designed: capture inert
(uWindowCapture cannot load its Win32 native library — see the section on those three log
lines below) and no transparency.

**The overlay design is now proven, not just specified.** The Phase 1 presenter spike in
`linux/presenter/` binds `zwlr_layer_shell_v1`, takes a full-output layer surface and gets
per-pixel alpha composited correctly -- verified in a nested Sway session under WSLg, with
the capture committed alongside it. The capture component (portal + PipeWire) is still
specification only.

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

**The plugin maps buffers itself instead of using `PW_STREAM_FLAG_MAP_BUFFERS`.** That
flag makes libpipewire map each buffer with the protection implied by its data flags:
`READABLE` gives `PROT_READ`, `WRITABLE` gives `PROT_WRITE`, and neither gives
`PROT_NONE`. xdg-desktop-portal-wlr publishes buffers with only `SPA_DATA_FLAG_MAPPABLE`
set, so the mapping came back `PROT_NONE` — a page-aligned, entirely plausible-looking
address that segfaults on the first byte read. Nothing reports an error, because nothing
failed: the stream negotiates, reaches `STREAMING`, hands over a buffer, and the consumer
dies touching it. Mapping the fd directly with `PROT_READ` does not depend on which side
is newer. Two related rules the same debugging session produced: request `SPA_DATA_MemFd`
only — a `MemPtr` address belongs to the process that produced it and is a valid-looking
pointer into nothing across a process boundary — and take each frame's extent from the
buffer's own `maxsize`/`offset`/`stride` rather than from the negotiated format, because
the two do disagree.

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

### The global cursor — decided: webcam gaze is the default here

Wayland does not let clients read the global pointer, deliberately, as part of the same
isolation that stops one client reading another's surface. `Input.mousePosition` reports a
position inside Unity's *own* window, and on this platform that window is not the overlay
— the overlay is the presenter process. So the mouse position VIP-Sim can see bears no
relation to where the pointer is over the screen being simulated.

Mouse-following would therefore place every gaze-contingent symptom in the wrong place
while looking like it worked, which is worse than not offering it. `GazeTracker` now
switches to `UnitEye` on Linux at startup and says so in the log.

VIP-Sim is unusual in having a second pointer to fall back on: on Windows and macOS webcam
gaze is the opt-in, here it is the only source that means anything. Calibration (F9)
matters more on this platform than on any other. Mouse remains selectable for anyone who
understands the caveat.

Rejected: `XQueryPointer` through XWayland works on today's compositors but is guaranteed
by nothing and is a bet on a deprecated path; per-compositor interfaces (hyprctl and
friends) are a maintenance treadmill.

### GNOME

No layer-shell → the presenter cannot run there. Options, decided later with data:
run the Unity window itself under XWayland with X11 overlay tricks (works today,
lives on borrowed time), or declare the overlay unsupported on GNOME while capture
(portal) still works. Revisit if GNOME's position moves.

## Local development loop (WSLg on the build machine, probed August 2026)

**This loop is now proven, not theoretical.** Nested Sway under WSLg runs the presenter
and layer-shell works inside it, so the whole edit-compile-run cycle happens on the
Windows build machine. Installing sway plus grim was the entire setup.

**A full portal stack also runs here**, which an earlier version of this document said
it could not. `xdg-desktop-portal`, `xdg-desktop-portal-wlr`, PipeWire and WirePlumber
install and run inside the nested Sway session, with `chooser_type=none` picking the only
output so the run needs nobody at the keyboard. One trap: the nested session must be
given its **own `XDG_RUNTIME_DIR`**. WSLg already runs a PipeWire daemon on the default
one and holds `pipewire-0.lock`, so a second daemon dies at startup and every client
silently falls through to WSLg's audio-only one — which has no session manager for video,
so the screencast node is published and never linked and the stream sits at `paused`
forever, with no error anywhere. Sway still needs to reach WSLg's compositor, so its
socket is passed as an absolute path, which libwayland uses verbatim.

WSL2/WSLg on the Windows build machine runs the player (verified: 30s, OpenGL 4.5,
seam reporting `wayland-0` / XWayland `:0`) and now carries gcc 15 plus
`libwayland-dev`/`wayland-protocols`. **WSLg's own compositor exposes no layer-shell**
(18 globals, zero `zwlr_layer_shell` — measured with `wayland-info`), so the presenter
cannot run against it directly. The loop that works: compile in WSL, run the presenter
inside a **nested compositor** that supports layer-shell (Weston 14+ or labwc/Sway as a
WSLg window). Final verification still wants a real distro, but the edit-compile-run
cycle no longer does.

## Phases, each gated on verification on a real Linux machine

1. ~~**Alpha spike**~~ — **DONE, and it passed.** See `linux/presenter/`. Verified
   2026-08-18 in a nested Sway 1.11 session: `zwlr_layer_shell_v1` v4 bound, the layer
   surface configured at full output size, and per-pixel alpha demonstrably composited —
   five bands at 100/75/50/25/0% alpha show the compositor's background through
   progressively more. That was the result the whole design hinged on, and it is no longer
   an assumption. Still to confirm on a real distro against KWin, and against GNOME, where
   the presenter is expected to report the missing protocol and exit.
2. ~~**Transport v1**~~ — **DONE.** `wl_shm` segment, producer library and presenter,
   verified in nested Sway: frames cross the process boundary, alpha survives, the
   presenter hot-attaches to a producer that starts later, and the input region switches
   between click-through and an interactive panel rect. `LinuxPresenter.cs` is the Unity
   end, using `AsyncGPUReadback` so the render thread is not stalled.
3. ~~**Capture plugin v1**~~ — **DONE, and verified against a real portal.** Run
   2026-08-19 in a nested Sway session carrying a full stack — PipeWire, WirePlumber,
   `xdg-desktop-portal` and `xdg-desktop-portal-wlr`. The portal created the session and
   handed over a node, the stream negotiated BGRx 1280x720, and `testcapture` read 12
   distinct frames in 12 seconds. The pixels are the proof rather than the frame count:
   the harness repaints the background between two colours and the captured centre pixel
   alternates `BGRA=50 30 20` / `20 30 a0`, which is exactly `#203050` / `#A03020`, with
   the dumped frame 99.97% the expected colour.
4. ~~**End-to-end**~~ — **RUN, and it holds together.** `linux/presenter/run-endtoend-test.sh`
   stages the libraries into a real Linux player, starts it inside the nested session, and
   photographs the result. One process: the player launches the presenter itself, because
   Wayland forcing a second process is our constraint and should not become a second thing
   for the user to start. The chain each run prints:

   ```
   [LinuxPresenter] started .../vipsim-presenter (pid 3455)
   [presenter] compositor offers zwlr_layer_shell_v1 v4
   [presenter] configured: 1280x720
   [presenter] attached to VIP-Sim: 1280x720, stride 5120
   [presenter] input region empty (click-through)
   [LinuxCapture] asking the compositor for a source
   [vipsim_capture] streaming 1280x720
   [LinuxCapture] source is 1280x720
   ```

   **One gap is left, and it is the architectural one this document already predicted.**
   The diagram above says "Unity player (hidden / offscreen rendering)"; nothing implements
   the hiding. Unity's own window is a fullscreen, opaque toplevel, so it sits between the
   real desktop and the overlay: the frame is 79% transparent, the presenter composites
   that alpha correctly, and what shows through is Unity's black window rather than the
   desktop. Closing it needs the render path to stop depending on the player's window size
   — a camera drawing into an output-sized RenderTexture, with the window itself reduced to
   nothing — rather than a flag. It is not a compositor problem and not a capture problem;
   both of those now work.

   A second thing to decide with it: on wlroots the portal only offers whole outputs, so a
   monitor capture contains the overlay and feeds back into itself. Window capture, which
   GNOME's and KDE's portals do offer, avoids that. VIP-Sim now reads
   `AvailableSourceTypes` and asks for exactly one kind rather than hardcoding both, and
   says what a whole-screen-only desktop costs. Measured on sway 1.11: it comes back
   `0x1`, because xdg-desktop-portal-wlr advertises WINDOW only when the compositor
   implements `ext_foreign_toplevel_image_capture_source_manager_v1`, which wlroots 0.19
   does not. There is no protocol by which a client asks not to be captured -- that
   exclusion exists nowhere in wayland-protocols and `ext-image-copy-capture-v1` does not
   add it -- so source selection is the only lever.

### What the player's window turned out to be, measured

Everything below was run rather than reasoned about, because the obvious answers were
wrong twice.

- **The player is a native Wayland client.** `swaymsg -t get_tree` reports
  `shell: xdg_shell`, `app_id: VIP-Sim`, no X11 window id, and the player log says
  "Selected window backend: wayland".
- **Nothing can be hooked in-process.** `objdump -T UnityPlayer.so` exports exactly one
  dynamic symbol, `PlayerMain`. SDL2 is statically linked with every symbol hidden, and it
  reaches libwayland by `dlopen` + `dlsym` rather than by linkage, so neither
  `dlopen(NULL)` + `dlsym("SDL_GetWindowWMInfo")` nor a plain `LD_PRELOAD` of a
  `wl_proxy_*` symbol can see it.
- **`SDL_VIDEODRIVER=x11` does work**, and the whole pipeline survives it: the window
  becomes an XWayland client with a real X11 id and capture, transport and the presenter
  all still run. In WSL this additionally needs a writable `/tmp/.X11-unix` with its
  sticky bit, which WSLg mounts read-only; an unprivileged user namespace with a tmpfs
  over that path is enough.
- **But the XWayland window has no alpha channel** -- `xwininfo` reports `Depth: 24` --
  so it cannot be made transparent from the X11 side, however the compositing is done.
- **And wlroots does not honour an empty `ShapeBounding`.** Setting both the bounding and
  input shapes to nothing with `XShapeCombineRectangles` succeeds, and sway carries on
  drawing the whole window: the desktop behind stayed hidden. The desktop was confirmed
  visible in the same run by photographing it before the player started. So under
  XWayland on wlroots this buys click-through and not invisibility.

### What actually fixed it, and what is left

Neither of those was needed for the main defect. Two things were hiding the desktop, and
both were outside the compositing code:

- **A fullscreen surface tells the compositor to stop painting what is behind it**, so a
  perfectly transparent fullscreen window still composites over black. That is policy, not
  a bug, and it is what made every earlier experiment look like broken alpha. The overlay
  is now a window the size of the display that is simply not marked fullscreen.
- **SDL declares the whole window opaque unless told otherwise.**
  `SDL_VIDEO_EGL_ALLOW_TRANSPARENCY=1` suppresses that, and it has to be set before the
  player starts, so the build writes a launcher beside the player that sets it.

With those two the desktop shows through. What remains is that the player's window is still
*there*: the interface is drawn twice, once by the transparent window and once by the
overlay above it, offset because the window is 1276x693 under a server-side title bar while
the overlay is the size of the output.

**The crux of finishing it is input, not visibility.** The layer surface is click-through by
design, `vipsim_present_set_panel` is the only channel that changes that, and the shared
segment carries no events. Every click that reaches VIP-Sim today reaches it through the
player's own toplevel. Any mechanism that hides that toplevel -- unmapping it, an empty
input region, an alpha multiplier of zero -- takes the mouse away from the tool at the same
moment, and a simulator whose severity sliders cannot be dragged is not a product. Three
approaches were designed and judged against each other, and all three verdicts landed on
this same objection from different directions.

**This was done, and the answer was to stop hiding the window and give the player a
compositor of its own.** `linux/presenter/host.c` is a Wayland server on a private socket;
the player is pointed at it, so its `xdg_toplevel` is created inside the presenter's world
and never reaches the user's compositor. There is then nothing to hide, no decorations to
suppress and no size to disagree about, because the `wl_output` the player is told about is
whatever the real compositor configured the layer surface to be. Measured in nested sway
with the real player: `VIP-Sim toplevels in sway: NONE`, the desktop visible across the
whole output, and the interface drawn exactly once.

Everything the host advertises is a measured floor, and each one fails silently or
misleadingly if it is wrong -- `wl_output` at v1 kills the player with "invalid version",
a missing `wl_seat` segfaults it inside `SDL_Init` before it prints anything, a `done` event
sent late rather than from inside `bind` produces "the video driver did not add any
displays", and a first `xdg_surface.configure` withheld until the client commits deadlocks
both sides in silence. `zxdg_decoration_manager_v1` is advertised for one reason: its
presence stops SDL loading libdecor, and that is what removes the title bar.

What is left of the original order:

1. ~~**Server-side decorations and exact sizing.**~~ Done, and for free: the host owns the
   output the player sizes itself from, and advertising the decoration manager keeps
   libdecor out. Measured: `window geometry 0,0 1280x720`, no offset.
2. **An input path from the overlay back to the player.** Still outstanding, and now the
   whole remaining task: the host's `wl_seat` advertises no capabilities, so nothing in
   VIP-Sim is clickable when it runs this way. It is ours to do rather than a protocol
   problem -- the seat is our own, and the overlay already knows which rectangle should
   catch the mouse.
3. ~~**Hiding the player's window.**~~ Moot. `wp_alpha_modifier_v1.set_multiplier(0)` makes
   a surface invisible while frame callbacks keep flowing, which minimising and unmapping do
   not: sway ignores `set_minimized` outright, and Mutter and KWin stop frame callbacks for
   minimised windows, which would freeze the overlay. Whatever does the hiding must be gated
   on the presenter actually presenting -- GNOME has no layer-shell, so the overlay cannot
   exist there, but Mutter 49.2 *does* implement alpha-modifier, so a mechanism that armed
   on its own preconditions would make VIP-Sim vanish completely on GNOME with nothing to
   replace it and no error anywhere.

Two things the nested host does not solve. Frames still cross through a CPU copy, because
the host offers only `wl_shm`; the hardware path is `zwp_linux_dmabuf_v1`, and it cannot be
written here -- there is no GPU and no `/dev/dri`, so everything above ran on llvmpipe.
Note also that `wl_drm` is dead on modern distros: Mesa ships with `HAVE_BIND_WL_DISPLAY`
disabled, so `zwp_linux_dmabuf_v1` is the only route. And `LIBGL_ALWAYS_SOFTWARE=1` belongs
in the player's environment only while the host is shm-only -- with it set, Mesa goes
straight to the swrast path instead of climbing its three-rung retry ladder and printing
four alarming warnings that are pure noise on a GPU-less machine.
5. **dmabuf** — probed, not implemented, and the probe is why. Nested Sway on a software
   renderer does not advertise `zwp_linux_dmabuf_v1` at all, and the container has neither
   `/dev/dri` nor a `udmabuf` module, so a dmabuf cannot be created by any route here. The
   presenter binds the protocol and reports accepted formats when a compositor offers it;
   the import path itself is left for a machine with a GPU rather than written blind.
6. Packaging: tarball/AppImage, `.desktop` file, `setup.sh` equivalent (the execute bit
   dies in transit exactly as it does for macOS); flip CI's Linux entry from
   experimental once the licence secrets exist.

## The DllNotFoundException lines, and why they are still there

A Linux run logs exactly three `DllNotFoundException`s from uWindowCapture -- at `Awake`,
`OnDisable` and `OnApplicationQuit` of its manager. Worth writing down, because the
obvious fix does not work and was tried:

- **Disabling the manager at runtime does not help.** uWindowCapture's static accessors
  (`UwcWindowList.thereIsActiveWindow` and friends) route through `UwcManager.instance`,
  which `AddComponent`s a manager on demand. Disable the one that exists and the next
  read simply creates another -- the disable actively guarantees it, since
  `FindObjectOfType` skips inactive objects.
- **Not touching the plugin is necessary but not sufficient.** VIP-Sim's own callers
  (`HideMenu`, `HideImpairmentSelection`) are now compiled out on Linux, which removes the
  on-demand creation path. But a `UwcManager` also exists in the scene, and Unity calls a
  scene component's `Awake` during scene load -- before any `RuntimeInitializeOnLoadMethod`
  guard can reach it.

Three lines per session, not per frame, and nothing downstream depends on them. The real
fix is Component 1 below: when the portal/PipeWire backend exists, the Linux build stops
carrying a Win32 capture component at all. A build-time step that deactivates the object
for the Linux target would also work, at the cost of build complexity for cosmetics.

## What exists in-repo today

- `VipSimBuild.BuildLinux()` — now actually exercised; the player builds.
- `TransparentWindow` logs a one-shot, truthful status on Linux (session type, Wayland/
  X11 displays) instead of the misleading Windows-flavoured acquisition error.
- Everything platform-neutral ports untouched: effects and shaders, gaze stack
  (MediaPipe ships `libmediapipe_c.so`), presets, tutorial, F1 panel, DisplaySwitcher,
  theme. uWindowCapture compiles but is inert on Linux; the window list stays empty
  until Component 1 replaces it behind the same `thereIsActiveWindow` gate.

# VIP-Sim Wayland presenter — Phase 1 spike

A ~250-line Wayland client that exists to answer three questions before any effort goes
into frame transport. It does **not** talk to Unity; a spike that also had to move frames
could not tell you which half was broken.

The design it is testing is in [docs/LINUX_PORT.md](../../docs/LINUX_PORT.md).

## The three questions

1. **Can we get a layer surface on the overlay layer, covering the whole output?**
   Unity's own window cannot become one — a Wayland surface's role is fixed at creation
   and SDL creates an `xdg_toplevel` — so the overlay has to be a separate process.
2. **Does per-pixel alpha reach the compositor?** Alpha is load-bearing in VIP-Sim: on
   Windows a wrong alpha channel looks exactly like a dead effect, which cost three rounds
   of misdiagnosis. It has to be proven here, not assumed.
3. **Does an empty input region give click-through?** If so, Linux gets it for free at the
   compositor level, without the `WS_EX_TRANSPARENT` juggling and focus races Windows needs.

## Building

```bash
sudo apt install build-essential libwayland-dev wayland-protocols
./build.sh
```

`wayland-scanner` generates the client stubs from the protocol XML, so nothing generated
is committed. `xdg-shell.xml` comes from the system `wayland-protocols` package — the
layer-shell protocol's `get_popup` references `xdg_popup`, so its interface table has to
be linked in even though the spike never creates a popup.

## Running

```bash
./build/vipsim-presenter
```

Exit codes: `0` drew successfully, `1` could not connect or was refused, **`2` the
compositor does not implement `zwlr_layer_shell_v1`** — which is an answer, not a failure.

You should see a full-screen pattern of five horizontal bands at 100%, 75%, 50%, 25% and
0% alpha over a colour ramp, inside a 2px white frame. If the bands show progressively
more of the desktop behind them, per-pixel alpha works. If they look uniform, it does not.
The white frame proves the surface really covers the whole output. Clicks should pass
straight through to whatever is underneath.

Buffers are `ARGB8888`, which Wayland defines as **premultiplied** — each colour channel
is scaled by its own alpha. Forgetting that yields a washed-out overlay that looks almost
right, the same class of mistake as the alpha squaring found on Windows.

## Where it runs

| Compositor | Layer shell | |
|---|---|---|
| KWin, Sway, Hyprland, labwc, niri, COSMIC, Wayfire | yes | the presenter works |
| GNOME / Mutter | **no** | exits 2 — see LINUX_PORT.md |
| WSLg | **no** | exits 2 — verified on this machine |

WSLg's compositor exposes 18 globals and no `zwlr_layer_shell_v1`, so the presenter cannot
be exercised against WSLg directly. Develop in WSL, run inside a **nested** compositor:

```bash
sudo apt install sway
sway            # opens as a window inside WSLg
# then, from a terminal inside that sway session:
./build/vipsim-presenter
```

## Status

**All three questions are answered yes.** Verified 2026-08-18 in a nested Sway 1.11
session under WSLg, software renderer, background set to solid `#1030C0`:

```
[presenter] compositor offers zwlr_layer_shell_v1 v4
[presenter] configured: 1280x720
[presenter] drew alpha test pattern; input region is empty (click-through)
```

![alpha verified](alpha-verified-sway.png)

1. **Layer surface: yes.** Anchored to all four edges, the compositor configured it at the
   full output size, and the 2px white frame is visible on all four edges of the capture.
2. **Per-pixel alpha: yes.** The five bands show the blue background through progressively
   more — opaque at the top, fully transparent at the bottom. If alpha were being dropped
   the bands would look uniform. This is the result the whole design depends on.
3. **Click-through: the empty input region was accepted** without protocol error. Not the
   same as observing a click land underneath, which needs a second window to click into,
   so treat this one as *very likely* rather than proven.

Also verified: against **WSLg** the presenter reports the missing protocol and exits 2
rather than crashing or hanging, which is the behaviour GNOME users will get.

Still unverified: behaviour on a real distro against KWin or GNOME, and anything involving
Unity — the spike does not touch it.

## Phase 3: frame transport — done and verified

VIP-Sim's frames now reach the layer surface. Three pieces:

| | |
|---|---|
| `vipsim_shm.h` | the whole interface: one header struct in one shared segment |
| `libvipsim_present.so` | producer side, loaded by Unity. No Wayland dependency at all |
| `vipsim-presenter` | owns the layer surface, presents whatever the producer publishes |

The producer writes pixels then bumps a sequence number; the presenter watches it and
presents. There is no lock, deliberately: a torn frame is harmless and 16ms from being
replaced, whereas a hung producer holding a lock would stall the compositor callback loop
and freeze an overlay covering the whole desktop.

Verified 2026-08-19 in nested Sway, background `#104020`:

```
[presenter] no VIP-Sim frames yet; showing the alpha test pattern.
[presenter] configured: 1280x720
[presenter] attached to VIP-Sim: 640x360, stride 2560
[presenter] input region = panel rect (interactive)
```

- **Frames arrive.** Two screenshots a second apart differ, so the moving pattern really
  is crossing the process boundary rather than a first frame having stuck.
- **Alpha survives the transport.** The producer's half-transparent ground shows the
  compositor's green through it; the opaque disc does not.
- **Hot attach works.** The presenter was already running and showing the test pattern
  when the producer started, and picked it up without a restart.
- **The input region switches**, which is the Wayland answer to `infoState` /
  `tutorialState`: outside the panel rectangle the compositor never routes input here.

`testproducer` stands in for Unity against the same library, so what was tested is the
real path rather than a mock of it.

## Next

1. **Capture** — `xdg-desktop-portal` + PipeWire. Separate component, and the one that
   also works on GNOME, where the overlay cannot.
2. **dmabuf** — `zwp_linux_dmabuf_v1` to drop the CPU copies, needed for 4K at rate.
3. **The global cursor** — Wayland does not let a client read the pointer. Still open;
   webcam gaze may simply be the default on this platform.

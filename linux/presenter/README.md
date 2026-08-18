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

## Next, once the above is verified

1. Frame transport, Unity → presenter: `wl_shm` first (simple and correct), then
   `zwp_linux_dmabuf_v1` for zero-copy at 4K.
2. Toggle the input region to the panel rect when the UI needs to be interactive — the
   moral equivalent of `infoState` / `tutorialState` on the other platforms.
3. Capture via `xdg-desktop-portal` + PipeWire, which is a separate component and works on
   GNOME too, even where the overlay cannot.

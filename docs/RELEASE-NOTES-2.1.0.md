# VIP-Sim 2.1.0

Experience your own design through impaired vision. VIP-Sim overlays a live simulation of
vision impairments on any application already running on your desktop, while you keep
using that application normally.

This release adds **Save image** and corrects how several symptoms are drawn. Some of the
corrections change what you see. If you compare screenshots or study material made with
2.0.x against 2.1.0, read *What looks different* first.

## Downloads

| File | Platform |
|---|---|
| `VIP-Sim-Windows-x64.zip` | Windows 10 / 11, 64-bit |
| `VIP-Sim-macOS-universal.zip` | macOS 12+, Apple silicon and Intel |
| `VIP-Sim-Linux-x64.tar.gz` | Linux, Wayland with layer-shell (not GNOME) |

`SHA256SUMS.txt` lists the checksums. Each archive contains the application, a
`READ-ME-FIRST.md` with the first-launch steps for that platform, the licence, third-party
notices and the changelog.

## New

**Save an image of the simulation.** The camera button in the toolbar writes the captured
window with the symptoms applied, cropped to that window and without VIP-Sim's interface,
to `Pictures/VIP-Sim`. A profile is written next to it under the same name; loading that
`.json` reproduces the image. Gaze-following symptoms are centred on the captured window.

## What looks different

- **Blur** applied its vertical pass twice, so vertical blur was √2 times stronger than
  horizontal. It is now the same in both directions, and the area around the captured
  window no longer turns opaque.
- **Foveal darkness** and **Distortion** are circles. They were ellipses stretched to the
  screen's aspect ratio (and badly distorted on 21:9 screens).
- **Wavy Distortion** is symmetric and identical on Windows and macOS. On Windows its warp
  was lopsided; on macOS the whole image was shifted.
- **In-Filling** fills from outside the scotoma on Windows, as it already did on macOS.
- **Central and peripheral vision loss** no longer soften the healthy part of the image.
- **Flickering stars** are 36% less bright (they were brightened by a normalisation error).
- **Teichopsia** no longer lets the real window show through its shimmering ring.
- A profile's **blur severity** now spans the blur slider's whole range. Severity 1 used to
  give almost no blur.

## Fixes you may have run into

- Typing `@` on a German or Austrian keyboard (AltGr+Q) quit VIP-Sim, and so did pressing
  F12 in the captured application. Ctrl+Alt+Q (left Alt) still quits from anywhere; F12
  quits only when VIP-Sim has focus.
- Closing the F1 panel with Esc left the overlay swallowing every click.
- Saved profiles did not load back correctly: the main value of most effects was ignored,
  and blur and nystagmus came back reversed. Four effect rows were paired with the wrong
  effect when a profile was loaded.
- The webcam stayed on after switching gaze to the mouse.
- Saving over a profile on a full or locked disk destroyed the existing file.

The full list, including performance and Linux fixes, is in `CHANGELOG.md`.

## Privacy

UnitEye's CSV logger was switched on in the shipped prefab, so while eye tracking was
running every gaze sample was written to a `CSVLogs` folder next to the application. That
contradicted the privacy notice. It is off in 2.1.0. If you used eye tracking with 2.0.x,
you can delete `VIP-Sim_Data/CSVLogs` (Windows, Linux) or the `CSVLogs` folder inside the
app bundle (macOS).

## Known issues found in this release's review, not changed

These affect how faithfully or how consistently a symptom is drawn. They are listed rather
than silently changed because each needs a decision about the intended stimulus, or testing
on hardware this release was not tested on.

- **Windows and macOS render in different colour spaces** (Linear vs Gamma). Every effect
  that does arithmetic on colour — contrast, bloom threshold, blur, colour vision
  deficiency — therefore produces numerically different output for the same setting on the
  two platforms.
- The **field-loss mask is imported as an sRGB texture**, so the same mask value maps to a
  different blur level on Windows and macOS.
- **Scotoma and floater masks are stretched to the screen's aspect ratio**: a circular
  central scotoma is drawn 1.78 times wider than tall on a 16:9 screen.
- **Nystagmus defaults** run with a 0.88 s foveation period, 0.87 s rise and exponent 2.25,
  not the 0.12 s / 0.15 s / 1.75 the source it cites gives. The slider maps its value as
  `1 − duration`, and the defaults appear to have been entered as durations.
- **Teichopsia and floater flicker** change every rendered frame, so they flicker faster on
  a 144 Hz display than on a 60 Hz one.
- **Preset severities are uncalibrated** starting points, as before.
- macOS: on a second display the cursor is mapped against the main display, and a captured
  window is drawn at 16:9 whatever its shape.
- Linux: the hosted player receives no keyboard input, HiDPI outputs are upscaled, and the
  screen-capture picker blocks the interface while it is open.

## Platform notes

**Windows** — unblock the zip (right-click → Properties → Unblock) before extracting.
**macOS** — the app is unsigned: follow `READ-ME-FIRST.md`, then grant Screen Recording and
restart the app. **Linux** — run `VIP-Sim.sh`; it needs a compositor implementing
`zwlr_layer_shell_v1` (Sway, KWin, Hyprland, labwc, niri), not GNOME.

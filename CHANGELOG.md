# Changelog

All notable changes to VIP-Sim are recorded here. Versions follow
[Semantic Versioning](https://semver.org/).

---

## [Unreleased]

### Added

- **Save an image of the simulation.** A camera button in the toolbar writes what you are
  looking at — the captured window with the symptoms applied, cropped to that window and
  with none of VIP-Sim's own interface in the frame — to `Pictures/VIP-Sim`. A profile is
  written beside it under the same name, so the image shows what it looked like and the
  `.json` loads back in to reproduce it. Gaze-following symptoms — central vision loss
  above all — are rendered at the middle of the captured window rather than wherever the
  pointer happens to be, because where the pointer happens to be when you press Save is the
  Save button, and a scotoma parked in the corner is not what you were studying. Until now the only way to get a picture out was a
  developer hotkey behind `-vipsim-dev`, which captures the whole screen including the
  toolbar: a diagnostic, not something to put in a design review.

### Fixed

- **Switching a symptom off no longer switches the whole simulation off.** The per-effect
  parameter panel stored its open/closed state in the master Enable slider, and that
  slider is what gates both the panel and the effect list. So closing one effect's
  parameters set the master switch to zero and took every symptom off the screen with it.
  The switch was left looking half-thrown, because its fill colour is set by its own
  on/off events — which never fired — while its knob follows the slider value, which had
  been moved behind its back. The panel now has state of its own.
- **Picking a window no longer switches a symptom on by itself.** A row's state lives in
  two places: the sprite the master switch reads, and a flag the gear logic reads. `Start`
  set the sprite and left the flag alone, so there was a window in which they disagreed —
  and the master switch, cycling as a window is selected, pressed a row in that state. One
  click to pick a window was enough to end up with an effect running. `Start` now sets
  both, and the master switch refuses to act on a row whose two records disagree, saying
  so in the log instead.
- **A profile loaded before the effect list has ever been shown now reaches it.** The
  binder looked the list up with `GameObject.Find`, which skips inactive objects, so
  loading a profile with the list hidden updated no rows at all.

### Changed

- **The diagnostics report the effect list's own state** — `ROWS list=shown|HIDDEN
  paramsPanel=open|closed` — next to what is actually running. Every fault in this entry
  is a disagreement between two records of the same thing, and this is what makes such a
  disagreement visible in a user's log rather than only on their screen.

---

## [2.0.1] — 2026-08-22

### Fixed

- **The capture is placed on the screen the overlay is actually on.** Window rectangles
  arrive from Windows in global desktop coordinates; the placement made them local by
  subtracting `Screen.mainWindowPosition`, which is measured from the display the overlay
  is on and is therefore `(0,0)` on every monitor. On one display the two spaces coincide
  and everything was correct. On two, every capture was drawn the distance between the
  monitors away from where it belonged — so a window on the other screen showed nothing,
  and a window near the desktop origin appeared at the wrong size. It now asks Windows
  where the overlay is, in the coordinates the capture plugin uses.
- **A window on another screen says so**, instead of correctly drawing nothing. It cannot
  be shown over a screen it is not on without misplacing every click relative to the
  content, so the overlay names the window and points at F3.
- **A minimised window no longer drags the capture off screen.** Windows parks minimised
  windows at (-32000,-32000) and the placement followed them there.
- **Loading a profile switches its symptoms on.** The binder set the menu row's active
  state, which is not what makes an effect run — every effect is a component on the camera
  rig, and `enabled` is the only switch. Parameters were applied to effects that stayed
  dark, and the load reported success.
- **Loading a profile no longer removes the other symptoms from the list.** Switching an
  effect off deactivated its row, so a profile deleted every symptom it did not mention
  from the interface. A profile decides what is switched on, never what is available.
- **Toggling an effect on a hidden list no longer logs an error**, and switched-off effects
  no longer log a warning each for being switched off.
- **The settings labels get the font they were meant to have** — the lookup asked
  `Resources` for a path that has never existed.

### Removed

- **The manual window-size dialog** (X-Offset, Y-Offset, Zoom). It existed for when
  "the automatic detection of the window size was unsuccessful", which the 1:1 placement
  work settled. It was also actively harmful: after a single Apply it rewrote the capture
  plane's position and size ten times a second for the rest of the session, from fields
  that were no longer on screen, overwriting every automatic placement.

### Changed

- **The F1 panel is three sections** — Symptoms, Display & text, Help & updates — instead
  of all of it at once with nine controls in the footer.
- **Hover help waits** about six tenths of a second, so crossing the toolbar no longer
  flashes every button's description.
- **The diagnostics report what the effect list claims** (`ROWS`) alongside what is
  actually running (`ALPHA ... enabled(n)`), and the effect count now covers every effect
  the application manages rather than the subset sharing one base class. It undercounted by
  four, and that undercount was twice read as evidence of a bug that did not exist.

---

## [2.0.0] — 2026-08-18

The modernisation release. VIP-Sim moves from a working research prototype to something
that can be handed to someone who was not in the room when it was written.

### Added

- **First-run tutorial.** A four-page walkthrough on first launch — what the tool is, how
  click-through works, picking a window, symptoms and their settings, gaze tracking,
  moving between monitors, and how to quit. Re-openable from the F1 panel.
- **In-app symptom reference (F1).** Every simulated symptom explained in plain language
  next to its clinical term, grouped by the part of vision it affects, with a link to the
  UIST'25 paper.
- **Multi-monitor support.** Move the overlay to any connected display with **F3** or from
  the F1 panel; the choice is remembered between sessions.
- **Plain-language effect names and condition presets**, replacing the clinical
  vocabulary in the effect list. Preset severities are uncalibrated starting points and
  are labelled as such.
- **Update check.** Tells you when a newer version exists; links to the release page, never
  downloads anything, and can be switched off. Documented in `docs/PRIVACY.md`.
- **Support affordances.** Report-a-problem link and a one-click copy of the diagnostics
  folder path, in the F1 panel.
- **Crash and error reporting** to a local file the user can attach to a report.
- **Consent-gated research telemetry.** Off unless explicitly enabled; refuses to send
  silently.
- **Tooltips** on the toolbar, and hover feedback consistent across every toolbar button.
- **Linux build foundation.** The project now compiles and runs for Linux; the
  Wayland-native overlay design is documented in `docs/LINUX_PORT.md`. Window capture and
  transparency are not yet implemented there.
- **Linux presenter spike** (`linux/presenter/`). A standalone Wayland client proving the
  overlay design: layer surface on the overlay layer, per-pixel alpha composited by the
  compositor, empty input region for click-through. Verified in a nested Sway session.
- **Linux frame transport and capture.** VIP-Sim feeds the Wayland presenter over shared
  memory, and screen capture goes through xdg-desktop-portal and PipeWire. On Linux the
  portal's picker replaces the window list, because Wayland does not let a client
  enumerate another application's windows.
- **Webcam gaze is the default on Linux.** Wayland does not expose the global pointer, so
  mouse-following cannot track the screen being simulated — see `docs/LINUX_PORT.md`.
- **Signing and release tooling** — `tools/sign-macos.sh`, `tools/sign-windows.ps1`,
  entitlements, and `docs/RELEASE.md`.
- **Legal documentation** — `THIRD-PARTY-NOTICES.md`, `docs/PRIVACY.md`, `docs/EULA.md`.
- **Accessibility of VIP-Sim's own interface.** Keyboard operation (Tab/Shift+Tab in
  reading order, arrows, Enter), a high-contrast focus outline, a text-size control from
  80% to 250%, and a high-contrast palette — all in the F1 panel, all persisted.
  Documented, including what does not work, in `docs/ACCESSIBILITY.md`.

### Changed

- **Unity 6000.5.8f1** (Unity 6.5) across both projects.
- **Redesigned the in-app panels.** The symptom reference and tutorial now share a
  generated visual language — rounded surfaces, a type hierarchy, primary/secondary
  buttons, and metrics that scale with the display instead of assuming 1080p.
- **Mouse-following is the startup default.** Eye tracking is opt-in, so the webcam is not
  touched at launch.
- **Developer hotkeys (F6/F7/F8/F10/F11) are gated** behind `-vipsim-dev`. F1, F3, F9 and
  Ctrl+Alt+Q remain available to everyone.
- **Removed the per-eye duplication** left over from the retired VR rig — 816 lines, with
  rendering output verified identical before and after.
- **Replaced ALGLIB** with a self-contained RBF interpolator, removing a GPL dependency
  from a product intended for distribution.
- **Version metadata**: `2.0.0` rather than `2.0.0beta`; the macOS bundle identifier is
  pinned explicitly and the App Store category is no longer "games".
- Panel layout values moved into a `VipSimUiTheme` asset instead of being embedded in an
  editor script.
- 514 compiler warnings → 0.

### Fixed

- **Effects that rendered nothing when enabled alone.** Several shaders blended their
  output against its own alpha, squaring it; on a compositor-backed overlay that made the
  result invisible. Found by measurement, fixed in seven shaders.
- **Captured windows placed 1:1** where the real window is, aligned to the painted frame
  bounds rather than the invisible resize border, and now correct on non-primary monitors.
- **macOS: captures were stretched to the screen's shape.** Each capture keeps its own
  aspect ratio.
- **macOS: the permission screen no longer crops** its own content on displays whose aspect
  differs from the authoring resolution.
- **The monitor switch now survives the move** — the window is taken out of full screen
  before being moved, rather than in the same frame, which is the sequence Unity documents.
- A second, stray cursor drawn over the desktop by the gaze subsystem.
- The settings panel staying open for a disabled effect.
- The effect list being deadlocked by its own visibility gate.
- Calibration and info toolbar buttons that had borders and no hover feedback.
- Footer buttons rendering as unreadable slivers on 4K displays and on laptops.
- **Three symptoms that did nothing when switched on.** Cataract and Color Vision
  Deficiency shipped with severity 0 -- Cataract's shader returns early at
  zero, so it was a literal no-op -- and Contrast Sensitivity shipped at neutral
  brightness and contrast. Switching them on changed nothing, which is indistinguishable
  from a broken effect.
- **Degree-to-pixel conversion used a hardcoded screen width.** Blur, Double Vision and
  Eye Tremor converted visual angle to pixels with a constant of 1334 or 2560, so on any
  other display every degree-based severity was mis-scaled -- on a 3840px screen by a
  factor of 2.9. The width is now read from the display in use.

### Known limitations

- **Screen readers cannot read VIP-Sim.** Unity publishes no UI Automation or
  NSAccessibility tree on desktop, so NVDA, JAWS and VoiceOver see one unlabelled window.
  Supporting them needs a native plugin per platform. VIP-Sim is currently usable with low
  vision and not usable non-visually; see `docs/ACCESSIBILITY.md`.

- **On Linux the player runs inside VIP-Sim's own compositor.** The presenter serves a
  Wayland socket, starts the player against it, composites the player's buffer onto its
  layer surface, and forwards the user's pointer and keyboard back the other way. The
  player's window therefore never appears on the user's desktop at all -- which is what
  removes the duplicated interface, the window decorations and the size mismatch, none of
  which could be fixed while it was there. Needs a compositor with `zwlr_layer_shell_v1`:
  Sway, KWin, Hyprland, labwc and niri have it, GNOME does not.

- **Linux is verified in a nested compositor, not yet on a real desktop.** Overlay,
  transport and screen capture all work end-to-end under Sway with a real portal stack,
  including pixel-verified frames. Confirmation against KWin and a real GPU is still
  outstanding, and GNOME is expected to refuse the overlay because Mutter implements no
  layer-shell protocol.
- **Severities are not clinically validated.** They are plausible starting points, and the
  tool must not be presented as showing "what condition X looks like".
- **Not signed or notarised.** Until certificates exist, Gatekeeper and SmartScreen will
  warn on first launch; see `docs/MACOS_README.md`.
- **Multi-monitor switching has not been exercised on multi-monitor hardware.**
- **Linux logs three `DllNotFoundException` lines per session.** The Windows-only capture
  plugin has a manager component in the scene, and Unity runs its `Awake` at scene load,
  before any guard can intervene. VIP-Sim's own code no longer touches that plugin on
  Linux, which removed the on-demand path that could have repeated it, but the three
  scene-driven lines remain until the Linux capture backend replaces the component.

---

## [1.x] — prior research versions

The versions used for the UIST'25 paper. See the paper for the design rationale and
evaluation: https://doi.org/10.1145/3746059.3747704

# Changelog

All notable changes to VIP-Sim are recorded here. Versions follow
[Semantic Versioning](https://semver.org/).

---

## [Unreleased]

### Added: reviews and reports

- **The camera button records a review finding.** Each press saves the captured window with
  the symptoms applied, the same crop without them, and a profile that reproduces the
  simulation. The new **Review** tab in the F1 panel names the product and the reviewer,
  and takes a note, an impact (observation, minor, major, blocker) and the requirements
  each finding concerns: EN 301 549 clauses 4.2.2 (usage with limited vision) and 4.2.4
  (usage without perception of colour), the WCAG 2.2 success criteria on low vision, or
  none.
- **Every change writes three files** to `Pictures/VIP-Sim/Review <date and time>`:
  `report.html` (findings with both images side by side, settings with their units, the
  viewing conditions, method and limitations; prints to PDF from a browser),
  `findings.csv` (one row per finding with Summary, Description, Priority and Labels for
  Jira or Azure DevOps import) and `findings.json` (schema `vipsim-review/1`).
- **Settings in the report carry their units.** logMAR, degrees of visual angle and prism
  dioptres are named; every other value is marked as a position on the effect's own,
  uncalibrated scale.
- **Window titles stay out of reports unless switched on.** A title can name a document or
  a customer, so recording it is a separate choice in the Review tab, off by default.
- The image the camera button saved before had no record of the viewing distance, the
  screen, the version or the unsimulated window. Saved images now go into the review folder
  instead of directly into `Pictures/VIP-Sim`.
- The effect gallery takes `-galleryReview`, which saves one finding through the camera
  button's code path and checks the files it writes.

### Fixed — what the simulation draws

- **Pixelation no longer vanishes at its maximum.** The slider's (and a profile's) maximum
  leaves one cell. The shader sampled cell corners, so that one cell took the four screen
  corners, usually transparent desktop, and the whole overlay went transparent: the real
  window showed through unchanged. It now samples cell centres, clamped inside the frame,
  and keeps at least 8 cells across: one cell filled the screen, desktop included, with a
  single sampled pixel.
- **Foveal darkness darkens the centre of gaze.** At its defaults (radius 0.01, fade 0.05)
  the fade ran inwards from below zero, so the centre was darkened by only 5%. The spot is
  now at full strength inside the radius and fades out over the fade width beyond it.
  **Opacity now means the spot's strength** (0 leaves the image, 1 is black). It was the
  reverse, against its label and against the profiles, which map severity onto it. A saved
  opacity of 0.5, the default, looks the same; other saved values are reversed.
- **Floaters are drawn.** Each floater's shape was Perlin noise above 0.5 from the same
  origin, where the noise is below 0.5, and size 1 (the slider's minimum and the scene
  default) floored to 0 pixels half the time. So the default floaters drew no pixel at all.
  Each floater is now a disc with a noise-roughened edge from its own patch of noise, at
  least one pixel wide. The scene defaults move from size 1 and density 0.5 (50 floaters)
  to size 5 and density 1 (100 floaters).
- **Blur severity is linear in logMAR.** Blur's setting is now the acuity loss in logMAR,
  from 0 (20/20) to 2 (20/2000), where each 0.1 is one line of an acuity chart. A profile's
  severity maps onto it linearly, so severity 0.5 is 20/200. The mapping was linear in
  cycles per degree. Up to severity 0.6 it asked for finer detail than a 1920 px screen
  spanning 80° can show (12 cycles per degree), so the first 60% of the slider and of the
  severity range drew nothing. A 1.5 px blur, now reached at severity 0.5, took severity 0.9.
  The setting is renamed from `maxCPD` to `logMAR`. Settings files and captured profiles
  that carry `maxCPD` are converted when they load. Above 32 px per degree (a 27-inch
  2560 px screen at 60 cm has 45), the strongest settings reach the blur's 20 px limit, and
  the log names the logMAR where that happens.
- **Degrees of visual angle come from the screen's size and the viewing distance.** Blur and
  eye tremor (nystagmus) are set in degrees. Converting degrees to pixels assumed a fixed
  angle across the whole screen width, 80° for blur and 100° for nystagmus and the
  prism-dioptre double vision component. A 24-inch screen viewed from 60 cm spans 48°, so a
  degree came out 1.6 times too small for blur there. VIP-Sim now reads the screen's physical
  width from the monitor's EDID (Windows, and macOS with one display) and takes the viewing
  distance from a new setting, 60 cm by default. Both are in the Display tab of the help
  panel (F1), where a width can also be entered; a monitor that reports no size is taken as
  53.1 cm wide (24 inches). One degree is the number of pixels it covers at the screen
  centre. The setting is stored per machine, so settings files and profiles do not carry
  it. The per-effect `screenWidth_px`, `autoScreenWidth` and `viewingAngle_deg` fields are
  removed.
- **Nystagmus moves the image by its amplitude in degrees.** It rotated the orthographic
  camera, which moved the image by about 2.6 px per degree whatever the screen, and left
  the camera rotated after the effect was switched off: every later render differed from
  the baseline. Nystagmus now shifts the pixels (`artificialRotation`, the mode its tooltip
  gives for 2D images) by its amplitude in degrees through the viewing geometry. At the
  default geometry that is about 14 times the previous motion, so severity 0.5 (8°) moves
  the image by up to 300 px on a 1920 px, 24-inch screen.

### Changed — dependencies

- **UnitEye's runtime matches upstream `main` (083bc07).** `HomulerGaze.cs` takes the fix
  from [wgnrto/uniteye#13](https://github.com/wgnrto/uniteye/pull/13): disabling gaze
  before `HomulerGaze.Start` now pauses the webcam and face-landmark inference.
- **UnitEye's `UnitEyeAPI.cs` matches upstream again.** VIP-Sim's `DisableGaze()` change
  duplicated upstream: `HomulerGaze.OnDisable` already pauses `FaceMeshSolution` and
  `WebCamSource` through its gaze provider. `GazeTracker` also deactivates the whole UnitEye
  rig whenever UnitEye is not the gaze source, so the webcam is still released. The remaining
  differences from upstream are the licence file, no second cursor, the CSV logger off and
  the crosshair texture import setting.

### Build and release tooling

- The CI workflow uses the Node 24 releases of its actions: `actions/checkout@v7`,
  `actions/cache@v6`, `actions/upload-artifact@v7`, `actions/download-artifact@v8`,
  `game-ci/unity-builder@v6` and `softprops/action-gh-release@v3`. GitHub deprecated the
  Node 20 runtime of the earlier majors. The release job now fails if a downloaded
  artifact's digest does not match.

---

## [2.1.0] — 2026-11-23

### Fixed — what the simulation draws

These change the picture. Each was a defect, not a recalibration; where a value's meaning
was ambiguous, the behaviour on screen was kept and the ambiguity is listed under
*Known issues* in the release notes.

- **Blur applied its vertical pass twice.** The "downsample" copy used pass 0, which is the
  vertical blur, and pass 0 then ran again: the vertical sigma was √2 times the horizontal
  one, so the simulated acuity was wrong vertically. Alpha was summed with weight 1 per tap
  and turned the area around the captured window opaque. Even kernel widths shifted the
  image by half a pixel. The kernel now also follows a change of display width.
- **Blur and nystagmus stored their slider values inverted.** The conversion from slider to
  internal value (`30 − maxCPD`, `1 − foveat_d`, …) was written back into the public field,
  so a saved profile reloaded with the severity reversed, reopening the panel showed the
  opposite of what was running, and switching blur off and on forgot its setting. The
  mapping is unchanged; it is no longer stored in the field.
- **Foveal darkness and Distortion were ellipses.** The aspect ratio was `int / int`: 1 on
  16:9 and 16:10 screens, 2 on 21:9, where foveal darkness also stretched the whole frame.
- **Wavy Distortion differed between Windows and macOS.** Its displacement maps were sRGB
  textures: decoded on Windows (Linear colour space), giving an asymmetric, non-linear warp
  that a hand-tuned constant partly hid; not decoded on macOS (Gamma), where the same
  constant shifted the whole image by 0.058 UV. The maps are linear now and decoded exactly.
- **In-Filling sampled from inside the scotoma on Windows.** Its offset texture holds texel
  distances but was sRGB, so three of its four channels were gamma-decoded.
- **Central and peripheral vision loss softened the whole picture.** The frame went through a
  power-of-two, 8-bit linear intermediate (2560×1440 → 2048×1024), so the healthy field was
  resampled too and shadows banded. It is now allocated at the frame's own size and format,
  on first use, and follows resolution changes.
- **Flickering stars went blank for good after one switch off and on**, the star-size slider
  took effect only at the next 5 s regeneration, and every star was 1.56× too bright
  (25 taps divided by 16).
- **Teichopsia let the real window show through its ring** (noise was multiplied into
  alpha) and ran four full-screen passes, three of them discarded.
- **Gaze-following effects could use the previous frame's gaze**; the gaze tracker now runs
  before them.
- Division by zero at the Detail Loss slider maximum and at a vision-loss scale of 0.

### Fixed — profiles and settings

- **Four rows were paired with the wrong effect.** "Distortion" toggles the vortex and
  "Metamorphopsia" the distortion map, but profiles paired them the other way round, and the
  same for Detail Loss / In-Filling. A profile with a vortex in it lit the wrong row, and the
  master switch, which presses lit rows, then switched on an effect nobody asked for.
- **A saved profile did not load back.** `Capture` wrote each effect's primary parameter
  under its own name (`maxCPD`, `amp_deg`, …) and `Apply` rejected those names.
- **Wavy Distortion II was invisible to profiles** — never saved, never switched off.
- **Blur severity covered a thirtieth of its range**: severity 1 gave almost no blur.
- **A malformed value no longer abandons a load halfway** with some effects already on.
- **Loading a profile while the master switch is off** no longer brings back the previous
  profile's effects when the switch is turned on again.
- **Old `.profile` files set every missing value to zero**, and set the scene light's
  intensity to the floaters' intensity. Missing keys now keep their current values.
- **Saving over a profile on a full or locked disk destroyed it.** Files are written beside
  the target and swapped in; read and write failures are logged.
- **A parameter at its maximum was shown at its minimum** in the panel.

### Fixed — application

- **Typing `@` on a German or Austrian keyboard quit VIP-Sim.** Windows reports AltGr as
  Ctrl+Alt, so AltGr+Q matched Ctrl+Alt+Q. And F12 was read globally, so opening DevTools in
  the captured browser quit too. Ctrl+Alt+Q now needs left Alt; F12 quits only when VIP-Sim
  has focus, as documented.
- **Closing the F1 panel with Esc left the whole overlay swallowing clicks.**
- **Save image** saved the wrong region for a window partly off-screen, threw for a window on
  another screen, and failed silently when the file could not be written.
- **The webcam kept running after switching gaze to the mouse**, and disabling gaze left face
  landmark inference running every camera frame.
- **macOS: the window list offered VIP-Sim's own overlay**, and one Material leaked per
  capture.
- **Linux:** a change of output size overran the presenter's buffers; the frame loop could
  stop for good when both buffers were busy; after a crash the presenter stayed attached to
  the dead frame buffer; the overlay outlived a crashed player; a resize between two native
  calls could write past the managed capture buffer; and a capture texture leaked per resize.
  The nested compositor no longer lets SDL's cursor surface replace the frame, checks buffer
  stride, and clears its references when the player destroys its window.

### Privacy

- **Gaze was written to disk.** The UnitEye prefab shipped with its CSV logger enabled, so
  every gaze sample was written under the install folder while eye tracking ran — contrary
  to `docs/PRIVACY.md`, which says gaze is only held in memory. The logger is off. If a
  study turns it on, a built player writes to `persistentDataPath`, with culture-invariant
  numbers. Three gaze recordings from May 2024 that an Editor run had put into the
  repository are removed (they remain in the git history).

### Changed — dependencies

- **UnitEye updated to upstream v0.1.0** ([wgnrto/uniteye](https://github.com/wgnrto/uniteye)
  `f566a12`). It brings upstream's gaze, calibration, drift, CSV and AOI fixes, pairs blink,
  drowsiness and distance with the camera frame their gaze came from, and adds editor
  inspectors. Upstream renumbered the package from 1.1.0 to 0.1.0; it is the newer code.
  VIP-Sim keeps its own differences: no second cursor, the CSV logger off, and disabling
  gaze also stopping face-landmark inference and the webcam.
- `com.unity.inputsystem` 1.19.0 → 1.20.1 and `com.unity.nuget.newtonsoft-json` 3.2.1 →
  3.2.2, as the new UnitEye requires. Editor-only: Rider integration 3.0.36 → 3.1.1, Visual
  Studio integration 2.0.23 → 2.0.29.
- Unchanged because already current: `com.unity.ai.inference` 2.6.1 and MediaPipe Unity
  Plugin 0.16.3.

### Performance

- With the mouse as gaze source, two scene searches and two thrown exceptions per frame are
  gone. The capture-mode check no longer searches the scene every frame.
- Blur does two fewer full-screen blits, Teichopsia three fewer passes, Double Vision one
  fewer blit and no longer holds an unused screen-sized texture (about 45 MB at 4K per eye).
- The diagnostics' alpha probe no longer allocates a full-screen texture and a 33 MB array
  every five seconds.
- Linux: the capture copy is skipped when no new frame arrived; the presenter's pixel
  statistics run every 64th frame instead of every frame.
- Smaller per-frame allocations removed (shader property names, UI hit names, IMGUI
  content), and the panel skin no longer rebuilds and leaks eight textures per A+/A−.

### Build and release tooling

- `VipSimBuild` no longer deletes a non-empty `-buildOutput` folder that is not a player build.
- `package-release.sh` refuses a Linux archive without the presenter library or launcher, and
  reports a missing build instead of failing silently in the checksum step.
- `sign-windows.ps1` signs every unsigned DLL and EXE, including `GameAssembly.dll`.
- The Linux test scripts no longer overwrite the user's portal configuration or VIP-Sim
  preferences.

### Fixed (earlier in this cycle)

- **Windows whose title mentions VIP-Sim can be captured again.** The window list dropped
  any window whose title contained "vipsim" — a filter meant to hide our own overlay that
  also hid a browser showing the VIP-Sim website, the folder the release was unzipped
  into, and any editor with a VIP-Sim file open. The same filter existed a second time in
  the click handler, so after the first was removed the window appeared in the list and
  clicking it did nothing at all. Both now identify our own windows by the owning
  process's name, which also excludes a second copy of VIP-Sim — a case the pid alone
  cannot catch, where two instances each offer to capture the other.
- **Selecting a window no longer risks an exception** where the questionnaire is gated
  off: its Firestore logger is absent in shipping builds and the call was unguarded.

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

# Changelog

All notable changes to VIP-Sim are recorded here. Versions follow
[Semantic Versioning](https://semver.org/).

---

## [2.0.0] — unreleased

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
- **Signing and release tooling** — `tools/sign-macos.sh`, `tools/sign-windows.ps1`,
  entitlements, and `docs/RELEASE.md`.
- **Legal documentation** — `THIRD-PARTY-NOTICES.md`, `docs/PRIVACY.md`, `docs/EULA.md`.

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

### Known limitations

- **Windows capture only.** macOS capture works; Linux has no capture or transparency yet.
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

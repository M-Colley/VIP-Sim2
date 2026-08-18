# Accessibility of VIP-Sim itself

VIP-Sim is a tool for finding visual-accessibility failures in other software. It would be
indefensible for it to be unusable by the people most affected by those failures. This
document states what works, what does not, and what was measured rather than assumed.

**Status: partially accessible. Screen readers are not supported.** That limitation is
stated first because it is the one most likely to matter, and burying it would be the
worst thing this document could do.

Last verified against VIP-Sim 2.0.0 on Windows, 3840×2160.

---

## What works

### Keyboard operation

| Key | Action |
|---|---|
| **Tab / Shift+Tab** | Move between controls in reading order (top to bottom, left to right) |
| **Arrow keys** | Move between neighbouring controls |
| **Enter / Space** | Activate the focused control |
| **F1** | Symptom reference, and the accessibility settings |
| **F2** | Accessibility settings |
| **F3** | Move to the next monitor |
| **F9** | Eye-tracker calibration |
| **Esc** | Close the open panel |
| **Ctrl+Alt+Q** | Quit, from anywhere |

**VIP-Sim must hold keyboard focus first — click its panel once.** This is a property of
what VIP-Sim is: a click-through overlay passes input to the application beneath it, so it
cannot capture keys it has not been given. It applies to every hotkey, not only navigation.

Two defects were found and fixed in 2.0.0, both measured in the scene rather than guessed:

- the EventSystem had no initially-selected object, so keyboard navigation had **no entry
  point at all** — 77 controls were on automatic navigation and none of them reachable;
- the focus colour differed from the resting colour by about 4% luminance, making focus
  effectively invisible.

Focus is now shown as a **high-contrast outline** drawn around the focused control. An
outline is a shape cue, so it does not rely on colour perception.

### Text size

**A −** and **A +** in the F1 panel scale VIP-Sim's own text from 80% to 250%. The setting
persists between sessions. The panels reflow; they do not clip.

Unity exposes no reliable cross-platform "OS text scale" value, so this is a direct control
rather than an inference from a system setting.

### High contrast

A high-contrast palette — pure black ground, pure white text, yellow accent — is available
from the F1 panel and persists. Yellow was chosen because it stays distinguishable under
the common forms of colour vision deficiency.

### Contrast, measured

Computed against WCAG 2.1, default palette:

| Element | Ratio | Required | |
|---|---|---|---|
| Titles and control labels | 16.4:1 | 4.5:1 | Pass |
| Body text | 5.7:1 | 4.5:1 | Pass |
| Section headings | 8.7:1 | 4.5:1 | Pass |
| Secondary buttons | 13.2:1 | 4.5:1 | Pass |
| Primary buttons | 9.1:1 | 4.5:1 | Pass |

All foreground text exceeds WCAG AA. The high-contrast palette exceeds AAA (7:1)
throughout. Decorative hairlines — panel edges and separators — sit near 1.3:1; WCAG 1.4.11
exempts purely decorative elements, and no information is conveyed by them.

### Not colour alone

State is conveyed in words, not only colour: the high-contrast control reads
"High contrast: ON" / "OFF" rather than relying on a highlight, and keyboard focus is an
outline rather than a tint.

---

## What does not work

### Screen readers — not supported

**NVDA, JAWS and macOS VoiceOver cannot read VIP-Sim.** They see one unlabelled window.

This is a property of the engine, not an oversight in the UI. Unity's accessibility module
targets iOS VoiceOver and Android TalkBack; it does not publish a UI Automation tree on
Windows or an NSAccessibility tree on macOS. Supporting desktop screen readers requires a
native plugin per platform, and no claim will be made here until it has been tested with a
real screen reader by someone who uses one.

Practical consequence: **VIP-Sim is currently usable by people with low vision, and not by
people who work non-visually.** For a tool whose output is inherently visual that is a
narrower gap than it first appears — but it is a real one, and it is the top item on the
accessibility backlog.

### Other known gaps

- **The captured simulation is inherently visual** and has no non-visual equivalent.
- **The main toolbar and effect list are icon-driven.** Icons carry hover tooltips and are
  keyboard-reachable, but they have no persistent text labels.
- **The main panel does not resize with the text-size setting.** That setting currently
  affects VIP-Sim's own panels (reference, tutorial, settings), not the toolbar and effect
  list, which are scene-authored uGUI.
- **No reduced-motion setting.** Several simulations animate by definition — eye tremor,
  flickering specks, visual aura. Suppressing that motion would remove the symptom being
  simulated, so the honest answer is to leave those effects switched off rather than to
  damp them.
- **Not tested with users.** Everything here was measured or verified on screen by the
  developer. No testing with disabled users has been done on VIP-Sim's own interface, and
  that is the difference between "meets the checks" and "actually works".

---

## Reporting a problem

Accessibility problems are bugs. Report them at
https://github.com/M-Colley/VIP-Sim2/issues — in the app, press **F1 → Report a problem**,
and **Copy diagnostics path** to attach the logs.

If a barrier stops you filing an issue at all, that is itself the most important thing to
tell us, by whatever route is easiest for you.

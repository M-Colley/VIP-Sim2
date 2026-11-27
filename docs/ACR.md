# VIP-Sim Accessibility Conformance Report (VPAT 2.5 INT)

## Report information

| Field | Value |
|---|---|
| Name of product | VIP-Sim |
| Version | 2.1.0 plus the unreleased review and policy features |
| Report date | 2026-11-27 |
| Product description | Desktop overlay for Windows and macOS (Linux experimental) that simulates 21 vision-impairment symptoms over any application. |
| Contact | [contact] |
| Report type | Self-assessment by the project. No third-party audit. |
| Notes | The unreleased review feature (Review tab, `report.html`, `findings.csv`, `findings.json`) was assessed through code and documentation only. The generated `report.html` is an HTML document that was not evaluated. |
| Evaluation methods | Review of [ACCESSIBILITY.md](ACCESSIBILITY.md), `windows/Assets/Scripts/VipSimAccessibility.cs` and `windows/Assets/Scripts/VipSimSkin.cs`. Contrast ratios in [ACCESSIBILITY.md](ACCESSIBILITY.md) were computed by the developer against WCAG 2.1 for VIP-Sim 2.0.0 on Windows at 3840x2160. No testing with assistive technology and no testing with users. |

## Applicable standards

| Standard | Included |
|---|---|
| EN 301 549 V3.2.1, chapter 4 (functional performance statements) | Yes |
| EN 301 549 V3.2.1, clause 11 (software), which references WCAG 2.1 and, for this report, WCAG 2.2 Level A and AA | Yes |
| EN 301 549 V3.2.1, clauses 5, 6, 7, 9, 10 and 12 | Summarised below |

## Terms

- Supports: the functionality has at least one method that meets the criterion without known defects.
- Partially Supports: some functionality does not meet the criterion.
- Does Not Support: the majority of functionality does not meet the criterion.
- Not Applicable: the criterion is irrelevant to the product.
- Not Evaluated: no evidence was collected for this criterion.

Every Supports entry names a code or document reference in its remarks. "Doc" refers to [ACCESSIBILITY.md](ACCESSIBILITY.md). "Skin" refers to `windows/Assets/Scripts/VipSimSkin.cs`. "A11y" refers to `windows/Assets/Scripts/VipSimAccessibility.cs`.

## Open question

Doc states that the main toolbar and effect list do not resize with the text-size setting. A11y `ApplyUiScale` scales every CanvasScaler in the scene, which includes the toolbar and effect list. The document predates this code and was not re-verified. Entries below follow the more cautious statement.

## WCAG 2.2 Level A

| Criterion | Level | Remarks |
|---|---|---|
| 1.1.1 Non-text Content | Partially Supports | Toolbar icons carry hover tooltips (`ToolbarTooltip.cs`). They have no accessible names for assistive technology (Doc, screen readers). |
| 1.2.1 Audio-only and Video-only (Prerecorded) | Not Evaluated | No audit of media in the interface. |
| 1.2.2 Captions (Prerecorded) | Not Evaluated | No audit of media in the interface. |
| 1.2.3 Audio Description or Media Alternative (Prerecorded) | Not Evaluated | No audit of media in the interface. |
| 1.3.1 Info and Relationships | Does Not Support | Desktop screen readers see one unlabelled window. No UI Automation or NSAccessibility tree is published (Doc). |
| 1.3.2 Meaningful Sequence | Partially Supports | Keyboard order follows screen position, top to bottom then left to right (A11y `RebuildOrder`). No programmatic sequence exists for assistive technology. |
| 1.3.3 Sensory Characteristics | Not Evaluated | Instructions in the tutorial and panels were not checked. |
| 1.4.1 Use of Color | Partially Supports | State is stated in words ("High contrast: ON" / "OFF") and keyboard focus is an outline (Doc, Skin `FocusRing`). The scene-authored toolbar and effect list were not checked. |
| 1.4.2 Audio Control | Not Evaluated | No audit of audio. |
| 2.1.1 Keyboard | Partially Supports | Tab, arrow keys, Enter, Space, F1, F2, F3, F9, Esc and Ctrl+Alt+Q operate the interface (Doc, A11y `Update`). VIP-Sim must first hold focus, which needs one click. |
| 2.1.2 No Keyboard Trap | Not Evaluated | Esc closes the open panel (Doc). Trap behaviour was not tested. |
| 2.1.4 Character Key Shortcuts | Supports | The hotkeys are function keys and Ctrl+Alt+Q (Doc). No single character key shortcut exists. |
| 2.2.1 Timing Adjustable | Not Evaluated | No timed interaction was audited. |
| 2.2.2 Pause, Stop, Hide | Partially Supports | Eye tremor, flickering specks and visual aura animate as the simulated symptom. Each effect can be switched off. No reduced-motion setting exists (Doc). |
| 2.3.1 Three Flashes or Below Threshold | Not Evaluated | Flicker effects exist (Doc). Flash rates and areas were not measured. |
| 2.4.1 Bypass Blocks | Not Evaluated | |
| 2.4.2 Page Titled | Not Evaluated | |
| 2.4.3 Focus Order | Partially Supports | Order follows screen position (A11y `RebuildOrder`). It was not checked against the meaning of each panel. |
| 2.4.4 Link Purpose (In Context) | Not Evaluated | |
| 2.5.1 Pointer Gestures | Not Evaluated | |
| 2.5.2 Pointer Cancellation | Not Evaluated | |
| 2.5.3 Label in Name | Not Evaluated | |
| 2.5.4 Motion Actuation | Not Evaluated | |
| 3.1.1 Language of Page | Not Evaluated | |
| 3.2.1 On Focus | Not Evaluated | |
| 3.2.2 On Input | Not Evaluated | |
| 3.2.6 Consistent Help | Not Evaluated | |
| 3.3.1 Error Identification | Not Evaluated | |
| 3.3.2 Labels or Instructions | Not Evaluated | |
| 3.3.7 Redundant Entry | Not Evaluated | |
| 4.1.2 Name, Role, Value | Does Not Support | No accessibility tree is published on Windows or macOS (Doc). |

## WCAG 2.2 Level AA

| Criterion | Level | Remarks |
|---|---|---|
| 1.2.4 Captions (Live) | Not Evaluated | |
| 1.2.5 Audio Description (Prerecorded) | Not Evaluated | |
| 1.3.4 Orientation | Not Evaluated | |
| 1.3.5 Identify Input Purpose | Not Evaluated | |
| 1.4.3 Contrast (Minimum) | Partially Supports | Panel text measures 5.7:1 to 16.4:1 against 4.5:1, and the high-contrast palette exceeds 7:1 (Doc, Skin `ApplyPalette`). Measured on 2.0.0. The scene-authored toolbar was not measured. |
| 1.4.4 Resize Text | Partially Supports | Text size scales from 80% to 250% and persists (Doc, Skin `UserScale` clamps 0.8 to 2.5). See the known discrepancy for the toolbar. |
| 1.4.5 Images of Text | Not Evaluated | |
| 1.4.10 Reflow | Not Evaluated | Doc states that panels reflow and do not clip. This was not tested at a defined viewport. |
| 1.4.11 Non-text Contrast | Partially Supports | The focus ring uses a dedicated colour and a dark underlay (Skin `Focus`, `FocusRing`). Panel edges sit near 1.3:1 and are treated as decorative (Doc). Toolbar icons were not measured. |
| 1.4.12 Text Spacing | Not Evaluated | |
| 1.4.13 Content on Hover or Focus | Not Evaluated | Tooltips exist (`ToolbarTooltip.cs`). Dismissal and persistence were not checked. |
| 2.4.5 Multiple Ways | Not Evaluated | |
| 2.4.6 Headings and Labels | Not Evaluated | |
| 2.4.7 Focus Visible | Supports | Keyboard focus is drawn as a high-contrast outline around the focused control (A11y focus ring, Skin `FocusRing`, Doc). |
| 2.4.11 Focus Not Obscured (Minimum) | Not Evaluated | The overlay covers the screen. Obscuring of the focused control was not tested. |
| 2.5.7 Dragging Movements | Not Evaluated | |
| 2.5.8 Target Size (Minimum) | Partially Supports | Standard control height is 44 px at 1080p, scaled with the display and the text-size setting (Skin `ControlHeight`). Scene-authored toolbar targets were not measured. |
| 3.1.2 Language of Parts | Not Evaluated | |
| 3.2.3 Consistent Navigation | Not Evaluated | |
| 3.2.4 Consistent Identification | Not Evaluated | |
| 3.3.3 Error Suggestion | Not Evaluated | |
| 3.3.4 Error Prevention (Legal, Financial, Data) | Not Applicable | VIP-Sim has no legal or financial transactions and no user accounts ([PRIVACY.md](PRIVACY.md)). |
| 3.3.8 Accessible Authentication (Minimum) | Not Applicable | VIP-Sim has no sign-in ([PRIVACY.md](PRIVACY.md)). |
| 4.1.3 Status Messages | Does Not Support | Messages are not exposed to assistive technology (Doc). |

## EN 301 549 chapter 4: functional performance statements

| Clause | Level | Remarks |
|---|---|---|
| 4.2.1 Usage without vision | Does Not Support | NVDA, JAWS and VoiceOver cannot read VIP-Sim (Doc). The simulation output is visual by definition. |
| 4.2.2 Usage with limited vision | Partially Supports | Text scaling, a high-contrast palette and a focus outline exist (Doc, Skin). The toolbar and effect list are icon-driven without persistent labels. No testing with users. |
| 4.2.3 Usage without perception of colour | Partially Supports | State is conveyed in words and focus is an outline (Doc, Skin `FocusRing`). The accent colour in the high-contrast palette is yellow (Skin `ApplyPalette`). The scene-authored toolbar was not checked. |
| 4.2.4 Usage without hearing | Not Evaluated | |
| 4.2.5 Usage with limited hearing | Not Evaluated | |
| 4.2.6 Usage without vocal capability | Not Evaluated | |
| 4.2.7 Usage with limited manipulation or strength | Partially Supports | Keyboard operation exists (A11y). Focus must first be given by a click. No testing with alternative input devices. |
| 4.2.8 Usage with limited reach | Not Evaluated | |
| 4.2.9 Minimize photosensitive seizure triggers | Not Evaluated | Flicker effects exist (Doc). Flash rates were not measured. |
| 4.2.10 Usage with limited cognition, language or learning | Not Evaluated | |
| 4.2.11 Privacy | Supports | Text size and contrast settings are stored locally as player preferences (Skin `ScalePref`, `ContrastPref`). The application does not transmit them ([PRIVACY.md](PRIVACY.md)). |

## EN 301 549 other chapters

| Clause | Level | Remarks |
|---|---|---|
| 5 Generic requirements | Not Evaluated | |
| 6 ICT with two-way voice communication | Not Applicable | VIP-Sim has no voice communication. |
| 7 ICT with video capabilities | Not Evaluated | |
| 9 Web | Not Evaluated | The project website is a separate product. |
| 10 Non-web documents | Not Evaluated | |
| 12.1 Product documentation | Partially Supports | Documentation is Markdown in the repository, with an accessibility statement (Doc). It was not tested with assistive technology. |
| 12.2 Support services | Partially Supports | Support is via GitHub issues and the in-app "Report a problem" ([SUPPORT.md](../SUPPORT.md)). Accessibility of those channels was not evaluated. |

## Legal disclaimer

This report is a self-assessment by the VIP-Sim maintainers. It is not a certification and no external body has verified it. Update it when the findings in [ACCESSIBILITY.md](ACCESSIBILITY.md) change.

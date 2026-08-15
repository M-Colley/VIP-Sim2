# Roadmap: product gaps and engineering debt

Written at `0fcda1f`, after the alglib removal. Ordered by leverage, with the specific
traps this codebase has already sprung so they are not rediscovered.

Baseline to revert to if anything here goes wrong: **`83973f6`** — user-confirmed working
on Windows, and `2.0.0beta` is confirmed running on macOS.

---

## 1. Remove the per-eye duplication  *(highest leverage in item 6)*

Every effect exists **twice**, once per eye, from the retired FOVE VR rig: 19
`LinkableBaseEffect` subclasses × 2 cameras = 38 live components, plus the whole linking
mechanism that exists only to copy `[Linkable]` fields from left to right each frame.
The product is monoscopic. This is pure dead complexity and it is what made the alpha
bug so hard to diagnose — "the effect is enabled" was true of an instance that was not
the one reaching the screen.

### Camera facts, established from the scene

| | Camera A `&197203504` | Camera B `&858823455` |
|---|---|---|
| Projection | perspective | **orthographic** |
| Clear flags | **1 = Skybox** | 2 = SolidColor |
| Background alpha | 0 | 0 |
| Depth | **0** | **0** |
| Enabled | yes | yes |

B is orthographic, so B is the camera `AlignBoxColliderWithCamera` drives — that method
forces `orthographic = true` — and therefore the one showing the capture. A is the
perspective/Skybox camera, i.e. `RightEye`.

**Both are at depth 0, so which writes the backbuffer last is undefined.** Fix that
first, on its own, whatever else happens.

**The trap:** camera A clears to **Skybox, which is opaque**. On an alpha-composited
overlay that clear is a plausible source of the non-zero alpha that makes anything
visible at all — which would explain why disabling `RightEye` removed the overlay
entirely even though `LeftEye` is the camera the code treats as primary and the one
drawing the capture. If that holds, deleting A takes the alpha source with it and
reproduces the "no overlay at all" failure that already cost one revert. The surviving
camera's clear flags and background alpha must be corrected in the *same* step, and the
result checked with F8's alpha readout, not by eye.

Confirm before deleting: give the two cameras distinct depths and distinct background
colours, run, and read the F8 alpha distribution to see which clear is supplying alpha.

**Establish first, before deleting anything:** which camera actually reaches the display.
There are two full-screen cameras at depth 0 (`CameraRig/LeftEye`, orthographic,
SolidColor clear; `CameraRig/RightEye`, perspective, Skybox clear) and **the render order
between them is undefined**. Disabling `RightEye` during this session removed the overlay
completely, so it is not the redundant one despite the code treating Left as primary.
Resolve this empirically — set distinct clear colours, screenshot with F6 — do not
reason about it.

Then, in order, building and checking between each step:

1. Give the surviving camera an explicit depth so ordering stops being luck.
2. Delete the other camera and its 19 effect components from the scene.
3. Strip `LinkableBaseEffect` down to `BaseEffect`: remove `isLeftEye`, the twin
   lookups, `LinkEyes`, the `[Linkable]` reflection cache and the field copiers.
   Keep the class name so the 19 subclasses and all scene references still bind.
4. Remove the `Update()` fallback that calls `OnEnable()` when the twin is missing —
   it exists solely to paper over the linking having failed.
5. Sweep the 19 subclasses for `isLeftEye` and `base.OnEnable()`/`base.OnDisable()`.

**Verify with:** `VipSimAlphaTest.Run` (17/17 must still pass) and each effect switched on
alone — that is precisely the case the duplication used to break.

## 2. Plain-language effect names and presets  *(cheapest slice of item 5)*

The list reads *Teichopsia*, *Metamorphopsia2*, *Glare Vision/Photophobia*. Correct for a
clinician, opaque to the designer who is the likely buyer. Add plain-language labels with
the clinical term secondary, and presets for common conditions (macular degeneration,
glaucoma, diabetic retinopathy, cataract) that set several effects at once. `Metamorphopsia2`
in particular is a developer name that reached the UI.

## 3. Give the settings panel a real selection model

It currently tracks a global open/closed flag plus "which gear was clicked last", with
nothing tying those to the same effect. Two bugs came out of that in one session, and the
first fix — clearing the master toggle — wiped every effect's settings at once. It needs
one piece of state: *which effect is selected*, with the panel deriving from it.

## 4. Replace the Editor-script scene mutation

`UiRefreshSetup` applies layout and colours by mutating the scene from an Editor menu
item. That was expedient under time pressure and is not a configuration mechanism: the
values live in C# constants, the scene is the output, and the two drift. Values belong in
a ScriptableObject the UI reads at runtime.

## 5. Consent for telemetry and biometrics

`FirestoreRESTManager` sends UI interactions to a remote endpoint with no consent step,
and the webcam gaze path is biometric-adjacent. For a sold product in the EU this needs a
lawful basis, a privacy policy, and an off switch. **Highest-priority compliance item.**

## 6. Signing, notarisation, installer

Neither platform is signed. macOS Gatekeeper blocks the app outright — the `chmod`/`xattr`
dance in `MACOS_README.md` is fine for a developer and unacceptable for a customer.
Needs an Apple Developer ID plus notarisation, a Windows code-signing certificate, and a
real installer. Note `bundleVersion` is `2.0.0beta`; Apple expects up to three
period-separated integers, so that needs revisiting before notarisation.

## 7. CI that actually builds

The `build` and `unity-tests` jobs are **skipped**, not passing — they need
`UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` secrets. A green tick currently means the
repository invariants hold, not that anything compiles. The workflow matrix is already
written; it only needs the secrets.

## 8. Warnings and crash reporting

~500 warnings per build, many `CS0114` (subclasses hiding base members without `new`) and
`CS0618` (deprecated Unity APIs). The `CS0114`s are the same family as the alpha bug —
`OnEnable`/`OnDisable` hiding rather than overriding. No crash reporting exists.

---

## Verification notes for whoever picks this up

- **Alpha is load-bearing.** VIP-Sim composites from framebuffer alpha; a shader with
  correct colour and wrong alpha is invisible and looks exactly like a dead effect. Run
  `VipSimAlphaTest.Run` after any shader change.
- **F6/F7/F8 only fire while the overlay holds focus**, which as a click-through window it
  usually does not. Anything that must be observed reliably belongs in the periodic log.
- **Synthetic clicks do not reach the overlay at all**, so the UI cannot be driven
  programmatically. Screenshots of the pre-selection state look fine while the effect list
  is broken — two layout changes shipped broken for exactly that reason.
- **Retry a failed Unity batchmode build once** before investigating. Four distinct
  transient failures were seen in a single session: FXC shader-compiler crashes, an
  `AccessViolationException` inside the C# compiler, `0xC0000005` in
  `MacStandalonePlayerBuildProgram`, and an error count with no error text.

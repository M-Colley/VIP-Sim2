# Testing VIP-Sim on macOS — step by step

Everything in `macos/` has been changed but **never run**. It was all written and
reasoned from source on a Windows machine, which cannot even open the macOS
project (see "Why Windows can't build this" at the end). Treat this as a first
bring-up, not a regression check.

Minimum: macOS 12.3 (ScreenCaptureKit), Apple Silicon or Intel.

---

## 1. Get the branch

```bash
git clone https://github.com/M-Colley/VIP-Sim2.git
cd VIP-Sim2
git checkout modernize/unity65-pipeline
```

If `packages/` looks empty or short, you are missing Git LFS:

```bash
git lfs install && git lfs pull
```

Sanity check — both must print `6000.5.8f1`:

```bash
grep m_EditorVersion windows/ProjectSettings/ProjectVersion.txt macos/ProjectSettings/ProjectVersion.txt
```

---

## 2. Install Unity 6000.5.8f1

In Unity Hub → Installs → Install Editor → **6000.5.8f1**.

Tick these modules (the last one matters — the default is Mono):

- **macOS Build Support (Mono)**
- **macOS Build Support (IL2CPP)**

---

## 3. Open the project

Unity Hub → Add → select the **`macos`** folder (not the repo root, not `windows`).

First import takes 10–25 minutes: it compiles MediaPipe's native plugin and
imports ~430 MB of package content. Expect the progress bar to sit still.

**Expected on first open:** the Console shows warnings but **no red errors**.
If you see `The type or namespace name 'Ookii' could not be found`, the active
build target is wrong — File → Build Settings → **macOS** → Switch Platform.

---

## 4. Deploy the MediaPipe model *(required — gaze silently fails without it)*

Menu: **UnitEye → Install MediaPipe StreamingAssets**

Console must print `MEDIAPIPE_INSTALL_OK`. Verify:

```bash
ls -la macos/Assets/StreamingAssets/
# expect face_landmarker_v2_with_blendshapes.bytes  (~2.3 MB)
```

It is already committed on this branch, so this should report as already
present. Run it anyway — it is idempotent.

---

## 5. Migrate the UnitEye rig *(required — this is the one I could not run for you)*

Menu: **VIP-Sim → Migrate UnitEye rig to 1.1**

Console must print **`UNITEYE_MIGRATION_OK`**.

**Why this is needed.** UnitEye 1.1 replaced the `Gaze` component with
`HomulerGaze`, which has a new `_mediaPipeGO` field pointing at a
`FaceMeshSolution` + `WebCamSource` subtree. The old Barracuda rig has no such
child. Without this step the app builds and launches fine and then logs
`gaze provider setup failed, disabling HomulerGaze` — every gaze-contingent
symptom silently stops tracking. I ran this successfully on the Windows scene;
the macOS scene still needs it.

If it prints `UNITEYE_MIGRATION_SKIPPED`, it is already wired — fine.

---

## 6. Run the upstream checks

Menu: **UnitEye → Run Smoke Tests** → expect `UNITEYE_SMOKE_TESTS_PASSED`.

Then Window → General → **Test Runner** → EditMode → **Run All**. Two suites:

- *Platform Parity* — every simulation file byte-identical to `windows/`
- *Shader Integrity* — every shader resolves, compiles on Metal, and is in
  Always Included Shaders

Shader Integrity is the one to watch, though with a caveat: a macOS player has
been built before (`macos/VIP_SIM_MacOS_BurstDebugInformation_DoNotShip` is
Burst output from a real macOS build, committed 2025-07-10), so these shaders
**have** compiled for Metal — on Unity 6000.0.34f1. What is untested is Metal on
**6.5 specifically**, which is a much weaker claim: a version bump rarely breaks
HLSL→MSL translation. Treat a failure as a real finding, but do not expect one.

The same caveat applies to the `half`/`fixed` types this code is full of: 23 of
the 24 shader files use them and evidently translated fine.

---

## 7. Play mode

Press Play. Expect no `NullReferenceException`. Then:

1. Pick a window from the capture list
2. Enable **Contrast Sensitivity**, drag severity — the captured content should
   change immediately. This exercises capture + the shader chain without a camera.
3. Switch gaze source to **UnitEye**, then run a calibration

> You **must recalibrate**. UnitEye 1.1 expanded the calibration feature vectors
> (EyeMU 19 → 36), so pre-1.1 calibration files fall back to raw, uncalibrated
> gaze. On Windows this logged
> `No RidgeRegression calibration found; using raw (uncalibrated) gaze`.

---

## 8. Build

Menu: **VIP-Sim → Build → macOS (Universal)**, or:

```bash
/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath macos \
  -buildTarget OSXUniversal \
  -executeMethod VipSim.EditorTools.VipSimBuild.BuildMacOS \
  -buildOutput ./Build/macOS \
  -scriptingBackend il2cpp \
  -logFile ./build-macos.log
```

Then check `grep VipSimBuild build-macos.log` for `result=Succeeded`.

---

## 9. Grant permissions, then **restart the app**

1. Run the app once so macOS registers it
2. System Settings → Privacy & Security → **Screen & System Audio Recording** → enable VIP-Sim
3. System Settings → Privacy & Security → **Camera** → enable VIP-Sim
4. **Quit and reopen.** macOS only applies a new screen-recording grant to a
   fresh process — without the restart you get black frames and it looks like a
   broken overlay.

The app now detects this explicitly and logs a clear message instead of showing
black, so check the log if the overlay is empty:

```bash
tail -f ~/Library/Logs/Zefwih/VIP-Sim/Player.log
```

---

## What I specifically want you to check

These are the macOS changes I made blind. Each has a concrete way to falsify it.

| # | Change | How to test | What a failure looks like |
|---|---|---|---|
| 1 | **ScreenCaptureKit texture rebinding** — the plugin cached the external-texture wrapper forever, so after a resize it pointed at a freed surface | Start capturing a window, then **resize that window** while capture runs | Before: frozen/garbled overlay or a crash. After: overlay follows the resize |
| 2 | **Capture material no longer discarded** — `StopCapture()` used to overwrite the material, so capture worked once per launch | Start capture → Stop → **start again** | Before: second capture shows nothing. After: works every time |
| 3 | **Stop button now exists** — the old code appended a "Stop" entry then filtered it out in the same loop | Look at the window list | The first entry should read "Stop capture" |
| 4 | **Texture no longer latched on frame 1** | Capture a window playing video | Before: frozen first frame. After: live |
| 5 | **Permission detection** | Run with Screen Recording *denied* | Should log the explicit "no Screen Recording permission… quit and reopen" error, not show black silently |
| 6 | **Field Loss parity** | Compare Central Vision Loss at the same severity against Windows | Should now look identical — macOS previously had a clamp Windows lacked |
| 7 | **Vertical gaze direction** (see below) | Enable Central Vision Loss, gaze source = Mouse, move the pointer **up** | The dark spot must move **up** with it. If it moves *down*, the Y flip is inverted on Metal |

### Why test 7 matters most

This is the one place where the two platforms genuinely diverge *by design*, and
the divergence is invisible until you look at it on a Mac.

Nine shaders — `myFieldLoss`, `myFieldLossInverted`, `myBloom`,
`myDistortionMap`, `myFloaters`, `myInpainter`, `myInpainter2`, `myNoise`,
`myScintillate` — contain:

```hlsl
#if UNITY_UV_STARTS_AT_TOP
    _MouseY = 1.0 - _MouseY;
#endif
```

`UNITY_UV_STARTS_AT_TOP` is defined on Direct3D and **not** on Metal. So the
gaze Y coordinate is flipped on Windows and left alone on macOS. That is the
macro doing its job — the two APIs really do disagree on texture origin — but it
means the vertical gaze mapping is the single most likely thing to be wrong on
macOS while being perfectly correct on Windows, and no amount of testing on
Windows can tell you.

Test it with the **mouse** gaze source rather than the webcam: it is
deterministic, and it isolates the shader's coordinate handling from eye-tracking
accuracy. Everything else in the simulation is now byte-identical across
platforms, so this is the remaining platform-conditional behaviour.

Please send me `Player.log` plus the Unity Console if anything fails.

---

## Known, not yet fixed

- `myInpainter2_hacked.cs` and `myInpainter.cs` are dead code (in no scene) and
  still leak textures. Candidates for deletion.
- The POT rounding (`1280×720 → 2048×1024`) still wastes memory on every effect.
- `macos/` carries **two** file-browser libraries: `StandaloneFileBrowser` and
  `Plugins/SimpleFileBrowser`. Only one is needed.

## Why Windows can't build this

`macos/Assets/StandaloneFileBrowser/Plugins/Ookii.Dialogs.dll.meta` is marked
`Exclude Win: 1` with the editor plugin restricted to `OS: OSX`, while
`StandaloneFileBrowserWindows.cs` is guarded by `#if UNITY_STANDALONE_WIN`. On a
Windows editor that guard is true while the DLL is unavailable, so the project
fails to compile before any script can run. That is why steps 4–6 above could not
be done for you.

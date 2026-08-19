# VIP-Sim: A User-Centered Approach to Vision Impairment Simulation for Accessible Design
<!-- GitHub Topics: accessibility, visual-impairment, unity, simulation, hci, user-centered-design -->
> A gaze-contingent vision impairment simulator to support accessible design.

![License](https://img.shields.io/github/license/M-Colley/VIP-Sim2)
![Release](https://img.shields.io/github/v/release/M-Colley/VIP-Sim2)
![Made with Unity](https://img.shields.io/badge/made%20with-Unity-000?logo=unity)

[Max Rädler](https://scholar.google.de/citations?user=HmSPxPsAAAAJ&hl=de&oi=ao), [Mark Colley](https://scholar.google.de/citations?user=Kt5I7wYAAAAJ&hl=de&oi=ao), and [Enrico Rukzio](https://scholar.google.de/citations?user=LEu4D5gAAAAJ&hl=de&oi=ao)

<p align="center">
  <img src="images/teaserfig.png" alt="Teaser Figure" width="600"/>
</p>

---



## 🆕 What's new in version 2.0

VIP-Sim 2.0 is a substantial rebuild of the version described in the UIST'25 paper. The
research contribution is unchanged — same participatory design, same symptom set — but
almost everything around it has been repaired or replaced. The short version: **the
simulation now renders what it claims to, on both platforms, and the tool can be handed to
someone who was not in the room when it was written.**

### Simulation correctness

- **Seven shaders produced nothing when switched on alone.** They blended their output
  against its own alpha, squaring it; on a compositor-backed overlay the result was
  invisible. Found by measuring the alpha channel rather than by looking, because a wrong
  alpha and a dead effect are indistinguishable on screen. This is the most consequential
  fix in the release: some symptoms simply did not work.
- **Captured windows are drawn 1:1 where the window actually is**, aligned to the painted
  frame rather than the invisible resize border, and correct on non-primary monitors.
- **The retired VR rig is gone.** Every effect existed twice, once per eye, from the
  FOVE-era stereo setup — 38 live components and a per-frame linking mechanism for a
  monoscopic product. 816 lines removed, output verified identical before and after. That
  duplication is what made the alpha bug so hard to find: "the effect is enabled" was true
  of an instance that never reached the screen.

### macOS

- **It runs.** The macOS build previously compiled but had never been launched. It now
  builds as a signed-ready universal binary, and the transparency, capture and permission
  paths have been exercised.
- **Captures keep their own shape** instead of being stretched to the screen's aspect.
- **The bundle identifier is pinned.** It was being derived from the product name at build
  time, so renaming the app silently invalidated every Screen Recording and Camera
  permission the user had granted.

### Usable without prior knowledge

- **Plain-language symptom names** in place of clinical vocabulary, grouped by the part of
  vision they affect, with condition presets.
- **An in-app reference (F1)** explaining every symptom beside its clinical term, and a
  **first-run tutorial**. Previously the panel was discoverable only if you already knew
  it was there.
- **Mouse-following is the default**, so the webcam is not touched at launch; eye tracking
  is opt-in.
- **Multi-monitor support** — move the overlay to whichever display the work is on.

### The tool's own accessibility

A simulator for finding visual-accessibility failures that a low-vision designer could not
operate would be indefensible. Keyboard operation, a visible focus indicator, text size
from 80% to 250%, and a high-contrast palette. What still does not work — screen readers
— is stated plainly in [docs/ACCESSIBILITY.md](docs/ACCESSIBILITY.md) rather than left
implied.

### Distribution and licensing

- **ALGLIB replaced** with a self-contained RBF interpolator, removing a GPL dependency
  that would have prevented distribution.
- **Third-party notices ship with the binaries**, which MIT requires and the previous
  release did not do.
- **Telemetry is consent-gated and off by default**; the update check sends no identifier.
- **Unity 6.5 (6000.5.8f1)**, 514 compiler warnings down to zero, crash logging, and an
  in-app route to report a problem.

### Linux

The project builds and runs on Linux for the first time. The overlay itself is not there
yet, but the Wayland design is proven rather than assumed: a layer-shell presenter spike
demonstrates per-pixel alpha compositing and click-through. See
[docs/LINUX_PORT.md](docs/LINUX_PORT.md).

Full detail in [CHANGELOG.md](CHANGELOG.md), including the known limitations.

---

## 🧠 Overview

**VIP‑Sim** is a symptom-based, Unity-powered desktop overlay simulator that accurately visualizes visual impairments based on real user input. Built through a **participatory design process** with **7 visually impaired participants (VIP)**, VIP‑Sim applies customizable shaders over any Windows/macOS design tool, simulating over **20 impairments** in a **gaze-contingent** manner.

People with vision impairments (VIPs) often rely on their remaining vision when interacting with user interfaces. Simulating visual impairments has proven to be an effective tool for designers, fostering awareness of the challenges faced by VIPs.
While previous research has introduced various vision impairment simulators, none have yet been developed with the direct involvement of VIPs or thoroughly evaluated from their perspective.
To address this gap, we developed VIP-Sim. This symptom-based vision simulator was created through a participatory design process tailored explicitly for this purpose, involving N=7 VIPs. The process led to the development of a symptom-based vision simulator. 21 symptoms, like field loss or light sensitivity, can be overlaid on desktop design tools. The results show that most participants felt VIP-Sim could replicate their symptoms.
VIP-Sim was received positively, but concerns about exclusion in design and comprehensiveness of the simulation remain, mainly whether it represents the experiences of other VIPs.

---

## ✨ Key Features

- 🧩 21 customizable symptom shaders (field loss, blur, glare, CVD, etc.)
- 👁️ Gaze-contingent simulation using webcam-based eye tracking
- 🧑‍🦯 Built **with and for** people with visual impairments
- 🪟 Transparent, click-through Unity overlay for any application (e.g. Figma, Adobe XD)
- ⚙️ Intuitive UI to toggle symptoms and tune severity

---

## 📦 Installation

We are currently in release 1.9.

### Prerequisites

If you want to use our shaders for your own project or use/contribute to VIP-Sim these are the Prerequisites

- Unity **6000.5.8f1** (Unity 6.5) — the projects have been upgraded and no longer open on 2022.3
- .NET Framework ≥ 4.x
- Windows 10/11 or macOS
- A webcam (optional; without one VIP-Sim follows the mouse instead of your gaze)

Building macOS from Windows additionally needs the **Mac Build Support (Mono)** module
installed in Unity Hub.

### 🚀 Quick Start

Download our release to install VIP-Sim on your system.

**Windows** — run `VIP-Sim.exe`. Pick the window you want to simulate from the list, then
switch on the impairments you want.

**macOS** — the build is produced on Windows, which cannot store the Unix execute bit, and
the app is not code-signed, so it will not start until both are corrected. Unzip, then:

```bash
bash setup.sh
```

That restores the execute bit and clears the quarantine flag. Doing it by hand is
`chmod +x "VIP-Sim.app/Contents/MacOS/VIP-Sim"` followed by
`xattr -dr com.apple.quarantine "VIP-Sim.app"`. Transfer the archive rather than the
`.app` itself — Finder shows the bundle as a single file and some copies flatten it.

macOS then needs **Screen Recording** (required — the capture is blank without it, and the
app must be **quit and reopened** after granting) and **Camera** (optional, for eye
tracking). Both live in System Settings → Privacy & Security.

Full instructions and troubleshooting: [docs/MACOS_README.md](docs/MACOS_README.md).

**Ctrl+Alt+Q always quits.** VIP-Sim is a borderless, always-on-top, click-through overlay
with no title bar, so this is the guaranteed way out if the toolbar is ever unreachable.

### 🎓 Tutorial

<p align="center">
  <img src="images/UI.png" alt="Teaser Figure" width="600"/>
</p>
The notebooks/tutorial.ipynb or docs/tutorial.md walks through:

Installing and running VIP-Sim

Applying shaders and tuning symptoms

Integrating with design tools

Simulating specific user diagnoses

### 🔍 Shader Examples
# Overview of Shaders in VIP-Sim

| Shader Name                 | Eye Tracking Required | Parameters                                                                 |
|----------------------------|-----------------------|---------------------------------------------------------------------------|
| Central Vision Loss        | ✅ Yes                | Size (0 to full screen size)                                               |
| Hyperopia                  | ❌ No                 | Visual acuity in CPD (0.01–30)                                             |
| Color Vision Deficiency    | ❌ No                 | Type (Protanomaly, Deuteranomaly, Tritanomaly, Monochrome), Severity (0–100%) |
| Contrast Sensitivity       | ❌ No                 | Brightness (-1 to 1), Contrast (-1 to 1), Gamma (0 to 1)                   |
| Metamorphopsia Pointwise   | ✅ Yes                | –                                                                         |
| Nystagmus                  | ❌ No                 | Speed (0–1 s), Amplitude (0–20% of screen width)                          |
| Retinopathy / Floaters     | ✅ Yes                | Color, Opacity, Density, Speed, Centering, Radius                         |
| Teichopsia                 | ✅ Yes                | Strength (0–1)                                                            |
| Metamorphopsia Overlay     | ❌ No                 | Speed, Frequency, Amplitude (each 0–1)                                    |
| Glare / Photophobia        | ❌ No                 | Intensity, Blur, Threshold (each 0–1)                                     |
| Peripheral Vision Loss     | ✅ Yes                | Size (0 to full screen size)                                              |
| Cataracts                  | ❌ No                 | Severity (0–1), Frosting                                                  |
| In-Filling                 | ✅ Yes                | Size (0 to ¼ of screen size)                                              |
| Double Vision              | ❌ No                 | Displacement (0 to ¼ of screen size)                                      |
| Distortion                 | ✅ Yes                | Radius, Suction Strength, Inner Radius, Noise Amount                      |
| Foveal Darkness            | ✅ Yes                | Size, Fade, Opacity (each 0–1)                                            |
| Flickering Stars           | ❌ No                 | Radius, Fade                                                              |
| Detail Loss                | ❌ No                 | Severity via number of clusters (10 to 1000)                              |

Example shader settings of our participants:

<p align="center">
  <img src="images/participants.png" alt="Teaser Figure" width="600"/>
</p>

### 🛠️ Building and diagnostics

Both projects build headlessly:

```bash
Unity -quit -batchmode -nographics -projectPath windows -executeMethod VipSim.EditorTools.VipSimBuild.BuildWindows -buildOutput <dir>
```

`BuildMacOS` and `BuildLinux` exist alongside it. The macOS build sets the camera usage
description and the product name itself, so a clean checkout produces a correct bundle
without anyone remembering to fix them in the inspector.

**Alpha is load-bearing.** VIP-Sim composites onto the desktop from the framebuffer's
per-pixel alpha, so an effect shader with perfect colour and wrong alpha is invisible —
a failure that looks exactly like "the effect does nothing". Every effect must carry alpha
through untouched. To check all of them:

```bash
Unity -batchmode -projectPath windows -executeMethod VipSim.EditorTools.VipSimAlphaTest.Run -logFile alpha.log
```

(No `-nographics`; it needs a device.) It pushes a constant-alpha ramp through each real
material and reports PASS/FAIL per shader. Constant alpha rather than a gradient is
deliberate — a gradient cannot tell an effect that *relocates* pixels from one that
*corrupts* alpha.

Runtime diagnostics are written to the player log. Frame timing and the capture placement
are logged periodically; **F6** (screenshot of the overlay's own framebuffer, the only way
to capture a layered window), **F7** (force the effect list visible) and **F8** (framebuffer
alpha distribution and the cursor→gaze chain) are hotkeys — but note they only fire while
VIP-Sim holds focus, which as a click-through overlay it usually does not.

Unity batchmode on some machines fails intermittently — shader-compiler crashes, access
violations in the build program, or an error count with no error text. **Retry a failed
build once before investigating it.**

### 📈 Changelog
View all updates in CHANGELOG.md

### 🙌 Contributing
We welcome bug reports, shader improvements, and new symptom contributions!

# 📝 Citation
If you use VIP-Sim in academic work, please cite:

```
@inproceedings{Raedler2025VIPSIM,
  title     = {{VIP-Sim: A User-Centered Approach to Vision Impairment Simulation for Accessible Design}},
  author    = {Max Rädler and Mark Colley and Enrico Rukzio},
  booktitle = {Proceedings of the 38th ACM Symposium on User Interface Software and Technology (UIST ’25)},
  year      = {2025},
  publisher = {ACM},
  doi       = {10.1145/3746059.3747704},
  url       = {https://doi.org/10.1145/3746059.3747704}
}
```

# 🛟 Support and documentation

| | |
|---|---|
| Report a problem | https://github.com/M-Colley/VIP-Sim2/issues -- also a button in the app under **F1** |
| Diagnostics to attach | **F1 → Copy diagnostics path**, then attach `Player.log` and `vipsim-errors.log` |
| Emergency exit | **Ctrl+Alt+Q** always quits |
| Install notes | [docs/INSTALL.md](docs/INSTALL.md), [docs/MACOS_README.md](docs/MACOS_README.md) |
| What each symptom is | [docs/EFFECTS.md](docs/EFFECTS.md), or press **F1** in the app |
| Privacy | [docs/PRIVACY.md](docs/PRIVACY.md) |
| Accessibility | [docs/ACCESSIBILITY.md](docs/ACCESSIBILITY.md) -- what works, and what does not |
| Changes | [CHANGELOG.md](CHANGELOG.md) |
| Linux status | [docs/LINUX_PORT.md](docs/LINUX_PORT.md) |

Developer hotkeys (F6/F7/F8/F10/F11) are disabled in release builds. Launch with
`-vipsim-dev` to enable them.

# 📄 License

VIP-Sim is released under the **MIT Licence** — see [LICENSE](LICENSE).
Copyright (c) 2025 Mark Colley and Max Rädler.

Earlier versions of this file said CC BY 4.0. That was wrong for software: Creative
Commons advises against CC licences for code, and CC BY says nothing about source
availability or patents, and leaves attribution for a compiled binary undefined. MIT
is what the `LICENSE` file has always contained and is what applies.

The vendored [UnitEye](packages/uniteye) component is MIT as well.
Third-party components and their licences are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

# 📬 Contact
Max Rädler – max.raedler@uni-ulm.de
Mark Colley - m.colley@ucl.ac.uk

Project Repository: github.com/Max-Raed/VIP-Sim

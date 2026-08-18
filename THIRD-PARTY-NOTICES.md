# Third-party notices

VIP-Sim is distributed with the components below. Several of their licences require that
their copyright notice travels with any copy of the software — including this one — so
this file must ship inside the release archive, not only live in the repository.

> **Several items need action before VIP-Sim can be sold.** They are marked **ACTION**
> below and listed again at the bottom. None is a code change: they are permissions to
> obtain and decisions to record.

---

## VIP-Sim itself

MIT Licence, Copyright (c) 2025 Max Rädler. See [LICENSE](LICENSE).

**ACTION — the project states two different licences.** `LICENSE` contains the MIT
Licence; `README.md` has said the project is licensed under CC BY 4.0. These are different
grants with different obligations, and a user cannot tell which one they have been given.
CC BY is in any case not intended for software. Decide which applies, make the two agree,
and record the decision — an ambiguous licence is a poor position from which to sell.

**ACTION — confirm who owns this.** The copyright line names an individual. Before any
money changes hands, establish in writing who holds the rights: the named author, the
current maintainer, or the employing institution, whose IP policy may claim work produced
by staff or students. Note also that MIT permits anyone to redistribute or sell this code,
so a published MIT repository cannot be the basis for exclusivity.

---

## UnitEye — webcam eye tracking

Vendored at `packages/uniteye`.

**ACTION — no licence file is present.** Absent a licence grant, the default position in
copyright law is that redistribution is not permitted, commercially or otherwise. This is
the eye-tracking core of the product. Obtain written permission from the authors, and add
the resulting licence text here, or replace the component.

---

## uWindowCapture — Windows window capture

Vendored at `windows/Assets/uWindowCapture`. Upstream:
https://github.com/hecomi/uWindowCapture — MIT Licence, Copyright (c) hecomi.

**ACTION — the licence text is not vendored with the copy in this repository.** MIT
requires the copyright notice and permission notice to be included in all copies and
substantial portions of the software, so the current release is technically out of
compliance even as a free download. Copy `LICENSE` from the upstream repository at the
revision in use into `windows/Assets/uWindowCapture/` and reference it here. This is the
cheapest of the outstanding items and it applies today, independently of any commercial
plan.

---

## MediaPipe Unity Plugin

Vendored at `packages/com.github.homuler.mediapipe`. MIT Licence, Copyright (c) 2021
homuler. Full text: `packages/com.github.homuler.mediapipe/LICENSE.md`.

The plugin embeds Google's MediaPipe (Apache Licence 2.0) and further components; their
notices are reproduced in `packages/com.github.homuler.mediapipe/Third Party Notices.md`,
which ships as part of that package.

---

## Google Protocol Buffers

`packages/com.github.homuler.mediapipe/Runtime/Plugins/Protobuf/` — BSD 3-Clause,
Copyright (c) Google Inc. Distributed as part of the MediaPipe plugin above; its notice is
included in that package's third-party notices.

---

## Unity Engine and TextMesh Pro

Built with Unity 6000.5.8f1. The Unity Runtime is redistributed under the Unity Companion
Licence / the Unity Terms of Service accepted by the licence holder. TextMesh Pro ships as
part of the engine under the same terms.

**Check your Unity plan before selling.** The project currently builds with the Unity
splash screen enabled (`m_ShowUnitySplashScreen: 1`), which is mandatory on Unity Personal.
Unity Personal is limited by a revenue threshold; exceeding it, or wanting the splash
removed, requires a paid plan.

Font assets under `Assets/TextMesh Pro/Examples & Extras/` are Unity's TMP examples and
carry their own upstream licences — Roboto under Apache 2.0, Bangers under the SIL Open
Font Licence. They are example content: if they are not actually used by the shipping UI,
removing them from the release is simpler than carrying their notices.

---

## Summary of outstanding actions

| # | Component | Action | Blocks a paid release? |
|---|-----------|--------|------------------------|
| 1 | UnitEye | Obtain a written licence, or replace | **Yes** |
| 2 | uWindowCapture | Vendor the upstream MIT licence text | **Yes** (also applies to the free release) |
| 3 | VIP-Sim | Establish ownership in writing | **Yes** |
| 3b | VIP-Sim | Resolve the MIT vs CC BY 4.0 contradiction | **Yes** |
| 4 | Unity | Confirm plan eligibility; decide on the splash | Yes, if over the Personal threshold |

This file records what is in the repository and what the upstream licences say. It is not
legal advice; have a qualified person review the position before selling.

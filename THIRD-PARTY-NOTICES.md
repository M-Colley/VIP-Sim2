# Third-party notices

VIP-Sim is distributed with the components below. Several of their licences require that
their copyright notice travels with any copy of the software — including this one — so
this file must ship inside the release archive, not only live in the repository.

> **Some items still need action before VIP-Sim can be sold.** They are marked **ACTION**
> below and listed again at the bottom. None is a code change: what remains are decisions
> to record, not permissions to chase.

---

## VIP-Sim itself

MIT Licence, Copyright (c) 2025 Mark Colley and Max Rädler. See [LICENSE](LICENSE).

**ACTION — the project states two different licences.** `LICENSE` contains the MIT
Licence; `README.md` has said the project is licensed under CC BY 4.0. These are different
grants with different obligations, and a user cannot tell which one they have been given.
CC BY is in any case not intended for software. Decide which applies, make the two agree,
and record the decision — an ambiguous licence is a poor position from which to sell.

Owned jointly by Mark Colley and Max Rädler, as stated by the rights-holders and now
reflected in the copyright line, which previously named only one of them.

Two consequences worth keeping in view rather than rediscovering later. Joint ownership
means decisions about licensing, sale or relicensing need both owners' agreement, so the
outstanding licence question below is a decision for both. And MIT permits anyone to
redistribute or sell this code, so a published MIT repository cannot itself be the basis
for exclusivity — what is sold is convenience, support and signed builds.

---

## UnitEye — webcam eye tracking

Vendored at `packages/uniteye`.

Authored by the VIP-Sim maintainer, who has confirmed that its use and redistribution
within VIP-Sim is permitted. Since the rights-holder for the component and for the product
are the same party, there is no third-party permission left to obtain and this no longer
blocks a release.

**ACTION — still no licence file in `packages/uniteye`.** The permission is real but
undocumented: nothing in the repository records it, so a collaborator, a purchaser, or a
future maintainer reading the tree sees the same all-rights-reserved default I did. Add a
licence file stating the grant. Which text goes in it is the same decision as the MIT
versus CC BY question above — settle that once and it covers both.

---

## uWindowCapture — Windows window capture

Vendored at `windows/Assets/uWindowCapture`. Upstream:
https://github.com/hecomi/uWindowCapture — MIT Licence, Copyright (c) hecomi.

Copyright (c) 2018 hecomi. The upstream licence file is vendored alongside the code at
`windows/Assets/uWindowCapture/LICENSE.md`, and its full text is reproduced under
[Full licence texts](#full-licence-texts) below — `.md` files under `Assets/` are not
included in a player build, so the copy that reaches users is the one in this document.

---

## MediaPipe Unity Plugin

Vendored at `packages/com.github.homuler.mediapipe`. MIT Licence, Copyright (c) 2021
homuler. The upstream file is at `packages/com.github.homuler.mediapipe/LICENSE.md`; it
does not reach a player build either, so the text is reproduced under
[Full licence texts](#full-licence-texts) below.

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

## Full licence texts

MIT requires that both the copyright notice **and** the permission notice travel with
every copy of the software. This document is the copy that ships inside the release
archives, so the texts are reproduced here in full rather than only referenced.

### uWindowCapture

```
The MIT License (MIT)

Copyright (c) 2018 hecomi

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

### MediaPipe Unity Plugin

```
MIT License

Copyright (c) 2021 homuler

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

### VIP-Sim

See [LICENSE](LICENSE) in the repository, and the ownership question recorded above.

---

## Summary of outstanding actions

| # | Component | Action | Blocks a paid release? |
|---|-----------|--------|------------------------|
| 1 | UnitEye | ~~Obtain a licence, or replace~~ — authored in-house, confirmed | Record it in a licence file |
| 2 | uWindowCapture | ~~Vendor the upstream MIT licence text~~ | **Done** |
| 3 | VIP-Sim | ~~Establish ownership~~ — Mark Colley and Max Rädler | **Done** |
| 3b | VIP-Sim | Resolve the MIT vs CC BY 4.0 contradiction | **Yes** |
| 4 | Unity | Confirm plan eligibility; decide on the splash | Yes, if over the Personal threshold |

This file records what is in the repository and what the upstream licences say. It is not
legal advice; have a qualified person review the position before selling.

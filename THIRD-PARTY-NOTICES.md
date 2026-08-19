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

**Settled: MIT.** `LICENSE` and `README.md` previously disagreed, the README claiming
CC BY 4.0. MIT is what applies. CC BY was never appropriate for software — Creative
Commons advises against it, and it addresses neither source availability nor patents, and
leaves attribution for a compiled binary undefined.

Owned jointly by Mark Colley and Max Rädler, as stated by the rights-holders and now
reflected in the copyright line, which previously named only one of them.

Two consequences worth keeping in view rather than rediscovering later. Joint ownership
means decisions about licensing, sale or relicensing need both owners' agreement, so the
outstanding licence question below is a decision for both. And MIT permits anyone to
redistribute or sell this code, so a published MIT repository cannot itself be the basis
for exclusivity — what is sold is convenience, support and signed builds.

**ACTION — the owners and the vendor are not the same people.** VIP-Sim is distributed as
a product of **Zefwih GbR**, whose partners are Fabian Fischbach, Pascal Jansen and Mark
Colley. The copyright is held by **Mark Colley and Max Rädler**. Only Mark Colley is in
both sets: Max Rädler is not a partner in Zefwih, and Fischbach and Jansen are not
copyright holders.

So Zefwih is currently selling something it does not itself own. That is entirely fixable
and completely ordinary — the owners grant Zefwih a licence, or assign the copyright to
the GbR — but it has to be written down. Until it is, the GbR's right to distribute rests
on nothing more than one partner also happening to be an owner, which is not a position to
be in when money is involved or if the partnership ever changes.

---

## UnitEye — webcam eye tracking

Vendored at `packages/uniteye`.

Authored by the VIP-Sim maintainer, who has confirmed that its use and redistribution
within VIP-Sim is permitted. Since the rights-holder for the component and for the product
are the same party, there is no third-party permission left to obtain and this no longer
blocks a release.

Licensed under the **MIT Licence**, Copyright (c) 2025 Mark Colley, recorded at
`packages/uniteye/LICENSE`. The permission is now documented rather than merely true, so a
collaborator or purchaser reading the tree sees the grant instead of the
all-rights-reserved default that applies when no licence file is present.

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

## wlr-layer-shell protocol definition

Vendored at `linux/presenter/protocols/wlr-layer-shell-unstable-v1.xml`, from
https://github.com/swaywm/wlr-protocols. Copyright © 2017 Drew DeVault, under an HPND-style
permission notice that requires the copyright notice to appear in all copies — the notice
is inside the XML file itself and is reproduced under
[Full licence texts](#full-licence-texts) below.

Used only to generate client stubs for the Linux presenter spike; it is not part of the
Windows or macOS builds, and no generated code is committed.

---

## Unity Engine and TextMesh Pro

Built with Unity 6000.5.8f1. The Unity Runtime is redistributed under the Unity Companion
Licence / the Unity Terms of Service accepted by the licence holder. TextMesh Pro ships as
part of the engine under the same terms.

**Decision: VIP-Sim stays on Unity Personal for now.** The build therefore keeps the
Unity splash screen (`m_ShowUnitySplashScreen: 1`), which is mandatory on that plan and
cannot be switched off without upgrading. Unity Personal permits commercial distribution
only while revenue and funding stay under Unity's threshold for the plan.

Two triggers to revisit, so this is not rediscovered at an awkward moment: crossing that
threshold obliges an upgrade, and wanting the splash gone for a paid product requires one
regardless of revenue. Neither is a code change — upgrading the plan and clearing the
checkbox is the whole of it.

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

### wlr-layer-shell protocol definition

```
Copyright © 2017 Drew DeVault

Permission to use, copy, modify, distribute, and sell this
software and its documentation for any purpose is hereby granted
without fee, provided that the above copyright notice appear in
all copies and that both that copyright notice and this permission
notice appear in supporting documentation, and that the name of
the copyright holders not be used in advertising or publicity
pertaining to distribution of the software without specific,
written prior permission.  The copyright holders make no
representations about the suitability of this software for any
purpose.  It is provided "as is" without express or implied
warranty.

THE COPYRIGHT HOLDERS DISCLAIM ALL WARRANTIES WITH REGARD TO THIS
SOFTWARE, INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS, IN NO EVENT SHALL THE COPYRIGHT HOLDERS BE LIABLE FOR ANY
SPECIAL, INDIRECT OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN
AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION,
ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
THIS SOFTWARE.
```

### VIP-Sim

See [LICENSE](LICENSE) in the repository, and the ownership question recorded above.

---

## Summary of outstanding actions

| # | Component | Action | Blocks a paid release? |
|---|-----------|--------|------------------------|
| 1 | UnitEye | ~~Obtain a licence~~ — authored in-house, now MIT-licensed in the tree | **Done** |
| 2 | uWindowCapture | ~~Vendor the upstream MIT licence text~~ | **Done** |
| 3 | VIP-Sim | ~~Establish ownership~~ — Mark Colley and Max Rädler | **Done** |
| 3c | VIP-Sim | Licence or assign the copyright from the owners to Zefwih GbR | **Yes** |
| 3b | VIP-Sim | ~~Resolve the MIT vs CC BY contradiction~~ — MIT | **Done** |
| 4 | Unity | ~~Confirm plan; decide on the splash~~ — staying on Personal, splash kept | Revisit if revenue crosses the threshold |

This file records what is in the repository and what the upstream licences say. It is not
legal advice; have a qualified person review the position before selling.

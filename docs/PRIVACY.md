# Privacy policy — VIP-Sim

> **DRAFT — not legal advice.** This describes what the software actually does, verified
> against the source. It is written so a lawyer can turn it into a published policy
> quickly, and so a reviewer can check every claim against the code. Before publishing:
> fill in the controller identity, confirm the lawful basis, and have it reviewed. In
> Germany a published policy is also accompanied by an Impressum.

**Applies to:** VIP-Sim 2.0.0 (Windows, macOS)
**Last updated:** _fill in on publication_
**Controller:** _name, address, email — required under GDPR Art. 13_

---

## The short version

VIP-Sim runs on your computer and processes what it needs locally. It does not have
accounts, it does not track usage, and it does not upload your screen or your camera
images. Two features touch the network, and both are described below: an optional update
check, and a research logging mode that is **off** unless someone deliberately switches it
on and obtains consent.

---

## What VIP-Sim processes on your computer

| Data | Why | Where it goes |
|---|---|---|
| **Screen / window images** | The window you select is captured so the simulation can be drawn over it. | Held in memory and drawn on screen. Never written to disk, never transmitted. |
| **Webcam images** | Only if you enable eye tracking. Used to estimate where you are looking. | Processed locally, frame by frame, by the on-device MediaPipe model. Not recorded, not transmitted. |
| **Gaze coordinates** | Position the simulated impairment. | Held in memory. |
| **Settings** (chosen effects, severities, chosen display, whether the tutorial has run) | Remember your setup between sessions. | Local only — Windows registry / macOS user defaults. |
| **Logs and error reports** | Diagnose problems. | Written locally to the application data folder. Sent to nobody; you choose whether to attach them to a bug report. |

Webcam images may constitute biometric data in some jurisdictions. VIP-Sim never stores or
transmits them, and eye tracking is not enabled by default — the simulation follows the
mouse until you switch it on.

### Where the local files are

- **Windows:** `%USERPROFILE%\AppData\LocalLow\Zefwih\VIP-Sim\`
- **macOS:** `~/Library/Logs/Zefwih/VIP-Sim/` and `~/Library/Application Support/`

The **Copy diagnostics path** button in the F1 panel puts this location on your clipboard.
Deleting the folder removes local logs; settings can be removed with the platform's normal
mechanisms.

---

## The two features that use the network

### 1. Update check — on by default, switchable

On start, VIP-Sim asks the public GitHub releases API whether a newer version exists. The
request contains **no identifier and no usage data** — it is an ordinary web request for a
public page. As with any web request, the server can see your IP address and the time of
the request. GitHub processes that under its own privacy statement.

It can be switched off; the setting persists. Nothing is downloaded or installed
automatically — if an update exists, VIP-Sim tells you and links to the release page.

### 2. Research logging — off by default, consent-gated

VIP-Sim contains an optional logging mode for **research studies**, which records which
programs were captured and which impairments were enabled, and sends that to a Firestore
database.

**This is disabled unless a study operator explicitly enables it in the build**, and it
must not be enabled without the participant's recorded, informed consent. It exists for
supervised research sessions, not for ordinary use. If you downloaded VIP-Sim and ran it
yourself, it is off.

If you are running a study with this enabled, you become a controller for that data and
take on the corresponding obligations: an ethics approval or other lawful basis, an
information sheet, a consent record, and a retention period.

---

## What VIP-Sim never does

- No account, sign-in, licence server or activation call.
- No analytics, telemetry or crash upload (Unity Analytics is disabled in the project).
- No advertising, profiling or automated decision-making.
- No sale or sharing of personal data — there is none to sell.
- No transmission of screen content or camera images, in any mode.

---

## Your rights

Because VIP-Sim holds no personal data on any server, there is normally nothing to request,
correct or erase — the data is on your machine and under your control. Where research
logging has been used with your consent, the study operator named on the consent form is
the controller and handles access, rectification, erasure, restriction, portability,
objection, and withdrawal of consent, as well as complaints to a supervisory authority.

## Children

VIP-Sim is a professional design and research tool and is not directed at children.

## Changes

Material changes will be noted in [CHANGELOG.md](../CHANGELOG.md) and the date above
updated.

## Contact

_Controller name and contact address — required before publication._
Bug reports: https://github.com/M-Colley/VIP-Sim2/issues

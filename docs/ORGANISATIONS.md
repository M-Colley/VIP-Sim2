# VIP-Sim for organisations

VIP-Sim is an MIT-licensed desktop overlay for Windows and macOS. It applies 21 simulated vision-impairment symptoms to any application on the screen. Product teams use it to find visual accessibility problems early and to record them. This document gives a team lead or a procurement reader the facts needed to evaluate, deploy and, if wanted, contract services for it.

## What VIP-Sim does for a team

A reviewer selects the window of the product under review and switches on one or more symptoms, such as blur, tunnel vision, a central scotoma or reduced contrast. The reviewer then presses the camera button to record a finding. Each finding stores the captured window with the symptoms applied, the same crop without them, and a profile that reproduces the simulation.

The Review tab in the F1 panel names the product and the reviewer. It takes a note, an impact level (observation, minor, major, blocker) and the requirements the finding concerns. The selectable requirements are EN 301 549 clauses 4.2.2 (usage with limited vision) and 4.2.3 (usage without perception of colour), the WCAG 2.2 success criteria on low vision, or none.

Every change rewrites three files in `Pictures/VIP-Sim/Review <date and time>`:

| File | Content | Use |
|---|---|---|
| `report.html` | Findings with both images side by side, settings with their units, viewing conditions, method and limitations. | Read in a browser, print to PDF, attach to conformity documentation. |
| `findings.csv` | One row per finding with Summary, Description, Priority and Labels columns. | Import into Jira or Azure DevOps. |
| `findings.json` | Machine-readable review, schema `vipsim-review/1`. | Scripts, archiving, reopening a review. |

Settings in the report carry their units. logMAR, degrees of visual angle and prism dioptres are named. Every other value is marked as a position on the effect's own, uncalibrated scale. Window titles are left out of reports unless the reviewer switches them on in the Review tab, because a title can name a document or a customer.

The target use is documentation of accessibility work under the European Accessibility Act (Directive 2019/882, Art. 13(2) documentation duty) and EN 301 549. VIP-Sim supports the record. It produces no statement of conformity.

## What VIP-Sim does not claim

Each effect approximates one symptom and models no diagnosis. Real conditions combine several symptoms, differ between individuals and change over time (see "A caution on interpretation" in [EFFECTS.md](EFFECTS.md)).

The severities of most effects are uncalibrated starting points. Only the values reported with units (logMAR, degrees, prism dioptres) have a defined physical meaning. A published or archived review should state which effects and values were used.

Use VIP-Sim alongside testing with people with vision impairments. Disability simulations can lower how capable observers judge disabled people to be (Silverman, Gwinn and Van Boven, 2015, doi:10.1177/1948550614559650). They can also raise discomfort without raising willingness to interact with disabled people (Nario-Redmond, Gospodinov and Cobb, 2017, doi:10.1037/rep0000127). VIP-Sim was developed with seven people with vision impairments, who raised the same concern (UIST 2025, doi:10.1145/3746059.3747704).

VIP-Sim itself is partially accessible and has no screen reader support. [ACCESSIBILITY.md](ACCESSIBILITY.md) lists what works and what does not. [ACR.md](ACR.md) is a conformance self-assessment.

## Data flows

The table summarises [PRIVACY.md](PRIVACY.md), which is the authoritative text. That file is a draft pending legal review.

| Data | Where it goes | Leaves the machine |
|---|---|---|
| Screen and window images | Held in memory and drawn on screen. | No |
| Webcam images (only if eye tracking is on) | Processed frame by frame by an on-device MediaPipe model. Not recorded. | No |
| Gaze coordinates | Held in memory. | No |
| Settings | Windows registry or macOS user defaults. | No |
| Logs | Application data folder. | No |
| Review files | `Pictures/VIP-Sim/Review <date and time>` or the `reportFolder` set by policy. | Only if the team moves them |
| Update check | HTTP GET to `api.github.com` for the public releases page. It carries no identifier. GitHub sees the IP address and the time. | Yes, unless switched off |
| Research logging | Disabled unless a study operator enables it in the build. | No by default |

Eye tracking is off by default and the simulation follows the mouse until it is switched on. The policy file can remove webcam eye tracking, switch off the update check and keep research logging off.

## Deployment

### Install

VIP-Sim ships as one archive per platform (`VIP-Sim-Windows-x64.zip`, `VIP-Sim-macOS-universal.zip`, `VIP-Sim-Linux-x64.tar.gz`, with `SHA256SUMS.txt`) on the [Releases page](https://github.com/M-Colley/VIP-Sim2/releases). There is no installer yet. Installation is extracting the archive to a folder. [INSTALL.md](INSTALL.md) lists the screen-recording and camera permissions per platform. The builds are currently unsigned (see [RELEASE.md](RELEASE.md)), so SmartScreen on Windows and Gatekeeper on macOS warn on first launch.

For Microsoft Intune or Jamf, copy the extracted folder to a target path and copy the policy file to its location. In Intune, a Win32 app with a PowerShell script can expand the zip and write `policy.json`. In Jamf, a package can carry the folder and a second payload the policy file. On macOS each user approves Screen Recording once. A device management profile cannot grant that permission on the user's behalf.

### Policy file

An optional JSON file sets organisation-wide behaviour. VIP-Sim reads it from these locations:

| Platform | Path |
|---|---|
| Windows | `%ProgramData%\VIP-Sim\policy.json` |
| macOS | `/Library/Application Support/VIP-Sim/policy.json` |
| Linux | `/etc/vip-sim/policy.json` |
| Any | The path in the command-line argument `--policy <path>` |

All keys are optional.

| Key | Type | Effect |
|---|---|---|
| `updateCheck` | bool | `false` disables the GitHub release check. The Help tab says the organisation switched it off. |
| `telemetryAllowed` | bool | `false` keeps research logging off. It is already off by default. |
| `webcamAllowed` | bool | `false` removes webcam eye tracking. Gaze follows the mouse. |
| `viewingDistanceCm` | number, 20 to 300 | Locks the viewing distance. |
| `screenWidthCm` | number, 10 to 500 | Locks the screen width. |
| `reportFolder` | string | Folder where reviews are written. Environment variables such as `%USERPROFILE%` or `$HOME` are expanded. |
| `profile` | string | Condition profile loaded at start. |

The command-line argument `--profile <path>` loads a profile at start and takes precedence over `profile`. A viewing distance or screen width set by policy appears in a disabled field in F1, Display and text, with a line saying the organisation set it. With `webcamAllowed` false, the toolbar's eye-tracking toggle returns to the mouse and the player log records why. VIP-Sim logs the policy it applied as a `[VipSimPolicy]` line. Invalid values are ignored with a warning in the player log, and the other settings still apply.

Example `policy.json`:

```json
{
  "updateCheck": false,
  "telemetryAllowed": false,
  "webcamAllowed": false,
  "viewingDistanceCm": 60,
  "screenWidthCm": 53,
  "reportFolder": "%USERPROFILE%\\Documents\\Accessibility Reviews",
  "profile": "C:\\ProgramData\\VIP-Sim\\profiles\\low-vision-baseline.json"
}
```

Locking the viewing distance and screen width makes the degrees of visual angle in reports comparable between reviewers.

## Security

Report vulnerabilities as described in [SECURITY.md](../SECURITY.md). The policy file sits in a machine-wide location. Restrict write access to that location to administrators.

## Licence

VIP-Sim is released under the MIT licence ([LICENSE](../LICENSE)). There is no per-seat fee and no licence server. Copyright is held by Mark Colley and Max Rädler. [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) lists the bundled components and their licences.

## Services

The software is free. Organisations can pay for services around it.

| Service | Content | Price | Contact |
|---|---|---|---|
| Support contract | Answers to problems and questions with agreed response times, notice of releases and fixes. | [price] | [contact] |
| Team training | Session for a product team on running reviews, choosing effects and values, writing findings and combining them with testing with people with vision impairments. | [price] | [contact] |
| Deployment help | Policy file design, packaging for Intune or Jamf, reviewer setup. | [price] | [contact] |
| Custom condition profiles | Profiles for the conditions and severities a team needs, with documented sources for the values. | [price] | [contact] |

Free community support is described in [SUPPORT.md](../SUPPORT.md).

## Open decisions

The repository owners have not yet decided the following.

- Code-signing certificates for Windows and macOS (see [RELEASE.md](RELEASE.md)).
- An installer for Windows and macOS.
- Prices for each service.
- The contact address for service enquiries.
- The legal entity that contracts for services. [PRIVACY.md](PRIVACY.md) names Zefwih GbR for the product and website, and [LICENSE](../LICENSE) names Mark Colley and Max Rädler as copyright holders.

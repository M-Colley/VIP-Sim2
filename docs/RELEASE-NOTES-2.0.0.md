# VIP-Sim 2.0.0

Experience your own design through impaired vision. VIP-Sim overlays a live simulation of
vision impairments on any application already running on your desktop, while you keep
using that application normally.

## Downloads

| File | Platform | Size |
|---|---|---|
| `VIP-Sim-Windows-x64.zip` | Windows 10 / 11, 64-bit | 147 MB |
| `VIP-Sim-macOS-universal.zip` | macOS 12+, Apple silicon and Intel | 161 MB |

Each archive contains the application, a `READ-ME-FIRST.md` with the first-launch steps
for that platform, the licence, third-party notices, and the changelog.

## Before you run it

**Neither build is code-signed yet**, so both systems will warn on first launch. The steps
are in the `READ-ME-FIRST.md` inside each archive — one right-click on Windows, two
Terminal commands on macOS. macOS additionally needs **Screen Recording** permission
granted and the app **restarted** before the window list fills.

## Highlights

- A first-run walkthrough, and an in-app symptom reference (**F1**) that explains every
  effect in plain language beside its clinical term.
- Move the overlay between monitors with **F3** or from the F1 panel.
- Redesigned in-app panels that scale properly on 4K displays and laptops.
- Mouse-following by default — the webcam is only touched if you switch to eye tracking.
- Seven shaders fixed that previously rendered nothing when enabled on their own.
- Captured windows are placed 1:1 over the real window, on any monitor.
- Support built in: report a problem, and copy the diagnostics path, from the F1 panel.

Full detail in `CHANGELOG.md`.

## Known limitations

- **Not signed or notarised.** First launch requires the manual step above.
- **Severities are not clinically validated.** They are plausible starting points. VIP-Sim
  is a design and awareness tool, **not a medical device**, and must not be presented as
  showing "what condition X looks like" or as evidence of accessibility compliance.
- **Multi-monitor switching has not been exercised on multi-monitor hardware.**
- **Linux is not released.** It builds and runs, but has no window capture and no
  transparency yet; see `docs/LINUX_PORT.md`.
- On Windows, the executable's file-properties version reads as the Unity version rather
  than 2.0.0. Cosmetic; the application itself reports 2.0.0.

## Verifying your download

`SHA256SUMS.txt` accompanies these files.

```bash
shasum -a 256 -c SHA256SUMS.txt
```

## Support

Issues: https://github.com/M-Colley/VIP-Sim2/issues
In the app, press **F1 → Copy diagnostics path** and attach `Player.log` and
`vipsim-errors.log` to your report.

## Citation

VIP-Sim is described in the UIST'25 paper: https://doi.org/10.1145/3746059.3747704

# VIP-Sim 2.0.1

Experience your own design through impaired vision. VIP-Sim overlays a live simulation of
vision impairments on any application already running on your desktop, while you keep
using that application normally.

This release repairs the capture. If you ran 2.0.0 and the simulation appeared to do
nothing, that was almost certainly one of the two faults fixed below, and neither was
anything you did.

## Downloads

| File | Platform | Size |
|---|---|---|
| `VIP-Sim-Windows-x64.zip` | Windows 10 / 11, 64-bit | 147 MB |
| `VIP-Sim-macOS-universal.zip` | macOS 12+, Apple silicon and Intel | 161 MB |
| `VIP-Sim-Linux-x64.tar.gz` | Linux, Wayland with layer-shell (not GNOME) | 147 MB |

Each archive contains the application, a `READ-ME-FIRST.md` with the first-launch steps for
that platform, the licence, third-party notices, and the changelog.

**Linux is included for the first time.** It needs a Wayland compositor implementing
`zwlr_layer_shell_v1` — Sway, KWin, Hyprland, labwc and niri do; GNOME does not, and
VIP-Sim says so and exits rather than pretending. Run `VIP-Sim.sh`, not the binary: the
overlay is a second program, because Wayland fixes a window's role when it is created and
an always-on-top click-through overlay is not a role Unity's window can take.

## What this fixes

**Windows that draw with the GPU are captured.** Every browser, anything built on
Electron, VS Code, recent Explorer and Office windows draw nothing through GDI, which is
what the old capture method read. The capture succeeded, reported the right title and the
right rectangle, and contained black — indistinguishable, from the outside, from the
simulation being switched off. VIP-Sim now uses Windows Graphics Capture, which reads what
is actually on screen. Notepad worked before and still does; that is why the fault looked
like it depended on the machine rather than on the window.

**Captures land on the right screen.** Windows reports every window in global desktop
coordinates, where a monitor arranged above the primary has a negative y; VIP-Sim was
making those local by subtracting a number measured from a different origin. On one
display the two agree and everything was correct. On two they do not, and every capture
was drawn the distance between the monitors away from where it belonged — so a window on
the other screen showed nothing at all, while a window near the desktop origin appeared at
the wrong size. Same fault, three different-looking symptoms.

**A window on another screen now says so** rather than correctly drawing nothing. It cannot
be shown over a screen it is not on without putting every click in the wrong place
relative to what you see, so VIP-Sim names the window and points at **F3**.

**The Load button opens a file dialog.** It did nothing at all before — no dialog, no
error. Save had the same fault. The file list also offered only `.profile`, so a folder of
`.json` profiles looked empty.

**Loading a profile switches its symptoms on**, instead of applying the numbers to
symptoms that stayed off and reporting success.

**Loading a profile leaves every other symptom in the list.** A profile decides what is
switched on; it never decides what is available. Loading one used to remove every symptom
it did not mention from the interface, with no way back except restarting.

**Minimised windows no longer drag the capture off screen.** Windows parks them at
(-32000,-32000) and the capture followed.

**The overlay says which screen it is on** for a few seconds at startup when more than one
display is connected, and again after each **F3**. It has always remembered the display it
was last used on, and moving there silently looks exactly like it failed to start.

## What changed

- **The F1 panel is three sections** — Symptoms, Display & text, Help & updates — instead
  of all of it at once.
- **Hover help waits** about six tenths of a second, so crossing the toolbar no longer
  flashes every button's description in turn.
- **The manual window-size dialog is gone.** It existed for when "the automatic detection
  of the window size was unsuccessful", which is what this release fixes. It was also doing
  harm: after a single **Apply** it rewrote the capture's position and size ten times a
  second for the rest of the session, from fields that were no longer on screen.

Full detail in `CHANGELOG.md`.

## Before you run it

**No build is code-signed yet**, so Windows and macOS will both warn on first launch. The
steps are in the `READ-ME-FIRST.md` inside each archive — one right-click on Windows, two
Terminal commands on macOS. macOS additionally needs **Screen Recording** permission
granted and the app **restarted** before the window list fills.

## Known limitations

- **Not signed or notarised.** First launch requires the manual step above.
- **Severities are not clinically validated.** They are plausible starting points. VIP-Sim
  is a design and awareness tool, **not a medical device**, and must not be presented as
  showing "what condition X looks like" or as evidence of accessibility compliance.
- **The macOS build of this release has not been run.** It compiles, packages and carries
  the same fixes, but no one has launched it. Please report anything that looks wrong.
- **Linux: whole-screen capture on wlroots compositors.** Sway and its relatives can only
  share an entire output, so the simulation is captured back into itself. With two
  monitors, share the one VIP-Sim is not overlaying. GNOME's and KDE's portals can share a
  single window, which avoids this.
- **Linux renders on the CPU**, so it is noticeably slower than Windows and macOS on a
  large screen with several symptoms enabled.
- **Linux gaze is webcam-only.** Wayland does not let an application read the global
  pointer position, so mouse-following cannot work there.
- On Windows, the executable's file-properties version reads as the Unity version rather
  than 2.0.1. Cosmetic; the application itself reports 2.0.1.

## Verifying your download

`SHA256SUMS.txt` accompanies these files.

```bash
shasum -a 256 -c SHA256SUMS.txt
```

## Support

Issues: https://github.com/M-Colley/VIP-Sim2/issues
In the app, press **F1 → Help & updates → Copy diagnostics path** and attach `Player.log`
and `vipsim-errors.log` to your report.

## Citation

VIP-Sim is described in the UIST'25 paper: https://doi.org/10.1145/3746059.3747704

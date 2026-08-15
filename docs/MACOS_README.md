# Running VIP-Sim on macOS

The macOS build is produced on Windows, which has consequences: macOS files carry
permission bits and a quarantine flag that a Windows filesystem cannot represent. The
app will not start until both are dealt with. This is normal for a cross-built bundle
and is not a sign that the build is broken.

## 1. Copy the app across

`VIP-Sim.app` is a **bundle** — a folder that Finder displays as a single file. Copying
it with anything that flattens folders, or onto a FAT/exFAT USB stick, can damage its
internal structure.

**Zip it on the Windows machine and unzip it on the Mac.** Any other method that
preserves a folder tree (network share, scp, cloud sync) is fine too.

## 2. Make it executable and clear the quarantine flag

In Terminal, `cd` to wherever you unzipped it, then:

```bash
chmod +x "VIP-Sim.app/Contents/MacOS/VIP-Sim"
xattr -dr com.apple.quarantine "VIP-Sim.app"
```

The first line restores the execute bit that Windows stripped. Without it macOS reports
the application "cannot be opened" or "is damaged".

The second removes the quarantine attribute macOS attaches to anything downloaded or
copied from elsewhere. The app is **not code-signed or notarised**, so without this
Gatekeeper refuses to run it. If you would rather not use `xattr`, you can instead
right-click the app and choose **Open**, then confirm — that only works after the
`chmod`, and only the first time.

## 3. Grant permissions

VIP-Sim needs two permissions. Both are requested on first launch, and both live in
**System Settings → Privacy & Security**.

| Permission | What it is for | Note |
|---|---|---|
| **Screen Recording** | Capturing the window whose appearance is being simulated. Without it the capture is blank. | **Quit and reopen VIP-Sim after granting.** macOS does not apply this to an already-running app. |
| **Camera** | Optional webcam eye tracking, so the simulated impairment follows your gaze. | Decline it and VIP-Sim falls back to mouse-following, which is fully functional. |

If VIP-Sim does not appear in the Screen Recording list, launch it once first — apps
only appear after they have asked.

## 4. Use it

1. Pick the window you want to simulate from the list in the panel.
2. Switch on the impairments you want. Each row has a gear for its own settings.
3. The toolbar eye icon toggles between **mouse-following** and **eye tracking**; the
   crosshair next to it runs eye-tracker calibration (also on **F9**).

The overlay is click-through: you can keep working in the window underneath while the
simulation runs on top of it.

**Ctrl+Alt+Q quits**, always. VIP-Sim is a borderless, always-on-top window with no
title bar, so if the toolbar is ever unreachable this is the way out.

## If something is wrong

The log is at `~/Library/Logs/Zefwih/VIP-Sim/Player.log`.

**The overlay is a solid rectangle covering the desktop.** The transparency setup
(`setOpaque:NO`, clear background colour, layer-backed content view) is applied when the
window is acquired. Search the log for `TransparentWindow:` — it reports whether the
NSWindow was acquired and whether the transparency call failed.

> This is the most likely thing to go wrong. The macOS transparency code has been
> compiled but never run — as of this writing nobody has launched VIP-Sim on a Mac.

**Clicks do not reach the window underneath.** Click-through uses
`setIgnoresMouseEvents:`. The same log lines cover it.

**The window list is empty.** Screen Recording has not been granted, or was granted
without restarting the app.

**Nothing happens when an effect is switched on.** Check a window is actually selected
first — the effect list is inert until then.

## Diagnostic hotkeys

These are developer aids, not features. They are harmless to press.

| Key | Effect |
|---|---|
| **Ctrl+Alt+Q** | Quit. Always works. |
| **F6** | Write a PNG of what VIP-Sim is rendering, next to the player log. Ordinary screenshots exclude overlay windows, so this is the only way to capture what the simulation actually looks like. |
| **F7** | Force the effect list on screen *when no window has been selected*. Only useful for inspecting that part of the UI in isolation — **if a window is already selected it does nothing visible, because the list is showing anyway.** |
| **F8** | Log the alpha distribution of the finished frame. Alpha is what decides whether the overlay is visible at all on a composited desktop, and it cannot be seen in a screenshot. |
| **F10** | Toggle the performance overlay. |
| **F11** | Run the effect-cost benchmark. |

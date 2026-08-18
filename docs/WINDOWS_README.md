# Running VIP-Sim on Windows

## 1. Unblock it

VIP-Sim is **not yet code-signed**, so Windows SmartScreen will warn the first time you
run it. Unzip the archive first, then either:

- click **More info → Run anyway** on the SmartScreen dialog, or
- right-click `VIP-Sim.exe` → **Properties** → tick **Unblock** → OK.

Run it from the unzipped folder. `VIP-Sim.exe` needs the `VIP-Sim_Data` folder beside it,
so moving the exe on its own will not work.

## 2. Use it

1. Pick the window you want to simulate from the list in the panel on the right.
2. Switch on the symptoms you want. Each row has a gear for its own settings.
3. The toolbar eye icon toggles between **mouse-following** (the default) and **webcam eye
   tracking**; the crosshair next to it runs calibration, also on **F9**.

The overlay is click-through: keep working in the window underneath while the simulation
runs on top of it.

**Ctrl+Alt+Q quits**, always. VIP-Sim is a borderless, always-on-top window with no title
bar, so if the toolbar is ever unreachable this is the way out.

A short walkthrough runs by itself the first time. You can bring it back any time from the
**(i)** button or **F1**.

## 3. If something is wrong

Press **F1** and use **Copy diagnostics path**, then attach `Player.log` and
`vipsim-errors.log` from that folder to a report at
https://github.com/M-Colley/VIP-Sim2/issues

The folder is normally `%USERPROFILE%\AppData\LocalLow\Zefwih\VIP-Sim\`.

**The window list is empty.** Nothing else is running with a visible, restored window —
minimised windows are not listed.

**Nothing happens when an effect is switched on.** Check a window is actually selected
first; the effect list is inert until then.

## Hotkeys

| Key | Effect |
|---|---|
| **Ctrl+Alt+Q** | Quit. Always works. |
| **F1** | Symptom reference, tutorial, display switching, support links. |
| **F3** | Move VIP-Sim to the next monitor. |
| **F9** | Run eye-tracker calibration. |

Developer instrumentation (F6/F7/F8/F10/F11) is disabled in release builds. Start with
`VIP-Sim.exe -vipsim-dev` if you have been asked to enable it for a bug report.

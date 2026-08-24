# VIP-Sim regression checklist

Every test below is a fault that has actually happened at least once, in this project, on a real machine. None of them is hypothetical, and every one of them shipped or nearly shipped. There are 70 of them.

Work through them on the packaged download rather than on a build made locally: two of the faults here only existed in the archive.

## Before you start

**The log is the instrument.** Every five seconds VIP-Sim writes a report to its player log, in every build — you do not need developer mode for it.

- Windows: `%USERPROFILE%\AppData\LocalLow\Zefwih\VIP-Sim\Player.log`
- macOS: `~/Library/Logs/Zefwih/VIP-Sim/Player.log`
- Linux: `~/.config/unity3d/Zefwih/VIP-Sim/Player.log`

The F1 panel has **Copy diagnostics path**, which puts the folder on the clipboard.

Three lines matter:

```
[VipSimDiagnostics] CAPTURE 'Title' rect=(x,y,WxH) ... mode=WindowsGraphicsCapture
[VipSimDiagnostics] ALPHA 1920x1080 mean=... transparent=76.7% ... | enabled(2): myDistortionMap(L),myFieldLoss(L)
[ConditionProfile] p1: applied 26 of 51 parameters
```

**Developer hotkeys** (F6 screenshot, F7 reveal menus, F8 alpha probe, F10 overlay, F11 benchmark) need `-vipsim-dev` on the command line. F1, F3, F9 and F12 are always live.

## Telling the lookalike faults apart

Four different faults produce the same sentence — *the effects do not work*. This is how to separate them before writing the report:

1. **No `CAPTURE` line in the log.** No window has been selected. Nothing is wrong.
2. **`enabled(0)` in the `ALPHA` line.** No effect is switched on — the fault is in the interface, not the simulation. Compare it with the `ROWS` line: if the list says a symptom is on and `enabled` does not name it, the interface and the simulation disagree, and that disagreement is itself the bug.
3. **Effects enabled, and `opaque` around 20–25%.** The overlay is drawing only its own panel: the captured image is empty. This is a capture fault — read `mode=` on the `CAPTURE` line and see C1.
4. **The whole screen washed grey or black.** An alpha fault in an effect shader; see D3.

## How to report a failure

Give the test number, what you did, what you saw, and attach `Player.log`. For anything visual, a photograph of the screen is worth more than a description — a screenshot taken by the machine itself does not always contain the overlay.

## A — Install and first launch

Do this on a machine that has never run VIP-Sim. Several of these faults only exist on a clean install, and several others only exist in the packaged archive rather than in a build made on the developer's machine.

### A1. The packaged build starts at all

*Windows*

- **Do:** Extract VIP-Sim-Windows-x64.zip into an empty folder and run VIP-Sim.exe.
- **Expect:** The toolbar appears within a few seconds, and Player.log is written.
- **Has failed as:** A build made on top of a poisoned incremental cache started and died immediately with 0xC0000005 — nothing on screen, and the only trace was in the Windows Event Viewer. Separately, a shader the capture plane needs was stripped from a player build: correct in the Editor, nothing drawn in the build.

### A2. The macOS app is executable straight out of the archive

*macOS*

- **Do:** Unzip VIP-Sim-macOS-universal.zip on a Mac and double-click VIP-Sim.app.
- **Expect:** It launches (see A3 for Gatekeeper).
- **Has failed as:** The zip was created on Windows, which does not carry the execute bit. The binary inside the bundle arrived non-executable and macOS reported the application as damaged.

### A3. Gatekeeper refuses the first launch

*macOS*

- **Do:** Right-click the app and choose Open, then confirm.
- **Expect:** It opens on the second attempt and every launch after that.
- **Has failed as:** Reported as a failure, and it is expected: this build is unsigned. Worth walking through so the instructions we ship are the ones that actually work.

### A4. The application is called VIP-Sim

*macOS*

- **Do:** Look at the app name in Finder, the menu bar and the Dock.
- **Expect:** VIP-Sim everywhere.
- **Has failed as:** The build was called 'macos' — the name of the project folder — in all three places.

### A5. The macOS build has everything the Windows build has

*macOS*

- **Do:** Confirm the toolbar has Load and Save, that F1 opens the symptom panel, that the display switch is offered when a second monitor is attached, and that hovering an icon produces help.
- **Expect:** Same features, same behaviour as Windows.
- **Has failed as:** The macOS archive shipped with none of the profile work in it. Load still ran the old code path — the one that reset every effect to zero — while the Windows build had been fixed. The two projects are separate and a fix in one is not a fix in the other.

## B — The overlay window

The overlay is borderless, always on top and click-through. Every property in that sentence has been broken at least once, and each one fails silently.

### B1. The overlay is full screen and transparent

*All platforms*

- **Do:** Start VIP-Sim with no effects switched on and look at the desktop behind it.
- **Expect:** The desktop is unchanged. Only the toolbar is drawn.
- **Has failed as:** The whole screen washed grey, then black. Several effect shaders were squaring the framebuffer alpha, so the overlay stopped being transparent. Alpha is load-bearing here: a wrong alpha looks exactly like a dead effect.

### B2. Clicks pass through the overlay

*All platforms*

- **Do:** Click on desktop icons and on another application through an empty part of the screen. Then click a toolbar button.
- **Expect:** Clicks reach whatever is underneath, except on the toolbar and open panels, which take them.
- **Has failed as:** Click-through was lost entirely and the desktop became unusable — the only way out was the quit hotkey. Do not judge this one by feel: on Windows, read WS_EX_TRANSPARENT off the live window; the by-hand test gives false passes.

### B3. The toolbar is entirely on screen

*All platforms*

- **Do:** Look at the toolbar and any panel you open, on the largest and smallest display you have.
- **Expect:** Nothing is cut off by a screen edge, and no panel overlaps the title bar.
- **Has failed as:** The toolbar overflowed past the right edge of the screen; a panel overhung the title bar and covered its own controls.

### B4. Window geometry survives a restart

*All platforms*

- **Do:** Quit VIP-Sim and start it again.
- **Expect:** It comes back full screen, exactly as it was.
- **Has failed as:** A persisted 1x1 window. The application started, ran, wrote its log — and was invisible, which reads as 'it does not start'.

### B5. The macOS window is genuinely transparent

*macOS*

- **Do:** Start the app and look at the desktop behind it.
- **Expect:** Same as B1.
- **Has failed as:** An opaque background covered the entire screen. macOS needed its own fix; the Windows transparency work did not carry over.

### B6. Nothing paints over the overlay

*All platforms*

- **Do:** Watch the whole screen for a few seconds with an effect on.
- **Expect:** Only the simulation and the interface are drawn.
- **Has failed as:** A second, perspective camera cleared to the skybox on top of everything, so the desktop disappeared behind a blue-grey gradient.

## C — Capturing a window

This is where the most expensive fault in the project lives, and it depends entirely on which window you pick. Test both kinds.

### C1. A window that draws with the GPU

*Windows · macOS*

- **Do:** Pick a browser (Chrome, Edge, Firefox), VS Code, or a File Explorer window.
- **Expect:** The simulation shows that window's real content, with the effects applied to it.
- **Has failed as:** Black. Every number in the log was correct — the right title, the right rectangle, the plane placed and scaled properly — and there was no picture. The capture plugin was on Auto, which reads what an application draws through GDI, and a GPU-rendered window draws nothing there. From the outside this is indistinguishable from the simulation being switched off, and it was reported as 'the effects do not work'. This is the single most important test on the list.

### C2. A window that draws through GDI

*Windows*

- **Do:** Pick Notepad and switch an effect on.
- **Expect:** The simulation shows Notepad.
- **Has failed as:** Never failed — which is the point. Notepad worked on the machine where browsers came out black, so testing only with Notepad hid C1 completely. If C1 fails and C2 passes, it is the capture method, not the effects.

### C3. The captured image sits exactly over the real window

*All platforms*

- **Do:** With a window captured, compare the simulated image against the real window edges. Then move and resize the real window.
- **Expect:** 1:1, in the same place, and it follows the window.
- **Has failed as:** Three separate ways: the capture was blown up to fill the screen; it was aligned to the invisible resize border rather than the painted bounds, so it sat a few pixels out; and the plane was moved instead of the camera, which broke the scale.

### C4. The capture keeps the window's shape

*macOS*

- **Do:** Capture a window that is not the same aspect ratio as the display.
- **Expect:** The image keeps the window's proportions.
- **Has failed as:** Every capture was stretched to the shape of the screen.

### C5. The window list is usable

*Windows*

- **Do:** Open the window picker.
- **Expect:** Real window titles, an icon for each, and the one being captured is marked.
- **Has failed as:** Nothing in the list said which window was currently captured, and the icons were blank — they were read from the window rather than from the executable.

### C6. Picking a second window switches the capture

*All platforms*

- **Do:** Capture one window, then pick a different one. On Windows, make the second one a browser.
- **Expect:** The simulation follows to the new window, still with the effects on.
- **Has failed as:** The capture surface is created when a window is picked, so anything configured once at startup is not applied to it. The capture method has to be re-applied every time — the first version of that fix set it only at startup, when there was nothing yet to set it on.

### C7. A window on the other monitor

*Windows*

- **Do:** With two displays, put a browser on the screen VIP-Sim is NOT overlaying and capture it.
- **Expect:** VIP-Sim says the window is on another screen and points at F3. Move it there, and the capture appears.
- **Has failed as:** Nothing at all -- and worse, nothing consistent. Windows reports every window in global desktop coordinates, where a monitor arranged above the primary has negative y; the placement subtracted Unity's Screen.mainWindowPosition, which is relative to the display the overlay is on and so is (0,0) on every monitor. The subtraction did nothing, and the capture was drawn the distance between the two monitors away from where it belonged. A window on the other screen landed off the edge and showed nothing; a window that happened to sit near the desktop origin landed roughly centred and appeared, at the wrong size. Reported as 'for Discord it worked but was distorted, for Claude and Outlook again nothing' -- which reads as a per-application fault and is not one.

### C8. A minimised window

*Windows*

- **Do:** Capture a window, then minimise it, then restore it.
- **Expect:** The image holds while it is minimised, and comes back when it does.
- **Has failed as:** Windows parks a minimised window at (-32000,-32000) and keeps reporting that as its position. The placement followed it there, throwing the capture 32000px off screen -- indistinguishable from the capture dying.

### C9. A window larger than the screen VIP-Sim is on

*Windows*

- **Do:** With two displays of different sizes, maximise a window on the larger one and capture it from the smaller one.
- **Expect:** The same notice as C7 -- the window is not on this screen.
- **Has failed as:** It was drawn at 1:1, which is correct and useless: a 3200x1880 window on a 2560x1440 screen shows its middle and nothing else, which reads as a zoomed, distorted capture rather than as a window that does not fit.

## D — The effects themselves

Read the ALPHA line in the log alongside these — it names every effect that is actually enabled, which settles most arguments about whether an effect ran.

### D1. Every effect visibly does something

*All platforms*

- **Do:** Switch each effect on in turn, alone, and move its sliders end to end.
- **Expect:** A visible change for every one of them.
- **Has failed as:** Several symptoms did nothing at all. Separately, a degree-to-pixel conversion was mis-scaled, so effects sized in degrees of visual angle came out at the wrong size on every display.

### D2. Switching one effect off leaves the others alone

*All platforms*

- **Do:** Switch on three effects, adjust each, then switch one of them off.
- **Expect:** The other two keep their settings.
- **Has failed as:** Switching a single effect off wiped every setting of every effect.

### D3. The desktop stays visible with effects running

*All platforms*

- **Do:** Switch on several effects at once, especially the vision-loss ones.
- **Expect:** The simulation is drawn over the desktop, not instead of it.
- **Has failed as:** Four separate shaders mangled the framebuffer alpha, each producing a different flavour of the same fault: a dimming sweep, a grey wash, and a black screen. Measure rather than eyeball — the ALPHA line in the log gives the numbers.

### D4. The effect list appears once a window is selected

*All platforms*

- **Do:** Start fresh, pick a window, and look for the list of effects.
- **Expect:** It appears.
- **Has failed as:** It never appeared at all: a gate that waited for open settings deadlocked against the list that was supposed to open them.

### D5. The settings panel belongs to the effect you clicked

*All platforms*

- **Do:** Open the settings of one effect, then another. Switch an effect off while a different effect's settings are open.
- **Expect:** The panel always shows the effect you selected, and switching an effect off closes only its own panel.
- **Has failed as:** The panel showed another effect's settings; and switching one effect off closed a different effect's open panel.

### D6. All of an effect's settings fit in the panel

*All platforms*

- **Do:** Open the effect with the most settings and scroll to the bottom.
- **Expect:** Every control is reachable.
- **Has failed as:** The controls overflowed the panel and the last ones could not be reached at all.

### D7. Toggling effects leaves nothing in the error log

*All platforms*

- **Do:** Apply a preset or a profile, switch several effects on and off, then read vipsim-errors.log next to Player.log.
- **Expect:** Empty.
- **Has failed as:** "Coroutine couldn't be started because the game object 'EnableToggle' is inactive!" -- something sets the toggles while the list they belong to is hidden, and Unity logs an error rather than ignoring it. The same action also wrote one warning per switched-off effect, seventeen at a time, for what is simply the normal state of most of them.

### D8. Closing an effect's parameters leaves the effect list alone

*All platforms*

- **Do:** Switch a symptom on (its parameters appear), then switch the same symptom off again. Watch the list and the master Enable switch.
- **Expect:** The parameters close. The list still shows all eighteen symptoms and the Enable switch is untouched.
- **Has failed as:** The entire effect list vanished and the simulation switched itself off. The parameter panel stored its open/closed state IN the master Enable slider, and that slider is what gates both the panel and the list -- so closing one effect's parameters set the master switch to zero. The switch was left looking half-thrown: its fill colour is set by the toggle's own events, which never fired, while its knob follows the slider value, which had been moved behind its back. Reported as 'enable is selected but no symptoms are shown'.

### D9. Picking a window does not switch a symptom on

*Windows · macOS*

- **Do:** Start fresh, pick a window from the list, and read the log before touching anything else.
- **Expect:** ROWS ... 0 shown on, and enabled(0). Nothing is running until you say so.
- **Has failed as:** A fresh session, one click to pick a window, and the log showed enabled(1) myFieldLoss with that row lit and its parameters open. Selecting a window cycles the master switch, and the master switch decides which effects are on by comparing each row's SPRITE -- while the gear logic reads a separate flag on the same row. Start() set the sprite and left the flag alone, so there was a window in which the two disagreed, and the switch pressed a row in that state.

### D10. A profile loaded before the list is shown still takes effect

*All platforms*

- **Do:** Pick a window but leave the master Enable off, so the effect list is hidden. Load p1.json. Now switch Enable on.
- **Expect:** The list appears with p1's eight symptoms already lit.
- **Has failed as:** The binder looked the list up with GameObject.Find, which skips inactive objects, so a profile loaded while the list was hidden updated no rows at all -- and revealing the list afterwards showed every symptom off while the effects were running.

## E — Toolbar, panels and accessibility

The toolbar is six unlabelled glyphs. Everything that explains it has broken at some point.

### E1. Hover help appears after a short pause

*All platforms*

- **Do:** Rest the pointer on one toolbar icon and hold it there for a second.
- **Expect:** The help text appears after roughly 0.6 s and names what the button does.
- **Has failed as:** Twice, in opposite directions. First it never appeared on any button: the lookup for the shared label skipped inactive objects, and the label is inactive by design. Then it appeared instantly, so crossing the toolbar flashed all six descriptions in sequence.

### E2. Hover help does not appear after you have left

*All platforms*

- **Do:** Sweep the pointer across the whole toolbar without stopping. Then rest on a button and move away before the help appears.
- **Expect:** Nothing appears in either case.
- **Has failed as:** The delayed version of E1 is only correct if a pending tooltip is cancelled on exit; otherwise it appears over a button the pointer left half a second ago.

### E3. Every toolbar icon tints on hover

*All platforms*

- **Do:** Move along the row and watch each glyph.
- **Expect:** All six respond the same way.
- **Has failed as:** Two buttons had a visible border and no hover tint while their four siblings had the opposite, so the row looked like two different toolbars.

### E4. The F1 panel scrolls and its footer is reachable

*All platforms*

- **Do:** Press F1 and scroll to the bottom. Repeat on a 4K display if you have one.
- **Expect:** Every button at the foot of the panel is visible and clickable.
- **Has failed as:** The panel did not scroll; its footer buttons were not scaled for the display, so on a 4K panel they were unreadably small; and the permission screen was cropped.

### E5. Text size and high contrast reach the whole interface

*All platforms*

- **Do:** In the F1 panel, press A+ several times and switch high contrast on.
- **Expect:** Every panel and label changes, not just the one you are looking at.
- **Has failed as:** The text size setting reached only part of the interface, which is worse than not having it: the panel you set it from grew and everything else stayed small.

### E6. The interface can be driven from the keyboard

*All platforms*

- **Do:** Use Tab and the arrow keys to move through the controls.
- **Expect:** Focus moves, and where it is is visible.
- **Has failed as:** Nothing in the interface could be reached from the keyboard at all — a gap worth naming in a tool about vision impairment.

### E7. The F1 panel is readable

*All platforms*

- **Do:** Press F1 and look at how much is on screen at once.
- **Expect:** Three sections -- Symptoms, Display & text, Help & updates -- one at a time, and a single Close button under them.
- **Has failed as:** All of it at once: an eighteen-entry symptom reference, a paper link, four navigation buttons, two rows of accessibility controls with their own paragraph of keyboard help, three support buttons and an update status line. Nine controls in the footer alone, and the reference the panel exists for was the hardest thing on it to read.

### E8. There is no manual window-size dialog

*Windows · macOS*

- **Do:** Select a window and look at the toolbar.
- **Expect:** Load, Save, gaze source, symptoms, calibrate, minimise, exit. No gear.
- **Has failed as:** A Settings dialog offered X-Offset, Y-Offset and Zoom, for when the automatic detection of the window size was unsuccessful. It outlived the problem, and did damage while it did: settingsOpen was set when the dialog opened and cleared only by Abort, so after one Apply it rewrote the capture plane's position and size ten times a second for the rest of the session, from fields nobody could see. One user log showed a stale -1.28 world-unit offset -- 1280 pixels -- still being applied. Removing the dialog and its toolbar button had to be a single act: the button suppressed click-through and only the dialog restored it, so removing either alone locks the desktop.

## F — Profiles

The condition profiles (p1.json to p7.json) are not in the download — get them separately and put them somewhere you can navigate to. Two of the three faults below made the profiles look like they did not exist.

### F1. Load opens a file dialog

*All platforms*

- **Do:** Click Load in the toolbar.
- **Expect:** A file dialog opens.
- **Has failed as:** The button did nothing whatsoever: no dialog, no error, nothing in the log. The dialog is a coroutine and it was being discarded rather than run, so the code then read a stale result from the previous dialog. Save had exactly the same fault.

### F2. The dialog lists .json profiles

*All platforms*

- **Do:** In the dialog, navigate to the folder holding p1.json … p7.json.
- **Expect:** The profiles are listed and selectable.
- **Has failed as:** The filter offered .profile only, so the folder appeared empty — which from the user's side is indistinguishable from the profiles not being there.

### F3. Loading a profile applies it

*All platforms*

- **Do:** Load p1.json with a window captured and watch both the screen and the log.
- **Expect:** Effects switch on and the image changes. The log prints [ConditionProfile] p1: applied N of M parameters.
- **Has failed as:** Loading reset every effect to zero, left the simulation blank, and logged that it had succeeded. Unrecognised fields were being ignored silently, so a file with nothing this build understands read as a file asking for nothing to be enabled.

### F4. Each of the seven profiles loads

*All platforms*

- **Do:** Load p1 through p7 in turn.
- **Expect:** Each changes the simulation, and each logs its own applied count.
- **Has failed as:** Roughly half of each profile's parameters have no counterpart in this build. That is expected and reported, not a failure — but an applied count of 0, or the same count for every profile, is.

### F5. A file that is neither kind is refused

*All platforms*

- **Do:** Point Load at some unrelated .json file.
- **Expect:** An error naming the file, the words 'Nothing was changed', and a simulation that is exactly as it was.
- **Has failed as:** This is the guard added after F3. Confirm it guards: the simulation must not change.

### F6. Save writes a profile

*All platforms*

- **Do:** Switch on three effects, click Save, and give it a name.
- **Expect:** A .json file is written, and the log says it saved a profile with 3 active effects.
- **Has failed as:** Save shared F1's discarded-coroutine fault, so the dialog never opened.

### F7. A saved profile reloads to the same state

*All platforms*

- **Do:** Save the current simulation, change several settings, then load the file you saved.
- **Expect:** The simulation returns to what it was when you saved.
- **Has failed as:** This is the whole point of Save, and it is only true if Save writes the same format Load reads. They were written at different times against different formats.

### F8. Profiles work on macOS too

*macOS*

- **Do:** Run F1 to F7 again on the Mac.
- **Expect:** Identical behaviour.
- **Has failed as:** See A5 — the macOS build shipped once with none of this in it.

### F9. A loaded profile switches its symptoms on

*All platforms*

- **Do:** With a window captured and the master Enable on, load p1.json. Watch the effect list and the log.
- **Expect:** The eight symptoms p1 names light up in the list and the simulation changes. The log agrees with itself: ROWS ... 8 shown on, and enabled(8) naming the same effects.
- **Has failed as:** Nothing switched on. The binder called SetActive on the object it found in the menu -- but a menu row is a bare RectTransform with two buttons, and every effect is a MonoBehaviour on the camera rig, where Behaviour.enabled is the only switch that makes anything render. So the profile's parameters were written to effects that stayed dark, and the load reported success.

### F10. A loaded profile leaves every other symptom in the list

*All platforms*

- **Do:** Count the rows in the effect list before and after loading a profile.
- **Expect:** Eighteen, both times. A profile decides what is switched ON, never what is available.
- **Has failed as:** The list shrank to the profile's own symptoms. SetActive(false) on the rows the profile did not mention deleted them from the interface, so after loading p1 there was no way to reach the other ten symptoms at all without restarting.

## G — More than one monitor

Needs two displays. Everything here passes trivially on a single-monitor machine, which is how the original fault shipped.

### G1. The overlay says which screen it is on

*All platforms*

- **Do:** Start VIP-Sim with two or more displays connected.
- **Expect:** A notice at the top of the screen for a few seconds: 'VIP-Sim is on display X of Y. Press F3 to move it to the next screen.'
- **Has failed as:** It restored the display it had been used on last, silently. The simulation appeared on a monitor the user was not looking at, over applications they had not meant, and nothing on screen said what had happened or how to undo it.

### G2. F3 moves it to the next screen

*All platforms*

- **Do:** Press F3.
- **Expect:** The overlay appears on the other display and the notice reappears naming it.
- **Has failed as:** The control existed and was findable by nobody.

### G3. The move actually completes

*All platforms*

- **Do:** After F3, check the overlay is full screen on the target display and still covers all of it — particularly when the two displays are different resolutions.
- **Expect:** Borderless, full screen, correct size.
- **Has failed as:** The window stayed where it was, or ended up stranded as a small window on the wrong monitor. The resolution change had not taken effect when the move was requested, so the move applied to the old geometry.

### G4. F3 keeps working

*All platforms*

- **Do:** Press F3 six times, going round the loop at least twice.
- **Expect:** It cycles every time.
- **Has failed as:** A move that never reported completion left a flag latched, after which every later F3 was ignored in silence.

### G5. The button in the F1 panel does the same

*All platforms*

- **Do:** Press F1 and use 'Move to next display'.
- **Expect:** Same result as F3.
- **Has failed as:** F3 only reaches VIP-Sim while it holds keyboard focus, and a click-through overlay almost never does — so on many machines the hotkey alone is not a control at all. Test the button, not just the key.

## H — Gaze and calibration

Mouse-following is the default and should be tested first; the webcam path needs a camera and, on macOS, a permission grant and a restart.

### H1. It starts on mouse-following

*All platforms*

- **Do:** Start fresh and switch on an effect that follows the gaze.
- **Expect:** The effect follows the mouse pointer immediately.
- **Has failed as:** It started on eye tracking. On a machine with no webcam, nothing moved and the tool looked broken from the first minute.

### H2. The gaze follows the pointer while another app has focus

*All platforms*

- **Do:** Click into another application, then move the mouse around the screen.
- **Expect:** The effect keeps following the pointer.
- **Has failed as:** The gaze point froze the moment the overlay lost focus — which, being click-through, is nearly always.

### H3. Switching to webcam tracking

*All platforms*

- **Do:** Use the toolbar toggle, then pick a camera.
- **Expect:** The camera list is populated and the picker looks like something you can click.
- **Has failed as:** The picker did not read as a control, so nobody used it.

### H4. The eye tracker does not paint on the screen

*All platforms*

- **Do:** Run with webcam tracking on and look at the whole desktop.
- **Expect:** No webcam preview, and exactly one cursor.
- **Has failed as:** Both, separately: a full-screen webcam preview over the desktop, and a second cursor painted next to the real one.

### H5. The gaze update rate is sane

*All platforms*

- **Do:** With webcam tracking on, read the gaze rate in the periodic log line.
- **Expect:** Tens of samples per second.
- **Has failed as:** 5 Hz. The webcam was being requested at 1920 px and 60 fps, and the tracker could not keep up with the frames it asked for.

### H6. Calibration can be entered, followed and left

*All platforms*

- **Do:** Start calibration from the toolbar, follow the dot, then press Escape part-way through and start it again with F9.
- **Expect:** Clicks reach the calibration screen, and Escape or a right-click aborts it at any point.
- **Has failed as:** Clicks did not reach the calibration screen, and once started it could not be left — on a click-through, always-on-top overlay that is a trap with no way out but the quit hotkey.

### H7. Camera permission on macOS

*macOS*

- **Do:** Switch to webcam tracking on a Mac that has never granted the app camera access. Grant it, then quit and start the app again.
- **Expect:** The system prompt appears with a sensible explanation, and tracking works after the restart.
- **Has failed as:** The app must be restarted after granting — without the restart the camera stays unavailable and it looks as though the permission did nothing.

## I — Getting out

### I1. All three ways out work

*All platforms*

- **Do:** Quit with the toolbar's exit button. Start again and quit with Ctrl+Alt+Q while a different application has focus. Start again and quit with F12.
- **Expect:** The application closes every time, and leaves no window behind.
- **Has failed as:** There were states with no way out: no title bar, no close button, click-through swallowing the attempt, and a calibration screen that could not be exited. The hotkey has to work when VIP-Sim is not the foreground application, which is the case that matters and the one easiest to skip.

## J — Linux

Needs a Wayland compositor with layer-shell — sway, KWin, Hyprland, labwc or niri. GNOME is expected to refuse; see J2. Everything here was found during bring-up, so treat the whole column as unproven on hardware other than the developer's.

### J1. Start it with the script

*Linux*

- **Do:** Run ./VIP-Sim.sh, not the VIP-Sim binary.
- **Expect:** One overlay covering the screen.
- **Has failed as:** Running the binary directly gives an ordinary window and no overlay. The overlay is a second program and the script starts the pair in the right order.

### J2. GNOME refuses clearly

*Linux*

- **Do:** Run it on GNOME.
- **Expect:** It says the compositor does not implement layer-shell, and exits.
- **Has failed as:** Mutter will not implement the protocol, so there is no overlay to be had. Half-working, with a plain window sitting in the middle of the screen, would be worse than the refusal.

### J3. Only the overlay is on the desktop

*Linux*

- **Do:** Ask the compositor for its window list (on sway: swaymsg -t get_tree).
- **Expect:** No VIP-Sim toplevel. The simulator runs inside the overlay's own compositor and has no window of its own.
- **Has failed as:** The simulator's window appeared on the desktop next to the overlay, decorated with a title bar, so there were two of everything.

### J4. Screen capture delivers real frames

*Linux*

- **Do:** Accept the compositor's screen-sharing dialog and watch the simulation.
- **Expect:** The captured screen appears and the effects apply to it.
- **Has failed as:** Three different failures, all at this step: no frames at all; a crash on the very first frame; and frames counted as delivered that were entirely black.

### J5. The image is the right way up

*Linux*

- **Do:** Look at any text in the captured screen.
- **Expect:** Readable, and the right way round.
- **Has failed as:** The overlay was vertically mirrored — the OpenGL texture origin is the opposite of the one the rest of the pipeline assumes.

### J6. The interface takes input

*Linux*

- **Do:** Click toolbar buttons, open a panel, drag a slider.
- **Expect:** It responds as it does on Windows.
- **Has failed as:** Input went nowhere: the overlay owns the pointer and keyboard, and until it forwarded them the simulator inside it received nothing.

### J7. Clicks pass through outside the interface

*Linux*

- **Do:** Click on an application behind the overlay.
- **Expect:** The click reaches it.
- **Has failed as:** The region that takes input has to match what is drawn. When it did not, either the whole screen swallowed clicks or the toolbar stopped taking them.

## K — The archive itself

### K1. Test the archive, not a build

*All platforms*

- **Do:** Verify the download against SHA256SUMS.txt, extract it somewhere new, and run everything above from that copy.
- **Expect:** The checksums match and the extracted copy is what you test.
- **Has failed as:** A build tree was tested and an archive was shipped. They were not the same thing: the macOS archive was missing an entire source file that the tested tree had.

### K2. The profiles are not inside the download

*All platforms*

- **Do:** Search the extracted folder for p1.json … p7.json.
- **Expect:** They are not there. The profiles are a separate, paid add-on.
- **Has failed as:** They were committed to the repository once and had to be removed. Anything that ships them inside the free download gives them away.

## Never yet proven on hardware

Start here if your time is limited. These are the parts of the list nobody has been able to check:

- **D8-D10 on macOS.** macOS has now been run on real hardware and works, which also settles the capture orientation there. Its effect list is gated differently from Windows though - HideImpairmentSelection is not in that scene at all - so the three state-machine checks are the ones worth repeating on a Mac specifically.
- **Linux on KWin.** Developed and verified on sway under WSL. KWin implements the same protocols and should work; it has not been tried.
- **Linux on GNOME (J2).** The refusal path has never been seen on real GNOME.

## Also worth trying

These have never failed, so they are not on the list proper — but they are untested rather than proven:

- Unplug one of two displays between runs, so the remembered display no longer exists.
- The first-run tutorial: it should appear once, and 'Show tutorial' in the F1 panel should bring it back.
- 'Copy diagnostics path' and 'Report a problem' in the F1 panel.
- Minimise and restore.
- The condition presets in the effect list.
- Leave it running for an hour with several effects on and watch the frame rate in the log.

## Known limits — not failures

- **Linux, wlroots compositors** (sway and relatives) can only share a whole output, so the simulation is captured back into itself. With two monitors, share the one VIP-Sim is not overlaying.
- **Linux rendering is on the CPU.** Slower than Windows and macOS, noticeably so on a large screen with several effects.
- **Linux gaze is webcam-only.** Wayland does not let an application read the global pointer position, so mouse-following cannot work there.
- **macOS and Windows builds are unsigned.** Both operating systems will warn on first launch.

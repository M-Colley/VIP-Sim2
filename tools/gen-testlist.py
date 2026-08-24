# -*- coding: utf-8 -*-
"""Generate the VIP-Sim regression checklist in two forms from one source of truth.

Every test on this list is a fault that has actually happened at least once. The data
below is the list; docs/TESTING.md and the shareable HTML page are both rendered from it,
so they cannot drift apart.
"""

import html
import io
import os

WIN, MAC, LNX = "win", "mac", "linux"
ALL = [WIN, MAC, LNX]

GROUPS = [
    {
        "key": "A",
        "title": "Install and first launch",
        "note": "Do this on a machine that has never run VIP-Sim. Several of these faults "
                "only exist on a clean install, and several others only exist in the "
                "packaged archive rather than in a build made on the developer's machine.",
        "tests": [
            {
                "id": "A1",
                "title": "The packaged build starts at all",
                "plat": [WIN],
                "do": "Extract VIP-Sim-Windows-x64.zip into an empty folder and run VIP-Sim.exe.",
                "expect": "The toolbar appears within a few seconds, and Player.log is written.",
                "fail": "A build made on top of a poisoned incremental cache started and died "
                        "immediately with 0xC0000005 — nothing on screen, and the only trace "
                        "was in the Windows Event Viewer. Separately, a shader the capture "
                        "plane needs was stripped from a player build: correct in the Editor, "
                        "nothing drawn in the build.",
            },
            {
                "id": "A2",
                "title": "The macOS app is executable straight out of the archive",
                "plat": [MAC],
                "do": "Unzip VIP-Sim-macOS-universal.zip on a Mac and double-click VIP-Sim.app.",
                "expect": "It launches (see A3 for Gatekeeper).",
                "fail": "The zip was created on Windows, which does not carry the execute bit. "
                        "The binary inside the bundle arrived non-executable and macOS reported "
                        "the application as damaged.",
            },
            {
                "id": "A3",
                "title": "Gatekeeper refuses the first launch",
                "plat": [MAC],
                "do": "Right-click the app and choose Open, then confirm.",
                "expect": "It opens on the second attempt and every launch after that.",
                "fail": "Reported as a failure, and it is expected: this build is unsigned. "
                        "Worth walking through so the instructions we ship are the ones that "
                        "actually work.",
            },
            {
                "id": "A4",
                "title": "The application is called VIP-Sim",
                "plat": [MAC],
                "do": "Look at the app name in Finder, the menu bar and the Dock.",
                "expect": "VIP-Sim everywhere.",
                "fail": "The build was called 'macos' — the name of the project folder — in all "
                        "three places.",
            },
            {
                "id": "A5",
                "title": "The macOS build has everything the Windows build has",
                "plat": [MAC],
                "do": "Confirm the toolbar has Load and Save, that F1 opens the symptom panel, "
                      "that the display switch is offered when a second monitor is attached, "
                      "and that hovering an icon produces help.",
                "expect": "Same features, same behaviour as Windows.",
                "fail": "The macOS archive shipped with none of the profile work in it. Load "
                        "still ran the old code path — the one that reset every effect to zero "
                        "— while the Windows build had been fixed. The two projects are "
                        "separate and a fix in one is not a fix in the other.",
            },
        ],
    },
    {
        "key": "B",
        "title": "The overlay window",
        "note": "The overlay is borderless, always on top and click-through. Every property "
                "in that sentence has been broken at least once, and each one fails silently.",
        "tests": [
            {
                "id": "B1",
                "title": "The overlay is full screen and transparent",
                "plat": ALL,
                "do": "Start VIP-Sim with no effects switched on and look at the desktop behind it.",
                "expect": "The desktop is unchanged. Only the toolbar is drawn.",
                "fail": "The whole screen washed grey, then black. Several effect shaders were "
                        "squaring the framebuffer alpha, so the overlay stopped being "
                        "transparent. Alpha is load-bearing here: a wrong alpha looks exactly "
                        "like a dead effect.",
            },
            {
                "id": "B2",
                "title": "Clicks pass through the overlay",
                "plat": ALL,
                "do": "Click on desktop icons and on another application through an empty part "
                      "of the screen. Then click a toolbar button.",
                "expect": "Clicks reach whatever is underneath, except on the toolbar and open "
                          "panels, which take them.",
                "fail": "Click-through was lost entirely and the desktop became unusable — the "
                        "only way out was the quit hotkey. Do not judge this one by feel: on "
                        "Windows, read WS_EX_TRANSPARENT off the live window; the by-hand test "
                        "gives false passes.",
            },
            {
                "id": "B3",
                "title": "The toolbar is entirely on screen",
                "plat": ALL,
                "do": "Look at the toolbar and any panel you open, on the largest and smallest "
                      "display you have.",
                "expect": "Nothing is cut off by a screen edge, and no panel overlaps the title bar.",
                "fail": "The toolbar overflowed past the right edge of the screen; a panel "
                        "overhung the title bar and covered its own controls.",
            },
            {
                "id": "B4",
                "title": "Window geometry survives a restart",
                "plat": ALL,
                "do": "Quit VIP-Sim and start it again.",
                "expect": "It comes back full screen, exactly as it was.",
                "fail": "A persisted 1x1 window. The application started, ran, wrote its log — "
                        "and was invisible, which reads as 'it does not start'.",
            },
            {
                "id": "B5",
                "title": "The macOS window is genuinely transparent",
                "plat": [MAC],
                "do": "Start the app and look at the desktop behind it.",
                "expect": "Same as B1.",
                "fail": "An opaque background covered the entire screen. macOS needed its own "
                        "fix; the Windows transparency work did not carry over.",
            },
            {
                "id": "B6",
                "title": "Nothing paints over the overlay",
                "plat": ALL,
                "do": "Watch the whole screen for a few seconds with an effect on.",
                "expect": "Only the simulation and the interface are drawn.",
                "fail": "A second, perspective camera cleared to the skybox on top of "
                        "everything, so the desktop disappeared behind a blue-grey gradient.",
            },
        ],
    },
    {
        "key": "C",
        "title": "Capturing a window",
        "note": "This is where the most expensive fault in the project lives, and it depends "
                "entirely on which window you pick. Test both kinds.",
        "tests": [
            {
                "id": "C1",
                "title": "A window that draws with the GPU",
                "plat": [WIN, MAC],
                "do": "Pick a browser (Chrome, Edge, Firefox), VS Code, or a File Explorer window.",
                "expect": "The simulation shows that window's real content, with the effects "
                          "applied to it.",
                "fail": "Black. Every number in the log was correct — the right title, the right "
                        "rectangle, the plane placed and scaled properly — and there was no "
                        "picture. The capture plugin was on Auto, which reads what an "
                        "application draws through GDI, and a GPU-rendered window draws nothing "
                        "there. From the outside this is indistinguishable from the simulation "
                        "being switched off, and it was reported as 'the effects do not work'. "
                        "This is the single most important test on the list.",
            },
            {
                "id": "C2",
                "title": "A window that draws through GDI",
                "plat": [WIN],
                "do": "Pick Notepad and switch an effect on.",
                "expect": "The simulation shows Notepad.",
                "fail": "Never failed — which is the point. Notepad worked on the machine where "
                        "browsers came out black, so testing only with Notepad hid C1 "
                        "completely. If C1 fails and C2 passes, it is the capture method, not "
                        "the effects.",
            },
            {
                "id": "C3",
                "title": "The captured image sits exactly over the real window",
                "plat": ALL,
                "do": "With a window captured, compare the simulated image against the real "
                      "window edges. Then move and resize the real window.",
                "expect": "1:1, in the same place, and it follows the window.",
                "fail": "Three separate ways: the capture was blown up to fill the screen; it "
                        "was aligned to the invisible resize border rather than the painted "
                        "bounds, so it sat a few pixels out; and the plane was moved instead of "
                        "the camera, which broke the scale.",
            },
            {
                "id": "C4",
                "title": "The capture keeps the window's shape",
                "plat": [MAC],
                "do": "Capture a window that is not the same aspect ratio as the display.",
                "expect": "The image keeps the window's proportions.",
                "fail": "Every capture was stretched to the shape of the screen.",
            },
            {
                "id": "C5",
                "title": "The window list is usable",
                "plat": [WIN],
                "do": "Open the window picker.",
                "expect": "Real window titles, an icon for each, and the one being captured is "
                          "marked.",
                "fail": "Nothing in the list said which window was currently captured, and the "
                        "icons were blank — they were read from the window rather than from the "
                        "executable.",
            },
            {
                "id": "C6",
                "title": "Picking a second window switches the capture",
                "plat": ALL,
                "do": "Capture one window, then pick a different one. On Windows, make the "
                      "second one a browser.",
                "expect": "The simulation follows to the new window, still with the effects on.",
                "fail": "The capture surface is created when a window is picked, so anything "
                        "configured once at startup is not applied to it. The capture method has "
                        "to be re-applied every time — the first version of that fix set it "
                        "only at startup, when there was nothing yet to set it on.",
            },
            {
                "id": "C7",
                "title": "A window on the other monitor",
                "plat": [WIN],
                "do": "With two displays, put a browser on the screen VIP-Sim is NOT overlaying "
                      "and capture it.",
                "expect": "VIP-Sim says the window is on another screen and points at F3. Move it "
                          "there, and the capture appears.",
                "fail": "Nothing at all -- and worse, nothing consistent. Windows reports every "
                        "window in global desktop coordinates, where a monitor arranged above the "
                        "primary has negative y; the placement subtracted Unity's "
                        "Screen.mainWindowPosition, which is relative to the display the overlay "
                        "is on and so is (0,0) on every monitor. The subtraction did nothing, and "
                        "the capture was drawn the distance between the two monitors away from "
                        "where it belonged. A window on the other screen landed off the edge and "
                        "showed nothing; a window that happened to sit near the desktop origin "
                        "landed roughly centred and appeared, at the wrong size. Reported as "
                        "'for Discord it worked but was distorted, for Claude and Outlook again "
                        "nothing' -- which reads as a per-application fault and is not one.",
            },
            {
                "id": "C8",
                "title": "A minimised window",
                "plat": [WIN],
                "do": "Capture a window, then minimise it, then restore it.",
                "expect": "The image holds while it is minimised, and comes back when it does.",
                "fail": "Windows parks a minimised window at (-32000,-32000) and keeps reporting "
                        "that as its position. The placement followed it there, throwing the "
                        "capture 32000px off screen -- indistinguishable from the capture dying.",
            },
            {
                "id": "C9",
                "title": "A window larger than the screen VIP-Sim is on",
                "plat": [WIN],
                "do": "With two displays of different sizes, maximise a window on the larger one "
                      "and capture it from the smaller one.",
                "expect": "The same notice as C7 -- the window is not on this screen.",
                "fail": "It was drawn at 1:1, which is correct and useless: a 3200x1880 window on "
                        "a 2560x1440 screen shows its middle and nothing else, which reads as a "
                        "zoomed, distorted capture rather than as a window that does not fit.",
            },
        ],
    },
    {
        "key": "D",
        "title": "The effects themselves",
        "note": "Read the ALPHA line in the log alongside these — it names every effect that is "
                "actually enabled, which settles most arguments about whether an effect ran.",
        "tests": [
            {
                "id": "D1",
                "title": "Every effect visibly does something",
                "plat": ALL,
                "do": "Switch each effect on in turn, alone, and move its sliders end to end.",
                "expect": "A visible change for every one of them.",
                "fail": "Several symptoms did nothing at all. Separately, a degree-to-pixel "
                        "conversion was mis-scaled, so effects sized in degrees of visual angle "
                        "came out at the wrong size on every display.",
            },
            {
                "id": "D2",
                "title": "Switching one effect off leaves the others alone",
                "plat": ALL,
                "do": "Switch on three effects, adjust each, then switch one of them off.",
                "expect": "The other two keep their settings.",
                "fail": "Switching a single effect off wiped every setting of every effect.",
            },
            {
                "id": "D3",
                "title": "The desktop stays visible with effects running",
                "plat": ALL,
                "do": "Switch on several effects at once, especially the vision-loss ones.",
                "expect": "The simulation is drawn over the desktop, not instead of it.",
                "fail": "Four separate shaders mangled the framebuffer alpha, each producing a "
                        "different flavour of the same fault: a dimming sweep, a grey wash, and "
                        "a black screen. Measure rather than eyeball — the ALPHA line in the log "
                        "gives the numbers.",
            },
            {
                "id": "D4",
                "title": "The effect list appears once a window is selected",
                "plat": ALL,
                "do": "Start fresh, pick a window, and look for the list of effects.",
                "expect": "It appears.",
                "fail": "It never appeared at all: a gate that waited for open settings "
                        "deadlocked against the list that was supposed to open them.",
            },
            {
                "id": "D5",
                "title": "The settings panel belongs to the effect you clicked",
                "plat": ALL,
                "do": "Open the settings of one effect, then another. Switch an effect off while "
                      "a different effect's settings are open.",
                "expect": "The panel always shows the effect you selected, and switching an "
                          "effect off closes only its own panel.",
                "fail": "The panel showed another effect's settings; and switching one effect "
                        "off closed a different effect's open panel.",
            },
            {
                "id": "D6",
                "title": "All of an effect's settings fit in the panel",
                "plat": ALL,
                "do": "Open the effect with the most settings and scroll to the bottom.",
                "expect": "Every control is reachable.",
                "fail": "The controls overflowed the panel and the last ones could not be "
                        "reached at all.",
            },
            {
                "id": "D7",
                "title": "Toggling effects leaves nothing in the error log",
                "plat": ALL,
                "do": "Apply a preset or a profile, switch several effects on and off, then read "
                      "vipsim-errors.log next to Player.log.",
                "expect": "Empty.",
                "fail": "\"Coroutine couldn't be started because the game object 'EnableToggle' "
                        "is inactive!\" -- something sets the toggles while the list they belong "
                        "to is hidden, and Unity logs an error rather than ignoring it. The same "
                        "action also wrote one warning per switched-off effect, seventeen at a "
                        "time, for what is simply the normal state of most of them.",
            },
            {
                "id": "D8",
                "title": "Closing an effect's parameters leaves the effect list alone",
                "plat": ALL,
                "do": "Switch a symptom on (its parameters appear), then switch the same symptom "
                      "off again. Watch the list and the master Enable switch.",
                "expect": "The parameters close. The list still shows all eighteen symptoms and "
                          "the Enable switch is untouched.",
                "fail": "The entire effect list vanished and the simulation switched itself off. "
                        "The parameter panel stored its open/closed state IN the master Enable "
                        "slider, and that slider is what gates both the panel and the list -- so "
                        "closing one effect's parameters set the master switch to zero. The "
                        "switch was left looking half-thrown: its fill colour is set by the "
                        "toggle's own events, which never fired, while its knob follows the "
                        "slider value, which had been moved behind its back. Reported as "
                        "'enable is selected but no symptoms are shown'.",
            },
            {
                "id": "D9",
                "title": "Picking a window does not switch a symptom on",
                "plat": [WIN, MAC],
                "do": "Start fresh, pick a window from the list, and read the log before touching "
                      "anything else.",
                "expect": "ROWS ... 0 shown on, and enabled(0). Nothing is running until you say so.",
                "fail": "A fresh session, one click to pick a window, and the log showed "
                        "enabled(1) myFieldLoss with that row lit and its parameters open. "
                        "Selecting a window cycles the master switch, and the master switch "
                        "decides which effects are on by comparing each row's SPRITE -- while "
                        "the gear logic reads a separate flag on the same row. Start() set the "
                        "sprite and left the flag alone, so there was a window in which the two "
                        "disagreed, and the switch pressed a row in that state.",
            },
            {
                "id": "D10",
                "title": "A profile loaded before the list is shown still takes effect",
                "plat": ALL,
                "do": "Pick a window but leave the master Enable off, so the effect list is "
                      "hidden. Load p1.json. Now switch Enable on.",
                "expect": "The list appears with p1's eight symptoms already lit.",
                "fail": "The binder looked the list up with GameObject.Find, which skips inactive "
                        "objects, so a profile loaded while the list was hidden updated no rows "
                        "at all -- and revealing the list afterwards showed every symptom off "
                        "while the effects were running.",
            },
        ],
    },
    {
        "key": "E",
        "title": "Toolbar, panels and accessibility",
        "note": "The toolbar is six unlabelled glyphs. Everything that explains it has broken "
                "at some point.",
        "tests": [
            {
                "id": "E1",
                "title": "Hover help appears after a short pause",
                "plat": ALL,
                "do": "Rest the pointer on one toolbar icon and hold it there for a second.",
                "expect": "The help text appears after roughly 0.6 s and names what the button does.",
                "fail": "Twice, in opposite directions. First it never appeared on any button: "
                        "the lookup for the shared label skipped inactive objects, and the label "
                        "is inactive by design. Then it appeared instantly, so crossing the "
                        "toolbar flashed all six descriptions in sequence.",
            },
            {
                "id": "E2",
                "title": "Hover help does not appear after you have left",
                "plat": ALL,
                "do": "Sweep the pointer across the whole toolbar without stopping. Then rest on "
                      "a button and move away before the help appears.",
                "expect": "Nothing appears in either case.",
                "fail": "The delayed version of E1 is only correct if a pending tooltip is "
                        "cancelled on exit; otherwise it appears over a button the pointer left "
                        "half a second ago.",
            },
            {
                "id": "E3",
                "title": "Every toolbar icon tints on hover",
                "plat": ALL,
                "do": "Move along the row and watch each glyph.",
                "expect": "All six respond the same way.",
                "fail": "Two buttons had a visible border and no hover tint while their four "
                        "siblings had the opposite, so the row looked like two different "
                        "toolbars.",
            },
            {
                "id": "E4",
                "title": "The F1 panel scrolls and its footer is reachable",
                "plat": ALL,
                "do": "Press F1 and scroll to the bottom. Repeat on a 4K display if you have one.",
                "expect": "Every button at the foot of the panel is visible and clickable.",
                "fail": "The panel did not scroll; its footer buttons were not scaled for the "
                        "display, so on a 4K panel they were unreadably small; and the "
                        "permission screen was cropped.",
            },
            {
                "id": "E5",
                "title": "Text size and high contrast reach the whole interface",
                "plat": ALL,
                "do": "In the F1 panel, press A+ several times and switch high contrast on.",
                "expect": "Every panel and label changes, not just the one you are looking at.",
                "fail": "The text size setting reached only part of the interface, which is worse "
                        "than not having it: the panel you set it from grew and everything else "
                        "stayed small.",
            },
            {
                "id": "E6",
                "title": "The interface can be driven from the keyboard",
                "plat": ALL,
                "do": "Use Tab and the arrow keys to move through the controls.",
                "expect": "Focus moves, and where it is is visible.",
                "fail": "Nothing in the interface could be reached from the keyboard at all — a "
                        "gap worth naming in a tool about vision impairment.",
            },
            {
                "id": "E7",
                "title": "The F1 panel is readable",
                "plat": ALL,
                "do": "Press F1 and look at how much is on screen at once.",
                "expect": "Three sections -- Symptoms, Display & text, Help & updates -- one at a "
                          "time, and a single Close button under them.",
                "fail": "All of it at once: an eighteen-entry symptom reference, a paper link, "
                        "four navigation buttons, two rows of accessibility controls with their "
                        "own paragraph of keyboard help, three support buttons and an update "
                        "status line. Nine controls in the footer alone, and the reference the "
                        "panel exists for was the hardest thing on it to read.",
            },
            {
                "id": "E8",
                "title": "There is no manual window-size dialog",
                "plat": [WIN, MAC],
                "do": "Select a window and look at the toolbar.",
                "expect": "Load, Save, gaze source, symptoms, calibrate, minimise, exit. No gear.",
                "fail": "A Settings dialog offered X-Offset, Y-Offset and Zoom, for when the "
                        "automatic detection of the window size was unsuccessful. It outlived "
                        "the problem, and did damage while it did: settingsOpen was set when the "
                        "dialog opened and cleared only by Abort, so after one Apply it rewrote "
                        "the capture plane's position and size ten times a second for the rest "
                        "of the session, from fields nobody could see. One user log showed a "
                        "stale -1.28 world-unit offset -- 1280 pixels -- still being applied. "
                        "Removing the dialog and its toolbar button had to be a single act: the "
                        "button suppressed click-through and only the dialog restored it, so "
                        "removing either alone locks the desktop.",
            },
        ],
    },
    {
        "key": "F",
        "title": "Profiles",
        "note": "The condition profiles (p1.json to p7.json) are not in the download — get them "
                "separately and put them somewhere you can navigate to. Two of the three faults "
                "below made the profiles look like they did not exist.",
        "tests": [
            {
                "id": "F1",
                "title": "Load opens a file dialog",
                "plat": ALL,
                "do": "Click Load in the toolbar.",
                "expect": "A file dialog opens.",
                "fail": "The button did nothing whatsoever: no dialog, no error, nothing in the "
                        "log. The dialog is a coroutine and it was being discarded rather than "
                        "run, so the code then read a stale result from the previous dialog. "
                        "Save had exactly the same fault.",
            },
            {
                "id": "F2",
                "title": "The dialog lists .json profiles",
                "plat": ALL,
                "do": "In the dialog, navigate to the folder holding p1.json … p7.json.",
                "expect": "The profiles are listed and selectable.",
                "fail": "The filter offered .profile only, so the folder appeared empty — which "
                        "from the user's side is indistinguishable from the profiles not being "
                        "there.",
            },
            {
                "id": "F3",
                "title": "Loading a profile applies it",
                "plat": ALL,
                "do": "Load p1.json with a window captured and watch both the screen and the log.",
                "expect": "Effects switch on and the image changes. The log prints "
                          "[ConditionProfile] p1: applied N of M parameters.",
                "fail": "Loading reset every effect to zero, left the simulation blank, and "
                        "logged that it had succeeded. Unrecognised fields were being ignored "
                        "silently, so a file with nothing this build understands read as a file "
                        "asking for nothing to be enabled.",
            },
            {
                "id": "F4",
                "title": "Each of the seven profiles loads",
                "plat": ALL,
                "do": "Load p1 through p7 in turn.",
                "expect": "Each changes the simulation, and each logs its own applied count.",
                "fail": "Roughly half of each profile's parameters have no counterpart in this "
                        "build. That is expected and reported, not a failure — but an applied "
                        "count of 0, or the same count for every profile, is.",
            },
            {
                "id": "F5",
                "title": "A file that is neither kind is refused",
                "plat": ALL,
                "do": "Point Load at some unrelated .json file.",
                "expect": "An error naming the file, the words 'Nothing was changed', and a "
                          "simulation that is exactly as it was.",
                "fail": "This is the guard added after F3. Confirm it guards: the simulation "
                        "must not change.",
            },
            {
                "id": "F6",
                "title": "Save writes a profile",
                "plat": ALL,
                "do": "Switch on three effects, click Save, and give it a name.",
                "expect": "A .json file is written, and the log says it saved a profile with 3 "
                          "active effects.",
                "fail": "Save shared F1's discarded-coroutine fault, so the dialog never opened.",
            },
            {
                "id": "F7",
                "title": "A saved profile reloads to the same state",
                "plat": ALL,
                "do": "Save the current simulation, change several settings, then load the file "
                      "you saved.",
                "expect": "The simulation returns to what it was when you saved.",
                "fail": "This is the whole point of Save, and it is only true if Save writes the "
                        "same format Load reads. They were written at different times against "
                        "different formats.",
            },
            {
                "id": "F8",
                "title": "Profiles work on macOS too",
                "plat": [MAC],
                "do": "Run F1 to F7 again on the Mac.",
                "expect": "Identical behaviour.",
                "fail": "See A5 — the macOS build shipped once with none of this in it.",
            },
            {
                "id": "F9",
                "title": "A loaded profile switches its symptoms on",
                "plat": ALL,
                "do": "With a window captured and the master Enable on, load p1.json. Watch the "
                      "effect list and the log.",
                "expect": "The eight symptoms p1 names light up in the list and the simulation "
                          "changes. The log agrees with itself: ROWS ... 8 shown on, and "
                          "enabled(8) naming the same effects.",
                "fail": "Nothing switched on. The binder called SetActive on the object it found "
                        "in the menu -- but a menu row is a bare RectTransform with two buttons, "
                        "and every effect is a MonoBehaviour on the camera rig, where "
                        "Behaviour.enabled is the only switch that makes anything render. So the "
                        "profile's parameters were written to effects that stayed dark, and the "
                        "load reported success.",
            },
            {
                "id": "F10",
                "title": "A loaded profile leaves every other symptom in the list",
                "plat": ALL,
                "do": "Count the rows in the effect list before and after loading a profile.",
                "expect": "Eighteen, both times. A profile decides what is switched ON, never "
                          "what is available.",
                "fail": "The list shrank to the profile's own symptoms. SetActive(false) on the "
                        "rows the profile did not mention deleted them from the interface, so "
                        "after loading p1 there was no way to reach the other ten symptoms at "
                        "all without restarting.",
            },
        ],
    },
    {
        "key": "G",
        "title": "More than one monitor",
        "note": "Needs two displays. Everything here passes trivially on a single-monitor "
                "machine, which is how the original fault shipped.",
        "tests": [
            {
                "id": "G1",
                "title": "The overlay says which screen it is on",
                "plat": ALL,
                "do": "Start VIP-Sim with two or more displays connected.",
                "expect": "A notice at the top of the screen for a few seconds: 'VIP-Sim is on "
                          "display X of Y. Press F3 to move it to the next screen.'",
                "fail": "It restored the display it had been used on last, silently. The "
                        "simulation appeared on a monitor the user was not looking at, over "
                        "applications they had not meant, and nothing on screen said what had "
                        "happened or how to undo it.",
            },
            {
                "id": "G2",
                "title": "F3 moves it to the next screen",
                "plat": ALL,
                "do": "Press F3.",
                "expect": "The overlay appears on the other display and the notice reappears "
                          "naming it.",
                "fail": "The control existed and was findable by nobody.",
            },
            {
                "id": "G3",
                "title": "The move actually completes",
                "plat": ALL,
                "do": "After F3, check the overlay is full screen on the target display and "
                      "still covers all of it — particularly when the two displays are "
                      "different resolutions.",
                "expect": "Borderless, full screen, correct size.",
                "fail": "The window stayed where it was, or ended up stranded as a small "
                        "window on the wrong monitor. The resolution change had not taken effect "
                        "when the move was requested, so the move applied to the old geometry.",
            },
            {
                "id": "G4",
                "title": "F3 keeps working",
                "plat": ALL,
                "do": "Press F3 six times, going round the loop at least twice.",
                "expect": "It cycles every time.",
                "fail": "A move that never reported completion left a flag latched, after which "
                        "every later F3 was ignored in silence.",
            },
            {
                "id": "G5",
                "title": "The button in the F1 panel does the same",
                "plat": ALL,
                "do": "Press F1 and use 'Move to next display'.",
                "expect": "Same result as F3.",
                "fail": "F3 only reaches VIP-Sim while it holds keyboard focus, and a "
                        "click-through overlay almost never does — so on many machines the "
                        "hotkey alone is not a control at all. Test the button, not just the key.",
            },
        ],
    },
    {
        "key": "H",
        "title": "Gaze and calibration",
        "note": "Mouse-following is the default and should be tested first; the webcam path "
                "needs a camera and, on macOS, a permission grant and a restart.",
        "tests": [
            {
                "id": "H1",
                "title": "It starts on mouse-following",
                "plat": ALL,
                "do": "Start fresh and switch on an effect that follows the gaze.",
                "expect": "The effect follows the mouse pointer immediately.",
                "fail": "It started on eye tracking. On a machine with no webcam, nothing moved "
                        "and the tool looked broken from the first minute.",
            },
            {
                "id": "H2",
                "title": "The gaze follows the pointer while another app has focus",
                "plat": ALL,
                "do": "Click into another application, then move the mouse around the screen.",
                "expect": "The effect keeps following the pointer.",
                "fail": "The gaze point froze the moment the overlay lost focus — which, being "
                        "click-through, is nearly always.",
            },
            {
                "id": "H3",
                "title": "Switching to webcam tracking",
                "plat": ALL,
                "do": "Use the toolbar toggle, then pick a camera.",
                "expect": "The camera list is populated and the picker looks like something you "
                          "can click.",
                "fail": "The picker did not read as a control, so nobody used it.",
            },
            {
                "id": "H4",
                "title": "The eye tracker does not paint on the screen",
                "plat": ALL,
                "do": "Run with webcam tracking on and look at the whole desktop.",
                "expect": "No webcam preview, and exactly one cursor.",
                "fail": "Both, separately: a full-screen webcam preview over the desktop, and a "
                        "second cursor painted next to the real one.",
            },
            {
                "id": "H5",
                "title": "The gaze update rate is sane",
                "plat": ALL,
                "do": "With webcam tracking on, read the gaze rate in the periodic log line.",
                "expect": "Tens of samples per second.",
                "fail": "5 Hz. The webcam was being requested at 1920 px and 60 fps, and the "
                        "tracker could not keep up with the frames it asked for.",
            },
            {
                "id": "H6",
                "title": "Calibration can be entered, followed and left",
                "plat": ALL,
                "do": "Start calibration from the toolbar, follow the dot, then press Escape "
                      "part-way through and start it again with F9.",
                "expect": "Clicks reach the calibration screen, and Escape or a right-click "
                          "aborts it at any point.",
                "fail": "Clicks did not reach the calibration screen, and once started it could "
                        "not be left — on a click-through, always-on-top overlay that is a trap "
                        "with no way out but the quit hotkey.",
            },
            {
                "id": "H7",
                "title": "Camera permission on macOS",
                "plat": [MAC],
                "do": "Switch to webcam tracking on a Mac that has never granted the app camera "
                      "access. Grant it, then quit and start the app again.",
                "expect": "The system prompt appears with a sensible explanation, and tracking "
                          "works after the restart.",
                "fail": "The app must be restarted after granting — without the restart the "
                        "camera stays unavailable and it looks as though the permission did "
                        "nothing.",
            },
        ],
    },
    {
        "key": "I",
        "title": "Getting out",
        "note": "",
        "tests": [
            {
                "id": "I1",
                "title": "All three ways out work",
                "plat": ALL,
                "do": "Quit with the toolbar's exit button. Start again and quit with Ctrl+Alt+Q "
                      "while a different application has focus. Start again and quit with F12.",
                "expect": "The application closes every time, and leaves no window behind.",
                "fail": "There were states with no way out: no title bar, no close button, "
                        "click-through swallowing the attempt, and a calibration screen that "
                        "could not be exited. The hotkey has to work when VIP-Sim is not the "
                        "foreground application, which is the case that matters and the one "
                        "easiest to skip.",
            },
        ],
    },
    {
        "key": "J",
        "title": "Linux",
        "note": "Needs a Wayland compositor with layer-shell — sway, KWin, Hyprland, labwc or "
                "niri. GNOME is expected to refuse; see J2. Everything here was found during "
                "bring-up, so treat the whole column as unproven on hardware other than the "
                "developer's.",
        "tests": [
            {
                "id": "J1",
                "title": "Start it with the script",
                "plat": [LNX],
                "do": "Run ./VIP-Sim.sh, not the VIP-Sim binary.",
                "expect": "One overlay covering the screen.",
                "fail": "Running the binary directly gives an ordinary window and no overlay. "
                        "The overlay is a second program and the script starts the pair in the "
                        "right order.",
            },
            {
                "id": "J2",
                "title": "GNOME refuses clearly",
                "plat": [LNX],
                "do": "Run it on GNOME.",
                "expect": "It says the compositor does not implement layer-shell, and exits.",
                "fail": "Mutter will not implement the protocol, so there is no overlay to be "
                        "had. Half-working, with a plain window sitting in the middle of the "
                        "screen, would be worse than the refusal.",
            },
            {
                "id": "J3",
                "title": "Only the overlay is on the desktop",
                "plat": [LNX],
                "do": "Ask the compositor for its window list (on sway: swaymsg -t get_tree).",
                "expect": "No VIP-Sim toplevel. The simulator runs inside the overlay's own "
                          "compositor and has no window of its own.",
                "fail": "The simulator's window appeared on the desktop next to the overlay, "
                        "decorated with a title bar, so there were two of everything.",
            },
            {
                "id": "J4",
                "title": "Screen capture delivers real frames",
                "plat": [LNX],
                "do": "Accept the compositor's screen-sharing dialog and watch the simulation.",
                "expect": "The captured screen appears and the effects apply to it.",
                "fail": "Three different failures, all at this step: no frames at all; a crash on "
                        "the very first frame; and frames counted as delivered that were "
                        "entirely black.",
            },
            {
                "id": "J5",
                "title": "The image is the right way up",
                "plat": [LNX],
                "do": "Look at any text in the captured screen.",
                "expect": "Readable, and the right way round.",
                "fail": "The overlay was vertically mirrored — the OpenGL texture origin is the "
                        "opposite of the one the rest of the pipeline assumes.",
            },
            {
                "id": "J6",
                "title": "The interface takes input",
                "plat": [LNX],
                "do": "Click toolbar buttons, open a panel, drag a slider.",
                "expect": "It responds as it does on Windows.",
                "fail": "Input went nowhere: the overlay owns the pointer and keyboard, and until "
                        "it forwarded them the simulator inside it received nothing.",
            },
            {
                "id": "J7",
                "title": "Clicks pass through outside the interface",
                "plat": [LNX],
                "do": "Click on an application behind the overlay.",
                "expect": "The click reaches it.",
                "fail": "The region that takes input has to match what is drawn. When it did "
                        "not, either the whole screen swallowed clicks or the toolbar stopped "
                        "taking them.",
            },
        ],
    },
    {
        "key": "K",
        "title": "The archive itself",
        "note": "",
        "tests": [
            {
                "id": "K1",
                "title": "Test the archive, not a build",
                "plat": ALL,
                "do": "Verify the download against SHA256SUMS.txt, extract it somewhere new, and "
                      "run everything above from that copy.",
                "expect": "The checksums match and the extracted copy is what you test.",
                "fail": "A build tree was tested and an archive was shipped. They were not the "
                        "same thing: the macOS archive was missing an entire source file that "
                        "the tested tree had.",
            },
            {
                "id": "K2",
                "title": "The profiles are not inside the download",
                "plat": ALL,
                "do": "Search the extracted folder for p1.json … p7.json.",
                "expect": "They are not there. The profiles are a separate, paid add-on.",
                "fail": "They were committed to the repository once and had to be removed. "
                        "Anything that ships them inside the free download gives them away.",
            },
        ],
    },
]

UNPROVEN = [
    ("D8-D10 on macOS", "macOS has now been run on real hardware and works, which also settles "
     "the capture orientation there. Its effect list is gated differently from Windows though "
     "- HideImpairmentSelection is not in that scene at all - so the three state-machine "
     "checks are the ones worth repeating on a Mac specifically."),
    ("Linux on KWin", "Developed and verified on sway under WSL. KWin implements the same "
     "protocols and should work; it has not been tried."),
    ("Linux on GNOME (J2)", "The refusal path has never been seen on real GNOME."),
]

ALSO_WORTH = [
    "Unplug one of two displays between runs, so the remembered display no longer exists.",
    "The first-run tutorial: it should appear once, and 'Show tutorial' in the F1 panel should "
    "bring it back.",
    "'Copy diagnostics path' and 'Report a problem' in the F1 panel.",
    "Minimise and restore.",
    "The condition presets in the effect list.",
    "Leave it running for an hour with several effects on and watch the frame rate in the log.",
]

PLAT_LABEL = {WIN: "Windows", MAC: "macOS", LNX: "Linux"}


def plat_text(plat):
    if set(plat) == set(ALL):
        return "All platforms"
    return " · ".join(PLAT_LABEL[p] for p in plat)


# ---------------------------------------------------------------- markdown

def render_markdown():
    o = io.StringIO()
    w = o.write
    total = sum(len(g["tests"]) for g in GROUPS)

    w("# VIP-Sim regression checklist\n\n")
    w("Every test below is a fault that has actually happened at least once, in this project, "
      "on a real machine. None of them is hypothetical, and every one of them shipped or nearly "
      "shipped. There are %d of them.\n\n" % total)
    w("Work through them on the packaged download rather than on a build made locally: two of "
      "the faults here only existed in the archive.\n\n")

    w("## Before you start\n\n")
    w("**The log is the instrument.** Every five seconds VIP-Sim writes a report to its player "
      "log, in every build — you do not need developer mode for it.\n\n")
    w("- Windows: `%USERPROFILE%\\AppData\\LocalLow\\Zefwih\\VIP-Sim\\Player.log`\n")
    w("- macOS: `~/Library/Logs/Zefwih/VIP-Sim/Player.log`\n")
    w("- Linux: `~/.config/unity3d/Zefwih/VIP-Sim/Player.log`\n\n")
    w("The F1 panel has **Copy diagnostics path**, which puts the folder on the clipboard.\n\n")
    w("Three lines matter:\n\n")
    w("```\n")
    w("[VipSimDiagnostics] CAPTURE 'Title' rect=(x,y,WxH) ... mode=WindowsGraphicsCapture\n")
    w("[VipSimDiagnostics] ALPHA 1920x1080 mean=... transparent=76.7% ... | enabled(2): "
      "myDistortionMap(L),myFieldLoss(L)\n")
    w("[ConditionProfile] p1: applied 26 of 51 parameters\n")
    w("```\n\n")
    w("**Developer hotkeys** (F6 screenshot, F7 reveal menus, F8 alpha probe, F10 overlay, F11 "
      "benchmark) need `-vipsim-dev` on the command line. F1, F3, F9 and F12 are always live.\n\n")

    w("## Telling the lookalike faults apart\n\n")
    w("Four different faults produce the same sentence — *the effects do not work*. This is how "
      "to separate them before writing the report:\n\n")
    w("1. **No `CAPTURE` line in the log.** No window has been selected. Nothing is wrong.\n")
    w("2. **`enabled(0)` in the `ALPHA` line.** No effect is switched on — the fault is in the "
      "interface, not the simulation. Compare it with the `ROWS` line: if the list says a "
      "symptom is on and `enabled` does not name it, the interface and the simulation "
      "disagree, and that disagreement is itself the bug.\n")
    w("3. **Effects enabled, and `opaque` around 20–25%.** The overlay is drawing only its own "
      "panel: the captured image is empty. This is a capture fault — read `mode=` on the "
      "`CAPTURE` line and see C1.\n")
    w("4. **The whole screen washed grey or black.** An alpha fault in an effect shader; see "
      "D3.\n\n")

    w("## How to report a failure\n\n")
    w("Give the test number, what you did, what you saw, and attach `Player.log`. For anything "
      "visual, a photograph of the screen is worth more than a description — a screenshot taken "
      "by the machine itself does not always contain the overlay.\n\n")

    for g in GROUPS:
        w("## %s — %s\n\n" % (g["key"], g["title"]))
        if g["note"]:
            w("%s\n\n" % g["note"])
        for t in g["tests"]:
            w("### %s. %s\n\n" % (t["id"], t["title"]))
            w("*%s*\n\n" % plat_text(t["plat"]))
            w("- **Do:** %s\n" % t["do"])
            w("- **Expect:** %s\n" % t["expect"])
            w("- **Has failed as:** %s\n\n" % t["fail"])

    w("## Never yet proven on hardware\n\n")
    w("Start here if your time is limited. These are the parts of the list nobody has been able "
      "to check:\n\n")
    for name, why in UNPROVEN:
        w("- **%s.** %s\n" % (name, why))
    w("\n")

    w("## Also worth trying\n\n")
    w("These have never failed, so they are not on the list proper — but they are untested "
      "rather than proven:\n\n")
    for item in ALSO_WORTH:
        w("- %s\n" % item)
    w("\n")

    w("## Known limits — not failures\n\n")
    w("- **Linux, wlroots compositors** (sway and relatives) can only share a whole output, so "
      "the simulation is captured back into itself. With two monitors, share the one VIP-Sim is "
      "not overlaying.\n")
    w("- **Linux rendering is on the CPU.** Slower than Windows and macOS, noticeably so on a "
      "large screen with several effects.\n")
    w("- **Linux gaze is webcam-only.** Wayland does not let an application read the global "
      "pointer position, so mouse-following cannot work there.\n")
    w("- **macOS and Windows builds are unsigned.** Both operating systems will warn on first "
      "launch.\n")

    return o.getvalue()


# ---------------------------------------------------------------- html

def esc(s):
    return html.escape(s, quote=False)


def render_html():
    total = sum(len(g["tests"]) for g in GROUPS)
    o = io.StringIO()
    w = o.write

    w('<title>VIP-Sim Regression Checklist</title>\n')
    w('<link rel="preconnect" href="https://fonts.googleapis.com">\n')
    w('<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>\n')
    w('<link rel="stylesheet" href="https://fonts.googleapis.com/css2?'
      'family=Archivo:wght@500;600;700&family=IBM+Plex+Mono:wght@400;500&'
      'family=Source+Sans+3:ital,wght@0,400;0,600;1,400&display=swap">\n')
    w("<style>\n" + CSS + "</style>\n")

    # ---- sticky bar
    w('<header class="bar">\n')
    w('  <div class="bar-in">\n')
    w('    <span class="wordmark">VIP-Sim <span>QA</span></span>\n')
    w('    <div class="filters" role="group" aria-label="Filter by platform">\n')
    for key, label in [("all", "All"), (WIN, "Windows"), (MAC, "macOS"), (LNX, "Linux")]:
        cls = " on" if key == "all" else ""
        w('      <button type="button" class="chip%s" data-filter="%s">%s</button>\n'
          % (cls, key, label))
    w('    </div>\n')
    w('    <label class="hide-done"><input type="checkbox" id="hidedone"> hide checked</label>\n')
    w('    <span class="count"><b id="done">0</b> / <span id="shown">%d</span></span>\n' % total)
    w('  </div>\n')
    w('  <div class="progress"><i id="pbar"></i></div>\n')
    w('</header>\n')

    # ---- hero
    w('<main>\n')
    w('<section class="hero">\n')
    w('  <p class="eyebrow">Regression checklist · version 2.0.0</p>\n')
    w('  <h1>Everything that has broken here at least once</h1>\n')
    w('  <p class="lede">%d checks. None of them is hypothetical: every one is a fault that '
      'actually happened on a real machine, and most of them shipped. Work through them on the '
      'packaged download, not on a build made locally — two of these existed only in the '
      'archive.</p>\n' % total)
    w('</section>\n')

    # ---- before you start
    w('<section class="panel">\n')
    w('  <h2>Before you start</h2>\n')
    w('  <p><b>The log is the instrument.</b> Every five seconds VIP-Sim writes a report to its '
      'player log, in every build — developer mode is not needed for it.</p>\n')
    w('  <ul class="paths">\n')
    w('    <li><span>Windows</span><code>%USERPROFILE%\\AppData\\LocalLow\\Zefwih\\VIP-Sim\\Player.log</code></li>\n')
    w('    <li><span>macOS</span><code>~/Library/Logs/Zefwih/VIP-Sim/Player.log</code></li>\n')
    w('    <li><span>Linux</span><code>~/.config/unity3d/Zefwih/VIP-Sim/Player.log</code></li>\n')
    w('  </ul>\n')
    w('  <p>The F1 panel has <b>Copy diagnostics path</b>, which puts that folder on the '
      'clipboard. Three lines in the log matter:</p>\n')
    w('  <pre><code>[VipSimDiagnostics] CAPTURE \'Title\' rect=(x,y,WxH) … <b>mode=WindowsGraphicsCapture</b>\n'
      '[VipSimDiagnostics] ALPHA 1920x1080 mean=… <b>transparent=76.7%</b> … | <b>enabled(2)</b>: myDistortionMap(L),myFieldLoss(L)\n'
      '[ConditionProfile] p1: <b>applied 26 of 51 parameters</b></code></pre>\n')
    w('  <p class="fineprint">Developer hotkeys — F6 screenshot, F7 reveal menus, F8 alpha '
      'probe, F10 overlay, F11 benchmark — need <code>-vipsim-dev</code> on the command line. '
      'F1, F3, F9 and F12 are always live.</p>\n')
    w('</section>\n')

    # ---- differential
    w('<section class="panel diff">\n')
    w('  <h2>Telling the lookalike faults apart</h2>\n')
    w('  <p>Four different faults produce the same sentence — <i>the effects do not work</i>. '
      'Separate them before writing the report.</p>\n')
    w('  <ol class="dx">\n')
    w('    <li><b>No <code>CAPTURE</code> line at all.</b> No window has been selected. Nothing '
      'is wrong.</li>\n')
    w('    <li><b><code>enabled(0)</code> in the <code>ALPHA</code> line.</b> No effect is '
      'switched on — the fault is in the interface, not the simulation. Compare it with the '
      '<code>ROWS</code> line: if the list says a symptom is on and <code>enabled</code> does '
      'not name it, the interface and the simulation disagree, and that disagreement is '
      'itself the bug.</li>\n')
    w('    <li><b>Effects enabled, <code>opaque</code> around 20–25%.</b> The overlay is drawing '
      'only its own panel, so the captured image is empty. A capture fault: read <code>mode=</code> '
      'and go to C1.</li>\n')
    w('    <li><b>The whole screen washed grey or black.</b> An alpha fault in an effect shader. '
      'Go to D3.</li>\n')
    w('  </ol>\n')
    w('</section>\n')

    # ---- groups
    for g in GROUPS:
        w('<section class="group" id="g%s">\n' % g["key"])
        w('  <div class="ghead">\n')
        w('    <span class="gkey">%s</span>\n' % g["key"])
        w('    <h2>%s</h2>\n' % esc(g["title"]))
        w('  </div>\n')
        if g["note"]:
            w('  <p class="gnote">%s</p>\n' % esc(g["note"]))
        w('  <div class="tests">\n')
        for t in g["tests"]:
            w('    <article class="test" data-plat="%s">\n' % " ".join(t["plat"]))
            w('      <input type="checkbox" class="tick" id="%s" aria-label="%s done">\n'
              % (t["id"], t["id"]))
            w('      <label class="box" for="%s"></label>\n' % t["id"])
            w('      <div class="tbody">\n')
            w('        <div class="thead"><span class="tid">%s</span>'
              '<h3>%s</h3><span class="plat">%s</span></div>\n'
              % (t["id"], esc(t["title"]), esc(plat_text(t["plat"]))))
            w('        <p class="do"><span>Do</span>%s</p>\n' % esc(t["do"]))
            w('        <p class="exp"><span>Expect</span>%s</p>\n' % esc(t["expect"]))
            w('        <p class="seen"><span>Has failed as</span>%s</p>\n' % esc(t["fail"]))
            w('      </div>\n')
            w('    </article>\n')
        w('  </div>\n')
        w('</section>\n')

    # ---- unproven
    w('<section class="panel warn">\n')
    w('  <h2>Never yet proven on hardware</h2>\n')
    w('  <p>Start here if your time is limited. These are the parts of the list nobody has been '
      'able to check.</p>\n')
    w('  <dl class="unproven">\n')
    for name, why in UNPROVEN:
        w('    <dt>%s</dt><dd>%s</dd>\n' % (esc(name), esc(why)))
    w('  </dl>\n')
    w('</section>\n')

    # ---- also worth / known limits
    w('<section class="two">\n')
    w('  <div class="panel">\n')
    w('    <h2>Also worth trying</h2>\n')
    w('    <p class="fineprint">Never failed, so not on the list proper — but untested rather '
      'than proven.</p>\n    <ul class="plain">\n')
    for item in ALSO_WORTH:
        w('      <li>%s</li>\n' % esc(item))
    w('    </ul>\n  </div>\n')
    w('  <div class="panel">\n')
    w('    <h2>Known limits — not failures</h2>\n    <ul class="plain">\n')
    w('      <li><b>Linux, wlroots compositors</b> (sway and relatives) can only share a whole '
      'output, so the simulation is captured back into itself. With two monitors, share the one '
      'VIP-Sim is not overlaying.</li>\n')
    w('      <li><b>Linux renders on the CPU.</b> Noticeably slower than Windows and macOS on a '
      'large screen with several effects.</li>\n')
    w('      <li><b>Linux gaze is webcam-only.</b> Wayland does not let an application read the '
      'global pointer position, so mouse-following cannot work there.</li>\n')
    w('      <li><b>Both builds are unsigned.</b> Windows and macOS will each warn on first '
      'launch.</li>\n')
    w('    </ul>\n  </div>\n')
    w('</section>\n')

    w('<footer><p>Report a failure with its number, what you did, what you saw, and '
      '<code>Player.log</code> attached. For anything visual, a photograph of the screen beats a '
      'description — a screenshot taken by the machine does not always contain the '
      'overlay.</p></footer>\n')
    w('</main>\n')
    w("<script>\n" + JS + "</script>\n")
    return o.getvalue()


CSS = r"""
:root{
  --ground:#E9EBED;
  --surface:#FFFFFF;
  --surface-2:#F4F6F7;
  --ink:#14181B;
  --ink-2:#48535C;
  --ink-3:#7A858E;
  --rule:#D5D9DD;
  --rule-2:#E4E7EA;
  --accent:#1C5C6B;
  --accent-2:#2E8497;
  --accent-soft:#DBE9EC;
  --fail:#9E3524;
  --fail-soft:#F7E7E3;
  --fail-rule:#C8695A;
  --pass:#2A6B50;
  --warn-soft:#F6EEDC;
  --warn-rule:#B08533;
  --shadow:0 1px 2px rgba(20,24,27,.06), 0 8px 24px -16px rgba(20,24,27,.30);
}
@media (prefers-color-scheme: dark){
  :root:not([data-theme="light"]){
    --ground:#0E1113;
    --surface:#171B1E;
    --surface-2:#1D2226;
    --ink:#E7EBEE;
    --ink-2:#A6B0B8;
    --ink-3:#79848C;
    --rule:#2A3137;
    --rule-2:#232A2F;
    --accent:#6FBECD;
    --accent-2:#8ED2DF;
    --accent-soft:#14313A;
    --fail:#E39284;
    --fail-soft:#33201B;
    --fail-rule:#8C4638;
    --pass:#7DC3A0;
    --warn-soft:#2E2718;
    --warn-rule:#8A6B2A;
    --shadow:0 1px 2px rgba(0,0,0,.4), 0 8px 24px -16px rgba(0,0,0,.7);
  }
}
:root[data-theme="dark"]{
  --ground:#0E1113;
  --surface:#171B1E;
  --surface-2:#1D2226;
  --ink:#E7EBEE;
  --ink-2:#A6B0B8;
  --ink-3:#79848C;
  --rule:#2A3137;
  --rule-2:#232A2F;
  --accent:#6FBECD;
  --accent-2:#8ED2DF;
  --accent-soft:#14313A;
  --fail:#E39284;
  --fail-soft:#33201B;
  --fail-rule:#8C4638;
  --pass:#7DC3A0;
  --warn-soft:#2E2718;
  --warn-rule:#8A6B2A;
  --shadow:0 1px 2px rgba(0,0,0,.4), 0 8px 24px -16px rgba(0,0,0,.7);
}

*{box-sizing:border-box}
html{-webkit-text-size-adjust:100%}
body{
  margin:0;
  background:var(--ground);
  color:var(--ink);
  font-family:"Source Sans 3","Segoe UI",system-ui,sans-serif;
  font-size:16.5px;
  line-height:1.62;
}
main{max-width:56rem;margin:0 auto;padding:0 1.25rem 5rem}
h1,h2,h3{font-family:Archivo,"Segoe UI",system-ui,sans-serif;text-wrap:balance;margin:0}
code,pre,.tid,.gkey,.count,.plat{font-family:"IBM Plex Mono",ui-monospace,Menlo,Consolas,monospace}

/* ---------- sticky bar ---------- */
.bar{
  position:sticky;top:0;z-index:20;
  background:var(--surface);
  background:color-mix(in srgb, var(--surface) 88%, transparent);
  backdrop-filter:blur(10px);
  border-bottom:1px solid var(--rule);
}
.bar-in{
  max-width:56rem;margin:0 auto;padding:.55rem 1.25rem;
  display:flex;align-items:center;gap:.85rem;flex-wrap:wrap;
}
.wordmark{
  font-family:Archivo,sans-serif;font-weight:700;font-size:.95rem;
  letter-spacing:.02em;color:var(--ink);
}
.wordmark span{color:var(--accent);font-weight:600}
.filters{display:flex;gap:.3rem;margin-left:auto}
.chip{
  font:500 .78rem/1 "IBM Plex Mono",monospace;
  padding:.42rem .6rem;border-radius:3px;cursor:pointer;
  background:transparent;color:var(--ink-2);
  border:1px solid var(--rule);
}
.chip:hover{color:var(--ink);border-color:var(--ink-3)}
.chip.on{background:var(--accent-soft);border-color:var(--accent);color:var(--accent)}
.hide-done{
  display:flex;align-items:center;gap:.35rem;
  font-size:.82rem;color:var(--ink-2);cursor:pointer;white-space:nowrap;
}
.hide-done input{accent-color:var(--accent)}
.count{font-size:.82rem;color:var(--ink-3);white-space:nowrap;font-variant-numeric:tabular-nums}
.count b{color:var(--ink);font-weight:500}
.progress{height:2px;background:var(--rule-2)}
.progress i{display:block;height:100%;width:0;background:var(--accent);transition:width .18s ease}
:focus-visible{outline:2px solid var(--accent-2);outline-offset:2px;border-radius:2px}

/* ---------- hero ---------- */
.hero{padding:3.5rem 0 2rem;max-width:38rem}
.eyebrow{
  margin:0 0 1rem;font-family:"IBM Plex Mono",monospace;font-size:.74rem;
  letter-spacing:.14em;text-transform:uppercase;color:var(--accent);
}
.hero h1{font-size:clamp(2rem,5.2vw,2.9rem);font-weight:700;line-height:1.08;letter-spacing:-.018em}
.lede{margin:1.15rem 0 0;font-size:1.08rem;color:var(--ink-2)}

/* ---------- panels ---------- */
.panel{
  background:var(--surface);border:1px solid var(--rule);border-radius:5px;
  padding:1.5rem 1.6rem;margin:1.25rem 0;box-shadow:var(--shadow);
}
.panel h2{font-size:1.02rem;font-weight:600;letter-spacing:.005em;margin-bottom:.7rem}
.panel p{margin:.7rem 0}
.panel > p:first-of-type{margin-top:0}
.fineprint{font-size:.9rem;color:var(--ink-3)}
.paths{list-style:none;margin:.9rem 0;padding:0;display:grid;gap:.4rem}
.paths li{display:flex;gap:.7rem;align-items:baseline;flex-wrap:wrap}
.paths span{
  font:500 .72rem/1.4 "IBM Plex Mono",monospace;text-transform:uppercase;letter-spacing:.07em;
  color:var(--ink-3);min-width:4.6rem;
}
code{font-size:.87em;background:var(--surface-2);border:1px solid var(--rule-2);
  border-radius:3px;padding:.06em .34em;color:var(--ink-2)}
pre{
  margin:.9rem 0;padding:.9rem 1rem;background:var(--surface-2);
  border:1px solid var(--rule-2);border-left:2px solid var(--accent);border-radius:3px;
  overflow-x:auto;font-size:.8rem;line-height:1.7;
}
pre code{background:none;border:0;padding:0;color:var(--ink-2)}
pre b{color:var(--accent);font-weight:500}
.dx{margin:.8rem 0 0;padding-left:1.3rem;display:grid;gap:.55rem}
.dx li{padding-left:.2rem}
.dx li::marker{font-family:"IBM Plex Mono",monospace;font-size:.85em;color:var(--accent)}
.panel.warn{background:var(--warn-soft);border-color:var(--warn-rule)}
.panel.warn code{background:transparent}
.unproven{margin:1rem 0 0;display:grid;gap:.75rem}
.unproven dt{font-weight:600;font-size:.95rem}
.unproven dd{margin:.1rem 0 0;color:var(--ink-2);font-size:.95rem}
.plain{margin:.6rem 0 0;padding-left:1.15rem;display:grid;gap:.45rem;font-size:.95rem;color:var(--ink-2)}
.two{display:grid;gap:1.25rem;grid-template-columns:1fr 1fr;align-items:start;margin-top:2.5rem}
.two .panel{margin:0;height:100%}
@media (max-width:720px){.two{grid-template-columns:1fr}}

/* ---------- groups ---------- */
.group{margin-top:3rem;scroll-margin-top:5rem}
.group.empty{display:none}
.ghead{display:flex;align-items:baseline;gap:.75rem;padding-bottom:.5rem;border-bottom:1px solid var(--rule)}
.gkey{
  font-size:.78rem;font-weight:500;letter-spacing:.1em;color:var(--surface);
  background:var(--accent);border-radius:3px;padding:.16rem .42rem;
}
.ghead h2{font-size:1.28rem;font-weight:600;letter-spacing:-.01em}
.gnote{margin:.85rem 0 0;color:var(--ink-2);font-size:.95rem;max-width:52rem}
.tests{display:grid;gap:.65rem;margin-top:1.1rem}

/* ---------- one test ---------- */
.test{
  position:relative;display:grid;grid-template-columns:auto 1fr;gap:.9rem;
  background:var(--surface);border:1px solid var(--rule);border-radius:5px;
  padding:1.05rem 1.25rem;box-shadow:var(--shadow);
  transition:opacity .15s ease, border-color .15s ease;
}
.test.hidden{display:none}
.tick{position:absolute;opacity:0;width:1px;height:1px;pointer-events:none}
.box{
  width:1.15rem;height:1.15rem;margin-top:.28rem;border:1.5px solid var(--ink-3);
  border-radius:3px;cursor:pointer;flex:none;position:relative;
  transition:background .12s ease,border-color .12s ease;
}
.box:hover{border-color:var(--accent)}
.tick:focus-visible + .box{outline:2px solid var(--accent-2);outline-offset:2px}
.tick:checked + .box{background:var(--accent);border-color:var(--accent)}
.tick:checked + .box::after{
  content:"";position:absolute;left:.32rem;top:.11rem;width:.28rem;height:.58rem;
  border:solid var(--surface);border-width:0 2px 2px 0;transform:rotate(42deg);
}
.tick:checked ~ .tbody{opacity:.42}
.tick:checked ~ .tbody h3{text-decoration:line-through;text-decoration-color:var(--ink-3)}
.thead{display:flex;align-items:baseline;gap:.55rem;flex-wrap:wrap;margin-bottom:.55rem}
.tid{font-size:.78rem;font-weight:500;color:var(--accent);letter-spacing:.02em}
.thead h3{font-size:1.02rem;font-weight:600;letter-spacing:-.005em;flex:1 1 14rem}
.plat{
  font-size:.68rem;letter-spacing:.06em;text-transform:uppercase;color:var(--ink-3);
  border:1px solid var(--rule);border-radius:2px;padding:.1rem .35rem;white-space:nowrap;
}
.tbody p{
  margin:0 0 .38rem;padding-left:5.4rem;position:relative;
  font-size:.95rem;color:var(--ink-2);
}
.tbody p span{
  position:absolute;left:0;top:.16rem;width:4.7rem;
  font:500 .68rem/1.5 "IBM Plex Mono",monospace;text-transform:uppercase;letter-spacing:.06em;
  color:var(--ink-3);
}
.tbody .exp{color:var(--ink)}
.tbody .exp span{color:var(--pass)}
.tbody .seen{
  margin-top:.6rem;margin-bottom:0;padding:.6rem .8rem .6rem 5.4rem;
  background:var(--fail-soft);border-left:2px solid var(--fail-rule);border-radius:0 3px 3px 0;
  color:var(--ink-2);
}
.tbody .seen span{left:.8rem;top:.75rem;color:var(--fail)}
@media (max-width:640px){
  .tbody p{padding-left:0}
  .tbody p span{position:static;display:block;width:auto;margin-bottom:.1rem}
  .tbody .seen{padding:.6rem .8rem}
  .tbody .seen span{position:static}
}

footer{
  margin-top:3rem;padding-top:1.5rem;border-top:1px solid var(--rule);
  font-size:.9rem;color:var(--ink-3);max-width:44rem;
}

@media print{
  .bar,.progress{display:none}
  body{background:#fff;font-size:10.5pt}
  .test,.panel{box-shadow:none;break-inside:avoid}
  .test.hidden{display:grid}
  .group.empty{display:block}
}
@media (prefers-reduced-motion: reduce){
  *{transition:none !important}
}
"""

JS = r"""
(function(){
  var KEY = "vipsim-qa-v1";
  var state = {};
  try { state = JSON.parse(localStorage.getItem(KEY) || "{}") || {}; } catch (e) { state = {}; }

  var ticks = Array.prototype.slice.call(document.querySelectorAll(".tick"));
  var tests = Array.prototype.slice.call(document.querySelectorAll(".test"));
  var doneEl = document.getElementById("done");
  var shownEl = document.getElementById("shown");
  var bar = document.getElementById("pbar");
  var hideDone = document.getElementById("hidedone");
  var filter = "all";

  ticks.forEach(function(t){ if (state[t.id]) t.checked = true; });

  function save(){
    try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (e) {}
  }

  function apply(){
    var shown = 0, done = 0;
    tests.forEach(function(el){
      var tick = el.querySelector(".tick");
      var platOk = filter === "all" || el.dataset.plat.split(" ").indexOf(filter) !== -1;
      var visible = platOk && !(hideDone.checked && tick.checked);
      el.classList.toggle("hidden", !visible);
      if (platOk) { shown++; if (tick.checked) done++; }
    });
    document.querySelectorAll(".group").forEach(function(g){
      var any = g.querySelector(".test:not(.hidden)");
      g.classList.toggle("empty", !any);
    });
    doneEl.textContent = done;
    shownEl.textContent = shown;
    bar.style.width = shown ? (done / shown * 100) + "%" : "0%";
  }

  ticks.forEach(function(t){
    t.addEventListener("change", function(){
      state[t.id] = t.checked;
      if (!t.checked) delete state[t.id];
      save();
      apply();
    });
  });

  document.querySelectorAll(".chip").forEach(function(c){
    c.addEventListener("click", function(){
      document.querySelectorAll(".chip").forEach(function(o){ o.classList.remove("on"); });
      c.classList.add("on");
      filter = c.dataset.filter;
      apply();
    });
  });

  hideDone.addEventListener("change", apply);
  apply();
})();
"""


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    md = render_markdown()
    with io.open(os.path.join(root, "docs", "TESTING.md"), "w",
                 encoding="utf-8", newline="\n") as f:
        f.write(md)
    print("docs/TESTING.md: %d bytes, %d tests"
          % (len(md), sum(len(g["tests"]) for g in GROUPS)))

    # The shareable HTML page is not a repository file -- it is published as an artifact
    # and would only rot here. Set VIPSIM_HTML_OUT to regenerate it.
    out = os.environ.get("VIPSIM_HTML_OUT")
    if out:
        with io.open(out, "w", encoding="utf-8", newline="\n") as f:
            f.write(render_html())
        print("%s written" % out)


if __name__ == "__main__":
    main()

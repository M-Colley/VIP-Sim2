# VIP-Sim on Linux — read me first

VIP-Sim overlays a simulation of vision impairments on your screen while you keep using
your computer normally.

## Running it

```bash
tar xzf VIP-Sim-Linux-x64.tar.gz
cd VIP-Sim
./VIP-Sim.sh
```

Run `VIP-Sim.sh`, not the `VIP-Sim` binary. The overlay is a second program — the reason is
below — and the script starts the pair in the right order.

## What it needs

**A Wayland compositor that implements `zwlr_layer_shell_v1`.** Sway, KWin (Plasma),
Hyprland, labwc and niri do. **GNOME does not**, and there is no way around it: Mutter has
declined to implement the protocol, so VIP-Sim will tell you that and exit rather than
pretend. On GNOME the Windows and macOS builds are the ones to use.

**A desktop portal for screen capture** — `xdg-desktop-portal` plus the backend for your
compositor, usually `xdg-desktop-portal-wlr` or `xdg-desktop-portal-kde`. Your distribution
almost certainly installs these already. When you start the simulation your compositor asks
which screen to share; that dialogue is the compositor's, not ours, and it is the only way
Wayland lets an application see the screen.

Without a portal, everything except capture still works and VIP-Sim says which piece is
missing.

## Why there are two programs

On Wayland a window's role is fixed when it is created, and an always-on-top click-through
overlay is a different role from an ordinary window. Unity can only make the ordinary kind.
So `vipsim-presenter` owns the overlay and runs the simulator inside a small compositor of
its own: the simulator's window never appears on your desktop, which is why you see one
overlay and not a window with an overlay on top of it.

## Known limits on Linux

- **Whole-screen capture on wlroots compositors.** Sway and its relatives can only share an
  entire output, so the simulation is captured back into itself. If you have two monitors,
  share the one VIP-Sim is not overlaying. GNOME's and KDE's portals can share a single
  window, which avoids this entirely.
- **Software rendering.** The simulator currently receives frames through shared memory,
  which means Mesa renders it on the CPU. On a large screen with several effects enabled
  this is noticeably slower than the Windows and macOS builds.
- **Eye tracking uses the webcam.** Wayland does not let an application read the global
  pointer position, so mouse-following cannot work here.

## Getting out

**Ctrl+Alt+Q** quits from anywhere. The overlay has no title bar and no close button, so
this is the reliable way out.

# Installing VIP-Sim

VIP-Sim overlays simulated vision-impairment symptoms on top of whatever is on
your screen. That means it needs two privileges most applications never ask for:
**screen capture** and, if you want gaze-contingent symptoms, **camera access**.
Most installation problems are one of those two permissions, so they are covered
first for each platform.

---

## Windows 10 (2004+) / Windows 11

1. Download `VIP-Sim-Windows.zip` from the [Releases page](https://github.com/M-Colley/VIP-Sim2/releases).
2. Unblock the archive **before** extracting — right-click → Properties → tick
   **Unblock** → OK. Windows marks downloaded archives as coming from the
   internet, and that flag is inherited by every extracted file, which makes the
   native capture plugin fail to load with no visible error.
3. Extract anywhere and run `VIP-Sim.exe`.

**Camera:** Settings → Privacy & security → Camera → allow desktop apps.

**No SmartScreen bypass needed** if you use a release build; if SmartScreen does
appear, "More info" → "Run anyway".

### If the overlay is black or empty
The capture backend is Windows.Graphics.Capture, which requires Windows 10
version 2004 or later. Check with `winver`. On older builds the app falls back to
PrintWindow/BitBlt, which cannot capture GPU-composited windows (most browsers,
Figma desktop) — update Windows.

---

## macOS 12.3 or later (Intel and Apple Silicon)

macOS is the platform where the permission step is mandatory and non-obvious.

1. Download `VIP-Sim-macOS.zip` and drag `VIP-Sim.app` to `/Applications`.
2. The build is not notarised, so Gatekeeper will refuse it on first launch.
   Right-click the app → **Open** → **Open**. Do this once. (Do not use
   `xattr -dr com.apple.quarantine` unless you understand what it disables.)
3. **Grant Screen Recording.** System Settings → Privacy & Security →
   **Screen & System Audio Recording** → enable **VIP-Sim**.
4. **Quit and reopen VIP-Sim.** macOS only applies a newly granted screen
   recording permission to a fresh process. Without the restart the app keeps
   receiving black frames.
5. **Grant Camera** if you want gaze tracking: System Settings → Privacy &
   Security → Camera → enable VIP-Sim.

### If the overlay stays black after granting permission
That is almost always the missing restart in step 4. If it persists, remove
VIP-Sim from the Screen Recording list with the `−` button, re-add it, and
restart the app — macOS caches a stale permission record per code-signing
identity, and unsigned rebuilds can collide with it.

**Minimum version:** macOS 12.3. The capture backend is ScreenCaptureKit, which
does not exist before 12.3.

---

## Linux (experimental)

Linux is a best-effort target: it builds and the simulation shaders run, but the
transparent click-through overlay behaves differently between compositors and is
not something we can promise.

- **X11** — works closest to the Windows/macOS behaviour.
- **Wayland** — capture goes through `xdg-desktop-portal` and shows a system
  picker dialog every session; click-through overlays depend on compositor
  support and may not work at all on GNOME.

Requirements: `xdg-desktop-portal` plus a backend
(`xdg-desktop-portal-gnome`, `-kde`, or `-wlr`), and PipeWire.

```bash
tar -xzf VIP-Sim-Linux.tar.gz
cd VIP-Sim-Linux
chmod +x VIP-Sim
./VIP-Sim
```

If you need a guaranteed cross-platform experience on Linux, prefer the native
(non-Unity) VIP-Sim build rather than the Unity one.

---

## Verifying the install

1. Launch VIP-Sim. You should see a control panel and a transparent overlay.
2. Pick a window to capture from the window list.
3. Enable **Contrast Sensitivity** and drag the severity slider — the captured
   content should change immediately. This exercises capture and the shader chain
   without needing the camera.
4. For gaze: set the gaze source to **UnitEye**, then run a calibration.

> **Calibration note.** UnitEye 1.1 changed its feature vectors (EyeMU went from
> 19 to 36 features). Calibration files saved with an older VIP-Sim are not
> compatible and will silently fall back to raw, uncalibrated gaze. **Recalibrate
> after upgrading.**

---

## Building from source

See [DEVELOPMENT.md](DEVELOPMENT.md).

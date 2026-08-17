/*
    ------------------- Code Monkey -------------------

    Thank you for downloading this package
    I hope you find it useful in your projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Makes the VIP-Sim overlay click-through everywhere except its own panel.
///
/// Windows: the native window handle is found by walking this process's own
/// top-level windows instead of asking for the active one. GetActiveWindow()
/// returns the active window *of the calling thread's message queue*, so it
/// hands back NULL whenever VIP-Sim is not focused at the instant it is called
/// -- and a borderless overlay launched from Explorer, a terminal or a build
/// script very often is not. When that happened no WS_EX_TRANSPARENT was ever
/// applied and the overlay silently swallowed every click meant for the window
/// underneath, so scrolling and typing stopped working with no visible cause.
/// Being a race, it looked intermittent: consecutive runs of an identical build
/// show one acquiring the handle and the next failing.
///
/// macOS: this class used to be a byte-for-byte copy of the Windows one, calling
/// user32.dll and Dwmapi.dll. Those raise DllNotFoundException on a Mac, which
/// aborted Start() and left hWnd null forever -- so the macOS overlay has never
/// been click-through at all. The Cocoa equivalent of WS_EX_TRANSPARENT is
/// -[NSWindow setIgnoresMouseEvents:], reached here through the Objective-C
/// runtime so it needs no native plugin to build.
/// </summary>
public class TransparentWindow : MonoBehaviour {

#if UNITY_STANDALONE_WIN
    [DllImport("user32.dll")]
    public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    private struct RECT { public int left, top, right, bottom; }

    private struct POINT { public int X, Y; }

    private struct MARGINS {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    private const int GWL_EXSTYLE = -20;

    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;

    private const uint GW_OWNER = 4;

    private IntPtr hWnd;

    // Kept alive for the duration of EnumWindows so the callback is not collected.
    private static EnumWindowsProc _enumCallback;
#endif

#if UNITY_STANDALONE_OSX
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjC, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjC, EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ptr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(IntPtr receiver, IntPtr selector, bool arg);

    // Cursor position comes from CoreGraphics C functions rather than
    // [NSEvent mouseLocation]: objc_msgSend with a struct return needs a
    // different trampoline (objc_msgSend_stret) on x86_64 but not on arm64,
    // whereas P/Invoke marshals plain C struct returns correctly on both.
    private const string CoreGraphics =
        "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    [DllImport(CoreGraphics)] private static extern IntPtr CGEventCreate(IntPtr source);
    [DllImport(CoreGraphics)] private static extern CGPoint CGEventGetLocation(IntPtr theEvent);
    [DllImport(CoreGraphics)] private static extern uint CGMainDisplayID();
    [DllImport(CoreGraphics)] private static extern CGRect CGDisplayBounds(uint display);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [StructLayout(LayoutKind.Sequential)] private struct CGPoint { public double x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct CGSize { public double width, height; }
    [StructLayout(LayoutKind.Sequential)] private struct CGRect { public CGPoint origin; public CGSize size; }

    private IntPtr nsWindow;
#endif

    public Camera maincam;
    public RectTransform canvasRectTransform;
    public RectTransform panelRectTransform;

    private bool feedbackState = false;

    /// <summary>
    /// Diagnostics only. Null while no native window has been acquired -- which
    /// is precisely the state in which the overlay captures every click meant for
    /// the application underneath. Surfaced in the F10 overlay because this
    /// failure is otherwise completely invisible from inside the app.
    /// </summary>
    public static bool? ClickthroughActive { get; private set; }

    private bool _lastClickthrough;
    private bool _appliedOnce;
    private int _acquireAttempts;
    private bool _missingPanelLogged;

    [Tooltip("Cover the whole screen at startup. VIP-Sim is a fullscreen overlay, so a " +
             "windowed or degenerate window size is always a fault -- and Unity persists " +
             "window geometry between runs, so once a bad size is saved every later launch " +
             "inherits it.")]
    public bool forceFullScreenOverlay = true;

    private void Start() {
        if (forceFullScreenOverlay) RestoreOverlayGeometry();

        // Window acquisition happens in the clickthrough coroutine, not here.
        // OnEnable -- and therefore the coroutine's first iteration -- runs before
        // Start, so setting the handle up here meant the very first clickthrough
        // decision was always made without one. Doing it in the loop also gives
        // acquisition something to retry on, which a one-shot Start cannot.

        // Frame rate and runInBackground are owned by FrameRateController;
        // three scripts used to set targetFrameRate and fight over it.
    }

    // Set while a modal panel (e.g. the end-of-session questionnaire) is open, so
    // the overlay stops being click-through and the panel can actually be used.
    public void enableFeedbackState()
    {
        feedbackState = true;
    }

    public void disableFeedbackState()
    {
        feedbackState = false;
    }

    // Set while UnitEye's gaze calibration is running. Calibration is driven
    // entirely by left-clicks and draws a full-screen backdrop -- with the
    // overlay click-through outside the panel, those clicks fell through into
    // whatever application sat invisibly BEHIND the backdrop, and the
    // calibration itself never received them. A separate flag from
    // feedbackState so the two modal owners cannot fight over one bool.
    private bool calibrationState = false;

    public void enableCalibrationState()
    {
        calibrationState = true;
    }

    public void disableCalibrationState()
    {
        calibrationState = false;
    }

    // Same problem again, for the symptom reference panel. It is IMGUI, so the uGUI
    // raycast that decides whether the pointer is over the UI cannot see it at all --
    // the overlay stays click-through while the panel is on screen and the mouse wheel
    // goes straight through to whatever is behind it, so the panel cannot be scrolled.
    private bool infoState = false;

    public void enableInfoState()
    {
        infoState = true;
    }

    public void disableInfoState()
    {
        infoState = false;
    }

    private static TransparentWindow _instance;

    private void OnEnable()
    {
        _instance = this;
    }

    private void OnDisable()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// The cursor in Unity screen coordinates (bottom-left origin), read from the
    /// OS rather than from Unity.
    ///
    /// Input.mousePosition freezes the moment this window stops being the
    /// foreground window: Windows delivers mouse messages only to the window
    /// under the cursor -- which a click-through window never is -- and raw input
    /// only to the foreground window. Since the entire purpose of VIP-Sim is that
    /// the user clicks *into another application*, the overlay spends most of its
    /// life unfocused, which is exactly the state in which Input.mousePosition
    /// stops updating. A frozen position then froze the panel hover test: stuck
    /// "inside the panel" the overlay captured every click and all typing went to
    /// VIP-Sim; stuck "outside" the toolbar became unclickable and its clicks
    /// fell through to the application underneath.
    ///
    /// Everything that must keep working while unfocused (the hover test, the
    /// mouse gaze source, the diagnostics readout) reads this instead.
    /// </summary>
    public static Vector3 CursorPosition
    {
        get
        {
            var inst = _instance;
            if (inst != null && inst.TryGetNativeCursor(out var p)) return p;
            return Input.mousePosition; // editor, or no window acquired yet
        }
    }

    private bool TryGetNativeCursor(out Vector3 pos)
    {
        pos = default;
#if UNITY_EDITOR
        return false;
#elif UNITY_STANDALONE_WIN
        if (hWnd == IntPtr.Zero) return false;
        if (!GetCursorPos(out POINT pt)) return false;
        if (!ScreenToClient(hWnd, ref pt)) return false;
        if (!GetClientRect(hWnd, out RECT r)) return false;

        int clientH = r.bottom - r.top;
        if (clientH <= 0) return false;

        // Client coordinates are y-down from the top-left; Unity is y-up. Using
        // the client rect's own height keeps everything in one coordinate space
        // regardless of the process's DPI-awareness mode.
        pos = new Vector3(pt.X, clientH - pt.Y, 0f);
        return true;
#elif UNITY_STANDALONE_OSX
        IntPtr e = CGEventCreate(IntPtr.Zero);
        if (e == IntPtr.Zero) return false;
        CGPoint loc = CGEventGetLocation(e);
        CFRelease(e);

        CGRect bounds = CGDisplayBounds(CGMainDisplayID());
        if (bounds.size.width <= 0 || bounds.size.height <= 0) return false;

        // CGEvent coordinates are y-down from the top-left of the main display,
        // in points. Mapping proportionally onto Unity's screen size sidesteps
        // the points-vs-pixels question on Retina displays.
        pos = new Vector3(
            (float)(loc.x / bounds.size.width) * Screen.width,
            Screen.height - (float)(loc.y / bounds.size.height) * Screen.height,
            0f);
        return true;
#else
        return false;
#endif
    }

    private void Update()
    {
        // Window upkeep and the clickthrough decision run every frame. This used
        // to be a 10 Hz coroutine, but 100 ms is long enough for a quick flick
        // onto the toolbar followed by an immediate click to be routed to the
        // window underneath instead. GetCursorPos costs microseconds; the
        // SetWindowLong call still only happens on a state change.
        if (EnsureWindow())
        {
            // Evaluate the hover test even while a modal flag forces capture, so
            // the LastUiHit diagnostic keeps reflecting reality instead of
            // freezing at whatever was hit when the modal state began.
            bool outsideUi = IsCoordinateOutsidePanel();
            bool clickthrough = !feedbackState && !calibrationState && !infoState && outsideUi;
            if (clickthrough != _lastClickthrough || !_appliedOnce)
            {
                SetClickthrough(clickthrough);
                _lastClickthrough = clickthrough;
                _appliedOnce = true;
            }
            ClickthroughActive = clickthrough;
        }
        else
        {
            ClickthroughActive = null;
        }
    }

    /// <summary>
    /// Resolves (and re-resolves) the native window. Returns false while none is
    /// available yet, so the caller simply tries again on the next tick.
    /// </summary>
    private bool EnsureWindow()
    {
#if UNITY_EDITOR
        return false; // never restyle the editor's own window
#elif UNITY_STANDALONE_WIN
        // A resolution or fullscreen change can destroy and recreate the window,
        // leaving a stale handle that silently accepts every call.
        if (hWnd != IntPtr.Zero && !IsWindow(hWnd))
        {
            Debug.LogWarning("TransparentWindow: the window was recreated; re-acquiring its handle.");
            hWnd = IntPtr.Zero;
            _appliedOnce = false;
        }

        if (hWnd != IntPtr.Zero) return true;

        hWnd = FindOwnMainWindow();
        if (hWnd == IntPtr.Zero) { ReportAcquisitionFailure(); return false; }

        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);

        // NOMOVE|NOSIZE: only the Z order is being changed here. The original call
        // passed 0,0,0,0 with no flags, which asks Windows to move the window to
        // the origin and resize it to nothing.
        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

        Debug.Log($"TransparentWindow: window 0x{hWnd.ToInt64():X} acquired after {_acquireAttempts} " +
                  $"retries (ex-style 0x{GetWindowLong(hWnd, GWL_EXSTYLE):X}); clickthrough is active.");
        return true;
#elif UNITY_STANDALONE_OSX
        if (nsWindow != IntPtr.Zero) return true;

        try
        {
            IntPtr app = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            if (app != IntPtr.Zero)
            {
                IntPtr windows = objc_msgSend(app, sel_registerName("windows"));
                if (windows != IntPtr.Zero)
                    nsWindow = objc_msgSend_ptr(windows, sel_registerName("objectAtIndex:"), IntPtr.Zero);
            }
        }
        catch (Exception e)
        {
            // A missing runtime or a changed selector must not take the app with it.
            Debug.LogError($"TransparentWindow: could not reach the Cocoa window ({e.GetType().Name}: " +
                           $"{e.Message}). The overlay will capture clicks. Press Ctrl+Alt+Q to quit.");
            enabled = false;
            return false;
        }

        if (nsWindow == IntPtr.Zero) { ReportAcquisitionFailure(); return false; }

        ApplyMacTransparency();

        Debug.Log($"TransparentWindow: NSWindow 0x{nsWindow.ToInt64():X} acquired after {_acquireAttempts} " +
                  "retries; clickthrough is active and the window composites from framebuffer alpha.");
        return true;
#else
        return false;
#endif
    }

#if UNITY_STANDALONE_OSX
    /// <summary>
    /// Make the Cocoa window composite from the framebuffer's alpha.
    ///
    /// This is the macOS counterpart of DwmExtendFrameIntoClientArea, and it was simply
    /// absent: the window was made click-through but never made transparent, so VIP-Sim
    /// would have come up as an opaque rectangle covering the desktop no matter how
    /// correct the effect shaders' alpha was. Every alpha fix in the effect chain is
    /// wasted without this, because AppKit never lets the alpha reach the screen.
    ///
    /// An NSWindow is opaque by default and paints itself with its background colour.
    /// setOpaque:NO stops AppKit assuming every pixel is covered, and a clear background
    /// colour stops it painting one. The content view needs a backing layer for per-pixel
    /// alpha to survive compositing. The drop shadow has to go as well: AppKit derives the
    /// shadow from the window's opaque region, so a transparent window otherwise keeps a
    /// rectangular shadow drawn around content that is no longer there.
    /// </summary>
    private void ApplyMacTransparency()
    {
        if (nsWindow == IntPtr.Zero) return;

        try
        {
            objc_msgSend_bool(nsWindow, sel_registerName("setOpaque:"), false);

            IntPtr clear = objc_msgSend(objc_getClass("NSColor"), sel_registerName("clearColor"));
            if (clear != IntPtr.Zero)
                objc_msgSend_ptr(nsWindow, sel_registerName("setBackgroundColor:"), clear);

            IntPtr contentView = objc_msgSend(nsWindow, sel_registerName("contentView"));
            if (contentView != IntPtr.Zero)
                objc_msgSend_bool(contentView, sel_registerName("setWantsLayer:"), true);

            objc_msgSend_bool(nsWindow, sel_registerName("setHasShadow:"), false);
        }
        catch (Exception e)
        {
            // Same reasoning as the acquisition path: a changed selector must not take
            // the app down. An opaque overlay is bad but recoverable; a crash is not.
            Debug.LogError($"TransparentWindow: could not make the window transparent " +
                           $"({e.GetType().Name}: {e.Message}). The overlay will be opaque; " +
                           "press Ctrl+Alt+Q to quit.");
        }
    }
#endif

    /// <summary>
    /// Put the window back to full screen.
    ///
    /// The previous SetWindowPos call passed 0,0,0,0 with no flags, which asks
    /// Windows to move the window to the origin and resize it to nothing. Unity
    /// then saved that geometry, so the overlay came back as a 1x1 windowed player
    /// on every subsequent launch -- invisible, covering nothing, and capturing no
    /// clicks, with the saved size outliving the code that caused it:
    ///
    ///     Screenmanager Resolution Width  = 1
    ///     Screenmanager Resolution Height = 1
    ///     Screenmanager Fullscreen mode   = 3   (Windowed)
    ///
    /// Asserting the geometry here both repairs that and removes the class of bug:
    /// there is no legitimate windowed or partial state for a fullscreen overlay.
    /// Done once at startup rather than on a timer, so minimising still works.
    /// </summary>
    private void RestoreOverlayGeometry()
    {
#if !UNITY_EDITOR
        int w = Display.main.systemWidth;
        int h = Display.main.systemHeight;
        if (w <= 0 || h <= 0) return;

        if (Screen.width != w || Screen.height != h ||
            Screen.fullScreenMode != FullScreenMode.FullScreenWindow)
        {
            Debug.Log($"TransparentWindow: overlay geometry was {Screen.width}x{Screen.height} " +
                      $"({Screen.fullScreenMode}); restoring {w}x{h} FullScreenWindow.");
            Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
        }
#endif
    }

    private void ReportAcquisitionFailure()
    {
        _acquireAttempts++;
        // ~5 s at the 30 fps frame rate. Reported once, because until this
        // succeeds the overlay blocks the desktop and the user needs to be told why.
        if (_acquireAttempts == 150)
        {
            Debug.LogError("TransparentWindow: no native window found for this process after 5 s. " +
                           "The overlay will keep swallowing clicks. Press Ctrl+Alt+Q to quit.");
        }
    }

#if UNITY_STANDALONE_WIN
    /// <summary>
    /// The largest visible, unowned top-level window belonging to this process.
    ///
    /// Focus-independent, unlike GetActiveWindow. The owner and size filters skip
    /// the small helper windows Unity creates alongside the player, and the
    /// zero-size state that exists briefly before the real window is shown.
    /// </summary>
    private static IntPtr FindOwnMainWindow()
    {
        uint self = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        IntPtr best = IntPtr.Zero;
        long bestArea = 0;

        _enumCallback = (h, l) =>
        {
            GetWindowThreadProcessId(h, out uint pid);
            if (pid != self) return true;
            if (!IsWindowVisible(h)) return true;
            if (GetWindow(h, GW_OWNER) != IntPtr.Zero) return true;
            if (!GetClientRect(h, out RECT r)) return true;

            long area = (long)(r.right - r.left) * (r.bottom - r.top);
            if (area > bestArea) { bestArea = area; best = h; }
            return true;
        };

        EnumWindows(_enumCallback, IntPtr.Zero);
        _enumCallback = null;
        return best;
    }
#endif

    private void SetClickthrough(bool clickthrough) {

#if UNITY_EDITOR
        _lastClickthrough = clickthrough;
#elif UNITY_STANDALONE_WIN
        if (hWnd == IntPtr.Zero) return;

        // Read-modify-write rather than overwriting the whole style word. The
        // previous version assigned WS_EX_LAYERED outright, discarding whatever
        // else Unity had set on the window.
        uint style = GetWindowLong(hWnd, GWL_EXSTYLE);
        uint updated = style | WS_EX_LAYERED;
        if (clickthrough) updated |= WS_EX_TRANSPARENT;
        else              updated &= ~WS_EX_TRANSPARENT;

        if (updated != style) SetWindowLong(hWnd, GWL_EXSTYLE, updated);
#elif UNITY_STANDALONE_OSX
        if (nsWindow == IntPtr.Zero) return;
        objc_msgSend_bool(nsWindow, sel_registerName("setIgnoresMouseEvents:"), clickthrough);
#endif
    }

    // Get Mouse Position in World with Z = 0f
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 vec = GetMouseWorldPositionWithZ(Input.mousePosition, maincam);
        vec.z = 0f;
        return vec;
    }
    public static Vector3 GetMouseWorldPositionWithZ()
    {
        return GetMouseWorldPositionWithZ(Input.mousePosition, Camera.main);
    }
    public static Vector3 GetMouseWorldPositionWithZ(Camera worldCamera)
    {
        return GetMouseWorldPositionWithZ(Input.mousePosition, worldCamera);
    }
    public static Vector3 GetMouseWorldPositionWithZ(Vector3 screenPosition, Camera worldCamera)
    {
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);
        return worldPosition;
    }

    // Reused raycast state; allocation-free per frame.
    private PointerEventData _pointerData;
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

    /// <summary>
    /// Name of the topmost UI element under the cursor from the last hover test,
    /// or "" when nothing was hit. Diagnostics only (shown on the F10 overlay).
    /// </summary>
    public static string LastUiHit { get; private set; } = "";

    public bool IsCoordinateOutsidePanel()
    {
        // CursorPosition, not Input.mousePosition: this test must keep working
        // while the window is unfocused, which is its normal operating state.
        Vector2 screenPosition = CursorPosition;

        // Primary test: is there actually visible, clickable VIP-Sim UI under the
        // cursor? The old single-rectangle test had two failure modes seen in the
        // wild: the panel's rect kept capturing clicks after the panel was HIDDEN
        // (a dead zone over the user's application), and the Settings window --
        // which does not live inside the panel rect at all -- let clicks fall
        // through itself into whatever sat underneath. Raycasting the UI answers
        // the real question, for every current and future panel, wherever it is
        // dragged and whatever its visibility.
        var eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            _pointerData ??= new PointerEventData(eventSystem);
            _pointerData.position = screenPosition;
            _raycastResults.Clear();
            eventSystem.RaycastAll(_pointerData, _raycastResults);
            if (_raycastResults.Count > 0)
            {
                LastUiHit = _raycastResults[0].gameObject.name;
                return false;
            }
        }
        LastUiHit = "";

        // Fallback: the panel rectangle, counted only while the panel is actually
        // shown. Covers a missing EventSystem; fails towards click-through
        // otherwise -- the user keeps their desktop and loses VIP-Sim's buttons,
        // rather than the whole screen locking up behind an invisible overlay.
        if (panelRectTransform == null)
        {
            if (!_missingPanelLogged)
            {
                Debug.LogError("TransparentWindow.panelRectTransform is not assigned; treating the whole " +
                               "screen as click-through so the desktop stays usable.", this);
                _missingPanelLogged = true;
            }
            return true;
        }
        if (!panelRectTransform.gameObject.activeInHierarchy) return true;

        // Use the built-in rectangle check to avoid per-frame allocations
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(
            panelRectTransform, screenPosition, null);
        return !inside;
    }
}

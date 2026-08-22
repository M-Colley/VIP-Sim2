#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Where the overlay itself is on the desktop, in the coordinates the capture plugin uses.
///
/// This exists because Unity and Win32 do not agree on what "the window's position" means,
/// and the disagreement is invisible on a single monitor. uWindowCapture reports every
/// captured window in GLOBAL desktop coordinates -- y-down, origin at the primary display's
/// top-left, so a monitor arranged above the primary has negative y. Screen.mainWindowPosition
/// is relative to the display the window is on, so for a full-screen overlay it is (0,0) on
/// every monitor. Subtracting it, which is what the placement used to do, therefore subtracts
/// nothing, and the capture is drawn as though every window's global coordinates were local
/// to whichever screen the overlay happens to be on.
///
/// On one monitor those two spaces coincide and everything is correct. On two they do not,
/// and the error is the offset between the monitors -- so a window on the other screen lands
/// entirely outside the overlay and the simulation shows nothing at all, while a window that
/// happens to sit near the desktop origin lands roughly centred and looks like it works.
/// Both were reported from the same session: 'nothing' for two windows, and a third that
/// appeared but at the wrong size.
///
/// GetWindowRect on our own window answers the question in the same space the plugin uses,
/// which is the only thing that makes the subtraction meaningful.
/// </summary>
public static class OverlayScreen
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// The overlay's rectangle in global desktop pixels, or false if the window handle is
    /// not available yet. Callers must treat false as "do not move anything this frame":
    /// guessing (0,0) is exactly the bug this replaces.
    /// </summary>
    public static bool TryGetRect(out RectInt rect)
    {
        rect = default;

        var hwnd = TransparentWindow.OwnWindow;
        if (hwnd == IntPtr.Zero) return false;
        if (!GetWindowRect(hwnd, out RECT r)) return false;
        if (r.right <= r.left || r.bottom <= r.top) return false;

        rect = new RectInt(r.left, r.top, r.right - r.left, r.bottom - r.top);
        return true;
    }

    /// <summary>
    /// How much of a window, given in the same global coordinates, is actually over the
    /// overlay -- 0 for a window on another monitor, 1 for one wholly inside this screen.
    ///
    /// Used to tell the user that the window they picked is somewhere VIP-Sim is not. That
    /// case is otherwise silent and indistinguishable from a broken capture, which is how
    /// it was reported.
    /// </summary>
    public static float FractionOnScreen(RectInt overlay, float wx, float wy, float ww, float wh)
    {
        if (ww <= 0f || wh <= 0f) return 0f;

        float left = Mathf.Max(overlay.xMin, wx);
        float top = Mathf.Max(overlay.yMin, wy);
        float right = Mathf.Min(overlay.xMax, wx + ww);
        float bottom = Mathf.Min(overlay.yMax, wy + wh);

        if (right <= left || bottom <= top) return 0f;
        return ((right - left) * (bottom - top)) / (ww * wh);
    }
}
#endif

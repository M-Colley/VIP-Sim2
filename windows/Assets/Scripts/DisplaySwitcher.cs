using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves the VIP-Sim overlay between monitors.
///
/// The overlay always covered the primary display: RestoreOverlayGeometry sizes it from
/// Display.main and nothing anywhere let the user pick. On a multi-monitor desk that
/// means the tool cannot simulate the screen the participant is actually working on
/// unless that happens to be the primary.
///
/// Static and platform-neutral on purpose. Screen.GetDisplayLayout and
/// Screen.MoveMainWindowTo are the cross-platform window-management API, so the same
/// file ships in both projects unchanged -- per-platform edits to shared behaviour are
/// what produced this project's compile breaks (uWindowCapture references reaching the
/// macOS project, and an infoState that existed on one platform only).
///
/// Reachable three ways, because each alone has a gap:
///   - F3 (VipSimDiagnostics), which only fires while the overlay holds focus;
///   - a button in the F1 symptom panel, which forces the overlay interactive and
///     therefore always works;
///   - the first-run tutorial mentions both.
/// </summary>
public static class DisplaySwitcher
{
    private const string PrefKey = "vipsim.display.index";

    private static readonly List<DisplayInfo> _displays = new List<DisplayInfo>();
    private static bool _moving;

    public static int DisplayCount
    {
        get { Refresh(); return _displays.Count; }
    }

    /// <summary>"display 2 of 3" -- for button labels and logs.</summary>
    public static string Summary
    {
        get
        {
            Refresh();
            return $"display {CurrentIndex() + 1} of {_displays.Count}";
        }
    }

    public static void MoveToNext()
    {
        Refresh();
        if (_displays.Count < 2)
        {
            Debug.Log("[DisplaySwitcher] Only one display connected; nothing to move to.");
            return;
        }
        MoveTo((CurrentIndex() + 1) % _displays.Count);
    }

    /// <summary>
    /// Re-apply the display remembered from a previous session. Called once at startup,
    /// AFTER TransparentWindow has restored the full-screen geometry -- both write to the
    /// same window, and this one has to win.
    /// </summary>
    public static void ApplySaved()
    {
        Refresh();
        int saved = PlayerPrefs.GetInt(PrefKey, 0);
        if (saved <= 0 || saved >= _displays.Count) return; // primary, or a display since unplugged
        if (saved == CurrentIndex()) return;
        Debug.Log($"[DisplaySwitcher] Restoring remembered display {saved + 1} of {_displays.Count}.");
        MoveTo(saved);
    }

    private static void MoveTo(int index)
    {
        if (_moving) return; // MoveMainWindowTo is async; a second call mid-flight is undefined
        var target = _displays[index];
        _moving = true;

        // Unity's documented sequence: the move is only defined for a windowed window,
        // so drop out of the borderless full screen, move, then reassert full screen at
        // the TARGET display's resolution -- the displays need not match sizes. The
        // moment of windowed flicker is the price of the documented path; the undocumented
        // one simply fails silently on some setups, which on a click-through overlay would
        // be indistinguishable from the feature not existing.
        Screen.SetResolution(target.width, target.height, FullScreenMode.Windowed);
        var op = Screen.MoveMainWindowTo(target, Vector2Int.zero);
        op.completed += _ =>
        {
            Screen.SetResolution(target.width, target.height, FullScreenMode.FullScreenWindow);
            PlayerPrefs.SetInt(PrefKey, index);
            PlayerPrefs.Save();
            _moving = false;
            Debug.Log($"[DisplaySwitcher] Moved to '{target.name}' " +
                      $"{target.width}x{target.height} at {target.workArea.position} " +
                      $"({index + 1} of {_displays.Count}).");
        };
    }

    private static void Refresh()
    {
        _displays.Clear();
        Screen.GetDisplayLayout(_displays);
    }

    private static int CurrentIndex()
    {
        // Matched on identity fields rather than List.IndexOf: DisplayInfo carries
        // refresh-rate ratios whose exact numerators can differ between two queries for
        // the same physical display, and any mismatch would silently reset us to 0.
        var here = Screen.mainWindowDisplayInfo;
        for (int i = 0; i < _displays.Count; i++)
        {
            var d = _displays[i];
            if (d.name == here.name &&
                d.width == here.width && d.height == here.height &&
                d.workArea.position == here.workArea.position)
                return i;
        }
        return 0;
    }
}

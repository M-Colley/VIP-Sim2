using System.Collections;
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
    private static float _lastRefresh = -999f;

    // Where we believe the overlay is, used only when the layout cannot be matched (see
    // CurrentIndex). Seeded from the remembered display and updated on every completed
    // move, so after our own moves this is exact rather than a guess.
    private static int _indexHint = -1;

    /// <summary>How long a cached display layout is trusted. See Refresh.</summary>
    private const float CacheSeconds = 0.5f;

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
            int i = CurrentIndex();
            return i >= 0
                ? $"display {i + 1} of {_displays.Count}"
                : $"{_displays.Count} displays";
        }
    }

    public static void MoveToNext()
    {
        Refresh(force: true);
        if (_displays.Count < 2)
        {
            Debug.Log("[DisplaySwitcher] Only one display connected; nothing to move to.");
            return;
        }

        // An unmatched current display means we genuinely do not know where we are, so
        // advancing from an assumed primary could "move" us to the display we are already
        // on and look like the feature is broken. Falling back to the hint keeps cycling
        // predictable; only a cold start with no hint assumes the primary.
        int from = CurrentIndex();
        if (from < 0) from = _indexHint >= 0 && _indexHint < _displays.Count ? _indexHint : 0;

        MoveTo((from + 1) % _displays.Count);
    }

    /// <summary>
    /// Re-apply the display remembered from a previous session. Called once at startup,
    /// AFTER TransparentWindow has restored the full-screen geometry -- both write to the
    /// same window, and this one has to win.
    /// </summary>
    public static void ApplySaved()
    {
        Refresh(force: true);
        int saved = PlayerPrefs.GetInt(PrefKey, 0);
        if (saved < 0 || saved >= _displays.Count) return; // a display since unplugged
        _indexHint = saved;
        if (saved == CurrentIndex()) return;               // already there; nothing to do
        Debug.Log($"[DisplaySwitcher] Restoring remembered display {saved + 1} of {_displays.Count}.");
        MoveTo(saved);
    }

    private static void MoveTo(int index)
    {
        if (_moving) return; // the sequence below spans frames; overlapping runs fight
        if (index < 0 || index >= _displays.Count) return;
        Runner.Instance.StartCoroutine(MoveRoutine(index));
    }

    /// <summary>
    /// Windowed -> move -> full screen, with the waits that make it actually work.
    ///
    /// This is the part that was wrong when the feature was first written, and it was
    /// wrong in a way a single-monitor machine cannot reveal. Screen.SetResolution does
    /// not take effect immediately -- Unity applies it at the end of the frame -- so
    /// calling MoveMainWindowTo on the next line moves a window that is still borderless
    /// full screen. That is precisely the case Unity documents as undefined, and it fails
    /// by leaving the window unmoved or stranded windowed on the wrong monitor.
    ///
    /// Yielding between the steps is the whole fix: each stage observes the previous one.
    /// </summary>
    private static IEnumerator MoveRoutine(int index)
    {
        _moving = true;
        try
        {
            var target = _displays[index];

            // 1. Leave full screen, and let the change actually land.
            Screen.SetResolution(target.width, target.height, FullScreenMode.Windowed);
            yield return null;
            yield return new WaitForEndOfFrame();

            // 2. Move. The position is relative to the target display's top-left.
            var op = Screen.MoveMainWindowTo(target, Vector2Int.zero);
            if (op != null)
            {
                // Bounded wait. An operation that never completes must not strand the
                // feature for the rest of the session -- without this, _moving would
                // latch true and every later F3 would be silently ignored.
                float waited = 0f;
                while (!op.isDone && waited < 3f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!op.isDone)
                    Debug.LogWarning("[DisplaySwitcher] Move did not report completion within 3s; " +
                                     "continuing anyway. The window may not have moved.");
            }
            else
            {
                // Null means the platform did not accept the request at all.
                Debug.LogWarning("[DisplaySwitcher] Screen.MoveMainWindowTo returned nothing; " +
                                 "this platform may not support moving the main window.");
                yield return null;
            }

            // 3. Back to borderless full screen, at the TARGET display's resolution --
            //    the displays need not be the same size.
            Screen.SetResolution(target.width, target.height, FullScreenMode.FullScreenWindow);
            yield return null;

            _indexHint = index;
            PlayerPrefs.SetInt(PrefKey, index);
            PlayerPrefs.Save();

            // Logged unconditionally, with the values needed to tell a successful move
            // from a silent no-op when reading a user's Player.log.
            Debug.Log($"[DisplaySwitcher] Requested '{target.name}' " +
                      $"{target.width}x{target.height} at {target.workArea.position} " +
                      $"({index + 1} of {_displays.Count}); window now at " +
                      $"{Screen.mainWindowPosition}, screen {Screen.width}x{Screen.height}.");
        }
        finally
        {
            // try/finally, not a plain assignment at the end: a coroutine stopped early
            // (scene teardown, quit) would otherwise leave the flag latched.
            _moving = false;
        }
    }

    /// <summary>
    /// Re-query the display layout, at most a few times a second.
    ///
    /// DisplayCount and Summary are read from OnGUI, which runs at least twice per frame
    /// (layout and repaint) -- an uncached query meant several system calls and a list
    /// rebuild per frame just to label a button. Caching also keeps the count stable
    /// between the layout and repaint passes of one frame, which is what stops IMGUI
    /// throwing a mismatched-layout-group error if a display is hot-plugged mid-frame.
    /// </summary>
    private static void Refresh(bool force = false)
    {
        if (!force && _displays.Count > 0 && Time.unscaledTime - _lastRefresh < CacheSeconds) return;
        _lastRefresh = Time.unscaledTime;
        _displays.Clear();
        Screen.GetDisplayLayout(_displays);
    }

    /// <summary>Index of the display the overlay is on, or -1 if it cannot be matched.</summary>
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

        // -1, never 0. Reporting "primary" for an unknown display is a lie that makes
        // cycling jump to the wrong monitor and reads to the user as a broken feature.
        return -1;
    }

    /// <summary>
    /// Coroutine host. DisplaySwitcher is static by design -- it is called from a hotkey
    /// handler and from IMGUI, neither of which owns a suitable MonoBehaviour -- but the
    /// move has to span frames, so it needs something to run on.
    /// </summary>
    private class Runner : MonoBehaviour
    {
        private static Runner _instance;

        public static Runner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("DisplaySwitcherRunner")
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Runner>();
                }
                return _instance;
            }
        }
    }
}

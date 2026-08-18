using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnitEye;
using VisSim;

/// <summary>
/// On-demand performance measurement, so "it feels slow" can be turned into a
/// number and attributed to a stage.
///
/// This exists because the obvious guess was wrong once already: enabling
/// UnitEye's async GPU readback looked like the clear latency fix and instead
/// threw every frame, breaking gaze entirely. Measuring first is cheaper than
/// shipping another plausible-looking change.
///
/// Deliberately reads existing seams rather than instrumenting the vendored
/// uniteye package, so that package stays a clean upstream copy:
///   - HomulerGaze.MeasuredLatencySeconds  -- end-to-end gaze latency, already computed upstream
///   - IGazeProvider.CaptureTimestamp      -- changes once per genuinely new camera sample,
///                                            which gives the true gaze sample rate (usually far
///                                            below the render frame rate)
///   - ProfilerRecorder                    -- main/render thread time, draw calls
///
/// The shader question is answered by A/B rather than by attributing per-effect
/// costs: F11 disables every active effect for a few seconds, records the frame
/// time, re-enables them and compares. That measures what actually matters (what
/// the effect chain costs in situ) and cannot be fooled by GPU pipelining the way
/// per-effect CPU timers can.
///
/// Hotkeys: F10 toggles the overlay, F11 runs the effect A/B benchmark.
/// </summary>
public class VipSimDiagnostics : MonoBehaviour
{
    [Header("Hotkeys")]
    public KeyCode toggleKey = KeyCode.F10;
    public KeyCode benchmarkKey = KeyCode.F11;

    [Tooltip("Emergency quit. VIP-Sim is a topmost, click-through, borderless overlay: it has no " +
             "title bar, alt-tabbing away does not close it, and if the in-app exit path is broken " +
             "the only remaining option is Task Manager. This guarantees a way out.")]
    public KeyCode quitKey = KeyCode.F12;

    [Tooltip("Write a PNG of what VIP-Sim is actually rendering, next to the player log.\n\n" +
             "VIP-Sim is a layered window, and ordinary screen capture (BitBlt) excludes those -- a " +
             "desktop screenshot comes back without the overlay in it. Rendering problems therefore " +
             "cannot be seen from outside the app and have to be inferred, which went badly: three " +
             "rounds of fixes were spent on geometry that was already correct, because nobody could " +
             "look at the output. This captures the overlay's own framebuffer.")]
    public KeyCode screenshotKey = KeyCode.F6;

    [Tooltip("Measure the alpha channel of the finished framebuffer.\n\n" +
             "VIP-Sim composites through DWM (DwmExtendFrameIntoClientArea with a sheet-of-glass " +
             "margin), so a pixel is only visible on the desktop if its ALPHA is non-zero. RGB can " +
             "be perfectly correct and the overlay still be invisible. Nothing about that is " +
             "observable from a normal screenshot, so this reads the backbuffer back and reports " +
             "the alpha distribution alongside the list of enabled effects.")]
    public KeyCode alphaProbeKey = KeyCode.F8;

    [Tooltip("Force the effect list on screen without selecting a window first.\n\n" +
             "The effect list is gated behind having picked a window to capture, and a " +
             "click-through overlay cannot be driven by synthetic clicks -- so the whole " +
             "lower half of the UI was impossible to look at while working on it. A layout " +
             "change was shipped that left the effect list with no background and " +
             "overlapping the webcam row, purely because every screenshot showed the " +
             "pre-selection state. This makes that half of the UI reviewable.")]
    public KeyCode revealMenuKey = KeyCode.F7;

    [Tooltip("Move VIP-Sim to the next monitor. Only fires while the overlay holds focus " +
             "-- click the panel first -- so the same action is also a button in the F1 " +
             "symptom panel, which forces the overlay interactive and always works.")]
    public KeyCode displayKey = KeyCode.F3;

    /// <summary>
    /// Whether the developer hotkeys are live.
    ///
    /// F6/F7/F8/F10/F11 are instrumentation, not features: they were written to debug this
    /// codebase and they log volumes of detail, capture files to disk and can force UI
    /// states that a user cannot undo. Shipping them bound by default in a paid build is
    /// how a customer ends up in a state nobody can talk them out of over email.
    ///
    /// Resolved once. The Editor always qualifies; a release build needs -vipsim-dev on
    /// the command line, which is documented in RELEASE.md and costs a support reply.
    /// </summary>
    public static bool DeveloperMode
    {
        get
        {
            if (_devMode.HasValue) return _devMode.Value;
            bool dev = Application.isEditor;
            if (!dev)
            {
                foreach (var arg in System.Environment.GetCommandLineArgs())
                {
                    if (string.Equals(arg, "-vipsim-dev", System.StringComparison.OrdinalIgnoreCase))
                    {
                        dev = true;
                        break;
                    }
                }
            }
            _devMode = dev;
            return dev;
        }
    }

    private static bool? _devMode;

    /// <summary>
    /// While true, the parts of the UI that are normally gated behind "a window has been
    /// selected and the effect is switched on" are shown anyway.
    ///
    /// The first attempt at this hotkey just called SetActive on the menus, which did
    /// nothing: the gates re-evaluate every frame in Update and immediately switched them
    /// back off. The flag has to be read by the gates themselves, which is why it lives
    /// here -- somewhere both platforms can see. Windows gates on uWindowCapture's
    /// UwcWindowList, macOS uses a different capture backend entirely, so neither is a
    /// suitable home for a shared switch.
    ///
    /// This exists because the effect list is otherwise impossible to look at while
    /// working on it: it only appears once a window is selected, and a click-through
    /// overlay cannot be driven by synthetic clicks. Two layout changes were shipped
    /// broken because every screenshot showed the pre-selection state.
    /// </summary>
    public static bool ForceMenusVisible { get; private set; }

    [Tooltip("Draw the overlay. Off by default: this is an IMGUI overlay and, like " +
             "UnitEye's debug drawing, it paints over the simulation.")]
    public bool showOverlay = false;

    [Tooltip("Also write a summary line to the player log at this interval. 0 disables.\n\n" +
             "On by default: the overlay is on-screen only, so if the app is killed rather " +
             "than closed cleanly there is otherwise no record of what the numbers were.")]
    public float logIntervalSeconds = 5f;

    // --- rolling frame stats ---
    private const int Window = 120;
    private readonly float[] _frameMs = new float[Window];
    private int _frameIdx;
    private int _frameCount;

    // --- gaze sample rate ---
    private double _lastCaptureTs = -1;
    private int _gazeSamples;
    private float _gazeWindowStart;
    private float _gazeHz;

    private ProfilerRecorder _mainThread, _drawCalls;
    private float _nextLog;

    // --- benchmark state ---
    private string _benchmarkResult = "not run  (F11)";
    private bool _benchmarkRunning;

    private void OnEnable()
    {
        _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15,
                                                ProfilerRecorderOptions.SumAllSamplesInFrame);
        _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        _gazeWindowStart = Time.unscaledTime;
        Debug.Log($"[VipSimDiagnostics] {toggleKey}=overlay  {benchmarkKey}=effect benchmark  " +
                  $"{quitKey}=quit  F9=gaze calibration");
        StartCoroutine(DumpButtonRectsOnce());

        // Crash reporting. VIP-Sim is a borderless always-on-top overlay with no console,
        // usually running in front of a participant, and the player log is buried under
        // AppData -- so an exception currently disappears silently and the session is just
        // "it stopped working". Exceptions and errors are mirrored to a plain text file
        // next to the log, with a timestamp and stack, so a failed session leaves something
        // legible to send back.
        Application.logMessageReceived += OnLogMessage;
    }

    /// <summary>
    /// One-shot log of every button's actual screen rectangle, a few seconds
    /// after startup so the canvas scaler and the overlay geometry repair have
    /// both settled. Reasoning about this UI's positions from scene data has
    /// been wrong repeatedly (batch-mode canvases scale differently, the window
    /// itself was once 1x1); this prints the ground truth of the running build,
    /// which is also exactly what an automated UI test needs to click.
    /// </summary>
    private IEnumerator DumpButtonRectsOnce()
    {
        yield return new WaitForSeconds(3f);
        var sb = new StringBuilder("[VipSimDiagnostics] button rects (Unity px, origin bottom-left, " +
                                   $"screen {Screen.width}x{Screen.height}):\n");
        var corners = new Vector3[4];
        foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var rt = b.transform as RectTransform;
            if (rt == null) continue;
            rt.GetWorldCorners(corners);
            sb.AppendLine($"  BTNRECT {b.name} ({corners[0].x:F0},{corners[0].y:F0})-({corners[2].x:F0},{corners[2].y:F0}) " +
                          $"active={b.gameObject.activeInHierarchy}");
        }
        Debug.Log(sb.ToString());
    }

    private void OnDisable()
    {
        if (_mainThread.Valid) _mainThread.Dispose();
        if (_drawCalls.Valid) _drawCalls.Dispose();
        Application.logMessageReceived -= OnLogMessage;
    }

    private void Update()
    {
        if (Input.GetKeyDown(quitKey))
        {
            Debug.Log("[VipSimDiagnostics] Quit hotkey pressed.");
            Application.Quit();
        }
        // Shipping feature, never gated: moving the overlay to another monitor.
        if (Input.GetKeyDown(displayKey)) DisplaySwitcher.MoveToNext();

        // Everything below is a developer aid. In a release build a user who fat-fingers
        // F8 should not get an alpha histogram in their log, and F7 must not be able to
        // strand the UI in a forced-visible state. Enable with -vipsim-dev on the command
        // line; the Editor always has them.
        if (DeveloperMode)
        {
            if (Input.GetKeyDown(toggleKey)) showOverlay = !showOverlay;
            if (Input.GetKeyDown(benchmarkKey) && !_benchmarkRunning) StartCoroutine(RunEffectBenchmark());

            if (Input.GetKeyDown(screenshotKey))
            {
                var path = System.IO.Path.Combine(Application.persistentDataPath, "vipsim-shot.png");
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[VipSimDiagnostics] SHOT {path}");
            }

            if (Input.GetKeyDown(alphaProbeKey) && !_alphaProbeRunning)
            {
                LogCursorAlignment();
                StartCoroutine(ProbeBackbufferAlpha());
            }

            if (Input.GetKeyDown(revealMenuKey))
            {
                ForceMenusVisible = !ForceMenusVisible;
                Debug.Log($"[VipSimDiagnostics] REVEAL force-menus={ForceMenusVisible}");
            }
        }

        _frameMs[_frameIdx] = Time.unscaledDeltaTime * 1000f;
        _frameIdx = (_frameIdx + 1) % Window;
        if (_frameCount < Window) _frameCount++;

        SampleGazeRate();

        if (logIntervalSeconds > 0f && Time.unscaledTime >= _nextLog)
        {
            _nextLog = Time.unscaledTime + logIntervalSeconds;
            Debug.Log("[VipSimDiagnostics] " + BuildReport().Replace('\n', ' '));

            // Capture placement goes in the periodic report, not only behind the hotkey.
            // VIP-Sim is click-through and therefore almost never the foreground window, so
            // Input.GetKeyDown never fires for it -- pressing F8 sends the key to whatever
            // the user is actually working in. A diagnostic that cannot be triggered is not
            // a diagnostic. This costs one line every logIntervalSeconds and only prints
            // when a window is genuinely being captured.
            LogCapturePlacement();
            LogCameras();

            // Alpha in the periodic report as well as on F8. Alpha decides whether the
            // overlay is visible at all, and the hotkey only fires when the overlay holds
            // focus -- which, being click-through, it almost never does.
            if (!_alphaProbeRunning) StartCoroutine(ProbeBackbufferAlpha());
        }
    }

    /// <summary>
    /// Report every number involved in turning the pointer into the gaze point, so a
    /// reported misalignment can be measured instead of guessed at.
    ///
    /// The chain is: OS cursor -> client-rect pixels (y flipped) -> divided by Screen size
    /// -> xy_norm -> the shaders. A constant offset, a scale error and a flipped axis all
    /// look identical from the outside but disagree at different points in that chain, so
    /// the fix depends on which number first stops matching the pointer.
    ///
    /// Put the pointer somewhere known -- a screen corner is easiest to judge -- and press
    /// the alpha probe key. Compare CURSOR against where the pointer actually was.
    /// </summary>
    private void LogCursorAlignment()
    {
        var native = TransparentWindow.CursorPosition;
        var legacy = Input.mousePosition;
        var tracker = GazeTracker.GetInstance;
        var norm = tracker != null ? tracker.xy_norm : new Vector2(-1f, -1f);

        Debug.Log($"[VipSimDiagnostics] CURSOR native=({native.x:F0},{native.y:F0}) " +
                  $"legacy=({legacy.x:F0},{legacy.y:F0}) screen={Screen.width}x{Screen.height} " +
                  $"dpi={Screen.dpi:F0} fullscreen={Screen.fullScreenMode} " +
                  $"xy_norm=({norm.x:F3},{norm.y:F3}) " +
                  $"-> expected pixel=({norm.x * Screen.width:F0},{norm.y * Screen.height:F0})");

        LogCapturePlacement();
    }

    /// <summary>
    /// Report every number the 1:1 capture placement depends on.
    ///
    /// The capture is drawn at the right SIZE but in the wrong PLACE, which narrows the
    /// fault to the screen-to-world conversion. Logging the window's reported desktop
    /// rectangle next to the resulting world position makes the discrepancy measurable:
    /// if win.x/y do not match where the window visibly is, uWindowCapture is reporting a
    /// different rectangle than expected (frame borders, DPI space, or multi-monitor
    /// origin); if they do match, the error is in the conversion or in the plane's pivot.
    /// </summary>
    private void LogCapturePlacement()
    {
        // Windows only. The capture placement numbers come from uWindowCapture, which does
        // not exist in this project -- macOS uses a different capture backend entirely --
        // so there is nothing here to report. Kept as a no-op rather than removed so the
        // two copies of this file stay structurally comparable.
    }

    /// <summary>
    /// Report every enabled camera in render order, with the properties that decide what
    /// reaches the backbuffer.
    ///
    /// VIP-Sim carries two full-screen cameras from the retired stereo rig, and they were
    /// both at depth 0 -- so which one wrote the backbuffer last was undefined. Each also
    /// CLEARS, so whichever renders second wipes the first; only one of them can actually
    /// be contributing. Establishing which, and what its clear does to alpha, is the
    /// prerequisite for deleting the other, and it cannot be settled by reading the scene:
    /// disabling the seemingly-redundant camera removed the overlay entirely, which is the
    /// opposite of what the code structure implies.
    ///
    /// Clear flags: 1 Skybox, 2 SolidColor, 3 Depth, 4 Nothing. A Skybox clear is opaque,
    /// which on an alpha-composited overlay is a very different thing from a SolidColor
    /// clear at alpha 0.
    /// </summary>
    private void LogCameras()
    {
        var cams = FindObjectsByType<Camera>(FindObjectsInactive.Include);
        System.Array.Sort(cams, (a, b) => a.depth.CompareTo(b.depth));

        foreach (var c in cams)
        {
            var bg = c.backgroundColor;
            Debug.Log($"[VipSimDiagnostics] CAMERA '{c.name}' enabled={c.enabled} depth={c.depth:F1} " +
                      $"ortho={c.orthographic} size={c.orthographicSize:F3} clear={c.clearFlags} " +
                      $"bg=({bg.r:F2},{bg.g:F2},{bg.b:F2},a={bg.a:F2}) " +
                      $"target={c.targetTexture?.name ?? "backbuffer"} cull=0x{c.cullingMask:X}");
        }
    }

    private static int _crashesLogged;

    /// <summary>
    /// Mirror exceptions and errors to a file the user can actually find and send.
    /// Capped, because a per-frame exception would otherwise fill the disk -- and a
    /// per-frame exception is exactly the failure mode most likely to occur here, since
    /// effects run their OnRenderImage every frame.
    /// </summary>
    private void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error) return;
        if (_crashesLogged >= 50) return;
        _crashesLogged++;

        try
        {
            var path = System.IO.Path.Combine(Application.persistentDataPath, "vipsim-errors.log");
            System.IO.File.AppendAllText(path,
                $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] {type}: {condition}\n{stackTrace}\n");
            if (_crashesLogged == 50)
                System.IO.File.AppendAllText(path, "--- further errors suppressed this session ---\n");
        }
        catch
        {
            // Never let the error reporter become the error. If the file cannot be written
            // the player log still has everything; losing the mirror is not worth a crash.
        }
    }

    private bool _alphaProbeRunning;

    /// <summary>
    /// Read the finished framebuffer back and report its alpha distribution.
    ///
    /// The overlay is composited by DWM from the window's own per-pixel alpha, so
    /// alpha is what decides whether anything reaches the screen -- and it is the
    /// one channel a screenshot of the app cannot tell you about. Reported next to
    /// the enabled-effect list so a "works only alongside another effect" report
    /// can be checked rather than reasoned about.
    /// </summary>
    private System.Collections.IEnumerator ProbeBackbufferAlpha()
    {
        _alphaProbeRunning = true;
        yield return new WaitForEndOfFrame();

        int w = Screen.width, h = Screen.height;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        var px = tex.GetPixels32();
        long transparent = 0, partial = 0, opaque = 0, alphaSum = 0;
        for (int i = 0; i < px.Length; i++)
        {
            byte a = px[i].a;
            alphaSum += a;
            if (a < 13) transparent++;
            else if (a > 242) opaque++;
            else partial++;
        }
        Destroy(tex);

        double n = px.Length;
        var effects = FindObjectsByType<VisSim.LinkableBaseEffect>(FindObjectsInactive.Exclude);
        var on = new System.Collections.Generic.List<string>();
        foreach (var e in effects)
            if (e.enabled) on.Add(e.GetType().Name + (e.gameObject.tag == "LeftEye" ? "(L)" : "(R)"));
        on.Sort();

        Debug.Log($"[VipSimDiagnostics] ALPHA {w}x{h} mean={alphaSum / n / 255.0:F3} " +
                  $"transparent={transparent / n:P1} partial={partial / n:P1} opaque={opaque / n:P1} " +
                  $"| enabled({on.Count}): {(on.Count == 0 ? "-" : string.Join(",", on))}");

        _alphaProbeRunning = false;
    }

    /// <summary>
    /// Count genuinely new camera samples. CaptureTimestamp only advances when a
    /// new webcam frame has been through the pipeline, so this is the real gaze
    /// update rate -- which is the number that matters for "the cursor lags",
    /// not the render frame rate.
    /// </summary>
    private void SampleGazeRate()
    {
        double ts;
        try
        {
            var provider = UnitEyeAPI.GetGazeReference()?.Provider;
            if (provider == null) return;
            ts = provider.CaptureTimestamp;
        }
        catch (System.InvalidOperationException)
        {
            return; // no rig in scene; nothing to measure
        }

        if (ts > 0 && ts != _lastCaptureTs)
        {
            _lastCaptureTs = ts;
            _gazeSamples++;
        }

        float elapsed = Time.unscaledTime - _gazeWindowStart;
        if (elapsed >= 1f)
        {
            _gazeHz = _gazeSamples / elapsed;
            _gazeSamples = 0;
            _gazeWindowStart = Time.unscaledTime;
        }
    }

    private static List<BaseEffect> ActiveEffects()
    {
        var list = new List<BaseEffect>();
        foreach (var e in FindObjectsByType<BaseEffect>(FindObjectsInactive.Exclude))
            if (e.enabled) list.Add(e);
        return list;
    }

    /// <summary>
    /// A/B the whole effect chain: measure frame time with effects on, then with
    /// every active effect disabled, then restore. The difference is what the
    /// symptom shaders actually cost on this machine at this resolution.
    /// </summary>
    private IEnumerator RunEffectBenchmark()
    {
        _benchmarkRunning = true;
        _benchmarkResult = "running...";

        var effects = ActiveEffects();
        if (effects.Count == 0)
        {
            _benchmarkResult = "no effects enabled - enable a symptom first";
            _benchmarkRunning = false;
            yield break;
        }

        float withEffects = 0f;
        yield return MeasureFor(1.5f, r => withEffects = r);

        foreach (var e in effects) e.enabled = false;
        yield return null; // let the change take effect

        float withoutEffects = 0f;
        yield return MeasureFor(1.5f, r => withoutEffects = r);

        foreach (var e in effects) e.enabled = true;

        float delta = withEffects - withoutEffects;
        float pct = withoutEffects > 0f ? (delta / withoutEffects) * 100f : 0f;

        _benchmarkResult =
            $"{effects.Count} effect(s): {withEffects:F2}ms on / {withoutEffects:F2}ms off " +
            $"=> chain costs {delta:F2}ms ({pct:F0}%)";
        Debug.Log("[VipSimDiagnostics] " + _benchmarkResult);
        _benchmarkRunning = false;
    }

    private IEnumerator MeasureFor(float seconds, System.Action<float> result)
    {
        // Discard the first frames: toggling effects triggers material and
        // RenderTexture allocation that is not representative of steady state.
        for (int i = 0; i < 5; i++) yield return new WaitForEndOfFrame();

        float total = 0f;
        int n = 0;
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end)
        {
            yield return new WaitForEndOfFrame();
            total += Time.unscaledDeltaTime * 1000f;
            n++;
        }
        result(n > 0 ? total / n : 0f);
    }

    private string BuildReport()
    {
        float sum = 0f, worst = 0f;
        for (int i = 0; i < _frameCount; i++)
        {
            sum += _frameMs[i];
            if (_frameMs[i] > worst) worst = _frameMs[i];
        }
        float avg = _frameCount > 0 ? sum / _frameCount : 0f;

        var sb = new StringBuilder();
        sb.AppendLine($"frame   {avg:F2} ms avg ({(avg > 0 ? 1000f / avg : 0):F0} fps), worst {worst:F1} ms");
        sb.AppendLine($"target  {Application.targetFrameRate} fps, vSync {QualitySettings.vSyncCount}");

        if (_mainThread.Valid)
            sb.AppendLine($"main    {_mainThread.LastValue / 1_000_000f:F2} ms");
        if (_drawCalls.Valid)
            sb.AppendLine($"draws   {_drawCalls.LastValue}");

        sb.AppendLine($"effects {ActiveEffects().Count} enabled");

        // Whether clicks reach the application underneath. Worth a line of its
        // own: when the native window could not be acquired the overlay silently
        // swallows every click, and nothing on screen says so.
        sb.AppendLine("clicks  " + (TransparentWindow.ClickthroughActive switch
        {
            null  => "ALL CAPTURED - no native window found",
            true  => "passing through to the app below",
            false => "captured by VIP-Sim (pointer is over the panel)",
        }));

        // The rectangle that decision is made against, in screen pixels. Printed
        // because "clicks pass through everywhere" and "the toolbar is not
        // clickable" are the same fault seen from two sides, and only the panel's
        // actual screen rect distinguishes a hidden panel from a broken one.
        var tw = FindAnyObjectByType<TransparentWindow>(FindObjectsInactive.Include);
        if (tw != null && tw.panelRectTransform != null)
        {
            var c = new Vector3[4];
            tw.panelRectTransform.GetWorldCorners(c);
            sb.AppendLine($"panel   ({c[0].x:F0},{c[0].y:F0})-({c[2].x:F0},{c[2].y:F0}) " +
                          $"shown={tw.panelRectTransform.gameObject.activeInHierarchy}");
            var cur = TransparentWindow.CursorPosition;
            var hit = TransparentWindow.LastUiHit;
            sb.AppendLine($"        mouse ({cur.x:F0},{cur.y:F0}) " +
                          $"screen {Screen.width}x{Screen.height} focused={Application.isFocused} " +
                          $"ui-hit={(string.IsNullOrEmpty(hit) ? "-" : hit)}");
        }

        // Gaze
        try
        {
            var gaze = UnitEyeAPI.GetGazeReference();
            sb.AppendLine($"gaze    {_gazeHz:F1} Hz sample rate, {gaze.MeasuredLatencySeconds * 1000f:F0} ms latency");
            sb.AppendLine($"        user {(UnitEyeAPI.IsUserPresent() ? "present" : "absent")}, " +
                          $"backbone {gaze.GazeBackbone}");
        }
        catch (System.InvalidOperationException)
        {
            sb.AppendLine("gaze    (no UnitEye rig active)");
        }

        sb.AppendLine($"bench   {_benchmarkResult}");
        return sb.ToString();
    }

    private void OnGUI()
    {
        if (!showOverlay) return;

        const int w = 470, h = 210;
        var rect = new Rect(10, Screen.height - h - 10, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(rect.x + 8, rect.y + 6, w - 16, h - 12),
                  $"VIP-Sim diagnostics  ({toggleKey} hide, {benchmarkKey} benchmark)\n\n" + BuildReport());
    }
}

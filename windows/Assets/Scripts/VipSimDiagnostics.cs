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
    }

    private void OnDisable()
    {
        if (_mainThread.Valid) _mainThread.Dispose();
        if (_drawCalls.Valid) _drawCalls.Dispose();
    }

    private void Update()
    {
        if (Input.GetKeyDown(quitKey))
        {
            Debug.Log("[VipSimDiagnostics] Quit hotkey pressed.");
            Application.Quit();
        }
        if (Input.GetKeyDown(toggleKey)) showOverlay = !showOverlay;
        if (Input.GetKeyDown(benchmarkKey) && !_benchmarkRunning) StartCoroutine(RunEffectBenchmark());

        _frameMs[_frameIdx] = Time.unscaledDeltaTime * 1000f;
        _frameIdx = (_frameIdx + 1) % Window;
        if (_frameCount < Window) _frameCount++;

        SampleGazeRate();

        if (logIntervalSeconds > 0f && Time.unscaledTime >= _nextLog)
        {
            _nextLog = Time.unscaledTime + logIntervalSeconds;
            Debug.Log("[VipSimDiagnostics] " + BuildReport().Replace('\n', ' '));
        }
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
            sb.AppendLine($"        mouse ({cur.x:F0},{cur.y:F0}) " +
                          $"screen {Screen.width}x{Screen.height} focused={Application.isFocused}");
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

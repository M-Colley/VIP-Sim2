using UnityEngine;

/// <summary>
/// Single owner of the application frame rate.
///
/// Why this exists: the target frame rate was being set from three different
/// places -- TransparentWindow.Start() set 30, ToggleScript.Start() set 60, and
/// on macOS MacCapture.Init() set 60 again. Whichever ran last won, so the
/// effective frame rate depended on Unity's script execution order and could
/// differ between platforms, between builds, and between the editor and a
/// player. Nothing declared ownership, so raising it in one place was silently
/// undone by another.
///
/// Why 60 rather than 30: VIP-Sim is a *gaze-contingent* simulator. At 30 fps a
/// full frame of latency is 33 ms between an eye movement and the simulated
/// scotoma following it, on top of the webcam and inference latency UnitEye
/// already carries. That directly weakens the effect being studied. 30 was
/// presumably chosen to keep background CPU use down, so this keeps that option
/// as an explicit, documented setting rather than an accident of ordering.
/// </summary>
[DefaultExecutionOrder(-1000)] // run before anything that might care
public class FrameRateController : MonoBehaviour
{
    [Tooltip("Target frames per second. 60 keeps gaze-contingent latency to ~17ms; " +
             "30 halves CPU use but doubles the lag between an eye movement and the " +
             "simulation following it. -1 means uncapped.")]
    [SerializeField] private int targetFrameRate = 60;

    [Tooltip("VSync divides the refresh rate and overrides targetFrameRate when non-zero. " +
             "Off by default so the value above is actually honoured.")]
    [SerializeField] private bool enableVSync = false;

    /// <summary>Current target, for display in the settings UI.</summary>
    public static int Current { get; private set; }

    private void Awake()
    {
        Apply();
    }

    /// <summary>Change the cap at runtime, e.g. from a settings slider.</summary>
    public void SetTargetFrameRate(int fps)
    {
        targetFrameRate = fps;
        Apply();
    }

    private void Apply()
    {
        // vSyncCount must be 0 or targetFrameRate is ignored entirely.
        QualitySettings.vSyncCount = enableVSync ? 1 : 0;
        Application.targetFrameRate = enableVSync ? -1 : targetFrameRate;
        Current = Application.targetFrameRate;

        // The overlay must keep rendering while the user works in the app
        // underneath it, which by definition means VIP-Sim is not focused.
        Application.runInBackground = true;
    }
}

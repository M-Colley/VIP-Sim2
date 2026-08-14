// VIP-Sim gaze source.
//
// Originally PJ 13/09/2017; rewritten for UnitEye 1.1 (Barracuda-free, MediaPipe
// Task API + Unity Inference Engine).
//
// Integration notes
// -----------------
// This talks to UnitEye *only* through UnitEyeAPI. The previous version held a
// direct reference to the `Gaze` MonoBehaviour, which is why it stopped
// compiling against UnitEye 1.1 (`Gaze` was replaced by `HomulerGaze`). Going
// through the documented static API keeps VIP-Sim decoupled from UnitEye's
// internal pipeline, which has now been reworked twice.
//
// UnitEyeAPI throws InvalidOperationException when no gaze component is present
// in the scene. That is a *per-call* throw, so the old direct-reference code
// would have thrown every frame. Availability is probed once and cached here,
// and any failure degrades to the mouse rather than taking the simulation down.
//
// The public surface consumed by all 21 symptom effects -- GazeTracker.GetInstance.xy_norm
// -- is unchanged, as are the serialized field names, so scenes and effects
// keep working untouched.

using UnityEngine;
using UnitEye;

public class GazeTracker : MonoBehaviour
{
    // NB: explicit values. These are serialized in VIP_SIM.unity; renumbering
    // would silently repoint the scene at a different source.
    public enum GazeSource
    {
        Fove = 0,   // legacy VR headset path, unused
        UnitEye = 1,
        Mouse = 2,
        None = 3,
    }

    public GazeSource gazeSource = GazeSource.Mouse;

    public Camera Lcam;
    public Camera Rcam;

    /// <summary>
    /// Gaze position, normalised to [0,1] with (0,0) at the bottom-left.
    /// This is what every symptom effect reads.
    /// </summary>
    public Vector2 xy_norm = new Vector2(0.5f, 0.5f);

    [Tooltip("The UnitEye rig in the scene. Activated only while the UnitEye source is selected.")]
    public GameObject unitEye;

    [Header("Gaze visualisation")]
    public bool visualiseGaze;
    public Texture2D crosshairImage;

    [Header("Tracking robustness")]
    [Tooltip("Keep the last valid gaze point when tracking drops out or the user blinks, " +
             "instead of snapping to a default. Prevents the simulated scotoma jumping " +
             "across the screen on a blink.")]
    public bool holdLastGoodGaze = true;

    [Tooltip("Seconds of continuous tracking loss after which the gaze recentres. " +
             "Zero holds the last point indefinitely.")]
    [Range(0f, 10f)]
    public float trackingLossTimeout = 2f;

    [Tooltip("Exponential smoothing applied on top of UnitEye's own filtering. " +
             "0 = off (matches published VIP-Sim behaviour), 1 = frozen.")]
    [Range(0f, 0.95f)]
    public float extraSmoothing = 0f;

    // --- runtime state -----------------------------------------------------

    /// <summary>True when the selected source is actively producing gaze data.</summary>
    public bool IsTracking { get; private set; }

    /// <summary>Last integration error, surfaced for the settings UI. Null when healthy.</summary>
    public string LastError { get; private set; }

    private bool unitEyeActive;
    private bool unitEyeUnavailable;   // latched after a failed probe; avoids per-frame exceptions
    private float lastGoodGazeTime;
    private Vector2 lastGoodGaze = new Vector2(0.5f, 0.5f);

    // Calibration click-capture (see UpdateCalibrationCapture).
    private TransparentWindow transparentWindow;
    private HomulerGazeCalibration calibrationUi;
    private bool calibrationCaptureOn;
    private bool warnedCalibrationBusy;

    // Singleton
    private static GazeTracker instance;
    public static GazeTracker GetInstance
    {
        get
        {
            // FindObjectOfType is obsolete in Unity 6, and its first replacement
            // (FindFirstObjectByType) is itself deprecated in 6.5 for relying on
            // instance-ID ordering. FindAnyObjectByType is the current API.
            if (instance == null)
                instance = FindAnyObjectByType<GazeTracker>();

            return instance;
        }
    }

    private GazeTracker() { }

    private void Awake()
    {
        // Claim the singleton eagerly so effects running in the same frame as the
        // first Update() do not each pay for a scene search.
        if (instance == null)
            instance = this;
    }

    private void OnEnable()
    {
        lastGoodGaze = xy_norm;
        lastGoodGazeTime = Time.time;
    }

    [Header("Calibration")]
    [Tooltip("Key that starts gaze calibration. Uncalibrated gaze is roughly " +
             "head-pose driven and drifts badly; nearly all of UnitEye's accuracy " +
             "comes from the per-user fit done during calibration.")]
    public KeyCode calibrationHotkey = KeyCode.F9;

    /// <summary>
    /// Start UnitEye's gaze calibration. Safe to call from a UI button.
    ///
    /// Calibration takes over the screen until it finishes, then hands control
    /// back (returnAfter), so VIP-Sim keeps running afterwards.
    /// </summary>
    public void StartCalibration()
    {
        if (gazeSource != GazeSource.UnitEye)
        {
            Debug.LogWarning("[GazeTracker] Calibration only applies to the UnitEye gaze source; " +
                             "switch to it first.", this);
            return;
        }

        try
        {
            SetUnitEyeActive(true);
            UnitEyeAPI.GetGazeReference().LoadCalibration();
            // Same-frame capture: the very first left-click must already reach
            // the calibration rather than fall through the overlay.
            UpdateCalibrationCapture();
            Debug.Log("[GazeTracker] Gaze calibration started. " +
                      "Left-click to begin and to advance; ESCAPE or right-click aborts and restores " +
                      "the previous settings.");
        }
        catch (System.InvalidOperationException e)
        {
            LastError = "Cannot calibrate: " + e.Message;
            Debug.LogWarning("[GazeTracker] " + LastError, this);
        }
    }

    /// <summary>
    /// Hold the overlay in full click-capture while UnitEye's calibration UI is
    /// up, and release it the moment the calibration ends.
    ///
    /// Calibration is driven entirely by left-clicks and draws a full-screen
    /// backdrop -- but the overlay is click-through outside VIP-Sim's own panel,
    /// so those clicks fell through into whatever application sat invisibly
    /// BEHIND the backdrop. The user clicked "some underlying window" they could
    /// not even see, and the calibration never advanced because Unity never
    /// received a single click. HomulerGaze enables the calibration component on
    /// LoadCalibration and disables it again on finish/abort, so its enabled
    /// state IS the "calibration in progress" signal.
    /// </summary>
    private void UpdateCalibrationCapture()
    {
        if (calibrationUi == null && unitEye != null)
            calibrationUi = unitEye.GetComponentInChildren<HomulerGazeCalibration>(true);

        bool calibrating = calibrationUi != null && calibrationUi.isActiveAndEnabled;
        if (calibrating == calibrationCaptureOn) return;

        if (transparentWindow == null)
            transparentWindow = FindAnyObjectByType<TransparentWindow>(FindObjectsInactive.Include);
        if (transparentWindow == null) return; // keep trying while the states differ

        if (calibrating) transparentWindow.enableCalibrationState();
        else transparentWindow.disableCalibrationState();
        calibrationCaptureOn = calibrating;

        Debug.Log(calibrating
            ? "[GazeTracker] Calibration UI is up; the overlay now captures every click so they reach the calibration."
            : "[GazeTracker] Calibration UI closed; normal click-through restored.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(calibrationHotkey))
            StartCalibration();

        UpdateCalibrationCapture();

        switch (gazeSource)
        {
            case GazeSource.UnitEye:
                UpdateFromUnitEye();
                break;

            case GazeSource.Mouse:
                SetUnitEyeActive(false);
                UpdateFromMouse();
                IsTracking = true;
                break;

            case GazeSource.None:
                SetUnitEyeActive(false);
                xy_norm = new Vector2(0.5f, 0.5f);
                IsTracking = false;
                break;

            case GazeSource.Fove:
                // Legacy FOVE headset path, removed. Fall back rather than throwing:
                // a scene left on this value should still be usable.
                SetUnitEyeActive(false);
                UpdateFromMouse();
                IsTracking = false;
                break;

            default:
                SetUnitEyeActive(false);
                UpdateFromMouse();
                IsTracking = false;
                break;
        }

        // Defensive clamp: shaders index overlay textures with this and would
        // wrap or clamp unpredictably outside [0,1].
        xy_norm.x = Mathf.Clamp01(xy_norm.x);
        xy_norm.y = Mathf.Clamp01(xy_norm.y);
    }

    private void UpdateFromMouse()
    {
        // TransparentWindow.CursorPosition, not Input.mousePosition: the latter
        // freezes whenever the overlay is not the foreground window -- and the
        // user spends most of a session focused on the application they are
        // inspecting, during which the gaze point must keep following the
        // pointer. Same bottom-left origin, matching xy_norm.
        float w = Screen.width;
        float h = Screen.height;
        if (w <= 0f || h <= 0f) return;

        Vector3 m = TransparentWindow.CursorPosition;
        ApplyGaze(new Vector2(Mathf.Clamp(m.x, 0f, w) / w,
                              Mathf.Clamp(m.y, 0f, h) / h));
    }

    private void UpdateFromUnitEye()
    {
        SetUnitEyeActive(true);

        if (unitEyeUnavailable)
        {
            // Probe failed earlier. Stay on the mouse instead of throwing every frame.
            UpdateFromMouse();
            IsTracking = false;
            return;
        }

        float w = Screen.width;
        float h = Screen.height;
        if (w <= 0f || h <= 0f) return;

        try
        {
            // Do not trust a gaze point taken mid-blink: the iris landmarks are
            // occluded and the estimate swings wildly.
            bool valid = UnitEyeAPI.IsUserPresent() && !UnitEyeAPI.IsBlinking();

            if (valid)
            {
                // GetGazeLocationInScreen returns pixels with a bottom-left
                // origin, which is the convention xy_norm uses.
                Vector2 px = UnitEyeAPI.GetGazeLocationInScreen();
                ApplyGaze(new Vector2(px.x / w, px.y / h));
                IsTracking = true;
                LastError = null;
            }
            else
            {
                HoldOrRecentre();
                IsTracking = false;
            }
        }
        catch (System.InvalidOperationException e)
        {
            // Thrown when no UnitEye rig is present in the scene.
            unitEyeUnavailable = true;
            LastError = "UnitEye is not present in the scene: " + e.Message +
                        " Falling back to mouse-driven gaze.";
            Debug.LogWarning("[GazeTracker] " + LastError, this);
            UpdateFromMouse();
            IsTracking = false;
        }
    }

    private void ApplyGaze(Vector2 normalised)
    {
        if (extraSmoothing > 0f)
        {
            // Frame-rate independent exponential smoothing.
            float a = 1f - Mathf.Pow(extraSmoothing, Time.unscaledDeltaTime * 60f);
            normalised = Vector2.Lerp(xy_norm, normalised, a);
        }

        xy_norm = normalised;
        lastGoodGaze = normalised;
        lastGoodGazeTime = Time.time;
    }

    private void HoldOrRecentre()
    {
        if (!holdLastGoodGaze)
        {
            xy_norm = new Vector2(0.5f, 0.5f);
            return;
        }

        if (trackingLossTimeout > 0f && Time.time - lastGoodGazeTime > trackingLossTimeout)
        {
            // Long dropout (user left, camera covered): ease back to centre so the
            // simulation does not stay stuck in a corner.
            xy_norm = Vector2.Lerp(xy_norm, new Vector2(0.5f, 0.5f), Time.unscaledDeltaTime * 2f);
        }
        else
        {
            xy_norm = lastGoodGaze;
        }
    }

    private void SetUnitEyeActive(bool active)
    {
        if (unitEye == null || unitEyeActive == active) return;

        // Never tear the rig down mid-calibration. Deactivating the GameObject
        // does NOT clear HomulerGazeCalibration.enabled, and the only code that
        // does -- HomulerGaze.UnloadCalibration -- runs from a LateUpdate the
        // now-inactive rig will never execute. The calibration would be frozen
        // rather than cancelled: settings left unrestored, CSV logging left
        // paused, and the still-enabled component springing a fresh full-screen
        // calibration the moment the user switched back to eye tracking.
        // Switching the source away simply takes effect once calibration ends.
        if (!active && calibrationUi != null && calibrationUi.isActiveAndEnabled)
        {
            if (!warnedCalibrationBusy)
            {
                warnedCalibrationBusy = true;
                Debug.LogWarning("[GazeTracker] Gaze source changed while calibration is running; " +
                                 "keeping the UnitEye rig alive until it finishes. " +
                                 "Press Escape or right-click to cancel the calibration.", this);
            }
            return;
        }
        warnedCalibrationBusy = false;

        unitEye.SetActive(active);
        unitEyeActive = active;

        // Also drive UnitEye's own enable/disable so it stops consuming the webcam
        // and running inference while the user is on the mouse source.
        if (unitEyeUnavailable) return;
        try
        {
            if (active)
            {
                UnitEyeAPI.EnableGaze();
                SuppressUnitEyeDebugOverlays();
            }
            else UnitEyeAPI.DisableGaze();
        }
        catch (System.InvalidOperationException)
        {
            // Rig genuinely absent; UpdateFromUnitEye reports this properly.
        }
    }

    /// <summary>
    /// Turn off UnitEye's own on-screen debug drawing.
    ///
    /// HomulerGaze ships with showFaceMesh and showEyes enabled, which draw the
    /// webcam eye crops into the screen corners via GUI.DrawTexture, plus a gaze
    /// dot. That is reasonable for a UnitEye demo scene and completely wrong for
    /// VIP-Sim: this is a transparent click-through overlay sitting on top of the
    /// user's real desktop, so the webcam feed appears over their work and cannot
    /// be dismissed -- UnitEye's own "Hide FaceMesh" button is itself unclickable,
    /// because the overlay is click-through everywhere outside VIP-Sim's panel.
    ///
    /// The Barracuda-era rig had no equivalent, so this only became visible after
    /// the UnitEye 1.1 rig migration.
    ///
    /// Safe with respect to tracking: FaceMeshSolution documents IsRendering and
    /// Annotate as preference storage on the Task API path ("overlay drawing is
    /// not reimplemented"), so none of these gate inference.
    /// </summary>
    private static void SuppressUnitEyeDebugOverlays()
    {
        var gaze = UnitEyeAPI.GetGazeReference();
        if (gaze == null) return;

        gaze.showEyes = false;      // webcam eye crops in the screen corners
        gaze.showFaceMesh = false;  // face-mesh annotation
        gaze.showGazeUI = false;    // UnitEye's IMGUI button panel
        gaze.visualizeAOI = false;  // AOI debug boxes
        gaze.drawDot = false;       // UnitEye's own gaze dot; VIP-Sim has visualiseGaze

        var provider = gaze.Provider;
        if (provider != null)
        {
            provider.AnnotateFaceMesh = false;
            provider.SetRendering(false);
        }
    }

    private void OnGUI()
    {
        if (!visualiseGaze || crosshairImage == null) return;

        // NB: the previous implementation halved the screen width and drew two
        // crosshairs, a leftover from the stereo FOVE rendering path. On a normal
        // monoscopic desktop overlay that placed the marker at half the intended
        // x position. Single crosshair, full width.
        float x = Screen.width * xy_norm.x - crosshairImage.width * 0.5f;
        float y = Screen.height * (1f - xy_norm.y) - crosshairImage.height * 0.5f; // GUI origin is top-left

        GUI.DrawTexture(new Rect(x, y, crosshairImage.width, crosshairImage.height), crosshairImage);
    }
}

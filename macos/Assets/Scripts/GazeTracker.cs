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

    // Singleton
    private static GazeTracker instance;
    public static GazeTracker GetInstance
    {
        get
        {
            // FindObjectOfType is obsolete in Unity 6; FindFirstObjectByType is the replacement.
            if (instance == null)
                instance = FindFirstObjectByType<GazeTracker>();

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

    private void Update()
    {
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
        // Input.mousePosition already uses a bottom-left origin, matching xy_norm.
        float w = Screen.width;
        float h = Screen.height;
        if (w <= 0f || h <= 0f) return;

        Vector3 m = Input.mousePosition;
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

        unitEye.SetActive(active);
        unitEyeActive = active;

        // Also drive UnitEye's own enable/disable so it stops consuming the webcam
        // and running inference while the user is on the mouse source.
        if (unitEyeUnavailable) return;
        try
        {
            if (active) UnitEyeAPI.EnableGaze();
            else UnitEyeAPI.DisableGaze();
        }
        catch (System.InvalidOperationException)
        {
            // Rig genuinely absent; UpdateFromUnitEye reports this properly.
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

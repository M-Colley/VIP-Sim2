using UnityEngine;
using uWindowCapture;

/// <summary>
/// Renders the captured window where the real window actually is, at its real
/// size, instead of blowing every window up to fill the screen.
///
/// Why the old behaviour existed: AlignBoxColliderWithCamera sets the
/// orthographic camera's size from the CAPTURED WINDOW's own collider height
/// (orthographicSize = boxHeight/2 + taskbar/2). uWindowCapture scales the quad
/// to the window's pixel size, so "fit the camera to the quad" means the camera
/// zooms until whatever window you picked exactly fills the view -- a 600x400
/// dialog and a maximised browser both end up full-screen.
///
/// That is worse than a cosmetic problem here. The overlay is click-through, so
/// a click lands on whatever REALLY sits at that desktop position. With the
/// image scaled up and re-centred, what the user sees at a point is not what
/// their click hits -- which is exactly the "I clicked on some underlying
/// window" symptom.
///
/// This component instead maps the desktop 1:1: the camera is sized to show the
/// whole screen, and the quad is placed at the window's own desktop offset. What
/// you see is where it is, and clicks line up. The rest of the desktop stays
/// visible through the transparent overlay and the symptom shaders still cover
/// the whole view.
///
/// The scaling of the quad itself is left to UwcWindowTexture (BaseScale mode,
/// which already produces the correct pixel size); this only owns the camera
/// size and the quad's position, so there is nothing to fight over.
///
/// Existing settings keep working: the Settings zoom field drives the same
/// collider height as before (1 = true 1:1, larger = see more), and the X/Y
/// offsets still move the WindowManager parent.
///
/// F8 toggles back to the legacy fit-to-screen behaviour.
/// </summary>
[DefaultExecutionOrder(50)] // after UwcWindowTexture has sized the quad
public class CaptureWindowPlacement : MonoBehaviour
{
    public enum Mode
    {
        /// <summary>1:1 with the desktop -- the capture sits over the real window.</summary>
        MatchWindowRect,
        /// <summary>Legacy: the selected window is scaled up to fill the screen.</summary>
        FitToScreen,
    }

    [Tooltip("MatchWindowRect draws the capture where the window really is, at its real size, " +
             "so clicks line up with what you see. FitToScreen is the legacy behaviour.")]
    public Mode mode = Mode.MatchWindowRect;

    [Tooltip("The overlay camera (CameraRig/LeftEye). Wired by the editor setup from " +
             "AlignBoxColliderWithCamera.")]
    public Camera targetCamera;

    [Tooltip("Toggle 1:1 placement vs the legacy fill-the-screen behaviour.")]
    public KeyCode toggleKey = KeyCode.F8;

    private AlignBoxColliderWithCamera legacyAlign;
    private UwcWindowTexture tracked;
    private int lastLoggedWindowId = -1;

    private void Awake()
    {
        legacyAlign = GetComponent<AlignBoxColliderWithCamera>();
        if (targetCamera == null && legacyAlign != null) targetCamera = legacyAlign.camera;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            mode = mode == Mode.MatchWindowRect ? Mode.FitToScreen : Mode.MatchWindowRect;
            lastLoggedWindowId = -1;
            Debug.Log($"[CaptureWindowPlacement] Mode: {mode}" +
                      (mode == Mode.MatchWindowRect
                          ? " -- capture drawn 1:1 over the real window; clicks line up."
                          : " -- legacy: selected window scaled up to fill the screen."));
        }

        bool match = mode == Mode.MatchWindowRect;

        if (tracked == null || tracked.window == null)
            tracked = GetComponentInChildren<UwcWindowTexture>();

        float sw = Screen.width, sh = Screen.height;
        bool canPlace = match && targetCamera != null && tracked != null &&
                        tracked.window != null && sw > 1f && sh > 1f;

        // Hand the camera back whenever this component is not actually driving
        // it. Releasing it up-front instead would leave the camera frozen at
        // whatever size it happened to have during the frames between a window
        // being selected and its texture existing.
        if (legacyAlign != null && legacyAlign.enabled == canPlace)
            legacyAlign.enabled = !canPlace;

        if (!canPlace) return;

        var win = tracked.window;

        // World units per desktop pixel. UwcWindowTexture's BaseScale mode gives
        // the quad a world width of (pixels / basePixel) where basePixel is
        // 1000/scalePer1000Pixel, independent of the mesh's own size.
        float worldPerPixel = tracked.scalePer1000Pixel / 1000f;
        if (worldPerPixel <= 0f) return;

        // Zoom: the Settings panel edits this collider's height, and the legacy
        // path turned it into the camera size. Keep it as a multiplier so the
        // control still works and its default (1) means true 1:1.
        float zoom = 1f;
        var box = GetComponentInChildren<BoxCollider>();
        if (box != null && box.size.y > 0f) zoom = box.size.y;

        targetCamera.orthographic = true;
        targetCamera.orthographicSize = sh * 0.5f * worldPerPixel * zoom;

        // Window centre relative to the screen centre, in world units. Desktop y
        // runs downwards, Unity's runs up.
        float dx = (win.x + win.width * 0.5f - sw * 0.5f) * worldPerPixel;
        float dy = (sh * 0.5f - (win.y + win.height * 0.5f)) * worldPerPixel;

        // localPosition, so the Settings X/Y offsets -- which move this
        // WindowManager parent -- still apply on top.
        var t = tracked.transform;
        if (!win.isMinimized) // a minimised window reports a junk rect; hold the last placement
            t.localPosition = new Vector3(dx, dy, t.localPosition.z);

        if (win.id != lastLoggedWindowId)
        {
            lastLoggedWindowId = win.id;
            Debug.Log($"[CaptureWindowPlacement] '{win.title}' desktop ({win.x},{win.y} " +
                      $"{win.width}x{win.height}) on a {sw}x{sh} screen -> drawn 1:1 " +
                      $"(zoom {zoom:F2}, {worldPerPixel * 1000f:F0} world units per 1000px).");
        }
    }

    private void OnDisable()
    {
        // Give the camera back if this component is switched off entirely.
        if (legacyAlign != null) legacyAlign.enabled = true;
    }
}

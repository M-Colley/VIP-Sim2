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
/// zooms until whatever window you picked exactly fills the view -- a 900x600
/// dialog and a maximised browser both end up full-screen.
///
/// That is worse than cosmetic. The overlay is click-through, so a click lands on
/// whatever REALLY sits at that desktop position; with the image scaled up and
/// re-centred, what the user sees at a point is not what their click hits.
///
/// Placement is computed in WORLD space from the camera's own basis rather than
/// as a local offset. AlignBoxColliderWithCamera MOVES the camera as well as
/// resizing it (it assigns camera.transform.position.x), and it runs for the
/// frames before a capture exists -- so by the time this component takes over,
/// the camera is no longer where the scene authored it. Anything expressed
/// relative to the parent inherited that leftover shift and the capture landed
/// visibly off. Deriving from cam.position/right/up is immune to it, and to
/// anything else that moves the rig later.
///
/// Quad SCALING is left to UwcWindowTexture (BaseScale already produces the
/// correct pixel size), so the two never fight.
///
/// Existing settings still work: the Settings zoom field drives the same collider
/// height as before (1 = true 1:1, larger = see more) and the X/Y offsets still
/// nudge the result. F8 toggles back to the legacy fill-the-screen behaviour;
/// F7 grabs the first capturable window without using the list.
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

    [Tooltip("The overlay camera (CameraRig/LeftEye). Wired by the editor setup.")]
    public Camera targetCamera;

    [Tooltip("Toggle 1:1 placement vs the legacy fill-the-screen behaviour.")]
    public KeyCode toggleKey = KeyCode.F8;

    [Tooltip("Capture the first available window without going through the list. " +
             "Exists so the placement can be exercised and its alignment checked.")]
    public KeyCode grabFirstWindowKey = KeyCode.F7;

    [Tooltip("Distance in front of the camera the capture plane sits at. Irrelevant to its " +
             "on-screen size under an orthographic camera; only needs to clear the near plane.")]
    public float planeDistance = 10f;

    [Tooltip("Log the measured on-screen rect against the window's real desktop rect whenever " +
             "the tracked window changes. This is the check that the mapping is truly 1:1.")]
    public bool verifyAlignment = true;

    [Tooltip("Optional correction in screen pixels. Zero means exact 1:1, which is what a correct " +
             "mapping needs; this exists only for cases the desktop geometry cannot express.")]
    public Vector2 offsetPixels = Vector2.zero;

    private AlignBoxColliderWithCamera legacyAlign;
    private UwcWindowTexture tracked;
    private int lastLoggedWindowId = -1;
    private int _lastGrabbedId = -1;
    private float _verifyAt;

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

        if (Input.GetKeyDown(grabFirstWindowKey)) GrabFirstWindow();

        bool match = mode == Mode.MatchWindowRect;

        ResolveTracked();

        float sw = Screen.width, sh = Screen.height;
        bool canPlace = match && targetCamera != null && tracked != null &&
                        tracked.window != null && sw > 1f && sh > 1f;

        // Hand the camera back whenever this component is not actually driving it.
        if (legacyAlign != null && legacyAlign.enabled == canPlace)
            legacyAlign.enabled = !canPlace;

        if (!canPlace) return;

        var win = tracked.window;

        // World units per desktop pixel. UwcWindowTexture's BaseScale gives the
        // quad a world width of (pixels / basePixel), basePixel being
        // 1000/scalePer1000Pixel -- independent of the mesh's own size.
        float worldPerPixel = tracked.scalePer1000Pixel / 1000f;
        if (worldPerPixel <= 0f) return;

        // Zoom: the Settings panel edits this collider's height, and the legacy
        // path turned that into the camera size. Kept as a multiplier so the
        // control still works and its default (1) means true 1:1.
        float zoom = 1f;
        var box = tracked.GetComponent<BoxCollider>();
        if (box != null && box.size.y > 0f) zoom = box.size.y;

        targetCamera.orthographic = true;
        targetCamera.orthographicSize = sh * 0.5f * worldPerPixel * zoom;

        // Window centre relative to the screen centre, in world units. Desktop y
        // runs downwards, Unity's runs up.
        float dx = (win.x + win.width * 0.5f - sw * 0.5f) * worldPerPixel;
        float dy = (sh * 0.5f - (win.y + win.height * 0.5f)) * worldPerPixel;

        // Deliberately NOT offset by this WindowManager's own transform. That
        // position is not a "correction from correct" -- it is authored layout
        // left over from the fit-to-screen design (measured: -0.5, +0.02 world
        // units, i.e. -500px and +20px at 1000px per unit), and adding it moved
        // the capture by exactly that much: the first measurement came back with
        // the right SIZE and a pure (-500, +20) translation.
        //
        // A 1:1 mapping should need no nudge, so any correction is an explicit,
        // zero-by-default field in pixels rather than inherited scene state.
        Vector2 nudge = offsetPixels * worldPerPixel;

        var camT = targetCamera.transform;
        if (!win.isMinimized) // a minimised window reports a junk rect; hold the last placement
        {
            tracked.transform.SetPositionAndRotation(
                camT.position + camT.forward * planeDistance
                              + camT.right * (dx + nudge.x)
                              + camT.up * (dy + nudge.y),
                camT.rotation);
        }

        if (win.id != lastLoggedWindowId)
        {
            lastLoggedWindowId = win.id;
            Debug.Log($"[CaptureWindowPlacement] '{win.title}' desktop ({win.x},{win.y} " +
                      $"{win.width}x{win.height}) on {sw}x{sh}, zoom {zoom:F2}.");
            // Measure after the capture has settled, not on the frame the window
            // changed. This component owns the POSITION; UwcWindowTexture owns the
            // SCALE, and it only applies it once the window becomes valid -- so an
            // immediate check measured a correctly-placed but still-unscaled quad
            // (a 1000x1000 square, the raw mesh) and reported a false failure.
            _verifyAt = Time.unscaledTime + 0.75f;
        }

        if (verifyAlignment && _verifyAt > 0f && Time.unscaledTime >= _verifyAt)
        {
            _verifyAt = 0f;
            VerifyAlignment(win, sw, sh);
        }
    }

    /// <summary>
    /// Finds the texture currently being captured, asking the manager rather than
    /// the transform hierarchy.
    ///
    /// Selecting a different window destroys the old quad and creates a new one
    /// (the list does DisableAllWindows then AddWindow). Unity defers destruction
    /// to the end of the frame, so GetComponentInChildren could keep handing back
    /// the outgoing texture; observed in practice as the capture never being
    /// placed after a switch -- no alignment report was produced for the second
    /// window at all. The manager's own dictionary is authoritative and updated
    /// immediately.
    /// </summary>
    private void ResolveTracked()
    {
        if (tracked != null && tracked.window != null) return;

        var manager = GetComponent<UwcWindowTextureManager>();
        if (manager != null)
        {
            foreach (var kv in manager.windows)
            {
                if (kv.Value == null || kv.Value.window == null) continue;
                tracked = kv.Value;
                return;
            }
        }

        tracked = GetComponentInChildren<UwcWindowTexture>();
    }

    /// <summary>
    /// Projects the placed quad back to screen pixels and compares it with the
    /// window's real desktop rectangle.
    ///
    /// This exists because the thing being fixed cannot be seen from a test
    /// harness: the overlay is a layered window, which BitBlt screen capture does
    /// not include, so a screenshot shows nothing. Projecting the quad through
    /// the same camera that renders it turns "is it aligned?" into a number.
    /// </summary>
    private void VerifyAlignment(UwcWindow win, float sw, float sh)
    {
        var mf = tracked.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        var e = mf.sharedMesh.bounds.extents;
        var t = tracked.transform;
        Vector3 bl = targetCamera.WorldToScreenPoint(t.TransformPoint(new Vector3(-e.x, -e.y, 0f)));
        Vector3 tr = targetCamera.WorldToScreenPoint(t.TransformPoint(new Vector3(e.x, e.y, 0f)));

        // Expected, in Unity screen pixels (origin bottom-left).
        float expL = win.x, expR = win.x + win.width;
        float expB = sh - (win.y + win.height), expT = sh - win.y;

        float err = Mathf.Max(Mathf.Max(Mathf.Abs(bl.x - expL), Mathf.Abs(tr.x - expR)),
                              Mathf.Max(Mathf.Abs(bl.y - expB), Mathf.Abs(tr.y - expT)));

        Debug.Log($"[CaptureWindowPlacement] ALIGN {(err <= 2f ? "PASS" : "FAIL")} " +
                  $"expected ({expL:F0},{expB:F0})-({expR:F0},{expT:F0}) " +
                  $"actual ({bl.x:F0},{bl.y:F0})-({tr.x:F0},{tr.y:F0}) maxError {err:F1}px");
    }

    /// <summary>
    /// Captures the first ordinary window, bypassing the list UI. The list rows
    /// cannot be driven synthetically, which left the placement untestable.
    ///
    /// Public and callable from outside because this component lives on the
    /// WindowManager, which is INACTIVE until the user opens the window list --
    /// so its own Update, and therefore its own hotkey, does not run at startup.
    /// </summary>
    public void GrabFirstWindow()
    {
        var manager = GetComponent<UwcWindowTextureManager>();
        if (manager == null)
        {
            Debug.LogWarning("[CaptureWindowPlacement] No UwcWindowTextureManager here.");
            return;
        }

        // Eligible windows in a stable order, so repeated presses cycle rather
        // than re-grabbing the same one -- which is what makes it possible to
        // check the alignment against several different window geometries.
        var candidates = new System.Collections.Generic.List<UwcWindow>();
        foreach (var kv in UwcManager.windows)
        {
            var w = kv.Value;
            if (w == null || !w.isAlive || w.isDesktop || w.isChild) continue;
            if (w.isMinimized || w.width < 200 || w.height < 200) continue;
            if (string.IsNullOrEmpty(w.title)) continue;
            if (w.title.ToLower().Replace("-", "").Replace("_", "").Contains("vipsim")) continue;
            candidates.Add(w);
        }
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[CaptureWindowPlacement] No capturable window found.");
            return;
        }
        candidates.Sort((a, b) => a.id.CompareTo(b.id));

        int next = 0;
        for (int i = 0; i < candidates.Count; i++)
            if (candidates[i].id == _lastGrabbedId) { next = (i + 1) % candidates.Count; break; }
        var pick = candidates[next];

        // One capture at a time, matching what selecting from the list does
        // (DisableAllWindows before AddWindow). Without this a second grab left
        // an extra quad parented here, and the placement -- which resolves the
        // tracked texture with GetComponentInChildren -- would keep driving the
        // first one while the second sat wherever it was spawned.
        foreach (var existing in new System.Collections.Generic.List<int>(manager.windows.Keys))
        {
            var w = UwcManager.Find(existing);
            if (w != null) manager.RemoveWindowTexture(w);
        }

        manager.AddWindowTexture(pick);
        _lastGrabbedId = pick.id;
        tracked = null;          // re-resolve next frame
        lastLoggedWindowId = -1; // force a fresh alignment report
        Debug.Log($"[CaptureWindowPlacement] Grabbed '{pick.title}' " +
                  $"({pick.x},{pick.y} {pick.width}x{pick.height}) " +
                  $"[{next + 1}/{candidates.Count}].");
    }

    private void OnDisable()
    {
        if (legacyAlign != null) legacyAlign.enabled = true;
    }
}

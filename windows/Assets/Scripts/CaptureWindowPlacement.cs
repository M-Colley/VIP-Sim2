using System.Linq;
using UnityEngine;
using uWindowCapture;

/// <summary>
/// Draws the captured window where the real window is, at its real size, instead
/// of scaling every window up to fill the screen.
///
/// Why this matters beyond looks: the overlay is click-through, so a click lands
/// wherever the pointer really is ON THE DESKTOP. The legacy behaviour scales the
/// capture up and re-centres it, so what the user sees at a point is not what
/// their click hits -- they click a button they can see and the click lands
/// somewhere else in the target application, or on empty desktop. Reported as
/// "I click through it and cannot engage with it at all".
///
/// Mechanism being replaced: AlignBoxColliderWithCamera sets the ORTHOGRAPHIC
/// camera's size from the captured window's own collider height, and
/// UwcWindowTexture scales the quad to the window's pixel size -- so "fit the
/// camera to the quad" zooms until whichever window was picked fills the view.
/// Here the camera is instead sized to show the whole screen, and the quad is
/// placed at the window's own desktop offset, giving a 1:1 mapping.
///
/// Deliberately narrow: this owns the camera SIZE and the quad POSITION and
/// nothing else. Quad scaling stays with UwcWindowTexture, which already produces
/// the correct pixel size. It does not disable cameras and does not touch
/// AlignBoxColliderWithCamera's own logic -- an earlier attempt did both and broke
/// rendering outright.
///
/// F8 toggles back to the legacy behaviour at runtime; F7 (on VipSimDiagnostics)
/// captures a window without using the list.
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

    [Tooltip("MatchWindowRect draws the capture where the window really is, so clicks line up " +
             "with what you see. FitToScreen is the legacy fill-the-screen behaviour.")]
    public Mode mode = Mode.MatchWindowRect;

    [Tooltip("The overlay camera (CameraRig/LeftEye). Wired by the editor setup.")]
    public Camera targetCamera;

    public KeyCode toggleKey = KeyCode.F8;

    [Tooltip("Distance in front of the camera the capture plane sits at. Under an orthographic " +
             "camera this does not affect on-screen size; it only has to clear the near plane.")]
    public float planeDistance = 10f;

    [Tooltip("Optional correction in screen pixels. Zero means exact 1:1, which is what a correct " +
             "mapping needs.")]
    public Vector2 offsetPixels = Vector2.zero;

    [Tooltip("Log the measured on-screen rect against the window's real desktop rect. This is the " +
             "check that the mapping is truly 1:1.")]
    public bool verifyAlignment = true;

    private AlignBoxColliderWithCamera legacyAlign;
    private UwcWindowTexture tracked;
    private int lastLoggedWindowId = -1;
    private int lastGrabbedId = -1;
    private float verifyAt;

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
            Debug.Log($"[CaptureWindowPlacement] Mode: {mode}");
        }

        ResolveTracked();

        float sw = Screen.width, sh = Screen.height;
        bool canPlace = mode == Mode.MatchWindowRect && targetCamera != null &&
                        tracked != null && tracked.window != null && sw > 1f && sh > 1f;

        // Exactly one driver may own the camera size. Released whenever this
        // component is not actually driving, so the camera is never left frozen
        // between a window being selected and its texture existing.
        if (legacyAlign != null && legacyAlign.enabled == canPlace)
            legacyAlign.enabled = !canPlace;

        if (!canPlace) return;

        var win = tracked.window;

        // World units per desktop pixel: UwcWindowTexture's BaseScale mode gives
        // the quad a world width of (pixels / basePixel), basePixel being
        // 1000/scalePer1000Pixel -- independent of the mesh's own size.
        float worldPerPixel = tracked.scalePer1000Pixel / 1000f;
        if (worldPerPixel <= 0f) return;

        // Zoom: the Settings panel edits this collider's height and the legacy path
        // turned that into the camera size. Kept as a multiplier so the control
        // still works, with its default (1) meaning true 1:1.
        float zoom = 1f;
        var box = tracked.GetComponent<BoxCollider>();
        if (box != null && box.size.y > 0f) zoom = box.size.y;

        targetCamera.orthographicSize = sh * 0.5f * worldPerPixel * zoom;

        // Window centre relative to the screen centre, in world units. Desktop y
        // runs downwards, Unity's runs up.
        float dx = (win.x + win.width * 0.5f - sw * 0.5f) * worldPerPixel + offsetPixels.x * worldPerPixel;
        float dy = (sh * 0.5f - (win.y + win.height * 0.5f)) * worldPerPixel + offsetPixels.y * worldPerPixel;

        // Positioned in WORLD space from the camera's own basis. AlignBoxColliderWithCamera
        // moves the camera as well as resizing it, so anything expressed relative to
        // the parent inherits that shift; deriving from cam.position/right/up does not.
        var camT = targetCamera.transform;
        if (!win.isMinimized) // a minimised window reports a junk rect; hold the last placement
        {
            tracked.transform.SetPositionAndRotation(
                camT.position + camT.forward * planeDistance + camT.right * dx + camT.up * dy,
                camT.rotation);
        }

        if (win.id != lastLoggedWindowId)
        {
            lastLoggedWindowId = win.id;
            Debug.Log($"[CaptureWindowPlacement] '{win.title}' desktop ({win.x},{win.y} " +
                      $"{win.width}x{win.height}) on {sw}x{sh}, zoom {zoom:F2}.");
            // Measured after the capture settles: this owns the position but
            // UwcWindowTexture owns the scale, and it only applies that once the
            // window becomes valid. Checking immediately measured a correctly
            // placed but still-unscaled quad and reported a false failure.
            verifyAt = Time.unscaledTime + 0.75f;
        }

        if (verifyAlignment && verifyAt > 0f && Time.unscaledTime >= verifyAt)
        {
            verifyAt = 0f;
            VerifyAlignment(win, sw, sh);
        }
    }

    /// <summary>
    /// Finds the texture currently being captured by asking the manager, not the
    /// transform hierarchy: selecting a different window destroys the old quad and
    /// creates a new one, and Unity defers destruction to end of frame, so
    /// GetComponentInChildren can keep returning the outgoing texture -- leaving
    /// the newly selected window never placed.
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
    /// Projects the placed quad back through the camera that renders it and
    /// compares the result with the window's real desktop rectangle, so "is it
    /// aligned?" is a number rather than an impression.
    /// </summary>
    private void VerifyAlignment(UwcWindow win, float sw, float sh)
    {
        var mf = tracked.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        var e = mf.sharedMesh.bounds.extents;
        var t = tracked.transform;
        Vector3 bl = targetCamera.WorldToScreenPoint(t.TransformPoint(new Vector3(-e.x, -e.y, 0f)));
        Vector3 tr = targetCamera.WorldToScreenPoint(t.TransformPoint(new Vector3(e.x, e.y, 0f)));

        float expL = win.x, expR = win.x + win.width;
        float expB = sh - (win.y + win.height), expT = sh - win.y;
        float err = Mathf.Max(Mathf.Max(Mathf.Abs(bl.x - expL), Mathf.Abs(tr.x - expR)),
                              Mathf.Max(Mathf.Abs(bl.y - expB), Mathf.Abs(tr.y - expT)));

        var rend = tracked.GetComponent<Renderer>();
        var mat = rend != null ? rend.sharedMaterial : null;
        Debug.Log($"[CaptureWindowPlacement] ALIGN {(err <= 2f ? "PASS" : "FAIL")} " +
                  $"expected ({expL:F0},{expB:F0})-({expR:F0},{expT:F0}) " +
                  $"actual ({bl.x:F0},{bl.y:F0})-({tr.x:F0},{tr.y:F0}) maxError {err:F1}px | " +
                  $"drawn: winTex={(win.texture == null ? "null" : win.texture.width + "x" + win.texture.height)} " +
                  $"matTex={(mat == null || mat.mainTexture == null ? "null" : mat.mainTexture.width + "x" + mat.mainTexture.height)} " +
                  $"rend={(rend == null ? "none" : rend.enabled + "/" + rend.isVisible)} " +
                  $"camSize={targetCamera.orthographicSize:F3} ortho={targetCamera.orthographic}");
    }

    /// <summary>
    /// Captures the next ordinary window, bypassing the list UI. Public because
    /// the hotkey lives on VipSimDiagnostics -- this object is inactive until the
    /// window list is opened, so its own Update does not run at startup.
    /// </summary>
    public void GrabNextWindow()
    {
        var manager = GetComponent<UwcWindowTextureManager>();
        if (manager == null) return;

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
            if (candidates[i].id == lastGrabbedId) { next = (i + 1) % candidates.Count; break; }
        var pick = candidates[next];

        // Go through the list item's own OnClick, NOT straight to
        // AddWindowTexture.
        //
        // UwcWindowList.thereIsActiveWindow is what gates the whole simulation --
        // HideMenu and HideImpairmentSelection both read it -- and it is computed
        // from the LIST ITEMS' windowTexture, not from the texture manager.
        // Adding a texture directly therefore produced a correctly placed, fully
        // bound, invisible capture: the simulation stayed hidden because nothing
        // ever told the list a window was active. Every "the capture renders
        // black" screenshot taken with this shortcut was measuring the shortcut,
        // not the app.
        var item = FindObjectsByType<UwcWindowListItem>(FindObjectsInactive.Include,
                                                        FindObjectsSortMode.None)
            .FirstOrDefault(i => i != null && i.window != null && i.window.id == pick.id);

        if (item != null)
        {
            item.OnClick();
        }
        else
        {
            // No list row for it (list not built yet); fall back, and say so,
            // because the simulation will not appear in this state.
            foreach (var id in new System.Collections.Generic.List<int>(manager.windows.Keys))
            {
                var w = UwcManager.Find(id);
                if (w != null) manager.RemoveWindowTexture(w);
            }
            manager.AddWindowTexture(pick);
            Debug.LogWarning("[CaptureWindowPlacement] No list row for that window; captured it " +
                             "directly, which does NOT set UwcWindowList.thereIsActiveWindow, so the " +
                             "simulation will stay hidden.");
        }

        lastGrabbedId = pick.id;
        tracked = null;
        lastLoggedWindowId = -1;
        Debug.Log($"[CaptureWindowPlacement] Grabbed '{pick.title}' " +
                  $"({pick.x},{pick.y} {pick.width}x{pick.height}) [{next + 1}/{candidates.Count}] " +
                  $"via {(item != null ? "the list row" : "the manager (fallback)")}.");
    }

    private void OnDisable()
    {
        if (legacyAlign != null) legacyAlign.enabled = true;
    }
}

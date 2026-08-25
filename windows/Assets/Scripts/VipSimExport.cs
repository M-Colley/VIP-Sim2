using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Save an image of the simulation, as the user is seeing it.
///
/// Until now VIP-Sim persuaded exactly one person, standing at one screen, and left nothing
/// behind. The only way to get a picture out was F6, which is a developer hotkey behind
/// -vipsim-dev and captures the whole framebuffer including VIP-Sim's own interface. That is
/// a diagnostic, not a deliverable.
///
/// What a designer actually needs is the thing they just looked at, in a form that goes into
/// a review, a ticket or a slide: the captured window with the symptoms applied, and no
/// simulator chrome in the frame.
///
/// Rendered rather than screenshotted. ScreenCapture takes the backbuffer, which contains
/// the toolbar, the effect list, and whatever IMGUI happened to be up -- hiding all of that
/// first and putting it back afterwards is several frames of surgery on a UI this project has
/// broken repeatedly. Rendering the effect camera into a texture with the interface layer
/// culled asks for exactly what is wanted and touches nothing.
///
/// A profile is written beside the image. The two together are the useful artefact: the
/// picture shows what it looked like, and the .json loads back into VIP-Sim to reproduce it.
/// It is the same format the Save button writes, so it is a profile, not a log.
/// </summary>
public class VipSimExport : MonoBehaviour
{
    /// <summary>How long the confirmation stays on screen.</summary>
    private const float NoticeSeconds = 5f;

    private static string _notice;
    private static float _noticeUntil;
    private GUIStyle _style;

    public static void Install(GameObject host)
    {
        if (host.GetComponent<VipSimExport>() == null)
            host.AddComponent<VipSimExport>();
    }

    /// <summary>Wired to the toolbar button. Public so the Button onClick can find it.</summary>
    public void SaveSimulatedView()
    {
        try
        {
            StartCoroutine(Save());
        }
        catch (Exception e)
        {
            // Never let this take the application down: it is a convenience, and it runs on a
            // click from a click-through overlay where an exception has nowhere to surface.
            Debug.LogError($"[VipSimExport] could not save the image: {e}");
            Show("VIP-Sim could not save the image. See the log.");
        }
    }

    private System.Collections.IEnumerator Save()
    {
        var camera = FindEffectCamera();
        if (camera == null)
        {
            Show("Nothing to save yet.");
            Debug.LogWarning("[VipSimExport] no camera to render; nothing saved.");
            yield break;
        }

        RectInt crop = CapturedWindowRect();
        if (crop.width <= 0 || crop.height <= 0)
        {
            Show("Pick a window first, then save.");
            Debug.LogWarning("[VipSimExport] no window is being captured; nothing saved.");
            yield break;
        }

        // Pin the gaze to the middle of the captured window, then let a WHOLE FRAME run
        // before rendering.
        //
        // The frame is the part that matters. Effects push the gaze into their shaders from
        // their own Update -- myFieldLoss does exactly this -- so rendering immediately after
        // setting it renders with LAST frame's value: the pointer, which is on the Save
        // button in the corner at that moment. That is the reported fault, and setting the
        // gaze without waiting does not fix it. One frame gives every effect its Update.
        //
        // The window, not the screen: the image is cropped to the window, so a screen-centred
        // gaze still lands off-centre in the file.
        GazeTracker.Forced = new Vector2(
            Mathf.Clamp01((crop.x + crop.width * 0.5f) / Mathf.Max(1, Screen.width)),
            Mathf.Clamp01(1f - (crop.y + crop.height * 0.5f) / Mathf.Max(1, Screen.height)));

        Texture2D image;
        try
        {
            yield return null;
            image = Render(camera, crop);
        }
        finally
        {
            // Always, so a failure cannot leave the live overlay staring at one spot.
            GazeTracker.Forced = null;
        }

        if (image == null)
        {
            Show("VIP-Sim could not save the image. See the log.");
            yield break;
        }

        string folder = OutputFolder();
        Directory.CreateDirectory(folder);

        // Seconds in the name, because a designer comparing two severities saves twice in a
        // row and neither should overwrite the other.
        string stamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
        string png = Path.Combine(folder, $"VIP-Sim {stamp}.png");
        string json = Path.Combine(folder, $"VIP-Sim {stamp}.json");

        File.WriteAllBytes(png, image.EncodeToPNG());
        Destroy(image);

        string symptoms = WriteProfileBeside(json, stamp);

        Debug.Log($"[VipSimExport] saved {png}" +
                  (symptoms == null ? "" : $" showing {symptoms}"));
        Show($"Saved to {folder}");
    }

    /// <summary>
    /// Render the simulation into a texture, cropped to the window it is simulating.
    ///
    /// The interface layer is culled for the duration. Both of the camera properties touched
    /// here are restored before returning, including on the failure path -- this camera is
    /// the overlay, and leaving it pointed at a texture would blank the whole screen.
    /// </summary>
    private static Texture2D Render(Camera camera, RectInt crop)
    {
        int w = Screen.width, h = Screen.height;
        if (w <= 0 || h <= 0) return null;

        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture;
        int previousMask = camera.cullingMask;
        var previousActive = RenderTexture.active;

        try
        {
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) camera.cullingMask &= ~(1 << ui);

            // The gaze was pinned by the caller a frame ago; see Save.
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;

            // ReadPixels measures from the BOTTOM-left; window rectangles are top-left.
            int y = Mathf.Clamp(h - (crop.y + crop.height), 0, h);
            int x = Mathf.Clamp(crop.x, 0, w);
            int cw = Mathf.Clamp(crop.width, 1, w - x);
            int ch = Mathf.Clamp(crop.height, 1, h - y);

            // RGB24, so the file is opaque. The overlay's alpha is meaningful on screen and
            // meaningless in a PNG someone opens later -- carrying it through produces an
            // image that looks empty in most viewers.
            var shot = new Texture2D(cw, ch, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(x, y, cw, ch), 0, 0);
            shot.Apply();
            return shot;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.cullingMask = previousMask;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    /// <summary>
    /// The rectangle of the window being simulated, in this screen's coordinates.
    ///
    /// Same conversion as the capture placement, and for the same reason: the plugin reports
    /// windows in global desktop coordinates, which are only this screen's coordinates when
    /// the overlay happens to be on the primary display.
    /// </summary>
    private static RectInt CapturedWindowRect()
    {
// UNITY_STANDALONE_WIN alone, never with UNITY_EDITOR_WIN: the macOS project is edited on
// a Windows machine, where UNITY_EDITOR_WIN is true and neither uWindowCapture nor
// OverlayScreen exists. Keying on the BUILD TARGET is what keeps the two projects apart.
#if UNITY_STANDALONE_WIN
        foreach (var t in FindObjectsByType<uWindowCapture.UwcWindowTexture>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var win = t.window;
            if (win == null || win.width <= 0 || win.height <= 0 || win.isMinimized) continue;
            if (!OverlayScreen.TryGetRect(out RectInt overlay)) continue;

            return new RectInt(win.x - overlay.xMin, win.y - overlay.yMin, win.width, win.height);
        }

        // Nothing captured. Refusing beats writing a frame that is empty by construction.
        return new RectInt(0, 0, 0, 0);
#else
        // macOS composes its capture differently -- MacCapture letterboxes the window onto a
        // fixed plane rather than placing it over the real one -- so there is no desktop
        // rectangle to crop to. Save the whole view instead of refusing; it still contains
        // the simulation, and it still excludes the interface.
        return new RectInt(0, 0, Screen.width, Screen.height);
#endif
    }

    private static Camera FindEffectCamera()
    {
        // The effects live on the camera tagged LeftEye; the diagnostics report it by that
        // name. Camera.main is not it -- nothing in this scene carries the MainCamera tag,
        // which is why the diagnostics line prints an empty orthographic size.
        Camera best = null;
        foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Exclude,
                                                    FindObjectsSortMode.None))
        {
            if (!c.enabled) continue;
            if (c.name == "LeftEye") return c;
            if (best == null || c.depth > best.depth) best = c;
        }
        return best;
    }

    /// <summary>
    /// Somewhere the user will find it. Pictures on Windows and macOS; on a Linux box that
    /// does not report one, next to the log rather than nowhere.
    /// </summary>
    private static string OutputFolder()
    {
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        string root = string.IsNullOrEmpty(pictures) ? Application.persistentDataPath : pictures;
        return Path.Combine(root, "VIP-Sim");
    }

    /// <summary>
    /// Write the simulation's settings beside the image, as a profile that loads back.
    /// Returns the symptoms it recorded, for the log, or null if it could not be written.
    /// </summary>
    private static string WriteProfileBeside(string path, string stamp)
    {
        var manager = FindAnyObjectByType<SettingsManager>(FindObjectsInactive.Include);
        if (manager == null) return null;

        try
        {
            var profile = ProfileBinder.Capture(manager, $"saved-{stamp}",
                                                "Saved alongside the image of the same name.");
            File.WriteAllText(path, profile.ToString(Newtonsoft.Json.Formatting.Indented));

            var filters = profile["filters"] as Newtonsoft.Json.Linq.JArray;
            return filters == null ? null : $"{filters.Count} symptom(s)";
        }
        catch (Exception e)
        {
            // The image is the point; the profile is a bonus. One failing must not lose both.
            Debug.LogWarning($"[VipSimExport] saved the image but not the profile: {e.Message}");
            return null;
        }
    }

    private static void Show(string message)
    {
        _notice = message;
        _noticeUntil = Time.unscaledTime + NoticeSeconds;
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(_notice) || Time.unscaledTime > _noticeUntil) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.018f, 14f, 34f)),
                wordWrap = false,
            };
            _style.normal.textColor = Color.white;
        }

        var size = _style.CalcSize(new GUIContent(_notice));
        float pad = size.y * 0.8f;
        var rect = new Rect((Screen.width - (size.x + pad * 2f)) * 0.5f,
                            Screen.height * 0.18f, size.x + pad * 2f, size.y + pad);

        float left = _noticeUntil - Time.unscaledTime;
        float alpha = Mathf.Clamp01(left / 1.5f);

        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.78f * alpha);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(rect, _notice, _style);
        GUI.color = previous;
    }
}

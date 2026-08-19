#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Draws the captured screen in front of the camera, so the effects have something to act on.
///
/// On Windows this surface is not ours: uWindowCapture spawns a quad, owns its transform,
/// and AlignBoxColliderWithCamera moves it to the captured window's real screen rectangle
/// so one captured pixel lands on one screen pixel. None of that exists on Linux -- the
/// portal hands over a texture and nothing else -- so without this the camera renders an
/// empty transparent clear, all nineteen image effects run over nothing, and VIP-Sim is an
/// overlay with no image in it. Every part reports success: the portal streams, the
/// texture updates, the presenter composites. There is simply nothing drawn.
///
/// The geometry is simpler here than on Windows because the portal gives a whole output
/// rather than one window: the quad fills the camera's view. It keeps the capture's own
/// aspect ratio rather than stretching to the screen's, which is the same mistake that had
/// to be fixed on macOS.
/// </summary>
[DefaultExecutionOrder(-100)]   // place the surface before anything reads the frame
public class LinuxCaptureSurface : MonoBehaviour
{
    private Camera _cam;
    private Renderer _renderer;
    private Transform _quad;
    private Texture _shown;
    private int _reportIn = 120;   // frames, so the first render has happened

    public static void Install(GameObject host)
    {
        if (host.GetComponent<LinuxCaptureSurface>() == null)
            host.AddComponent<LinuxCaptureSurface>();
    }

    private void Start()
    {
        _cam = PresentingCamera();
        if (_cam == null)
        {
            Debug.LogWarning("[LinuxCaptureSurface] no camera renders to the screen; the " +
                             "captured image has nowhere to go.");
            enabled = false;
            return;
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "VipSimCaptureSurface";
        Destroy(go.GetComponent<Collider>());          // nothing raycasts against this
        go.transform.SetParent(_cam.transform, false);

        _quad = go.transform;
        _renderer = go.GetComponent<Renderer>();

        // Shader.Find only finds what survived build-time stripping, and a shader nothing
        // in the project references does not survive. A null here yields a material that
        // draws nothing at all, with no exception and no log line -- the captured screen
        // simply never appears. VipSimBuild keeps this one in Always Included Shaders; the
        // check is here because that is a setting somebody can undo without knowing what
        // it was for.
        var shader = Shader.Find("Unlit/Texture");
        if (shader == null)
        {
            Debug.LogError("[LinuxCaptureSurface] the shader 'Unlit/Texture' was stripped from " +
                           "this build, so the captured screen cannot be drawn. Add it to " +
                           "Project Settings > Graphics > Always Included Shaders.");
            enabled = false;
            return;
        }
        _renderer.material = new Material(shader);
        _renderer.enabled = false;                     // until there is something to show

        Debug.Log("[LinuxCaptureSurface] surface ready; waiting for a source.");
    }

    private void LateUpdate()
    {
        if (_cam == null || _quad == null) return;

        var tex = LinuxCapture.Texture;
        if (tex == null) { _renderer.enabled = false; return; }

        if (!ReferenceEquals(tex, _shown))
        {
            _shown = tex;
            _renderer.material.mainTexture = tex;
            _renderer.enabled = true;
            Debug.Log($"[LinuxCaptureSurface] showing {tex.width}x{tex.height}.");
        }

        // Fit the view, keeping the capture's own shape. The camera is orthographic and
        // pinned to the screen, so its half-height in world units is orthographicSize.
        float viewH = _cam.orthographicSize * 2f;
        float viewW = viewH * _cam.aspect;
        float texAspect = (float)tex.width / Mathf.Max(1, tex.height);

        float w = viewW, h = viewW / texAspect;
        if (h > viewH) { h = viewH; w = viewH * texAspect; }

        _quad.localScale = new Vector3(w, h, 1f);
        _quad.localPosition = new Vector3(0f, 0f, _cam.nearClipPlane + 0.01f);

        // Turned to face the camera. Unity's Quad primitive is built in the XY plane with
        // its normal along -Z, so parented in front of a camera that looks down +Z it
        // presents its back face and is culled: the surface exists, reports its size, and
        // draws nothing.
        _quad.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // One report, a couple of seconds in, of what is actually being drawn. The centre
        // pixel is the useful half: a capture that is entirely black is not necessarily
        // broken -- on a whole-output capture it means the only thing on screen was
        // VIP-Sim's own window, which is the feedback loop described in docs/LINUX_PORT.md
        // and reads exactly like a dead capture.
        if (_reportIn > 0 && --_reportIn == 0)
        {
            var t2d = tex as Texture2D;
            string sample = "n/a";
            if (t2d != null)
            {
                try
                {
                    var c = t2d.GetPixel(t2d.width / 2, t2d.height / 2);
                    sample = $"RGBA({c.r:F2},{c.g:F2},{c.b:F2},a={c.a:F2})";
                }
                catch (System.Exception e) { sample = "unreadable: " + e.GetType().Name; }
            }
            Debug.Log($"[LinuxCaptureSurface] drawing {tex.width}x{tex.height} on {_cam.name}, " +
                      $"centre pixel {sample}, visible={_renderer.isVisible}.");
        }
    }

    /// <summary>
    /// The camera that draws the screen: enabled, no render texture, highest depth.
    /// Camera.main is not used because it needs the MainCamera tag, and this scene's
    /// camera is named for the eye it came from rather than tagged.
    /// </summary>
    private static Camera PresentingCamera()
    {
        Camera best = null;
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (!c.isActiveAndEnabled || c.targetTexture != null) continue;
            if (best == null || c.depth > best.depth) best = c;
        }
        return best;
    }

    private void OnDisable()
    {
        if (_quad != null) Destroy(_quad.gameObject);
        _quad = null;
    }
}
#endif

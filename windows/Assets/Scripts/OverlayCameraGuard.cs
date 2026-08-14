using UnityEngine;

/// <summary>
/// Makes the overlay's cameras render like an overlay: transparent background,
/// and exactly one of them.
///
/// Two faults, both visible in a screenshot of the player's own framebuffer:
///
/// 1. A camera was clearing to the SKYBOX. VIP-Sim composites over the live
///    desktop through a layered window, so anything other than a transparent
///    clear makes the overlay opaque -- the desktop disappears behind a blue sky
///    and a brown ground plane, with the captured window floating in the middle.
///    A skybox clear is never correct for this application.
///
/// 2. The rig carries TWO enabled, full-screen cameras at the SAME depth 0 --
///    LeftEye and RightEye, both at the origin with all-layers culling masks --
///    left over from the old FOVE stereo path. Render order between equal-depth
///    cameras is undefined, so the two fought over the frame: whichever ran last
///    overwrote the other. RightEye is also PERSPECTIVE while the capture
///    placement and the symptom shaders assume the ORTHOGRAPHIC LeftEye, so the
///    frame that reached the screen was the one none of the maths was written
///    for. Only the orthographic eye is kept.
///
/// Installed via RuntimeInitializeOnLoadMethod so it cannot be lost by a scene
/// edit, and it re-asserts for a few seconds because other scripts (and Unity's
/// own camera setup) touch these fields during start-up.
/// </summary>
public static class OverlayCameraGuard
{
    private const float AssertForSeconds = 5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var go = new GameObject("~VipSimOverlayCameraGuard") { hideFlags = HideFlags.HideAndDontSave };
        go.AddComponent<Runner>();
        Object.DontDestroyOnLoad(go);
    }

    private class Runner : MonoBehaviour
    {
        private float _until;
        private bool _reported;

        private void Start() => _until = Time.unscaledTime + AssertForSeconds;

        private void LateUpdate()
        {
            if (Time.unscaledTime > _until) { enabled = false; return; }

            Camera keep = null;
            var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            // Prefer the orthographic camera: the capture placement and the
            // symptom shaders are written against it.
            foreach (var c in cams)
                if (c.targetTexture == null && c.orthographic) { keep = c; break; }
            if (keep == null)
                foreach (var c in cams)
                    if (c.targetTexture == null) { keep = c; break; }
            if (keep == null) return;

            foreach (var c in cams)
            {
                if (c.targetTexture != null) continue; // render-texture cameras are not the overlay

                if (c != keep)
                {
                    if (c.enabled)
                    {
                        c.enabled = false;
                        Report($"disabled redundant camera '{c.name}' (ortho={c.orthographic})");
                    }
                    continue;
                }

                if (c.clearFlags != CameraClearFlags.SolidColor)
                {
                    Report($"'{c.name}' was clearing with {c.clearFlags}; a transparent overlay must clear to a transparent colour");
                    c.clearFlags = CameraClearFlags.SolidColor;
                }
                // Alpha 0 is what makes the desktop show through; RGB 0 keeps the
                // untouched area from tinting the composite.
                if (c.backgroundColor.a != 0f)
                    c.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
        }

        private void Report(string what)
        {
            Debug.Log($"[OverlayCameraGuard] {what}.");
            _reported = true;
        }

        private void OnDisable()
        {
            if (_reported) Debug.Log("[OverlayCameraGuard] Overlay cameras settled.");
        }
    }
}

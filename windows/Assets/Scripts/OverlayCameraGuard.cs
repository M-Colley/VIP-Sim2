using UnityEngine;

/// <summary>
/// Leaves exactly one camera rendering to the screen.
///
/// The rig carries TWO enabled, full-screen cameras at the SAME depth 0 --
/// LeftEye (orthographic, clears to a transparent solid colour) and RightEye
/// (perspective, clears to the SKYBOX) -- both at the origin with all-layers
/// culling masks, left over from the old FOVE stereo path. Render order between
/// equal-depth cameras is undefined, so they fight over the frame and whichever
/// runs last wins.
///
/// RightEye winning is what puts a blue sky and a brown ground plane on screen
/// with the capture floating in the middle of it. A skybox clear also makes the
/// overlay opaque, which is fatal on its own for something that composites over
/// the live desktop through a layered window.
///
/// The legacy fit-to-screen mode hid this by accident: it blows the capture quad
/// up until it fills the view, so LeftEye's opaque quad painted over whatever
/// RightEye had drawn. Placing the capture at its true 1:1 size stops covering
/// the whole screen, and the sky reappears -- which is why this looked like a
/// regression of the placement work rather than a pre-existing rig fault.
///
/// Only the redundant camera is disabled. Clear flags are deliberately NOT
/// touched: LeftEye already clears to a transparent colour, and an earlier
/// version that also rewrote clear flags and swapped which eye was kept made
/// things considerably worse.
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
        private bool _logged;

        private void Start() => _until = Time.unscaledTime + AssertForSeconds;

        private void LateUpdate()
        {
            if (Time.unscaledTime > _until) { enabled = false; return; }

            var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            // Keep the orthographic one: it is the camera AlignBoxColliderWithCamera
            // drives, the one the capture placement's maths is written against, and
            // the one already clearing transparent.
            Camera keep = null;
            foreach (var c in cams)
                if (c.targetTexture == null && c.orthographic) { keep = c; break; }
            if (keep == null) return;

            foreach (var c in cams)
            {
                if (c.targetTexture != null || c == keep || !c.enabled) continue;
                c.enabled = false;
                if (!_logged)
                {
                    _logged = true;
                    Debug.Log($"[OverlayCameraGuard] Disabled '{c.name}' (ortho={c.orthographic}, " +
                              $"clear={c.clearFlags}); '{keep.name}' now owns the frame.");
                }
            }
        }
    }
}

using Unity.Mathematics;
using UnityEngine;
using uWindowCapture;

public class AlignBoxColliderWithCamera : MonoBehaviour
{
    public Camera camera;
    private BoxCollider boxCollider;

    void Update()
    {
        
        if (boxCollider == null)
        {
            FindBoxCollider();
        }

        /*
        if (boxCollider != null)
        {
            AlignBoxCollider();
        }
        */

        MatchPlaneToScreenSize();   
    }

    void FindBoxCollider()
    {
        // Find the BoxCollider in the child objects
        boxCollider = GetComponentInChildren<BoxCollider>();
    }

    void AlignBoxCollider()
    {
        // Get the camera's FOV and aspect ratio
        float fov = camera.fieldOfView;
        float aspect = camera.aspect;


        // Calculate the height of the BoxCollider
        float colliderHeight = boxCollider.size.y;
        float colliderTop = colliderHeight / 2;

        float delta = (colliderTop * aspect) / (2.0f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));

        
        // Calculate the delta
        //float delta = frustumTop - colliderTop;

        // Adjust the parent object's Z position
        Vector3 parentPosition = transform.position;
        parentPosition.z = camera.transform.position.z + delta;
        transform.position = parentPosition;
        
    }

    /// <summary>
    /// Draw the captured window at 1:1, where it actually is on the desktop.
    ///
    /// This used to set orthographicSize from the capture plane's own bounds, which
    /// zoomed the camera until whatever had been captured filled the display. A small
    /// window and a maximised one both ended up full-screen -- hence "the windows are
    /// always maximized once selected" -- and because the image was then a different size
    /// and in a different place from the real window underneath it, nothing lined up:
    /// the pointer sat somewhere other than the content it was over.
    ///
    /// The fix is to stop the camera chasing the plane. The camera is pinned to the
    /// SCREEN, so one captured pixel is one screen pixel, and the plane is moved to the
    /// window's real screen rectangle. Its size already follows the texture via
    /// UwcWindowTexture's own scaling, so 1:1 falls out once the zoom is gone.
    /// </summary>
    private void MatchPlaneToScreenSize()
    {
        // MUST stay, and must stay unconditional. An earlier attempt guarded this behind a
        // null check; the camera fell back to perspective, the UI smeared and the overlay
        // disappeared completely. It is the single most load-bearing line in this file.
        camera.orthographic = true;

        // Guarded here and not earlier, deliberately: the line above must run on every
        // frame regardless. The original dereferenced boxCollider unconditionally and threw
        // once per frame until a window was picked.
        if (boxCollider == null) return;

        var texture = GetComponentInChildren<UwcWindowTexture>();
        var win = texture != null ? texture.window : null;

        // Nothing captured yet, or a minimised window reporting a stale rect. Leave the
        // camera exactly as it is: zooming to an empty or stale plane is what produced the
        // "capture renders black" reports that sent three earlier attempts chasing geometry
        // that was already correct.
        if (texture == null || win == null || win.width <= 0 || win.height <= 0) return;

        // World units per captured pixel, taken from uWindowCapture's own scale so this
        // stays correct if scalePer1000Pixel is ever changed in the inspector.
        float unitsPerPixel = texture.scalePer1000Pixel / 1000f;
        if (unitsPerPixel <= 0f) return;

        // Pin the camera to the screen rather than to the plane.
        camera.orthographicSize = Screen.height * unitsPerPixel * 0.5f;

        // Screen coordinates are y-down from the top-left corner of the desktop; world
        // space is y-up from the camera's centre. Offsets are computed relative to the
        // camera's own position, so this does not care where the rig sits in world space.
        float dxPixels = (win.x + win.width * 0.5f) - Screen.width * 0.5f;
        float dyPixels = Screen.height * 0.5f - (win.y + win.height * 0.5f);

        // Move the CAMERA, not the plane.
        //
        // The first version of this set boxCollider.transform.position and had no effect
        // whatsoever: UwcWindowTexture owns the textured quad's transform, driving its
        // scale from the texture and holding its position, so anything written there is
        // overwritten. The diagnostic made it obvious -- planeScale tracked the window size
        // exactly (1908x1092 px -> 1.91x1.09 world units, so the 1:1 scale was already
        // right) while planeWorld stayed at the origin no matter what the window did.
        //
        // Offsetting the camera by the same amount in the opposite direction puts the plane
        // where it belongs on screen without fighting uWindowCapture for ownership of a
        // transform it manages. Read from the plane's ACTUAL position rather than assuming
        // the origin, so this keeps working if uWindowCapture ever starts placing it.
        Vector3 planePos = texture.transform.position;
        Vector3 camPos = camera.transform.position;
        camPos.x = planePos.x - dxPixels * unitsPerPixel;
        camPos.y = planePos.y - dyPixels * unitsPerPixel;
        camera.transform.position = camPos;
    }
}

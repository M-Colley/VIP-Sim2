using Unity.Mathematics;
using UnityEngine;

public class AlignBoxColliderWithCamera : MonoBehaviour
{
    public Camera camera;
    private BoxCollider boxCollider;

    // The camera's orthographic size, captured ONCE from the plane's authored height.
    // It used to be recomputed from the plane's bounds every frame, which is what
    // stretched every capture to fill the screen: MacCapture now reshapes the plane to
    // each captured window's aspect ratio, and a camera that zooms to the plane would
    // cancel that letterboxing frame by frame.
    private float pinnedOrthoSize = -1f;

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

    private void MatchPlaneToScreenSize()
    {
        /*
        float planeToCameradistance = Vector3.Distance(boxCollider.transform.position, camera.transform.position);
        float planeHeightScale = (2.0f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * planeToCameradistance) / 2;
        camera.orthographicSize = planeHeightScale;
        */
        // Ensure the camera is orthographic
        // Ensure the camera is orthographic
        camera.orthographic = true;

        // Guarded AFTER the orthographic line above, which must run unconditionally --
        // skipping it once turned the camera perspective and removed the overlay
        // entirely on Windows.
        if (boxCollider == null) return;

        // Pin the camera to the plane's AUTHORED height, once. Everything this method
        // used to do beyond that was wrong on this platform: it re-zoomed to the
        // plane's current bounds every frame (stretching every captured window to
        // fill the screen -- the reported distortion), it shifted the plane down by a
        // hardcoded 48px WINDOWS taskbar that does not exist on macOS, and it
        // re-centred the camera on the plane each frame. MacCapture now shapes the
        // plane to the captured window's aspect; the camera's job is only to hold a
        // stable full-screen view for it to sit in.
        if (pinnedOrthoSize < 0f)
        {
            float h = boxCollider.bounds.size.y;
            if (h <= 0f) return; // not laid out yet; try again next frame
            pinnedOrthoSize = h * 0.5f;
        }

        camera.orthographicSize = pinnedOrthoSize;


    }
}

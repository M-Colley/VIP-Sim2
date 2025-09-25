using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

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

        // Get the bounds of the BoxCollider
        Bounds bounds = boxCollider.bounds;

        // Calculate the full height of the BoxCollider
        float boxHeight = bounds.size.y;

        // Determine the taskbar height in pixels (you can set this value based on the screen resolution and taskbar size)
        int taskbarHeightPixels = 48; // Default medium size for a full HD screen

        // Get the screen height in pixels
        int screenHeightPixels = Screen.height;

        // Calculate the height of the taskbar in world units
        float taskbarHeightWorldUnits = (taskbarHeightPixels / (float)screenHeightPixels) * camera.orthographicSize * 2;

        // Calculate the orthographic size to fit the box height and the taskbar height
        camera.orthographicSize = (boxHeight / 2.0f) + taskbarHeightWorldUnits / 2.0f;

        // Ensure the camera's aspect ratio remains unchanged
        float aspectRatio = camera.aspect;

        // Adjust the position of the boxCollider to account for the taskbar height
        Vector3 newPosition = boxCollider.transform.position;
        newPosition.y = camera.transform.position.y - (bounds.extents.y) + (taskbarHeightWorldUnits / 2.0f);

        // Center the boxCollider horizontally within the camera view
        newPosition.x = camera.transform.position.x;

        // Apply the new position to the boxCollider to account for the taskbar height
        boxCollider.transform.position = newPosition;

        // Ensure the camera is centered on the object
        camera.transform.position = new Vector3(newPosition.x, camera.transform.position.y, camera.transform.position.z);


    }
}

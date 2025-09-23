using UnityEngine;

public class FitPlaneToCameraView : MonoBehaviour
{
    public Camera mainCamera; // Reference to the main camera
    private Transform planeTransform; // Reference to the plane's transform

    void Start()
    {
        planeTransform = GetFirstChildTransform();
        if (planeTransform == null)
        {
            Debug.LogError("No child found in the parent object.");
        }
    }

    void Update()
    {
        planeTransform = GetFirstChildTransform();
        if (planeTransform != null)
        {
            Debug.Log("Fit plane");
            FitPlane();
        }
    }

    Transform GetFirstChildTransform()
    {
        if (transform.childCount > 0)
        {
            return transform.GetChild(0);
        }
        else
        {
            return null;
        }
    }

    void FitPlane()
    {
        if (mainCamera == null || planeTransform == null)
        {
            Debug.LogError("Main camera or plane transform not assigned.");
            return;
        }

        // Get the mesh bounds of the plane
        MeshRenderer planeRenderer = planeTransform.GetComponent<MeshRenderer>();
        if (planeRenderer == null)
        {
            Debug.LogError("Plane does not have a MeshRenderer component.");
            return;
        }

        Bounds planeBounds = planeRenderer.bounds;
        float planeHeight = planeBounds.size.y;
        float planeWidth = planeBounds.size.x;

        // Get the frustum height at the distance of the plane from the camera
        float frustumHeight = 2.0f * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * mainCamera.aspect;

        // Calculate the required distance from the camera to fit the plane
        float requiredDistanceHeight = planeHeight / (2.0f * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        float requiredDistanceWidth = planeWidth / (2.0f * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / mainCamera.aspect);
        float requiredDistance = Mathf.Max(requiredDistanceHeight, requiredDistanceWidth);

        // Set the parent's position
        //Vector3 direction = (planeTransform.position - mainCamera.transform.position).normalized;
        Vector3 direction = new Vector3(0, 0, 1);
        transform.position = mainCamera.transform.position + direction * requiredDistance;

        // Correct the plane position to keep the parent-child relationship
        //planeTransform.localPosition = Vector3.zero;
    }
}

using UnityEngine;

public class FitPlaneToCameraView : MonoBehaviour
{
    public Camera mainCamera; // Reference to the main camera

    private Transform planeTransform; // Reference to the plane's transform
    private MeshRenderer planeRenderer;
    private bool missingPlaneLogged;
    private bool missingRendererLogged;

    private Vector3 lastBoundsSize;
    private Vector3 lastBoundsCenter;
    private float lastFieldOfView = float.NaN;
    private float lastAspect = float.NaN;
    private bool missingCameraLogged;

    private void Awake()
    {
        CachePlaneReferences();
    }

    private void OnEnable()
    {
        TryFitPlane(true);
    }

    private void Start()
    {
        TryFitPlane(true);
    }

    private void LateUpdate()
    {
        TryFitPlane(false);
    }

    private void CachePlaneReferences()
    {
        planeTransform = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (planeTransform == null)
        {
            planeRenderer = null;
            if (!missingPlaneLogged)
            {
                Debug.LogError("No child found in the parent object.");
                missingPlaneLogged = true;
            }
            return;
        }

        missingPlaneLogged = false;

        planeRenderer = planeTransform.GetComponent<MeshRenderer>();
        if (planeRenderer == null)
        {
            if (!missingRendererLogged)
            {
                Debug.LogError("Plane does not have a MeshRenderer component.");
                missingRendererLogged = true;
            }
            return;
        }

        missingRendererLogged = false;
    }

    private void TryFitPlane(bool forceRefresh)
    {
        if (mainCamera == null)
        {
            if (!missingCameraLogged)
            {
                Debug.LogError("Main camera not assigned.");
                missingCameraLogged = true;
            }
            return;
        }

        missingCameraLogged = false;

        if (planeTransform == null || planeRenderer == null)
        {
            CachePlaneReferences();
            if (planeTransform == null || planeRenderer == null)
            {
                return;
            }
            forceRefresh = true;
        }

        Bounds bounds = planeRenderer.bounds;
        bool needsUpdate = forceRefresh ||
                           Mathf.Abs(mainCamera.fieldOfView - lastFieldOfView) > Mathf.Epsilon ||
                           Mathf.Abs(mainCamera.aspect - lastAspect) > Mathf.Epsilon ||
                           bounds.size != lastBoundsSize ||
                           bounds.center != lastBoundsCenter ||
                           planeTransform.hasChanged;

        if (!needsUpdate)
        {
            return;
        }

        FitPlane(bounds);

        lastFieldOfView = mainCamera.fieldOfView;
        lastAspect = mainCamera.aspect;
        lastBoundsSize = bounds.size;
        lastBoundsCenter = bounds.center;
        planeTransform.hasChanged = false;
    }

    private void FitPlane(Bounds planeBounds)
    {
        float halfVerticalFov = mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float tanHalfVerticalFov = Mathf.Tan(halfVerticalFov);

        float requiredDistanceHeight = planeBounds.size.y * 0.5f / tanHalfVerticalFov;
        float requiredDistanceWidth = planeBounds.size.x * 0.5f / (tanHalfVerticalFov * mainCamera.aspect);
        float requiredDistance = Mathf.Max(requiredDistanceHeight, requiredDistanceWidth);

        Vector3 direction = new Vector3(0f, 0f, 1f);
        transform.position = mainCamera.transform.position + direction * requiredDistance;
    }
}

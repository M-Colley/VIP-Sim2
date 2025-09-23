using UnityEngine;
using UnityEngine.Serialization;

public class AlignBoxColliderWithCamera : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("camera")]
    private Camera targetCamera;
    [SerializeField]
    private int taskbarHeightPixels = 48;

    private BoxCollider boxCollider;
    private Transform boxColliderTransform;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private Vector3 lastBoundsSize;
    private Vector3 lastBoundsCenter;
    private bool hasCachedBounds;
    private bool missingColliderLogged;
    private bool missingCameraLogged;

    private void Awake()
    {
        CacheBoxCollider();
        EnsureCameraSetup();
        InvalidateCachedState();
    }

    private void OnEnable()
    {
        InvalidateCachedState();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            if (!missingCameraLogged)
            {
                Debug.LogError("Target camera not assigned.", this);
                missingCameraLogged = true;
            }
            return;
        }

        missingCameraLogged = false;

        if (boxCollider == null)
        {
            CacheBoxCollider();
            if (boxCollider == null)
            {
                return;
            }
        }

        Bounds bounds = boxCollider.bounds;
        bool screenChanged = Screen.width != lastScreenWidth || Screen.height != lastScreenHeight;
        bool boundsChanged = !hasCachedBounds ||
                             bounds.size != lastBoundsSize ||
                             bounds.center != lastBoundsCenter ||
                             (boxColliderTransform != null && boxColliderTransform.hasChanged);

        if (!screenChanged && !boundsChanged)
        {
            return;
        }

        Align(bounds);

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastBoundsSize = bounds.size;
        lastBoundsCenter = bounds.center;
        hasCachedBounds = true;

        if (boxColliderTransform != null)
        {
            boxColliderTransform.hasChanged = false;
        }
    }

    private void CacheBoxCollider()
    {
        boxCollider = GetComponentInChildren<BoxCollider>();
        if (boxCollider != null)
        {
            boxColliderTransform = boxCollider.transform;
            missingColliderLogged = false;
            hasCachedBounds = false;
        }
        else if (!missingColliderLogged)
        {
            Debug.LogError("No BoxCollider found in children.", this);
            missingColliderLogged = true;
        }
    }

    private void EnsureCameraSetup()
    {
        if (targetCamera != null && !targetCamera.orthographic)
        {
            targetCamera.orthographic = true;
        }
    }

    private void InvalidateCachedState()
    {
        lastScreenWidth = -1;
        lastScreenHeight = -1;
        hasCachedBounds = false;
    }

    private void Align(Bounds bounds)
    {
        EnsureCameraSetup();

        int screenHeight = Screen.height;
        if (screenHeight <= 0 || boxColliderTransform == null)
        {
            return;
        }

        float boxHeight = bounds.size.y;
        float pixelRatio = Mathf.Clamp01(taskbarHeightPixels / (float)screenHeight);
        float viewHeight = boxHeight / Mathf.Max(0.0001f, 1f - pixelRatio);
        targetCamera.orthographicSize = viewHeight * 0.5f;

        float additionalSpace = (viewHeight - boxHeight) * 0.5f;

        Vector3 newPosition = boxColliderTransform.position;
        newPosition.y = targetCamera.transform.position.y - bounds.extents.y + additionalSpace;
        newPosition.x = targetCamera.transform.position.x;
        boxColliderTransform.position = newPosition;

        Vector3 cameraPosition = targetCamera.transform.position;
        if (!Mathf.Approximately(cameraPosition.x, newPosition.x))
        {
            cameraPosition.x = newPosition.x;
            targetCamera.transform.position = cameraPosition;
        }
    }
}

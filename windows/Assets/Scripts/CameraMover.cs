using UnityEngine;
using UnityEngine.Serialization;

public class CameraMover : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("cameraToMove")]
    private Camera targetCamera;

    private Camera cachedCamera;

    private void Awake()
    {
        cachedCamera = targetCamera != null ? targetCamera : Camera.main;
        if (cachedCamera == null)
        {
            Debug.LogError("CameraMover requires a Camera reference.", this);
        }
    }

    private void OnMouseDrag()
    {
        if (cachedCamera == null)
        {
            return;
        }

        float distanceToScreen = cachedCamera.WorldToScreenPoint(transform.position).z;
        Vector3 screenPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, distanceToScreen);
        Vector3 worldPosition = cachedCamera.ScreenToWorldPoint(screenPoint);

        Vector3 currentPosition = transform.position;
        currentPosition.x = worldPosition.x;
        currentPosition.z = worldPosition.z;
        transform.position = currentPosition;
    }
}
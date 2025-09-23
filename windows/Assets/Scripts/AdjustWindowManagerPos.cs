using UnityEngine;

public class AdjustWindowManagerPos : MonoBehaviour
{
    private Transform trackedChild;
    private Vector3 lastChildLocalPosition;
    private bool forceUpdate;

    private void Awake()
    {
        CacheFirstChild();
    }

    private void OnEnable()
    {
        forceUpdate = true;
    }

    private void OnTransformChildrenChanged()
    {
        CacheFirstChild();
        forceUpdate = true;
    }

    private void LateUpdate()
    {
        if (trackedChild == null)
        {
            return;
        }

        Vector3 childLocalPosition = trackedChild.localPosition;
        bool childMoved = forceUpdate ||
                          trackedChild.hasChanged ||
                          (childLocalPosition - lastChildLocalPosition).sqrMagnitude > Mathf.Epsilon;

        if (!childMoved)
        {
            return;
        }

        Vector3 parentLocalPosition = transform.localPosition;
        parentLocalPosition.x = -childLocalPosition.x;
        transform.localPosition = parentLocalPosition;

        lastChildLocalPosition = childLocalPosition;
        trackedChild.hasChanged = false;
        forceUpdate = false;
    }

    private void CacheFirstChild()
    {
        trackedChild = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (trackedChild != null)
        {
            lastChildLocalPosition = trackedChild.localPosition;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple object pool for brush instances to avoid frequent instantiation
/// and destruction of LineRenderer objects.
/// </summary>
public class BrushPool : MonoBehaviour
{
    // Prefab used to create new brush instances when the pool is empty.
    public GameObject brushPrefab;

    // Queue storing available brush instances.
    private readonly Queue<GameObject> pool = new Queue<GameObject>();

    /// <summary>
    /// Retrieves a brush from the pool or instantiates a new one if none
    /// are available. The brush is reset to a default state before
    /// returning.
    /// </summary>
    public GameObject GetBrush()
    {
        GameObject brushInstance = pool.Count > 0 ? pool.Dequeue() : Instantiate(brushPrefab);

        brushInstance.SetActive(true);

        // Reset the LineRenderer so the new line starts clean.
        LineRenderer lr = brushInstance.GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, Vector3.zero);

        return brushInstance;
    }

    /// <summary>
    /// Returns a brush to the pool after resetting it. The object is
    /// deactivated and stored for future reuse.
    /// </summary>
    public void ReturnBrush(GameObject brushInstance)
    {
        LineRenderer lr = brushInstance.GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, Vector3.zero);

        brushInstance.SetActive(false);
        brushInstance.transform.SetParent(transform);
        pool.Enqueue(brushInstance);
    }
}


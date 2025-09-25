using UnityEngine;
using UnityEngine.EventSystems;

public class CameraMover : MonoBehaviour
{

    public Camera cameraToMove;
    public float moveSpeed = 0.1f;

    void OnMouseDrag()
    {
        float distance_to_screen = Camera.main.WorldToScreenPoint(gameObject.transform.position).z;
        Vector3 pos_move = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, distance_to_screen));
        transform.position = new Vector3(pos_move.x, transform.position.y, pos_move.z);

    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AdjustWindowManagerPos : MonoBehaviour
{

    private Vector3 lastChildLocalPosition;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (transform.childCount > 0)
        {
            Transform firstChild = transform.GetChild(0);
            Vector3 childLocalPosition = firstChild.localPosition;

            if (childLocalPosition != lastChildLocalPosition)
            {
                Vector3 parentLocalPosition = transform.localPosition;
                parentLocalPosition.x = -childLocalPosition.x;
                transform.localPosition = parentLocalPosition;

                // Update last known local position to prevent continuous adjustment
                lastChildLocalPosition = childLocalPosition;
            }
        }
    }
}

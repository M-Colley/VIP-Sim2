using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScaleManager : MonoBehaviour
{
    public Camera cam;

    private Vector3 lastCamPos;
    private Quaternion lastCamRot;
    private float lastFov;
    private int lastScreenW;
    private int lastScreenH;

    void Start()
    {
        CacheValues();
        UpdateScale();
    }

    void Update()
    {
        if (cam.transform.position != lastCamPos ||
            cam.transform.rotation != lastCamRot ||
            Math.Abs(cam.fieldOfView - lastFov) > Mathf.Epsilon ||
            Screen.width != lastScreenW ||
            Screen.height != lastScreenH)
        {
            CacheValues();
            UpdateScale();
        }
    }

    private void CacheValues()
    {
        lastCamPos = cam.transform.position;
        lastCamRot = cam.transform.rotation;
        lastFov = cam.fieldOfView;
        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
    }

    private void UpdateScale()
    {
        float pos = cam.nearClipPlane + 10.0f;
        transform.position = cam.transform.position + cam.transform.forward * pos;
        transform.LookAt(cam.transform);
        float h = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * pos * 2f * cam.aspect / 10.0f;
        float w = h * Screen.height / Screen.width;
        transform.localScale = new Vector3(h, w, 1);
    }
}

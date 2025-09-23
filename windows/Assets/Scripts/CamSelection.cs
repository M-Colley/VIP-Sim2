using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnitEye;
using UnityEngine;
using UnityEngine.Windows.WebCam;

public class CamSelection : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI webcamtext;
    [SerializeField]
    private WebCamInput webCamInput;
    [SerializeField]
    private Gaze gaze;

    private string previousName;

    private void Start()
    {
        previousName = webCamInput.webCamName;
        StartCoroutine(LateStart(1));
    }

    private void Update()
    {
        if (webCamInput.webCamName != previousName)
        {
            UpdateWebcamText();
        }
    }

    IEnumerator LateStart(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        //Your Function You Want to Call
        UpdateWebcamText();
    }

    public void OnPrevCam()
    {
        webCamInput.PreviousCamera((int)webCamInput.webCamResolution.x, (int)webCamInput.webCamResolution.y);
        gaze.EyeHelper.CameraChanged(webCamInput.webCamName);
        UpdateWebcamText();
    }

    public void OnNextCam()
    {
        webCamInput.NextCamera((int)webCamInput.webCamResolution.x, (int)webCamInput.webCamResolution.y);
        gaze.EyeHelper.CameraChanged(webCamInput.webCamName);
        UpdateWebcamText();
    }

    private void UpdateWebcamText()
    {
        previousName = webCamInput.webCamName;
        webcamtext.text = "Webcam: " + previousName;
    }
}


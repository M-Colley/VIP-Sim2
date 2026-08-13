using System.Collections;
using TMPro;
using UnitEye;
using UnityEngine;

/// <summary>
/// Webcam picker for the gaze tracker.
///
/// Rewritten for UnitEye 1.1. Previously this held a direct reference to the
/// `Gaze` MonoBehaviour and to `WebCamInput`, and had to remember to call
/// `gaze.EyeHelper.CameraChanged(...)` itself after every switch -- if that call
/// was ever missed, the eye-distance estimate silently kept using the previous
/// camera's intrinsics.
///
/// UnitEye 1.1 moved camera switching behind IGazeProvider, and
/// NextCamera()/PreviousCamera() now perform the CameraChanged bookkeeping
/// internally, so this class no longer has to know about it.
/// </summary>
public class CamSelection : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI webcamtext;

    private string previousName;

    private void Start()
    {
        // UnitEye needs a frame or two to bring the capture device up before it
        // can report a camera name.
        StartCoroutine(LateStart(1f));
    }

    private void Update()
    {
        string current = CurrentCameraName();
        if (current != previousName)
            UpdateWebcamText();
    }

    private IEnumerator LateStart(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        UpdateWebcamText();
    }

    public void OnPrevCam()
    {
        var provider = TryGetProvider();
        if (provider == null) return;

        provider.PreviousCamera();
        UpdateWebcamText();
    }

    public void OnNextCam()
    {
        var provider = TryGetProvider();
        if (provider == null) return;

        provider.NextCamera();
        UpdateWebcamText();
    }

    private void UpdateWebcamText()
    {
        previousName = CurrentCameraName();
        if (webcamtext != null)
            webcamtext.text = "Webcam: " + (string.IsNullOrEmpty(previousName) ? "none" : previousName);
    }

    private string CurrentCameraName()
    {
        var provider = TryGetProvider();
        return provider != null ? provider.CurrentCameraName : "";
    }

    /// <summary>
    /// UnitEyeAPI throws when no gaze rig is in the scene. The webcam picker is
    /// part of the settings UI and can be opened with the gaze source disabled,
    /// so treat absence as "no camera" rather than letting it throw every frame.
    /// </summary>
    private static IGazeProvider TryGetProvider()
    {
        try
        {
            return UnitEyeAPI.GetGazeReference().Provider;
        }
        catch (System.InvalidOperationException)
        {
            return null;
        }
    }
}

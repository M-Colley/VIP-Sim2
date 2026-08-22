using UnityEngine;
using TMPro;
using Kirurobo;

public class MacScale : MonoBehaviour
{
    // The fixed aspect ratio for the plane (16:9)
    private float aspectRatio = 16f / 10f;
        // Menu bar height (in pixels) - macOS default is 22px
    private const float menuBarHeight = 22f; 
    // Dock height (in pixels) - can vary but a typical value is 40px
    private const float dockHeight = 40f; 

    public float previousWidth;

    public float previousHeight;

    public float previousXOffset;

    public float previousYOffset;

    private float settingsWidthOffset;

    private float settingsHeightOffset;

    private float settingsXOffset;

    private float settingsYOffset;

    public TMP_InputField width;
    public TMP_InputField height;

    public TMP_InputField xOffset;

    public TMP_InputField yOffset;

    public UniWindowController overlay;

    public GameObject warning;

    public GameObject menu;

    public void Load()
    {
        lastSetting = transform.position;
        menu.SetActive(true);
        overlay.enableFeedbackState();
        previousXOffset = float.Parse(xOffset.text);
        previousYOffset = float.Parse(yOffset.text);
        previousHeight = float.Parse(height.text);
        previousWidth = float.Parse(width.text);
    }

    public void Start()
    {
        lastSetting = transform.position;
    }

    public void Abort()
    {
        overlay.disableFeedbackState();
        warning.SetActive(false);
    }

    public void resetOffset(){
        xOffset.text = previousXOffset.ToString();
        yOffset.text = previousYOffset.ToString();
        height.text = previousHeight.ToString();
        width.text = previousWidth.ToString();
        warning.SetActive(false);
    }


    /// <summary>
    /// A number from an input field that may no longer exist. Absent means no offset.
    /// </summary>
    private static float ParseOffset(TMP_InputField field)
    {
        if (field == null) return 0f;
        return float.TryParse(field.text, out float value) ? value : 0f;
    }

    private Vector3 lastSetting;
    void Update()
    {
        
        // The four fields belonged to the manual window-size dialog, which has been
        // removed: automatic detection is no longer unreliable, so a manual override for it
        // was a workaround for a fault that no longer exists. This component stays because
        // it does a second, unrelated job -- it is what gives the capture plane its
        // negative x scale, and without that every macOS capture would be a mirror image.
        //
        // With the dialog gone the references are null, so the offsets are simply zero.
        // Read once here rather than guarded at four separate call sites below.
        settingsWidthOffset = ParseOffset(width);
        settingsHeightOffset = ParseOffset(height);
        settingsXOffset = ParseOffset(xOffset);
        settingsYOffset = ParseOffset(yOffset);

        // Scale the plane's x value based on the required width
        Vector3 scale = transform.localScale;
        scale.x = -aspectRatio + settingsWidthOffset; // Scale based on the screen width (normalize to screen)
        scale.z = 1 - 0.1f + settingsHeightOffset; // Keep the y value fixed at 1 (as per your requirement)
        transform.localScale = scale;

        transform.position = lastSetting + new Vector3(settingsXOffset, settingsYOffset, 0);

        // No logging here. This runs in Update, so it wrote two lines per frame -- 120 a
        // second into a player log whose whole purpose is to make a user's fault report
        // readable.
    }

    public void CheckForUnsavedAndClose()
    {
        if (float.TryParse(xOffset.text, out float currentXOffset) &&
        float.TryParse(yOffset.text, out float currentYOffset) &&
        float.TryParse(width.text, out float currentWidth) &&
        float.TryParse(height.text, out float currentHeight)){
            if(currentXOffset != previousXOffset || currentYOffset != previousYOffset || currentHeight != previousHeight || currentWidth != previousWidth){
                warning.SetActive(true);
            } else {
                Abort();
            }
        }
    }
}

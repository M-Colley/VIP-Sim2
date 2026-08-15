using UnityEngine;
using UnityEngine.UI;

public class HideImpairmentSelection : MonoBehaviour
{
    // Serielles Field fï¿½r das GameObject, das gesetzt werden soll
    [SerializeField] private GameObject targetGameObject;
    [SerializeField] Slider enableToggle;

    /// <summary>
    /// Hide the per-effect settings panel.
    ///
    /// Clearing the slider rather than deactivating the panel directly, because Update
    /// re-applies the slider's value every frame and would simply switch it back on.
    /// Called when an effect is switched off, so its parameters stop being shown for
    /// something that is no longer running.
    /// </summary>
    public void SetSettingsOpen(bool open)
    {
        if (enableToggle != null) enableToggle.value = open ? 1f : 0f;
    }

    [SerializeField] Image settingWheel;

    [SerializeField] MacCapture macCapture;

    // Update is called once per frame
    void Update()
    {
        // Setzt das target GameObject je nach Rï¿½ckgabewert
        // VipSimDiagnostics.ForceMenusVisible (F7) shows the effect list without a capture
        // running and without the effect switched on, so that half of the UI can be looked
        // at during development. The Windows gate is uWindowCapture's thereIsActiveWindow;
        // here it is macCapture.isRunning, hence the override being applied in both places.
        if (macCapture.isRunning || VipSimDiagnostics.ForceMenusVisible)
        {
            if ((enableToggle.value > 0.9 && ChangeButtonAppearance.HasOpenSettings) || VipSimDiagnostics.ForceMenusVisible)
            {
                targetGameObject.SetActive(true);
            } else
            {
                targetGameObject.SetActive(false);
            }
            if(settingWheel != null)
            settingWheel.enabled = true;
        }
        else
        {
            targetGameObject.SetActive(false);
            if(settingWheel != null)
            settingWheel.enabled = false;    
        }
    }
}



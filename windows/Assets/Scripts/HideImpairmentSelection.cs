using UnityEngine;
using UnityEngine.UI;
using uWindowCapture;

public class HideImpairmentSelection : MonoBehaviour
{
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
    public void CloseSettings()
    {
        if (enableToggle != null) enableToggle.value = 0f;
    }
    [SerializeField] Image settingWheel;

    void Update()
    {
        bool hasActiveWindow = UwcWindowList.thereIsActiveWindow;

        // The F7 diagnostic override has to bypass the enable toggle as well as the
        // window-selected gate, or the effect list still stays hidden behind the second
        // condition and the hotkey looks broken for a different reason.
        bool desiredActive = hasActiveWindow &&
                             (VipSimDiagnostics.ForceMenusVisible || enableToggle.value > 0.9f);

        if (targetGameObject.activeSelf != desiredActive)
        {
            targetGameObject.SetActive(desiredActive);
        }

        if (settingWheel != null)
        {
            bool desiredWheel = hasActiveWindow;
            if (settingWheel.enabled != desiredWheel)
            {
                settingWheel.enabled = desiredWheel;
            }
        }
    }
}

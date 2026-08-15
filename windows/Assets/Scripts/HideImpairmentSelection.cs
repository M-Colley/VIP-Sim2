using UnityEngine;
using UnityEngine.UI;
using uWindowCapture;

public class HideImpairmentSelection : MonoBehaviour
{
    [SerializeField] private GameObject targetGameObject;
    [SerializeField] Slider enableToggle;
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

using UnityEngine;
using UnityEngine.UI;

public class HideImpairmentSelection : MonoBehaviour
{
    // Serielles Field f�r das GameObject, das gesetzt werden soll
    [SerializeField] private GameObject targetGameObject;
    [SerializeField] Slider enableToggle;
    [SerializeField] Image settingWheel;

    [SerializeField] MacCapture macCapture;

    // Update is called once per frame
    void Update()
    {
        // Setzt das target GameObject je nach R�ckgabewert
        // VipSimDiagnostics.ForceMenusVisible (F7) shows the effect list without a capture
        // running and without the effect switched on, so that half of the UI can be looked
        // at during development. The Windows gate is uWindowCapture's thereIsActiveWindow;
        // here it is macCapture.isRunning, hence the override being applied in both places.
        if (macCapture.isRunning || VipSimDiagnostics.ForceMenusVisible)
        {
            if (enableToggle.value > 0.9 || VipSimDiagnostics.ForceMenusVisible)
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

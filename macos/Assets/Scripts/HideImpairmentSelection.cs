using UnityEngine;
using UnityEngine.UI;

public class HideImpairmentSelection : MonoBehaviour
{
    // Serielles Field fï¿½r das GameObject, das gesetzt werden soll
    [SerializeField] private GameObject targetGameObject;
    [SerializeField] Slider enableToggle;

    /// <summary>
    /// Whether an effect's parameters are currently on display.
    ///
    /// This is its own piece of state, and that is the entire point of the field. It used to
    /// be stored in the master Enable slider -- SetSettingsOpen wrote enableToggle.value
    /// directly -- and that slider is what gates BOTH of this component's instances: one
    /// shows the per-effect parameter panel, the other shows the whole effect list. So
    /// closing one effect's parameters set the master switch to zero and took the entire
    /// list with it.
    ///
    /// From the outside that is exactly what was reported: Enable still lit, and no symptoms
    /// on screen. The switch even looked half-thrown, because its fill colour is set by the
    /// toggle's own on/off events -- which never fired -- while its knob follows the slider
    /// value, which had been moved behind its back.
    ///
    /// Static because there is only ever one parameter panel, and because a caller should
    /// not have to pick the right one of two identical components to talk to.
    /// </summary>
    public static bool SettingsOpen { get; private set; }

    /// <summary>
    /// Show or hide the per-effect parameter panel. Must be driven in BOTH directions: an
    /// earlier version only ever cleared it, so after the first time any effect was switched
    /// off the panel stayed empty for every effect after that.
    /// </summary>
    public static void SetSettingsOpen(bool open)
    {
        SettingsOpen = open;
    }

    [Tooltip("True on the instance that shows an effect's PARAMETERS, false on the one that " +
             "shows the effect list. Only the parameter panel answers to SettingsOpen.")]
    [SerializeField] private bool gatesSettingsPanel;

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
            bool master = enableToggle.value > 0.9 || VipSimDiagnostics.ForceMenusVisible;

            // The parameter panel carries a second condition of its own; see SettingsOpen.
            bool mine = !gatesSettingsPanel || SettingsOpen ||
                        VipSimDiagnostics.ForceMenusVisible;

            if (master && mine)
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



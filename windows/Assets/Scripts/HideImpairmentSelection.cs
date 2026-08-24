using UnityEngine;
using UnityEngine.UI;
using uWindowCapture;

public class HideImpairmentSelection : MonoBehaviour
{
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

    void Update()
    {
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
        // See HideMenu: reading UwcWindowList instantiates the Win32 capture manager,
        // which throws on every P/Invoke here. No capture on Linux yet, so no window.
        bool hasActiveWindow = false;
#else
        bool hasActiveWindow = UwcWindowList.thereIsActiveWindow;
#endif

        // The F7 diagnostic override has to bypass the enable toggle as well as the
        // window-selected gate, or the effect list still stays hidden behind the second
        // condition and the hotkey looks broken for a different reason.
        // Do NOT add a HasOpenSettings condition here. It was tried and it deadlocks:
        // targetGameObject is the EFFECT LIST, not the per-effect parameters panel, so
        // requiring an open gear means the list only appears once a gear has been clicked --
        // and no gear can be clicked while the list is hidden. The result is that the whole
        // lower half of the UI disappears and cannot be recovered.
        //
        // ChangeButtonAppearance.HasOpenSettings is still the right piece of state for
        // deciding whether an effect's PARAMETERS should show, and it is used for that where
        // an effect is switched off. It is simply not what gates this object.
        bool master = VipSimDiagnostics.ForceMenusVisible || enableToggle.value > 0.9f;

        // The parameter panel carries a second condition of its own -- an effect's
        // parameters are only worth showing while one is selected. The effect list has no
        // such condition, which is precisely why the two must not share a variable.
        bool mine = !gatesSettingsPanel || SettingsOpen || VipSimDiagnostics.ForceMenusVisible;

        bool desiredActive = hasActiveWindow && master && mine;

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

using UnityEngine;
using UnityEngine.UI;
using uWindowCapture;

public class HideImpairmentSelection : MonoBehaviour
{
    [SerializeField] private GameObject targetGameObject;
    [SerializeField] Slider enableToggle;

    /// <summary>
    /// Show or hide the per-effect settings panel.
    ///
    /// Drives the slider rather than the panel object directly, because Update re-applies
    /// the slider's value every frame and would undo a SetActive immediately.
    ///
    /// Must be driven in BOTH directions. An earlier version only ever cleared it, when an
    /// effect was switched off, and nothing set it back -- so after the first time any
    /// effect was disabled the settings panel stayed empty for every effect thereafter.
    /// Opening a gear now sets it, closing the open effect clears it.
    /// </summary>
    public void SetSettingsOpen(bool open)
    {
        if (enableToggle != null) enableToggle.value = open ? 1f : 0f;
    }
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

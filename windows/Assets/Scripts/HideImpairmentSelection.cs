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
        bool hasActiveWindow = UwcWindowList.thereIsActiveWindow;

        // The F7 diagnostic override has to bypass the enable toggle as well as the
        // window-selected gate, or the effect list still stays hidden behind the second
        // condition and the hotkey looks broken for a different reason.
        // Derived from the selection, not just from the slider. The slider alone said
        // "settings are switched on" without reference to WHICH effect they belonged to,
        // so the panel could show parameters when nothing was selected, or keep showing an
        // effect's parameters after it had been switched off. Requiring an open gear as
        // well means the panel and the selection cannot disagree.
        bool desiredActive = hasActiveWindow &&
                             (VipSimDiagnostics.ForceMenusVisible ||
                              (enableToggle.value > 0.9f && ChangeButtonAppearance.HasOpenSettings));

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

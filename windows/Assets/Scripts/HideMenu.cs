using Christina.UI;
using UnityEngine;
using uWindowCapture;

public class HideMenu : MonoBehaviour
{

    // Serielles Field für das GameObject, das gesetzt werden soll
    [SerializeField] private GameObject targetGameObject;
    [SerializeField] private ToggleSwitch toggle;

    // uWindowCapture is a Win32 native plugin, and merely READING UwcWindowList brings
    // the capture manager into existence: its static accessors go through
    // UwcManager.instance, which AddComponents one on demand, and that component's Awake
    // immediately P/Invokes. On Linux the call cannot resolve, so the touch itself is what
    // produced the DllNotFoundException spam -- disabling the manager afterwards does not
    // help, because the next read just creates another one. The only fix is not to ask.
    // Capture is unavailable on Linux until the portal/PipeWire backend lands; see
    // docs/LINUX_PORT.md. Everything else in VIP-Sim runs.
    void OnEnable()
    {
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
        HandleActiveWindowChanged(false);
#else
        UwcWindowList.OnActiveWindowChanged += HandleActiveWindowChanged;
        HandleActiveWindowChanged(UwcWindowList.thereIsActiveWindow);
#endif
    }

    void OnDisable()
    {
#if !UNITY_STANDALONE_LINUX || UNITY_EDITOR
        UwcWindowList.OnActiveWindowChanged -= HandleActiveWindowChanged;
#endif
    }

    void HandleActiveWindowChanged(bool hasActiveWindow)
    {
        targetGameObject.SetActive(hasActiveWindow);
        if (!hasActiveWindow && toggle.CurrentValue)
        {
            toggle.Toggle();
        }
    }
}

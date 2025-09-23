using Christina.UI;
using UnityEngine;
using uWindowCapture;

public class HideMenu : MonoBehaviour
{

    // Serielles Field für das GameObject, das gesetzt werden soll
    [SerializeField] private GameObject targetGameObject;
    [SerializeField] private ToggleSwitch toggle;

    void OnEnable()
    {
        UwcWindowList.OnActiveWindowChanged += HandleActiveWindowChanged;
        HandleActiveWindowChanged(UwcWindowList.thereIsActiveWindow);
    }

    void OnDisable()
    {
        UwcWindowList.OnActiveWindowChanged -= HandleActiveWindowChanged;
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

using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Guarantees VIP-Sim can always be closed.
///
/// Why this needs to exist at all: VIP-Sim is a borderless, topmost,
/// click-through overlay. It has no title bar and no taskbar close, clicks pass
/// straight through it to the application underneath, and because it is
/// click-through it frequently does not hold keyboard focus -- so Unity's
/// Input.GetKeyDown never fires. When the in-app exit path also broke, the only
/// remaining option was Task Manager. That is not an acceptable failure mode for
/// something that covers the user's entire screen and simulates vision loss.
///
/// Three independent layers, so no single failure removes the way out:
///
///   1. Installed automatically via RuntimeInitializeOnLoadMethod. It does not
///      depend on being placed in a scene, so it cannot be lost by scene edits,
///      a broken prefab, or a migration like the ones this project has needed.
///   2. GetAsyncKeyState polling on Windows. This reads global key state and
///      works even when the overlay has no focus -- the case Unity's Input
///      cannot cover.
///   3. Unity Input as a fallback for when the window IS focused, and on
///      platforms without the native call.
///
/// Chord is Ctrl+Alt+Q (deliberately awkward, so it cannot fire by accident
/// during a study session) plus plain F12 while focused.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class EmergencyQuit : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_CONTROL = 0x11;
    private const int VK_MENU    = 0x12; // Alt
    private const int VK_Q       = 0x51;
    private const int VK_F12     = 0x7B;

    private static bool GlobalKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
#endif

    private bool _quitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        // hideFlags keeps it out of the hierarchy so it cannot be deleted by
        // anything walking the scene, and DontDestroyOnLoad survives scene loads.
        var go = new GameObject("~VipSimEmergencyQuit") { hideFlags = HideFlags.HideAndDontSave };
        go.AddComponent<EmergencyQuit>();
        DontDestroyOnLoad(go);

        Debug.Log("[EmergencyQuit] Installed. Press Ctrl+Alt+Q at any time to close VIP-Sim " +
                  "(works even when the overlay does not have keyboard focus). F12 also quits when focused.");
    }

    private void Update()
    {
        if (_quitting) return;

        bool quit = false;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Global: works with no focus, which is the whole point.
        if (GlobalKeyDown(VK_CONTROL) && GlobalKeyDown(VK_MENU) && GlobalKeyDown(VK_Q)) quit = true;
        if (GlobalKeyDown(VK_F12)) quit = true;
#endif

        // Focused fallback, and the path used in the editor and on other platforms.
        if (Input.GetKey(KeyCode.F12)) quit = true;
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
            (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) &&
            Input.GetKey(KeyCode.Q)) quit = true;

        if (quit) QuitNow("hotkey");
    }

    public void QuitNow(string reason)
    {
        if (_quitting) return;
        _quitting = true;

        Debug.Log($"[EmergencyQuit] Quitting ({reason}).");
        Application.Quit();

        // Application.Quit() is asynchronous and a hung or blocked frame can stop
        // it landing. Since the whole point of this class is that there is always
        // a way out, fall back to terminating the process outright.
        Invoke(nameof(ForceKill), 2f);
    }

    private void ForceKill()
    {
        Debug.LogWarning("[EmergencyQuit] Application.Quit() did not take effect; terminating process.");
#if !UNITY_EDITOR
        System.Diagnostics.Process.GetCurrentProcess().Kill();
#endif
    }
}

#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Stops uWindowCapture from running on Linux.
///
/// uWindowCapture is a Win32 native plugin -- Windows Graphics Capture, PrintWindow and
/// BitBlt. Its managed side P/Invokes into uWindowCapture.dll unconditionally, so on Linux
/// every call throws DllNotFoundException. The first Linux run showed exactly that: three
/// of them in the player log before the app had finished starting, and it would have been
/// one per frame had the run gone on longer. Noise like that buries the log lines that
/// matter and reads, to anyone who opens the log, as a broken application.
///
/// The Linux player is built from the WINDOWS project (that is where the scenes live), so
/// the component comes along whether or not it can work. Disabling it is the honest thing
/// to do until the portal + PipeWire backend exists; see docs/LINUX_PORT.md.
///
/// Runs very early -- ahead of UwcManager's own Awake -- because the point is to disable
/// it BEFORE it makes its first native call.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class LinuxCaptureGuard : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        var go = new GameObject("LinuxCaptureGuard") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        go.AddComponent<LinuxCaptureGuard>();
    }

    private void Awake()
    {
        Disable();
    }

    private void Start()
    {
        // Again after the scene is up: the manager may be instantiated by the scene load
        // itself, i.e. after this component's Awake has already run.
        Disable();
    }

    private static void Disable()
    {
        int disabled = 0;
        foreach (var m in FindObjectsByType<uWindowCapture.UwcManager>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!m.enabled && !m.gameObject.activeSelf) continue;
            m.enabled = false;
            m.gameObject.SetActive(false);
            disabled++;
        }

        if (disabled > 0)
        {
            Debug.Log($"[LinuxCaptureGuard] Disabled {disabled} uWindowCapture manager(s). " +
                      "Window capture is unavailable on Linux until the portal/PipeWire " +
                      "backend lands -- see docs/LINUX_PORT.md. The rest of VIP-Sim runs.");
        }
    }
}
#endif

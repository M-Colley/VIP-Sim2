#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine;
using uWindowCapture;

/// <summary>
/// Ask the capture plugin for the method that can actually see a modern window.
///
/// uWindowCapture defaults to Auto, which chooses between PrintWindow and BitBlt. Both read
/// what an application draws through GDI, and an application that renders with the GPU --
/// any browser, anything built on Electron, VS Code, recent Explorer and Office windows --
/// draws nothing there. The capture succeeds, reports the right size and position, and
/// contains black.
///
/// That is the worst possible shape for a fault here, because from the outside it is
/// indistinguishable from the simulation being switched off: the effects are enabled, the
/// window is found, the plane is placed correctly, and the screen shows nothing. It also
/// depends entirely on which window the user happens to pick -- Notepad works, because
/// Notepad is still a GDI window.
///
/// Windows Graphics Capture reads the composited output instead, which is what the user can
/// actually see. It needs Windows 10 1903 or newer; where it is missing we leave the plugin
/// on Auto rather than force a mode that cannot work.
/// </summary>
public class WindowCaptureMode : MonoBehaviour
{
    private CaptureMode _wanted;
    private bool _decided;

    public static void Install(GameObject host)
    {
        if (host.GetComponent<WindowCaptureMode>() == null)
            host.AddComponent<WindowCaptureMode>();
    }

    private void Start()
    {
        bool wgc = false;
        try
        {
            wgc = UwcManager.isWindowsGraphicsCaptureSupported;
        }
        catch (System.Exception e)
        {
            // The plugin is a native DLL; on a machine where it cannot load at all this is
            // the first place that shows, and it should not take the application with it.
            Debug.LogWarning($"[WindowCaptureMode] could not ask the capture plugin which " +
                             $"methods it has ({e.GetType().Name}); leaving it on Auto.");
            enabled = false;
            return;
        }

        _wanted = wgc ? CaptureMode.WindowsGraphicsCapture : CaptureMode.Auto;
        _decided = true;

        Debug.Log(wgc
            ? "[WindowCaptureMode] using Windows Graphics Capture, so hardware-accelerated " +
              "windows (browsers, Electron apps, Explorer) are captured rather than coming " +
              "out black."
            : "[WindowCaptureMode] Windows Graphics Capture is not available on this system " +
              "(it needs Windows 10 1903 or newer). Falling back to the plugin's Auto mode, " +
              "which cannot see windows that draw with the GPU -- those will appear black.");
    }

    private void LateUpdate()
    {
        if (!_decided) return;

        // Applied continuously rather than once: the textured quad is created when the user
        // picks a window, so there is nothing to configure at startup, and picking another
        // window replaces it.
        foreach (var t in FindObjectsByType<UwcWindowTexture>(FindObjectsInactive.Include))
            if (t.captureMode != _wanted) t.captureMode = _wanted;
    }
}
#endif

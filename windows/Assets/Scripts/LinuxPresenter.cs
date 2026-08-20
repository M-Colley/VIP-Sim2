#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Sends VIP-Sim's finished frames to the Wayland presenter.
///
/// On Windows and macOS the engine's own window becomes the overlay: native flags are set
/// on it and it is done. Wayland does not allow that. A surface's role is fixed when it is
/// created, Unity's window is created by SDL as an xdg_toplevel, and SDL has no
/// layer-shell support (libsdl-org/SDL#7262). No plugin can re-role an existing surface,
/// so the overlay has to be a second process holding a zwlr_layer_surface_v1, and the
/// pixels have to cross a process boundary to reach it.
///
/// This is the Unity end of that crossing: read the frame back off the GPU, hand the bytes
/// to libvipsim_present, which copies them into shared memory and bumps a sequence number
/// the presenter is watching. See linux/presenter/ and docs/LINUX_PORT.md.
///
/// Readback is asynchronous on purpose. A synchronous ReadPixels stalls the render thread
/// until the GPU catches up, which at 4K is the difference between a simulation you can
/// work under and one you cannot.
/// </summary>
[DefaultExecutionOrder(10000)]   // after everything else has drawn
public class LinuxPresenter : MonoBehaviour
{
    private const string Lib = "vipsim_present";
    private const string PresenterName = "vipsim-presenter";

    private static System.Diagnostics.Process _presenter;

    [DllImport(Lib)] private static extern int vipsim_present_open(int width, int height);
    [DllImport(Lib)] private static extern int vipsim_present_push_rgba32(IntPtr src, int stride, int flip);
    [DllImport(Lib)] private static extern void vipsim_present_set_panel(int x, int y, int w, int h);
    [DllImport(Lib)] private static extern void vipsim_present_close();
    [DllImport(Lib)] private static extern uint vipsim_present_frame_count();

    private RenderTexture _rt;
    private bool _open;
    private bool _readbackPending;
    private int _w, _h;
    private readonly int[] _rect = { -1, -1, -1, -1 };
    private readonly Vector3[] _corners = new Vector3[4];
    private TransparentWindow _window;
    private bool _hosted;
    private byte[] _staging;
    private GCHandle _pin;
    private bool _flip;

    public static bool Active { get; private set; }
    public static uint FramesSent => Active ? vipsim_present_frame_count() : 0u;

    public static void Install(GameObject host)
    {
        if (host.GetComponent<LinuxPresenter>() == null)
            host.AddComponent<LinuxPresenter>();
    }

    private void Start()
    {
        _w = Screen.width;
        _h = Screen.height;

        // Already inside the presenter's own compositor?
        //
        // Then it started us, not the other way round, and our window IS the frame: it is
        // composited onto the layer surface directly. Reading it back off the GPU, copying
        // it through shared memory and flipping it on the way would be three copies of work
        // whose result is thrown away. The segment is still opened, because it is how the
        // rectangle that should catch the mouse gets published.
        _hosted = Environment.GetEnvironmentVariable("VIPSIM_HOSTED") == "1";
        if (!_hosted) StartPresenterProcess();

        try
        {
            int rc = vipsim_present_open(_w, _h);
            if (rc != 0)
            {
                Debug.LogWarning($"[LinuxPresenter] could not open the frame segment (code {rc}). " +
                                 "The overlay will not appear. See docs/LINUX_PORT.md.");
                StopPresenterProcess();
                enabled = false;
                return;
            }
        }
        catch (DllNotFoundException)
        {
            // Expected until the native library ships beside the player. Say so once, in
            // terms that name the file, rather than letting a P/Invoke exception surface
            // every frame with no explanation.
            Debug.LogWarning("[LinuxPresenter] libvipsim_present.so not found next to the player. " +
                             "Build it with linux/presenter/build.sh. Running without an overlay.");
            StopPresenterProcess();
            enabled = false;
            return;
        }

        // ScreenCapture hands back the screen with its first row at the bottom under an API
        // whose texture origin is bottom-left, which is every OpenGL target -- and Linux is
        // OpenGL by default. Left uncorrected the overlay is a perfect mirror image of the
        // application, which nothing in any log would ever mention. Logged because it is an
        // assumption about the graphics API, and assumptions should be visible when someone
        // runs this under Vulkan and sees it upside down.
        _flip = !SystemInfo.graphicsUVStartsAtTop;

        _open = true;
        Active = true;

        if (_hosted)
        {
            Debug.Log("[LinuxPresenter] running inside the presenter's compositor; the window " +
                      "is the frame, so no readback is needed.");
        }
        else
        {
            _rt = new RenderTexture(_w, _h, 0, RenderTextureFormat.ARGB32) { name = "VipSimPresent" };
            _rt.Create();
            StartCoroutine(PresentLoop());
        }
        Debug.Log($"[LinuxPresenter] presenting {_w}x{_h} to the Wayland layer surface.");
    }

    /// <summary>
    /// Launch the presenter alongside the player.
    ///
    /// On Windows and macOS VIP-Sim is one process the user starts and that is the whole
    /// story. Wayland forces a second process -- the overlay has to own a layer surface,
    /// and Unity's window cannot become one -- but that is our constraint, not the user's,
    /// and it should not turn into a second thing to launch. So the player starts it.
    ///
    /// The presenter hot-attaches to the shared segment, so it does not matter whether it
    /// comes up before or after the producer opens it.
    /// </summary>
    private static void StartPresenterProcess()
    {
        if (_presenter != null && !_presenter.HasExited) return;

        // Beside the executable first, which is where the tarball puts it; then the plugin
        // directory, which is where a build that treats it as a native artefact would.
        string root = System.IO.Path.GetDirectoryName(Application.dataPath);
        string[] candidates =
        {
            System.IO.Path.Combine(root ?? ".", PresenterName),
            System.IO.Path.Combine(Application.dataPath, "Plugins", "x86_64", PresenterName),
        };

        string exe = null;
        foreach (var c in candidates)
            if (System.IO.File.Exists(c)) { exe = c; break; }

        if (exe == null)
        {
            Debug.LogWarning($"[LinuxPresenter] {PresenterName} not found beside the player. " +
                             "Start it yourself, or build it with linux/presenter/build.sh. " +
                             "Running without an overlay.");
            return;
        }

        try
        {
            // Pull the presenter's own output into the player log. It is a separate process
            // only because Wayland forces it to be, and its messages -- no layer-shell here,
            // attached to the segment, and so on -- are the ones that explain a missing
            // overlay. Leaving them on an inherited stdout means they land wherever the
            // player was started from, which for a double-clicked application is nowhere.
            _presenter = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            _presenter.OutputDataReceived += (_, e) => LogFromPresenter(e.Data);
            _presenter.ErrorDataReceived  += (_, e) => LogFromPresenter(e.Data);
            _presenter.BeginOutputReadLine();
            _presenter.BeginErrorReadLine();
            Debug.Log($"[LinuxPresenter] started {exe} (pid {_presenter.Id}).");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            // Almost always the execute bit, which does not survive a zip and often does
            // not survive a copy off a Windows filesystem. Name the fix rather than the
            // error code.
            Debug.LogWarning($"[LinuxPresenter] could not start {exe} ({e.Message}). " +
                             $"If it is not executable: chmod +x '{exe}'. Running without an overlay.");
            _presenter = null;
        }
    }

    /// <summary>The presenter labels its own lines; only anything else needs a prefix.</summary>
    private static void LogFromPresenter(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        Debug.Log(line.StartsWith("[presenter]") ? line : $"[presenter] {line}");
    }

    private static void StopPresenterProcess()
    {
        if (_presenter == null) return;
        try
        {
            if (!_presenter.HasExited)
            {
                // The presenter also watches the segment's quit flag, which vipsim_present_close
                // sets; this is the backstop for the case where it is wedged or never attached.
                _presenter.Kill();
                _presenter.WaitForExit(2000);
            }
        }
        catch (System.Exception) { /* already gone; nothing to do */ }
        _presenter = null;
    }

    /// <summary>
    /// Tell the presenter which rectangle should receive input. Everything outside it stays
    /// click-through at the compositor level -- no per-event filtering and no focus races,
    /// which is what makes this cleaner than the Windows implementation. Pass a zero-size
    /// rect to make the whole overlay click-through again.
    /// </summary>
    public static void SetInteractiveRect(int x, int y, int w, int h)
    {
        if (!Active) return;
        vipsim_present_set_panel(x, y, w, h);
    }

    /// <summary>
    /// Take each frame from the backbuffer once everything has drawn.
    ///
    /// The obvious source is OnRenderImage, and it is the wrong one twice over. It is
    /// delivered only to a component sitting on a Camera, and it hands over that camera's
    /// image-effect buffer -- the scene, before any ScreenSpaceOverlay canvas or IMGUI
    /// panel has been composited. VIP-Sim's whole interface is drawn after that point, so
    /// the presenter received frames that were 100% transparent while the screen itself
    /// was 20.8% opaque, and the overlay showed nothing while every log line said success.
    ///
    /// WaitForEndOfFrame is the point where the backbuffer holds what the user sees, alpha
    /// included -- the same place VipSimDiagnostics samples to measure it. The copy stays
    /// on the GPU and the readback stays asynchronous, so the render thread is not stalled.
    /// </summary>
    private System.Collections.IEnumerator PresentLoop()
    {
        var endOfFrame = new WaitForEndOfFrame();
        while (_open)
        {
            yield return endOfFrame;
            if (_readbackPending || _rt == null) continue;

            ScreenCapture.CaptureScreenshotIntoRenderTexture(_rt);
            _readbackPending = true;
            AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, OnReadback);
        }
    }

    private void OnReadback(AsyncGPUReadbackRequest req)
    {
        _readbackPending = false;
        if (!_open || req.hasError) return;

        var data = req.GetData<byte>();
        if (_staging == null || _staging.Length != data.Length)
        {
            if (_pin.IsAllocated) _pin.Free();
            _staging = new byte[data.Length];
            // Pinned once for the lifetime rather than per frame: the address is handed to
            // native code every frame, and a buffer this size is exactly what a moving
            // collector would want to relocate at the worst moment.
            _pin = GCHandle.Alloc(_staging, GCHandleType.Pinned);
        }

        // One copy out of the readback into the pinned buffer. It could be skipped with a
        // direct pointer, but that needs unsafe code enabled project-wide, and v1 of this
        // path is a CPU copy in any case -- the zero-copy answer is dmabuf, not this.
        data.CopyTo(_staging);
        vipsim_present_push_rgba32(_pin.AddrOfPinnedObject(), _w * 4, _flip ? 1 : 0);
    }

    private void Update()
    {
        if (!_open) return;

        // Publish the rectangle the user must be able to click.
        //
        // This is load-bearing on Linux in a way it is not elsewhere. When the player runs
        // inside the presenter's own compositor its window is not on the user's screen at
        // all, so the overlay is the only surface they can reach: whatever is left out of
        // this rectangle is not merely click-through, it is unreachable. The toolbar has to
        // be in it, and anything modal takes the whole screen.
        int x = 0, y = 0, w = 0, h = 0;
        if (SymptomInfoOpenHint)
        {
            w = _w; h = _h;
        }
        else if (TryGetToolbarRect(out int tx, out int ty, out int tw, out int th))
        {
            x = tx; y = ty; w = tw; h = th;
        }

        if (x != _rect[0] || y != _rect[1] || w != _rect[2] || h != _rect[3])
        {
            _rect[0] = x; _rect[1] = y; _rect[2] = w; _rect[3] = h;
            SetInteractiveRect(x, y, w, h);
        }
    }

    /// <summary>
    /// The toolbar's screen rectangle, in the compositor's coordinates.
    ///
    /// Unity's screen origin is bottom-left and Wayland's is top-left, so the vertical axis
    /// is flipped here. Getting that wrong puts the clickable region exactly as far from the
    /// toolbar as the toolbar is from the other edge -- which reads as "clicks land in the
    /// wrong place" rather than as an axis convention.
    /// </summary>
    private bool TryGetToolbarRect(out int x, out int y, out int w, out int h)
    {
        x = y = w = h = 0;

        if (_window == null)
            _window = FindAnyObjectByType<TransparentWindow>(FindObjectsInactive.Include);
        var rt = _window != null ? _window.panelRectTransform : null;
        if (rt == null || !rt.gameObject.activeInHierarchy) return false;

        rt.GetWorldCorners(_corners);
        float left = _corners[0].x, bottom = _corners[0].y;
        float right = _corners[2].x, top = _corners[2].y;

        x = Mathf.RoundToInt(left);
        y = Mathf.RoundToInt(_h - top);
        w = Mathf.RoundToInt(right - left);
        h = Mathf.RoundToInt(top - bottom);
        return w > 0 && h > 0;
    }

    /// <summary>
    /// True while a modal panel is up. Mirrors the infoState/tutorialState flags the other
    /// platforms set on TransparentWindow; kept as a single hint here because on Wayland
    /// the compositor needs one rectangle, not a set of booleans.
    /// </summary>
    public static bool SymptomInfoOpenHint { get; set; }

    private void OnDisable()
    {
        if (!_open) return;
        _open = false;
        Active = false;
        vipsim_present_close();
        StopPresenterProcess();
        if (_pin.IsAllocated) _pin.Free();
        _staging = null;
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
    }
}
#endif

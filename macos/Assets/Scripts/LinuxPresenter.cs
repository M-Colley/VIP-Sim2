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

    [DllImport(Lib)] private static extern int vipsim_present_open(int width, int height);
    [DllImport(Lib)] private static extern int vipsim_present_push_rgba32(IntPtr src, int stride);
    [DllImport(Lib)] private static extern void vipsim_present_set_panel(int x, int y, int w, int h);
    [DllImport(Lib)] private static extern void vipsim_present_close();
    [DllImport(Lib)] private static extern uint vipsim_present_frame_count();

    private RenderTexture _rt;
    private bool _open;
    private bool _readbackPending;
    private int _w, _h;
    private int _lastPanelHash;
    private byte[] _staging;
    private GCHandle _pin;

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

        try
        {
            int rc = vipsim_present_open(_w, _h);
            if (rc != 0)
            {
                Debug.LogWarning($"[LinuxPresenter] could not open the frame segment (code {rc}). " +
                                 "The overlay will not appear. See docs/LINUX_PORT.md.");
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
            enabled = false;
            return;
        }

        _rt = new RenderTexture(_w, _h, 0, RenderTextureFormat.ARGB32) { name = "VipSimPresent" };
        _rt.Create();
        _open = true;
        Active = true;
        Debug.Log($"[LinuxPresenter] presenting {_w}x{_h} to the Wayland layer surface.");
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

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        // Pass the image through untouched; this component observes, it does not alter.
        Graphics.Blit(src, dest);
        if (!_open || _readbackPending) return;

        Graphics.Blit(src, _rt);
        _readbackPending = true;
        AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, OnReadback);
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
        vipsim_present_push_rgba32(_pin.AddrOfPinnedObject(), _w * 4);
    }

    private void Update()
    {
        if (!_open) return;

        // Keep the interactive rectangle in step with whichever panel is open. The IMGUI
        // panels already know their own bounds; anything modal wants the whole screen.
        int hash = (SymptomInfoOpenHint ? 1 : 0);
        if (hash != _lastPanelHash)
        {
            _lastPanelHash = hash;
            if (SymptomInfoOpenHint) SetInteractiveRect(0, 0, _w, _h);
            else SetInteractiveRect(0, 0, 0, 0);
        }
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
        if (_pin.IsAllocated) _pin.Free();
        _staging = null;
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
    }
}
#endif

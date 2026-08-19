#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Screen capture on Wayland, through xdg-desktop-portal and PipeWire.
///
/// This replaces the window list rather than reimplementing it. On Windows VIP-Sim
/// enumerates other applications' windows and grabs their pixels; Wayland does not allow
/// that, deliberately -- a client cannot see, list or read another client's surface. So
/// there is nothing to enumerate, and the compositor's own picker takes the place of the
/// list: the user is asked, by the system, and hands over one source.
///
/// The closest thing on the other platforms is macOS's Screen Recording permission, which
/// users already understand. Worth saying in the UI rather than leaving the missing window
/// list to look like a bug.
///
/// Unlike the overlay half of this port, capture works on GNOME too, since the portal is
/// supported everywhere while layer-shell is not.
/// </summary>
public class LinuxCapture : MonoBehaviour
{
    private const string Lib = "vipsim_capture";

    [DllImport(Lib)] private static extern int vipsim_capture_init();
    [DllImport(Lib)] private static extern int vipsim_capture_start();
    [DllImport(Lib)] private static extern int vipsim_capture_state();
    [DllImport(Lib)] private static extern IntPtr vipsim_capture_message();
    [DllImport(Lib)] private static extern int vipsim_capture_frame_size(out int w, out int h);
    [DllImport(Lib)] private static extern uint vipsim_capture_copy_frame(IntPtr dst, int stride);
    [DllImport(Lib)] private static extern void vipsim_capture_stop();

    public enum State { Idle = 0, WaitingForUser = 1, Streaming = 2, Failed = 3, NoPortal = 4 }

    private bool _available;
    private uint _lastSeq;
    private byte[] _staging;
    private GCHandle _pin;

    /// <summary>The captured source, or null until the user has picked one.</summary>
    public static Texture2D Texture { get; private set; }

    public static State Status =>
        _instance != null && _instance._available ? (State)vipsim_capture_state() : State.NoPortal;

    /// <summary>Human-readable detail for the F1 panel; never null.</summary>
    public static string Message
    {
        get
        {
            if (_instance == null || !_instance._available) return "Screen capture is unavailable.";
            IntPtr p = vipsim_capture_message();
            return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p) ?? "";
        }
    }

    private static LinuxCapture _instance;

    public static void Install(GameObject host)
    {
        if (host.GetComponent<LinuxCapture>() == null) host.AddComponent<LinuxCapture>();
    }

    /// <summary>
    /// Ask the compositor for a source. This opens the portal's own picker, so it must be
    /// triggered by something the user did -- a button, not startup.
    /// </summary>
    public static void RequestSource()
    {
        if (_instance == null || !_instance._available) return;
        int rc = vipsim_capture_start();
        if (rc != 0) Debug.LogWarning($"[LinuxCapture] capture request failed ({rc}): {Message}");
    }

    private bool _requested;

    private void Awake() => _instance = this;

    private void Start()
    {
        try
        {
            _available = vipsim_capture_init() == 0;
        }
        catch (DllNotFoundException)
        {
            Debug.LogWarning("[LinuxCapture] libvipsim_capture.so not found next to the player. " +
                             "Build it with linux/presenter/build.sh. Running without capture.");
            _available = false;
        }

        if (!_available)
        {
            // Not a crash and not a bug: a machine with no desktop portal genuinely cannot
            // offer screen capture, and saying which piece is missing beats an empty list.
            Debug.LogWarning($"[LinuxCapture] {Message}");
            enabled = false;
        }
    }

    private void Update()
    {
        if (!_available) return;

        // Raise the picker once, as soon as the user is looking at the tool rather than at
        // the walkthrough.
        //
        // On Windows and macOS this is a window list VIP-Sim draws itself. Wayland will not
        // let a client enumerate anyone else's windows, so the compositor's own picker takes
        // its place -- which means there is no list to click, and something has to ask for
        // it. Asking during the tutorial would put a system dialog over the explanation of
        // what the tool does, so it waits for that to close.
        if (!_requested && !FirstRunTutorial.IsOpen)
        {
            _requested = true;
            Debug.Log("[LinuxCapture] asking the compositor for a source -- on Wayland its " +
                      "picker replaces VIP-Sim's window list.");
            RequestSource();
        }

        if ((State)vipsim_capture_state() != State.Streaming) return;
        if (vipsim_capture_frame_size(out int w, out int h) != 0 || w <= 0 || h <= 0) return;

        if (Texture == null || Texture.width != w || Texture.height != h)
        {
            // BGRA32 matches what the portal is asked for, so no per-pixel work is needed
            // on this side at all.
            Texture = new Texture2D(w, h, TextureFormat.BGRA32, false);
            if (_pin.IsAllocated) _pin.Free();
            _staging = new byte[w * h * 4];
            _pin = GCHandle.Alloc(_staging, GCHandleType.Pinned);
            Debug.Log($"[LinuxCapture] source is {w}x{h}");
        }

        uint seq = vipsim_capture_copy_frame(_pin.AddrOfPinnedObject(), w * 4);
        if (seq == 0 || seq == _lastSeq) return;   // nothing new; skip the upload
        _lastSeq = seq;

        Texture.LoadRawTextureData(_staging);
        Texture.Apply(false, false);
    }

    private void OnDisable()
    {
        if (_available) vipsim_capture_stop();
        if (_pin.IsAllocated) _pin.Free();
        _staging = null;
    }
}
#endif

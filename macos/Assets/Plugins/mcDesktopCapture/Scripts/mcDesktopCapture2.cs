using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace mcDesktopCapture
{
    [Serializable]
    public class WindowProperty
    {
        [Serializable]
        public class Frame
        {
            public int width;
            public int height;
        }
        [Serializable]
        public class RunningApplication
        {
            public string applicationName;
            public string bundleIdentifier;
        }
        public int windowID;
        public string title;
        public bool isOnScreen;
        public RunningApplication owningApplication;
        public Frame frame;
    }

    [Serializable]
    public class WindowList
    {
        public int count;
        public WindowProperty[] windows;
    }

    [Serializable]
    public class StartCaptureConfig
    {
        public int windowID;
        public int width;
        public int height;
        public bool showsCursor;
    }

    public static class DesktopCapture2
    {
        [DllImport("mcDesktopCapture")]
        private static extern long mcDesktopCapture2_addTwo(long src);

        [DllImport("mcDesktopCapture")]
        private static extern string mcDesktopCapture2_init();

        [DllImport("mcDesktopCapture")]
        private static extern void mcDesktopCapture2_destroy();

        [DllImport("mcDesktopCapture")]
        private static extern string mcDesktopCapture2_windows();

        [DllImport("mcDesktopCapture")]
        private static extern void mcDesktopCapture2_startWithWindowID(string config);

        [DllImport("mcDesktopCapture")]
        private static extern void mcDesktopCapture2_stop();

        [StructLayout(LayoutKind.Sequential)]
        struct FrameEntity
        {
            public long width;
            public long height;
            public IntPtr texturePtr;
        }
        [DllImport("mcDesktopCapture")]
        private static extern FrameEntity mcDesktopCapture2_getTexture();

        private static bool inited = false;
        private static bool isRunning = false;
        private static Texture2D _texture = null;

        /// <summary>
        /// Get the list of windows that can be captured.
        /// This property must be called after Init.
        /// </summary>
        public static WindowProperty[] WindowList
        {
            get
            {
                var str = mcDesktopCapture2_windows();
                var list = JsonUtility.FromJson<WindowList>(str);
                return list.windows;
            }
        }

        /// <summary>
        /// Initialize mcDesktopCapture2.
        /// </summary>
        public static void Init()
        {
            if (inited) return;
            string res = mcDesktopCapture2_init();
            inited = res == "completed";
            if (!inited) Log($"failed to init: {res}");
        }

        /// <summary>
        /// Close mcDesktopCapture2.
        /// This function must be called after Init.
        /// </summary>
        public static void Destroy()
        {
            if (!inited) return;
            mcDesktopCapture2_destroy();
            inited = false;
        }

        /// <summary>
        /// Start capturing the display.
        /// This function must be called after Init.
        /// </summary>
        /// <param name="windowID">Captures only the specified windowID.</param>
        /// <param name="width">The width of the output.</param>
        /// <param name="height">The height of the output.</param>
        /// <param name="showsCursor">A rectangle that specifies the source area to capture.</param>
        public static void StartCaptureWithWindowID(int windowID, int width, int height, bool showsCursor)
        {
            if (!inited || isRunning) return;
            isRunning = true;
            var config = new StartCaptureConfig
            {
                windowID = windowID,
                width = width,
                height = height,
                showsCursor = showsCursor
            };
            var str = JsonUtility.ToJson(config);
            Log($"mcDesktopCapture2: Start Capture: {str}");
            mcDesktopCapture2_startWithWindowID(str);
        }

        /// <summary>
        /// Stop capturing the display.
        /// This function must be called after Init.
        /// </summary>
        public static void StopCapture()
        {
            if (!inited || !isRunning) return;
            // Destroy the managed wrapper rather than just dropping the reference,
            // otherwise every start/stop cycle leaks a Texture2D.
            DestroyTextureWrapper();
            Log("mcDesktopCapture: Stop Capture");
            mcDesktopCapture2_stop();
            isRunning = false;
        }

        /// <summary>
        /// Get captured video frame.
        /// This function must be called after Init.
        /// </summary>
        /// <returns>If null, there is no frame yet received.</returns>
        /// <remarks>
        /// VIP-Sim fix: the original implementation created the external-texture
        /// wrapper once and then returned the cached instance forever. ScreenCaptureKit
        /// reallocates its backing surface when the captured window is resized (and
        /// when a display's scale factor changes), so the cached wrapper kept pointing
        /// at a native texture that had been freed -- showing a frozen or garbage frame,
        /// and risking a use-after-free. The native pointer and dimensions are now
        /// re-checked every call: the wrapper is rebound with UpdateExternalTexture
        /// when only the pointer moved, and fully recreated when the size changed.
        /// </remarks>
        public static Texture2D GetTexture2D()
        {
            if (!inited || !isRunning) return null;

            FrameEntity frame = mcDesktopCapture2_getTexture();
            if (frame.width <= 0 || frame.height <= 0 || frame.texturePtr == IntPtr.Zero)
                return _texture; // no frame yet; keep showing the previous one

            int w = (int)frame.width;
            int h = (int)frame.height;

            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                Log($"mcDesktopCapture: (re)creating Texture2D {w}x{h}");
                DestroyTextureWrapper();
                _texture = Texture2D.CreateExternalTexture(w, h, TextureFormat.ARGB32, false, false, frame.texturePtr);
                _lastTexturePtr = frame.texturePtr;
            }
            else if (frame.texturePtr != _lastTexturePtr)
            {
                // Same dimensions, new backing surface: rebind rather than reallocate.
                _texture.UpdateExternalTexture(frame.texturePtr);
                _lastTexturePtr = frame.texturePtr;
            }

            return _texture;
        }

        /// <summary>
        /// Whether the app currently holds macOS Screen Recording permission.
        /// Without it ScreenCaptureKit silently yields black frames, which previously
        /// looked to users like a broken overlay rather than a missing grant.
        /// </summary>
        public static bool HasScreenRecordingPermission()
        {
            if (!inited) return false;
            try
            {
                // SCShareableContent returns an empty window list when permission is denied.
                var windows = WindowList;
                return windows != null && windows.Length > 0;
            }
            catch (Exception e)
            {
                Log($"mcDesktopCapture: permission probe failed: {e.Message}");
                return false;
            }
        }

        private static IntPtr _lastTexturePtr = IntPtr.Zero;

        private static void DestroyTextureWrapper()
        {
            if (_texture == null) return;
            // The native texture is owned by ScreenCaptureKit; only the managed
            // wrapper is ours to destroy.
            UnityEngine.Object.Destroy(_texture);
            _texture = null;
            _lastTexturePtr = IntPtr.Zero;
        }

        private static void Log(object message)
        {
            Debug.Log(message);
        }
    }
}

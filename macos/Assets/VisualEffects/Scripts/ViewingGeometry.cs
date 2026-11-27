using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace VisSim
{
    /// <summary>
    /// Converts visual angle to screen pixels.
    ///
    /// Blur, nystagmus and the prism-dioptre double vision (myDoubleVision, which the effect
    /// list does not use) are specified in degrees of visual angle. The number of pixels in
    /// one degree depends on the screen's physical width and on the viewing distance. Each
    /// effect used to assume a fixed angle for the whole screen width (80 or 100 degrees). A
    /// 24-inch screen viewed from 60 cm spans 48 degrees, so there a degree came out 1.6
    /// times too small for blur and 2.0 times too small for the other two.
    ///
    /// The screen width is read from the monitor's EDID where the platform exposes it:
    /// the registry copy on Windows, CoreGraphics on macOS with a single display. The viewing
    /// distance cannot be measured, so it is a setting with a default of 60 cm. Both can be
    /// changed in the Display tab of the help panel. They are stored in PlayerPrefs because
    /// they describe the hardware, so a settings file or profile does not carry them.
    /// </summary>
    public static class ViewingGeometry
    {
        public const float DefaultDistanceCm = 60f;
        public const float MinDistanceCm = 20f, MaxDistanceCm = 300f;
        public const float MinWidthCm = 10f, MaxWidthCm = 500f;

        /// <summary>Used when the monitor reports no size: the width of a 24-inch 16:9 panel.</summary>
        public const float AssumedWidthCm = 53.1f;

        private const string DistancePref = "VipSim.ViewingDistanceCm";
        private const string WidthPref = "VipSim.ScreenWidthCm";

        public enum WidthSource { Entered, Monitor, Assumed }

        /// <summary>
        /// The overlay's window on Windows, set by TransparentWindow. It selects the monitor
        /// whose size is read. Without it the primary monitor is used.
        /// </summary>
        public static IntPtr WindowHandle { get; set; }

        private static bool _loaded;
        private static float _distanceCm = DefaultDistanceCm;
        private static float _enteredWidthCm;
        private static float? _fixedWidthCm, _fixedDistanceCm;
        private static float _monitorWidthCm;
        private static string _monitorName = "";
        private static long _monitorKey = long.MinValue;
        private static int _checkedFrame = -1;
        private static string _lastLog;

        /// <summary>Eye to screen distance in centimetres. Persisted.</summary>
        public static float DistanceCm
        {
            get { Load(); return _fixedDistanceCm ?? _distanceCm; }
            set
            {
                Load();
                _distanceCm = Mathf.Clamp(value, MinDistanceCm, MaxDistanceCm);
                PlayerPrefs.SetFloat(DistancePref, _distanceCm);
                PlayerPrefs.Save();
                LogGeometry();
            }
        }

        /// <summary>
        /// Screen width entered by the user in centimetres, or 0 to use the monitor's own value.
        /// Persisted.
        /// </summary>
        public static float EnteredWidthCm
        {
            get { Load(); return _enteredWidthCm; }
            set
            {
                Load();
                _enteredWidthCm = value > 0f ? Mathf.Clamp(value, MinWidthCm, MaxWidthCm) : 0f;
                PlayerPrefs.SetFloat(WidthPref, _enteredWidthCm);
                PlayerPrefs.Save();
                LogGeometry();
            }
        }

        /// <summary>Width the monitor reports for its image area, or 0 if it reports none.</summary>
        public static float MonitorWidthCm
        {
            get { Refresh(); return _monitorWidthCm; }
        }

        public static string MonitorName
        {
            get { Refresh(); return _monitorName; }
        }

        public static WidthSource Source
        {
            get
            {
                if (_fixedWidthCm.HasValue || EnteredWidthCm > 0f) return WidthSource.Entered;
                return MonitorWidthCm > 0f ? WidthSource.Monitor : WidthSource.Assumed;
            }
        }

        /// <summary>Physical width of the screen area the overlay covers, in centimetres.</summary>
        public static float WidthCm
        {
            get
            {
                if (_fixedWidthCm.HasValue) return _fixedWidthCm.Value;
                if (EnteredWidthCm > 0f) return EnteredWidthCm;
                float monitor = MonitorWidthCm;
                return monitor > 0f ? monitor : AssumedWidthCm;
            }
        }

        /// <summary>Pixels across the screen. The overlay always covers its whole display.</summary>
        public static int PixelsAcross => Mathf.Max(1, Screen.width);

        /// <summary>Pixels in one degree of visual angle at the screen centre.</summary>
        public static float PixelsPerDegree => PixelsPerDegreeFor(PixelsAcross, WidthCm, DistanceCm);

        /// <summary>Horizontal visual angle the whole screen subtends, in degrees.</summary>
        public static float ScreenSpanDeg => 2f * Mathf.Atan(WidthCm / (2f * DistanceCm)) * Mathf.Rad2Deg;

        /// <summary>
        /// Pixels in the one degree centred on the line of sight. Off-centre degrees cover more
        /// pixels, by 1/cos^2 of the eccentricity: 7% more at 15 degrees.
        /// </summary>
        public static float PixelsPerDegreeFor(float pixelsAcross, float widthCm, float distanceCm)
        {
            return pixelsAcross / widthCm * 2f * distanceCm * Mathf.Tan(0.5f * Mathf.Deg2Rad);
        }

        /// <summary>True while the distance or the width is fixed by SetFixed.</summary>
        public static bool DistanceFixed => _fixedDistanceCm.HasValue;
        public static bool WidthFixed => _fixedWidthCm.HasValue;

        /// <summary>
        /// Fixes the geometry for an organisation's policy file, the effect gallery and tests,
        /// without touching the saved settings. Null restores the setting.
        /// </summary>
        public static void SetFixed(float? widthCm, float? distanceCm)
        {
            _fixedWidthCm = widthCm;
            _fixedDistanceCm = distanceCm;
            LogGeometry();
        }

        /// <summary>
        /// Image size in millimetres from a 128-byte EDID base block, or null if it gives none.
        /// The first detailed timing descriptor has the size in millimetres. Some monitors put
        /// centimetres or nothing there, so it is used only when it agrees with the basic size
        /// in bytes 21 and 22, which is in whole centimetres.
        /// </summary>
        public static Vector2? EdidImageSizeMm(byte[] edid)
        {
            if (edid == null || edid.Length < 128) return null;
            for (int i = 0; i < 8; i++)
                if (edid[i] != (i == 0 || i == 7 ? 0x00 : 0xFF)) return null;

            // Both zero means the size is unknown or variable (a projector). One zero means
            // the other byte is an aspect ratio.
            int cmW = edid[21], cmH = edid[22];
            if (cmW == 0 || cmH == 0) return null;

            // A pixel clock of zero marks a display descriptor, which has no size.
            if (edid[54] != 0 || edid[55] != 0)
            {
                int mmW = edid[66] | ((edid[68] & 0xF0) << 4);
                int mmH = edid[67] | ((edid[68] & 0x0F) << 8);
                if (mmW > 0 && mmH > 0 && Math.Abs(mmW - 10 * cmW) <= 15 && Math.Abs(mmH - 10 * cmH) <= 15)
                    return new Vector2(mmW, mmH);
            }
            return new Vector2(10 * cmW, 10 * cmH);
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _distanceCm = Mathf.Clamp(PlayerPrefs.GetFloat(DistancePref, DefaultDistanceCm), MinDistanceCm, MaxDistanceCm);
            float w = PlayerPrefs.GetFloat(WidthPref, 0f);
            _enteredWidthCm = w >= MinWidthCm && w <= MaxWidthCm ? w : 0f;
        }

        /// <summary>Reads the monitor's size again when the overlay is on a different monitor.</summary>
        private static void Refresh()
        {
            if (_checkedFrame == Time.frameCount) return;
            _checkedFrame = Time.frameCount;

            long key;
            try { key = MonitorKey(); }
            catch (Exception) { key = 0; }
            if (key == _monitorKey) return;
            _monitorKey = key;

            _monitorWidthCm = 0f;
            _monitorName = "";
            try
            {
                float w = DetectWidthCm(out _monitorName);
                if (w >= MinWidthCm && w <= MaxWidthCm) _monitorWidthCm = w;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ViewingGeometry] could not read the monitor's size: {e.GetType().Name}: {e.Message}");
            }
            LogGeometry();
        }

        private static void LogGeometry()
        {
            string source = Source == WidthSource.Monitor ? "reported by the monitor"
                          : Source == WidthSource.Entered ? "entered"
                          : "assumed; the monitor reports no size";
            string line = $"[ViewingGeometry] {(_monitorName.Length > 0 ? _monitorName : "screen")}: " +
                          $"{WidthCm:0.0} cm wide ({source}), {PixelsAcross} px, viewed from {DistanceCm:0} cm: " +
                          $"1 degree = {PixelsPerDegree:0.0} px, the screen spans {ScreenSpanDeg:0.0} degrees";
            if (line == _lastLog) return;
            _lastLog = line;
            Debug.Log(line);
        }

        private static long MonitorKey()
        {
            switch (Application.platform)
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return MonitorFromWindow(WindowHandle, MONITOR_DEFAULTTOPRIMARY).ToInt64();
#endif
                default:
                    return ((long)Screen.width << 32) | (uint)Screen.height;
            }
        }

        private static float DetectWidthCm(out string name)
        {
            name = "";
            switch (Application.platform)
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return DetectWindows(out name);
#endif
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return DetectMac(out name);
#endif
                default:
                    return 0f;
            }
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint MONITOR_DEFAULTTOPRIMARY = 1;
        private const uint QDC_ONLY_ACTIVE_PATHS = 2;
        private const uint GET_SOURCE_NAME = 1, GET_TARGET_NAME = 2;
        private const uint EDID_IDS_VALID = 4;
        private const uint RRF_RT_REG_BINARY = 0x08;
        private const int KEY_READ = 0x20019;
        private const string DisplayEnum = @"SYSTEM\CurrentControlSet\Enum\DISPLAY";
        private static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor, rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint low; public int high; }

        [StructLayout(LayoutKind.Sequential)]
        private struct PATH_SOURCE_INFO { public LUID adapterId; public uint id, modeInfoIdx, statusFlags; }

        [StructLayout(LayoutKind.Sequential)]
        private struct PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id, modeInfoIdx, outputTechnology, rotation, scaling, refreshNum, refreshDen, scanLineOrdering;
            public int targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PATH_INFO { public PATH_SOURCE_INFO source; public PATH_TARGET_INFO target; public uint flags; }

        // Only the size matters; the union inside is never read.
        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct MODE_INFO { public uint infoType, id; public LUID adapterId; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVICE_INFO_HEADER { public uint type, size; public LUID adapterId; public uint id; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SOURCE_DEVICE_NAME
        {
            public DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TARGET_DEVICE_NAME
        {
            public DEVICE_INFO_HEADER header;
            public uint flags, outputTechnology;
            public ushort edidManufactureId, edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint pathCount, [Out] PATH_INFO[] paths,
                                                     ref uint modeCount, [Out] MODE_INFO[] modes, IntPtr topology);

        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        private static extern int GetDeviceInfo(ref SOURCE_DEVICE_NAME request);

        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        private static extern int GetDeviceInfo(ref TARGET_DEVICE_NAME request);

        [DllImport("advapi32.dll", EntryPoint = "RegGetValueW", CharSet = CharSet.Unicode)]
        private static extern int RegGetValue(IntPtr hkey, string subKey, string value, uint flags,
                                              IntPtr type, byte[] data, ref uint size);

        [DllImport("advapi32.dll", EntryPoint = "RegOpenKeyExW", CharSet = CharSet.Unicode)]
        private static extern int RegOpenKeyEx(IntPtr hkey, string subKey, uint options, int access, out IntPtr result);

        [DllImport("advapi32.dll", EntryPoint = "RegEnumKeyExW", CharSet = CharSet.Unicode)]
        private static extern int RegEnumKeyEx(IntPtr hkey, uint index, StringBuilder name, ref uint nameLength,
                                               IntPtr reserved, IntPtr className, IntPtr classLength, IntPtr lastWrite);

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr hkey);

        // The overlay window's monitor, then the display path that drives it, then the EDID
        // Windows keeps in the registry for that monitor.
        private static float DetectWindows(out string name)
        {
            name = "";
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (!GetMonitorInfo(MonitorFromWindow(WindowHandle, MONITOR_DEFAULTTOPRIMARY), ref mi)) return 0f;
            name = mi.szDevice;

            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0) return 0f;
            var paths = new PATH_INFO[pathCount];
            var modes = new MODE_INFO[modeCount];
            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                return 0f;

            for (int i = 0; i < pathCount; i++)
            {
                var source = new SOURCE_DEVICE_NAME
                {
                    header = new DEVICE_INFO_HEADER
                    {
                        type = GET_SOURCE_NAME, size = (uint)Marshal.SizeOf<SOURCE_DEVICE_NAME>(),
                        adapterId = paths[i].source.adapterId, id = paths[i].source.id
                    }
                };
                if (GetDeviceInfo(ref source) != 0 || source.viewGdiDeviceName != mi.szDevice) continue;

                var target = new TARGET_DEVICE_NAME
                {
                    header = new DEVICE_INFO_HEADER
                    {
                        type = GET_TARGET_NAME, size = (uint)Marshal.SizeOf<TARGET_DEVICE_NAME>(),
                        adapterId = paths[i].target.adapterId, id = paths[i].target.id
                    }
                };
                if (GetDeviceInfo(ref target) != 0) return 0f;
                name = $"{mi.szDevice} {target.monitorFriendlyDeviceName}".Trim();
                return RegistryEdidWidthCm(target);
            }
            return 0f;
        }

        // The EDID of each monitor Windows has seen is under DISPLAY\<model>\<instance>\Device
        // Parameters, where the model is the EDID manufacturer and product code (DEL42A3). The
        // device path names the instance. It is empty when the monitor's device is not
        // enumerated, which happens with working monitors. Then any instance of the same model
        // is used, since one model has one panel size.
        private static float RegistryEdidWidthCm(TARGET_DEVICE_NAME target)
        {
            string[] parts = (target.monitorDevicePath ?? "").Split('#');
            if (parts.Length >= 3)
            {
                float w = RegistryEdidWidthCm($@"{DisplayEnum}\{parts[1]}\{parts[2]}\Device Parameters");
                if (w > 0f) return w;
            }
            if ((target.flags & EDID_IDS_VALID) == 0) return 0f;

            string modelKey = $@"{DisplayEnum}\{PnpId(target.edidManufactureId)}{target.edidProductCodeId:X4}";
            if (RegOpenKeyEx(HKEY_LOCAL_MACHINE, modelKey, 0, KEY_READ, out IntPtr key) != 0) return 0f;
            try
            {
                var instance = new StringBuilder(256);
                for (uint i = 0; i < 64; i++)
                {
                    uint length = (uint)instance.Capacity;
                    if (RegEnumKeyEx(key, i, instance, ref length, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) != 0)
                        break;
                    float w = RegistryEdidWidthCm($@"{modelKey}\{instance}\Device Parameters");
                    if (w > 0f) return w;
                }
            }
            finally
            {
                RegCloseKey(key);
            }
            return 0f;
        }

        private static float RegistryEdidWidthCm(string key)
        {
            uint size = 0;
            if (RegGetValue(HKEY_LOCAL_MACHINE, key, "EDID", RRF_RT_REG_BINARY, IntPtr.Zero, null, ref size) != 0 || size == 0)
                return 0f;
            var edid = new byte[size];
            if (RegGetValue(HKEY_LOCAL_MACHINE, key, "EDID", RRF_RT_REG_BINARY, IntPtr.Zero, edid, ref size) != 0)
                return 0f;
            Vector2? mm = EdidImageSizeMm(edid);
            return mm.HasValue ? mm.Value.x / 10f : 0f;
        }

        // EDID packs the manufacturer as three 5-bit letters in two big-endian bytes.
        // DisplayConfig returns those bytes as a little-endian ushort.
        private static string PnpId(ushort raw)
        {
            int v = (raw >> 8) | ((raw & 0xFF) << 8);
            return new string(new[]
            {
                (char)('A' - 1 + ((v >> 10) & 31)), (char)('A' - 1 + ((v >> 5) & 31)), (char)('A' - 1 + (v & 31))
            });
        }
#endif

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

        [StructLayout(LayoutKind.Sequential)]
        private struct CGSize { public double width, height; }

        [DllImport(CoreGraphics)]
        private static extern uint CGMainDisplayID();

        [DllImport(CoreGraphics)]
        private static extern int CGGetActiveDisplayList(uint maxDisplays, uint[] displays, out uint count);

        [DllImport(CoreGraphics)]
        private static extern CGSize CGDisplayScreenSize(uint display);

        [DllImport(CoreGraphics)]
        private static extern UIntPtr CGDisplayPixelsWide(uint display);

        /// <summary>
        /// CoreGraphics reads the size from EDID. With several displays it is not known here
        /// which one the overlay is on, so the size is left unknown and can be entered.
        /// </summary>
        private static float DetectMac(out string name)
        {
            name = "";
            if (CGGetActiveDisplayList(0, null, out uint count) != 0 || count != 1)
            {
                name = $"{count} displays";
                return 0f;
            }
            uint id = CGMainDisplayID();
            name = $"display {id}";
            CGSize size = CGDisplayScreenSize(id);
            // Without valid EDID, CoreGraphics returns the size at 72 dpi (2.835 px per mm).
            double at72dpi = CGDisplayPixelsWide(id).ToUInt64() / 2.835;
            if (size.width <= 0 || Math.Abs(size.width - at72dpi) < 1.0) return 0f;
            return (float)(size.width / 10.0);
        }
#endif
    }
}

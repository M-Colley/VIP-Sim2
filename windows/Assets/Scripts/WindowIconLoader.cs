using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Loads a high-resolution application icon for a window, from the icon resource inside
/// the owning process's executable.
///
/// uWindowCapture asks the WINDOW for its icon, which has two problems. The window icon is
/// whatever small icon the app registered -- typically 16x16 or 32x32 -- and the list draws
/// it at roughly 100px, so it arrives visibly blocky and no filtering can invent the
/// missing detail. And plenty of applications never register one at all: Electron apps
/// (Claude among them) are a common case, and uWindowCapture then falls back to its
/// uWC_No_Image placeholder, so the entry is unidentifiable.
///
/// Executables carry icon resources at several sizes, usually up to 256x256, and they
/// always carry at least one. Going to the .exe therefore fixes the resolution and the
/// missing icons in one move.
///
/// Results are cached per executable path -- many windows share one process, and several
/// processes share one executable -- and failures are cached too, so a protected process
/// is not re-queried every time the list refreshes.
/// </summary>
public static class WindowIconLoader
{
    // Requested icon size. The list draws these at about 100px on a 4K display, so 128 is
    // comfortably sharp while still matching a resource most executables actually contain;
    // asking for 256 more often means Windows upscaling a smaller resource, which looks
    // softer than simply asking for a size that exists.
    private const int IconSize = 128;

    private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
    private static readonly HashSet<string> _failed = new HashSet<string>();
    private static readonly HashSet<int> _unresolvableProcesses = new HashSet<int>();

    /// <summary>
    /// Icon for the executable behind <paramref name="processId"/>, or null if it cannot be
    /// read. Null is a normal outcome -- protected and elevated processes cannot be queried
    /// from a non-elevated app -- and callers should keep whatever they were showing.
    /// </summary>
    public static Texture2D GetIcon(int processId)
    {
        if (processId <= 0 || _unresolvableProcesses.Contains(processId)) return null;

        var path = GetExecutablePath(processId);
        if (string.IsNullOrEmpty(path))
        {
            _unresolvableProcesses.Add(processId);
            return null;
        }

        if (_cache.TryGetValue(path, out var cached)) return cached;
        if (_failed.Contains(path)) return null;

        var tex = Extract(path);
        if (tex == null) { _failed.Add(path); return null; }

        _cache[path] = tex;
        return tex;
    }

    private static string GetExecutablePath(int processId)
    {
        // QueryFullProcessImageName with PROCESS_QUERY_LIMITED_INFORMATION rather than
        // Process.MainModule: MainModule throws Win32Exception for any process at a higher
        // integrity level, and for 64-bit processes when read from a 32-bit player. The
        // limited-information right is granted much more widely and is all that is needed
        // to read the image path.
        IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (handle == IntPtr.Zero) return null;

        try
        {
            var buffer = new System.Text.StringBuilder(1024);
            int size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static Texture2D Extract(string exePath)
    {
        IntPtr hIcon = IntPtr.Zero;
        try
        {
            // PrivateExtractIcons asks for a specific size and lets Windows choose the best
            // matching resource, which is what makes this worth doing at all --
            // ExtractIconEx only ever returns the system large icon (32x32) and would leave
            // the result nearly as blocky as the window icon it replaces.
            int extracted = PrivateExtractIcons(exePath, 0, IconSize, IconSize,
                                                out hIcon, out _, 1, 0);
            if (extracted <= 0 || hIcon == IntPtr.Zero) return null;

            return IconToTexture(hIcon);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WindowIconLoader] Could not read an icon from '{exePath}': " +
                             $"{e.GetType().Name}: {e.Message}");
            return null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
        }
    }

    private static Texture2D IconToTexture(IntPtr hIcon)
    {
        if (!GetIconInfo(hIcon, out ICONINFO info)) return null;

        IntPtr hdc = IntPtr.Zero;
        try
        {
            if (info.hbmColor == IntPtr.Zero) return null;
            if (GetObject(info.hbmColor, Marshal.SizeOf(typeof(BITMAP)), out BITMAP bmp) == 0) return null;

            int w = bmp.bmWidth, h = bmp.bmHeight;
            if (w <= 0 || h <= 0) return null;

            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth = w,
                // POSITIVE height, so GDI hands back a bottom-up DIB. Unity's raw texture
                // data is also bottom-up, so the rows land the right way round with no
                // flip; a top-down DIB (negative height) would arrive upside down.
                biHeight = h,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0
            };
            var bmi = new BITMAPINFO { bmiHeader = header };

            var pixels = new byte[w * h * 4];
            hdc = GetDC(IntPtr.Zero);
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            int scanned;
            try
            {
                scanned = GetDIBits(hdc, info.hbmColor, 0, (uint)h,
                                    handle.AddrOfPinnedObject(), ref bmi, DIB_RGB_COLORS);
            }
            finally
            {
                handle.Free();
            }
            if (scanned == 0) return null;

            ApplyMaskIfOpaque(pixels, info.hbmMask, hdc, w, h, ref bmi);

            var tex = new Texture2D(w, h, TextureFormat.BGRA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.LoadRawTextureData(pixels);
            tex.Apply(false, false);
            return tex;
        }
        finally
        {
            if (hdc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hdc);
            if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
            if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
        }
    }

    /// <summary>
    /// Older icon resources carry no alpha channel -- every pixel comes back with alpha 0,
    /// which would render the icon completely invisible. Their transparency lives in a
    /// separate 1-bit mask bitmap instead, where a set bit means "transparent". Only
    /// consulted when the colour bitmap turns out to be fully transparent, so modern
    /// 32-bit icons keep their real, smoothly antialiased alpha.
    /// </summary>
    private static void ApplyMaskIfOpaque(byte[] pixels, IntPtr hbmMask, IntPtr hdc,
                                          int w, int h, ref BITMAPINFO bmi)
    {
        for (int i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 0) return; // has a real alpha channel; leave it alone

        if (hbmMask == IntPtr.Zero)
        {
            // No mask either: treat it as fully opaque rather than leaving an icon that
            // exists but cannot be seen.
            for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            return;
        }

        var mask = new byte[w * h * 4];
        var handle = GCHandle.Alloc(mask, GCHandleType.Pinned);
        int scanned;
        try
        {
            scanned = GetDIBits(hdc, hbmMask, 0, (uint)h,
                                handle.AddrOfPinnedObject(), ref bmi, DIB_RGB_COLORS);
        }
        finally
        {
            handle.Free();
        }

        for (int i = 0; i < pixels.Length; i += 4)
            pixels[i + 3] = (byte)(scanned != 0 && mask[i] != 0 ? 0 : 255);
    }

    #region Win32

    private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] bmiColors;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags,
                                                         System.Text.StringBuilder buffer, ref int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int PrivateExtractIcons(string file, int index, int width, int height,
                                                  out IntPtr icon, out int id, int count, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr icon, out ICONINFO info);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr handle, int count, out BITMAP bitmap);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr bitmap, uint start, uint lines,
                                        IntPtr bits, ref BITMAPINFO info, uint usage);

    #endregion
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VipSim.Capture
{
    /// <summary>
    /// A capturable thing the user can pick: a window, a display, or the whole desktop.
    /// Deliberately platform-neutral -- the numeric handle means different things on
    /// each backend (HWND on Windows, CGWindowID on macOS, PipeWire node id on Linux)
    /// and must never be interpreted outside the backend that produced it.
    /// </summary>
    public readonly struct CaptureTarget : IEquatable<CaptureTarget>
    {
        public enum Kind { Window, Display, Desktop }

        public readonly long Handle;
        public readonly string Title;
        public readonly string Application;
        public readonly int Width;
        public readonly int Height;
        public readonly Kind TargetKind;

        public CaptureTarget(long handle, string title, string application, int width, int height, Kind kind)
        {
            Handle = handle;
            Title = title ?? string.Empty;
            Application = application ?? string.Empty;
            Width = width;
            Height = height;
            TargetKind = kind;
        }

        public string DisplayName =>
            string.IsNullOrWhiteSpace(Application) ? Title
            : string.IsNullOrWhiteSpace(Title) ? Application
            : Application;

        public bool Equals(CaptureTarget other) => Handle == other.Handle && TargetKind == other.TargetKind;
        public override bool Equals(object obj) => obj is CaptureTarget o && Equals(o);
        public override int GetHashCode() => (Handle.GetHashCode() * 397) ^ (int)TargetKind;
        public override string ToString() => $"{TargetKind} '{DisplayName}' ({Width}x{Height}) #{Handle}";
    }

    /// <summary>
    /// Backend-agnostic desktop capture.
    ///
    /// Why this exists: VIP-Sim was wired directly to uWindowCapture on Windows and
    /// mcDesktopCapture on macOS, with the call sites duplicated per platform. Both
    /// libraries are effectively unmaintained (uWindowCapture's last release was
    /// v1.1.2 in December 2021), so the app needs to be able to swap the capture
    /// implementation without touching the simulation pipeline. Everything above
    /// this interface deals in <see cref="Texture"/>, never in HWNDs or CGWindowIDs.
    /// </summary>
    public interface ICaptureSource : IDisposable
    {
        /// <summary>Human-readable backend name, e.g. "Windows.Graphics.Capture". Shown in diagnostics.</summary>
        string BackendName { get; }

        /// <summary>False when the platform or OS version cannot support this backend at all.</summary>
        bool IsSupported { get; }

        /// <summary>True between a successful StartCapture and StopCapture.</summary>
        bool IsCapturing { get; }

        /// <summary>
        /// The most recent frame, or null if no frame has arrived yet.
        /// The returned instance may be replaced when the target resizes, so callers
        /// must re-read it each frame rather than caching it once. (Caching it once
        /// was the bug that froze the macOS overlay on the first captured frame.)
        /// </summary>
        Texture CurrentTexture { get; }

        /// <summary>Raised when CurrentTexture is replaced, e.g. on target resize.</summary>
        event Action<Texture> TextureChanged;

        /// <summary>Raised with a human-readable reason when capture stops unexpectedly (permission revoked, window closed).</summary>
        event Action<string> CaptureFailed;

        /// <summary>
        /// Whether the OS has granted screen-recording permission.
        /// macOS requires explicit consent in System Settings; Linux requires a portal
        /// grant. Windows returns true. Used to show the right onboarding message
        /// instead of silently rendering a black overlay.
        /// </summary>
        bool HasPermission { get; }

        /// <summary>Trigger the OS permission prompt if the platform has one. No-op where not applicable.</summary>
        void RequestPermission();

        /// <summary>Enumerate capturable targets. Cheap enough to call when opening the picker, not per frame.</summary>
        IReadOnlyList<CaptureTarget> EnumerateTargets();

        /// <summary>Begin capturing. Returns false and raises CaptureFailed if the target is gone.</summary>
        bool StartCapture(CaptureTarget target);

        /// <summary>Stop capturing and release GPU resources. Safe to call when not capturing.</summary>
        void StopCapture();

        /// <summary>Pump the backend. Called once per frame by CaptureManager.</summary>
        void Tick();
    }
}

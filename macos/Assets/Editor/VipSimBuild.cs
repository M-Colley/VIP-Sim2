using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Headless build entry points.
    ///
    /// Invoked by CI and usable locally:
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; \
    ///             -executeMethod VipSim.EditorTools.VipSimBuild.BuildWindows \
    ///             -buildOutput &lt;dir&gt;
    ///
    /// Having one scripted path for builds is what stops the two projects drifting:
    /// previously releases were produced by hand from the Editor UI, so build
    /// settings (scenes, scripting backend, stripping) were whatever that machine
    /// happened to have.
    /// </summary>
    public static class VipSimBuild
    {
        // IL2CPP gives a substantial CPU win over Mono for the per-frame effect
        // parameter syncing, and effects run every frame on the main thread, so it
        // is the right choice for release builds. It is not the default here
        // because it needs the IL2CPP editor module plus an MSVC toolchain, and a
        // machine missing either would fail the build rather than produce a slower
        // one. CI and release builds pass -scriptingBackend il2cpp explicitly.
        private static ScriptingImplementation Backend
        {
            get
            {
                var requested = ArgValue("-scriptingBackend", "mono").ToLowerInvariant();
                return requested == "il2cpp"
                    ? ScriptingImplementation.IL2CPP
                    : ScriptingImplementation.Mono2x;
            }
        }

        [MenuItem("VIP-Sim/Build/Windows (x64)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "VIP-Sim.exe");

        [MenuItem("VIP-Sim/Build/macOS (Universal)")]
        public static void BuildMacOS() => Build(BuildTarget.StandaloneOSX, "VIP-Sim.app");

        [MenuItem("VIP-Sim/Build/Linux (x64, experimental)")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "VIP-Sim");

        private static string[] EnabledScenes()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                // Fall back to the main scene rather than producing an empty player,
                // which is a confusing way to fail in CI.
                const string fallback = "Assets/Scenes/VIP_SIM.unity";
                if (File.Exists(fallback))
                {
                    Debug.LogWarning($"[VipSimBuild] No scenes in Build Settings; falling back to {fallback}");
                    return new[] { fallback };
                }
                throw new BuildFailedException("No scenes enabled in Build Settings and no fallback scene found.");
            }
            return scenes;
        }

        private static string ArgValue(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return fallback;
        }

        private static void Build(BuildTarget target, string outputName)
        {
            var outDir = ArgValue("-buildOutput", Path.Combine(Directory.GetCurrentDirectory(), "Build", target.ToString()));
            Directory.CreateDirectory(outDir);

            var group = BuildPipeline.GetBuildTargetGroup(target);
            var named = NamedBuildTarget.FromBuildTargetGroup(group);

            // macOS refuses to build at all if anything references WebCamTexture and no
            // camera usage description is set -- the build dies in
            // OSXDesktopStandalonePostProcessor with "WebCamTexture class is used but
            // Camera Usage Description is empty in Player Settings", several seconds in and
            // after compilation has already succeeded, which reads like a toolchain fault
            // rather than a missing setting. VIP-Sim uses the webcam for gaze tracking, so
            // this is required, and the string is what macOS shows the user in the camera
            // permission prompt at first launch. Set here rather than by hand so the build
            // is reproducible on a clean checkout.
            if (target == BuildTarget.StandaloneOSX &&
                string.IsNullOrEmpty(PlayerSettings.macOS.cameraUsageDescription))
            {
                PlayerSettings.macOS.cameraUsageDescription =
                    "VIP-Sim uses the camera for optional webcam eye tracking, so that the " +
                    "simulated vision impairment can follow where you are looking. Video is " +
                    "processed on this device and is never recorded or transmitted.";
                Debug.Log("[VipSimBuild] Set macOS camera usage description (required for WebCamTexture).");
            }

            var backend = Backend;
            PlayerSettings.SetScriptingBackend(named, backend);
            PlayerSettings.SetApiCompatibilityLevel(named, ApiCompatibilityLevel.NET_Unity_4_8);

            // The overlay must never steal focus or letterbox: it sits on top of the
            // user's real desktop.
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = false;

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = Path.Combine(outDir, outputName),
                target = target,
                options = BuildOptions.None,
            };

            Debug.Log($"[VipSimBuild] {target} -> {options.locationPathName} " +
                      $"({options.scenes.Length} scene(s), backend={backend})");

            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;

            Debug.Log($"[VipSimBuild] result={s.result} size={s.totalSize / (1024 * 1024)}MB " +
                      $"errors={s.totalErrors} warnings={s.totalWarnings} time={s.totalTime}");

            if (s.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Build {s.result} with {s.totalErrors} error(s)");
        }
    }
}

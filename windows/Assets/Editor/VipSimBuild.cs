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

        /// <summary>
        /// Write the launcher that Linux needs, beside the player.
        ///
        /// SDL decides whether the window can be transparent when it creates it, which is
        /// before a single line of VIP-Sim's own code runs, so this cannot be set from C#.
        /// Without the hint SDL marks the whole window opaque and the compositor ignores
        /// the alpha VIP-Sim renders: the overlay is a black rectangle over the desktop.
        /// </summary>
        private static void WriteLinuxLauncher(string playerPath)
        {
            string dir = Path.GetDirectoryName(playerPath);
            string exe = Path.GetFileName(playerPath);
            if (string.IsNullOrEmpty(dir)) return;

            var sb = new System.Text.StringBuilder();
            sb.Append("#!/bin/sh\n");
            sb.Append("# Start VIP-Sim.\n");
            sb.Append("#\n");
            sb.Append("# The hint below has to be set before the player starts. SDL reads it when it creates\n");
            sb.Append("# the window, which happens before VIP-Sim's own code runs, and it is what stops SDL\n");
            sb.Append("# declaring the whole window opaque. Without it the compositor ignores the transparency\n");
            sb.Append("# VIP-Sim renders and the overlay is a black rectangle over your desktop. Run this\n");
            sb.Append("# script rather than the binary directly.\n");
            sb.Append("export SDL_VIDEO_EGL_ALLOW_TRANSPARENCY=1\n");
            sb.Append("cd \"$(dirname \"$0\")\" || exit 1\n");
            sb.Append("exec ./").Append(exe).Append(" \"$@\"\n");

            string path = Path.Combine(dir, exe + ".sh");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[VipSimBuild] wrote {path} (needs the execute bit; packaging sets it).");
        }

        /// <summary>
        /// Add a shader to Always Included Shaders if it is not already there.
        /// </summary>
        private static void EnsureAlwaysIncludedShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogWarning($"[VipSimBuild] shader '{name}' not found; cannot guarantee it " +
                                 "is included in the build.");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "ProjectSettings/GraphicsSettings.asset");
            if (settings == null) return;

            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"[VipSimBuild] added '{name}' to Always Included Shaders.");
        }

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

            var backend = Backend;
            PlayerSettings.SetScriptingBackend(named, backend);
            PlayerSettings.SetApiCompatibilityLevel(named, ApiCompatibilityLevel.NET_Unity_4_8);

            // The overlay must never steal focus or letterbox: it sits on top of the
            // user's real desktop.
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = false;

            // Linux draws the captured screen on a quad it builds at runtime, and reaches
            // its shader through Shader.Find. Nothing in the project references that shader,
            // so without this it is stripped from the build and Shader.Find returns null --
            // a material that draws nothing, no exception, no log line, and a simulation
            // with no image in it.
            EnsureAlwaysIncludedShader("Unlit/Texture");

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

            if (s.result == BuildResult.Succeeded && target == BuildTarget.StandaloneLinux64)
                WriteLinuxLauncher(options.locationPathName);

            if (s.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Build {s.result} with {s.totalErrors} error(s)");
        }
    }
}

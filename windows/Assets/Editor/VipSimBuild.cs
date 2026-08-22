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
        /// Empty the output directory when the scripting backend has changed since it was
        /// last built into.
        ///
        /// Unity refuses outright -- "Build path contains a project previously built with
        /// the Mono2x scripting backend, the current setting is for IL2CPP" -- and the two
        /// leave incompatible layouts behind, Mono a Managed folder of assemblies and IL2CPP
        /// a GameAssembly. Left to the caller this is a build that fails for a reason having
        /// nothing to do with the code, on the day someone first tries to cut a release with
        /// the faster backend.
        /// </summary>
        private static void ClearIfBackendChanged(string outDir, ScriptingImplementation backend)
        {
            string marker = Path.Combine(outDir, ".vipsim-backend");
            string want = backend.ToString();

            if (!Directory.Exists(outDir)) return;
            if (File.Exists(marker) && File.ReadAllText(marker).Trim() == want) return;

            bool empty = Directory.GetFileSystemEntries(outDir).Length == 0;
            if (!empty)
            {
                Debug.Log($"[VipSimBuild] output was built with a different backend; clearing {outDir}");
                Directory.Delete(outDir, true);
            }
            Directory.CreateDirectory(outDir);
            File.WriteAllText(marker, want);
        }

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
            sb.Append("# The presenter is started first and starts the player itself, because it is the only\n");
            sb.Append("# one that knows the name of the socket it is serving -- and that is the one thing the\n");
            sb.Append("# player needs and cannot discover. Started the other way round, the player's window\n");
            sb.Append("# already exists in your compositor by the time anything could ask it to move, and a\n");
            sb.Append("# Wayland surface's role cannot be changed after it is created.\n");
            sb.Append("#\n");
            sb.Append("# The presenter needs a compositor with zwlr_layer_shell_v1. Sway, KWin, Hyprland,\n");
            sb.Append("# labwc and niri have it; GNOME does not.\n");
            sb.Append("cd \"$(dirname \"$0\")\" || exit 1\n");
            sb.Append("exec ./vipsim-presenter --host --exec ./").Append(exe).Append(" \"$@\"\n");

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

            // IL2CPP's generated C++ can be produced two ways, and the choice is not only
            // about speed. "Faster runtime" instantiates generics per type, which makes
            // enormous translation units -- enough that MSVC 14.44 falls over compiling
            // Unity's own bundled sparsehash with an internal compiler error. Shared
            // generics produce far smaller code that the compiler survives, and still run
            // as native code rather than through a JIT. Selectable, because which one works
            // depends on the compiler installed rather than on anything in this project.
            if (backend == ScriptingImplementation.IL2CPP)
            {
                var codegen = ArgValue("-il2cppCodegen", "speed").ToLowerInvariant() == "size"
                    ? Il2CppCodeGeneration.OptimizeSize
                    : Il2CppCodeGeneration.OptimizeSpeed;
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Standalone, codegen);

                // And how hard the C++ compiler is asked to optimise. Release is right for a
                // shipped build; Debug exists here because an internal compiler error is
                // almost always a fault in the optimiser, so this is the switch that says
                // whether a toolchain can produce an IL2CPP build at all.
                var cfg = ArgValue("-il2cppConfig", "release").ToLowerInvariant();
                var config = cfg == "debug"  ? Il2CppCompilerConfiguration.Debug
                           : cfg == "master" ? Il2CppCompilerConfiguration.Master
                                             : Il2CppCompilerConfiguration.Release;
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone, config);
                Debug.Log($"[VipSimBuild] IL2CPP code generation: {codegen}, compiler: {config}");
            }

            ClearIfBackendChanged(outDir, backend);

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

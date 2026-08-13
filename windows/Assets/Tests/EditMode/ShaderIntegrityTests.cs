using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace VipSim.Tests
{
    /// <summary>
    /// "Avoiding the pink screen of doom."
    ///
    /// The effect shaders are never attached to a GameObject -- each effect calls
    /// Shader.Find(...) at runtime and blits with the result. Unity's build-time
    /// shader stripping only keeps shaders it can see referenced by a scene or a
    /// material, so a Hidden/VisSim shader that is not in Always Included Shaders
    /// compiles fine in the editor and then renders magenta in the shipped build.
    /// That failure mode is called out by name in a source comment in myFieldLoss.cs,
    /// which means it has bitten this project before.
    ///
    /// These tests are data-driven: they discover the shaders on disk rather than
    /// hardcoding a list, so a newly added symptom is covered automatically.
    /// </summary>
    [TestFixture]
    [Category("Shader Integrity")]
    public class ShaderIntegrityTests
    {
        private static readonly Regex ShaderNameRegex =
            new Regex("^\\s*Shader\\s+\"(?<name>[^\"]+)\"", RegexOptions.Multiline);

        /// <summary>Every shader name declared under Assets/VisualEffects/Shaders.</summary>
        private static IEnumerable<string> DeclaredShaderNames()
        {
            var dir = Path.Combine(Application.dataPath, "VisualEffects", "Shaders");
            if (!Directory.Exists(dir)) yield break;

            foreach (var file in Directory.GetFiles(dir, "*.shader", SearchOption.AllDirectories).OrderBy(f => f))
            {
                var m = ShaderNameRegex.Match(File.ReadAllText(file));
                if (m.Success) yield return m.Groups["name"].Value;
            }
        }

        private static string[] ShaderNameCases => DeclaredShaderNames().ToArray();

        [Test]
        public void ShadersWereDiscovered()
        {
            Assert.That(ShaderNameCases, Is.Not.Empty,
                "No .shader files found under Assets/VisualEffects/Shaders - has the layout changed?");
        }

        [Test]
        public void EveryDeclaredShaderResolves([ValueSource(nameof(ShaderNameCases))] string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null,
                $"Shader.Find(\"{shaderName}\") returned null. The effect that blits with it " +
                "would fall back to the magenta error shader at runtime.");
        }

        [Test]
        public void EveryDeclaredShaderIsSupportedOnThisMachine(
            [ValueSource(nameof(ShaderNameCases))] string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) Assert.Ignore($"{shaderName} not found; covered by EveryDeclaredShaderResolves");

            Assert.That(shader.isSupported, Is.True,
                $"'{shaderName}' failed to compile for the current graphics API. " +
                "BaseEffect.Start() disables the effect when this happens, so the symptom " +
                "would silently do nothing.");
        }

        [Test]
        public void HiddenEffectShadersAreInAlwaysIncludedShaders()
        {
            // Only Hidden/ shaders are at risk: they have no material or scene
            // reference for the stripper to follow.
            var hidden = DeclaredShaderNames().Where(n => n.StartsWith("Hidden/")).ToArray();
            if (hidden.Length == 0) Assert.Ignore("no Hidden/ shaders declared");

            var graphicsSettings = AssetDatabase
                .LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")
                .FirstOrDefault(a => a != null && a.GetType().Name == "GraphicsSettings");
            Assert.That(graphicsSettings, Is.Not.Null, "could not load GraphicsSettings.asset");

            var so = new SerializedObject(graphicsSettings);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            Assert.That(arr, Is.Not.Null.And.Property("isArray").True,
                "m_AlwaysIncludedShaders not found - Unity may have renamed this field");

            var included = new HashSet<string>();
            for (int i = 0; i < arr.arraySize; i++)
            {
                var s = arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (s != null) included.Add(s.name);
            }

            var missing = hidden.Where(n => !included.Contains(n)).OrderBy(n => n).ToArray();

            Assert.That(missing, Is.Empty,
                "These Hidden/ shaders are blitted via Shader.Find but are not in " +
                "Project Settings > Graphics > Always Included Shaders. They will be stripped " +
                "from the build and render magenta:\n  " + string.Join("\n  ", missing));
        }
    }
}

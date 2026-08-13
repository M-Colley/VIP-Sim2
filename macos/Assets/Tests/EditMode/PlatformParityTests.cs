using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;

namespace VipSim.Tests
{
    /// <summary>
    /// Guards the core scientific claim of VIP-Sim: a given symptom at a given
    /// severity must look the same regardless of platform.
    ///
    /// This suite exists because it would have caught a real regression. The
    /// windows/ and macos/ trees were forked from a common ancestor and then
    /// edited independently, and by the time it was noticed:
    ///   - macOS had a negative-LOD clamp in myFieldLoss / myFieldLossInverted
    ///     that Windows did not, so Central and Peripheral Vision Loss sampled
    ///     undefined mip levels on Windows;
    ///   - macOS had an overbright clamp in VortexEffect that Windows did not;
    ///   - Windows had cached-reflection parameter syncing that macOS did not.
    /// Nothing failed loudly. The simulation just quietly differed per platform.
    /// </summary>
    [TestFixture]
    [Category("Platform Parity")]
    public class PlatformParityTests
    {
        private static string RepoRoot
        {
            get
            {
                // Application.dataPath -> <repo>/windows/Assets
                var assets = UnityEngine.Application.dataPath;
                return Directory.GetParent(assets).Parent.FullName;
            }
        }

        private static string WindowsAssets => Path.Combine(RepoRoot, "windows", "Assets");
        private static string MacAssets => Path.Combine(RepoRoot, "macos", "Assets");

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return string.Concat(sha.ComputeHash(fs).Select(b => b.ToString("x2")));
        }

        private static string[] SimulationFiles(string assetsRoot)
        {
            var dir = Path.Combine(assetsRoot, "VisualEffects");
            if (!Directory.Exists(dir)) return new string[0];

            var extensions = new[] { ".cs", ".shader", ".compute", ".cginc", ".hlsl" };
            return Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                            .Where(f => extensions.Contains(Path.GetExtension(f)))
                            .OrderBy(f => f)
                            .ToArray();
        }

        [Test]
        public void BothPlatformTreesArePresent()
        {
            Assert.That(Directory.Exists(WindowsAssets), Is.True, $"missing {WindowsAssets}");
            Assert.That(Directory.Exists(MacAssets), Is.True, $"missing {MacAssets}");
        }

        [Test]
        public void EverySimulationFileExistsOnBothPlatforms()
        {
            var winRel = SimulationFiles(WindowsAssets)
                .Select(f => f.Substring(WindowsAssets.Length + 1)).ToArray();
            var macRel = SimulationFiles(MacAssets)
                .Select(f => f.Substring(MacAssets.Length + 1)).ToArray();

            Assert.That(winRel, Is.Not.Empty, "no simulation files found - has the layout changed?");

            CollectionAssert.AreEquivalent(winRel, macRel,
                "The set of simulation source files differs between platforms. " +
                "Every shader and effect script must exist on both.");
        }

        [Test]
        public void SimulationSourceIsByteIdenticalAcrossPlatforms()
        {
            var mismatches = SimulationFiles(WindowsAssets)
                .Select(win => new { rel = win.Substring(WindowsAssets.Length + 1), win })
                .Select(x => new { x.rel, x.win, mac = Path.Combine(MacAssets, x.rel) })
                .Where(x => File.Exists(x.mac) && Sha256(x.win) != Sha256(x.mac))
                .Select(x => x.rel)
                .ToArray();

            Assert.That(mismatches, Is.Empty,
                "Simulation source differs between windows/ and macos/:\n  " +
                string.Join("\n  ", mismatches) +
                "\nA symptom must render identically on every platform. Port the fix both ways.");
        }
    }
}

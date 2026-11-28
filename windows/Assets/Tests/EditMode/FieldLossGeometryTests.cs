using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VipSim.Tests
{
    /// <summary>
    /// Field loss is set as a radius in degrees of visual angle (myFieldLoss.radius_deg). That
    /// rests on one measured number, the overlay texture's half-weight radius, and on two
    /// conversions. The effects live in Assembly-CSharp, which a test assembly cannot
    /// reference, so they are reached by reflection.
    /// </summary>
    public class FieldLossGeometryTests
    {
        private const string Overlay = "Assets/Resources/macular-degeneration.jpg";
        private const float Ppd = 37.87f;   // 53.1 cm wide, 1920 px, at 60 cm (the gallery default)

        private static Type FieldLoss()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("VisSim.myFieldLoss")).FirstOrDefault(x => x != null);
            Assert.IsNotNull(t, "VisSim.myFieldLoss is not loaded");
            return t;
        }

        private static float HalfRadius() =>
            (float)FieldLoss().GetField("OverlayHalfRadius", BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue();

        private static object Call(string name, params object[] args)
        {
            var m = FieldLoss().GetMethod(name, BindingFlags.Public | BindingFlags.Static, null,
                args.Select(a => a.GetType()).ToArray(), null);
            Assert.IsNotNull(m, name + " not found");
            return m.Invoke(null, args);
        }

        [Test]
        public void HalfRadiusIsMeasuredFromTheOverlay()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Overlay);
            Assert.IsNotNull(tex, Overlay);
            Assert.IsTrue(tex.isReadable, Overlay + " must stay readable");

            // Radial mean of the grey the shader reads: linear values, Rec. 601 weights.
            const float bin = 0.0025f;
            const int n = 120;
            var sum = new double[n];
            var count = new int[n];
            Color[] px = tex.GetPixels();
            int w = tex.width, h = tex.height;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt(Math.Pow((x - (w - 1) / 2.0) / w, 2) + Math.Pow((y - (h - 1) / 2.0) / h, 2));
                    int b = (int)(r / bin);
                    if (b >= n) continue;
                    Color c = px[y * w + x];
                    sum[b] += 0.299 * Mathf.GammaToLinearSpace(c.r) + 0.587 * Mathf.GammaToLinearSpace(c.g) +
                              0.114 * Mathf.GammaToLinearSpace(c.b);
                    count[b]++;
                }
            }
            var profile = new double[n];
            for (int i = 0; i < n; i++) profile[i] = sum[i] / Math.Max(1, count[i]);

            // Half way between the centre and white, where the blur weight is half its maximum.
            double level = (profile[0] + 1.0) / 2.0;
            int k = Array.FindIndex(profile, g => g >= level);
            Assert.Greater(k, 0, "the overlay never reaches half weight");
            double c0 = (k - 0.5) * bin, c1 = (k + 0.5) * bin;
            double measured = c0 + (level - profile[k - 1]) * (c1 - c0) / (profile[k] - profile[k - 1]);

            Assert.AreEqual(measured, HalfRadius(), 0.002, "OverlayHalfRadius no longer matches " + Overlay);
        }

        [Test]
        public void OverlayIsRoundAndHasTheRadiusSet()
        {
            var scale = (Vector2)Call("OverlayScaleFor", 5f, Ppd, 1920, 1080);

            Assert.AreEqual(scale.x * 1920f, scale.y * 1080f, 1e-2, "as many pixels across as up");
            Assert.AreEqual(5f * Ppd, HalfRadius() * scale.x * 1920f, 1e-2, "half-weight radius in pixels");
        }

        [Test]
        public void FormerOverlayScaleKeepsItsArea()
        {
            // overlayScale 0.75 stretched the overlay to the screen: an ellipse. The radius is
            // that of the circle with the same area.
            float r = (float)Call("RadiusDegFromOverlayScale", 0.75f, 1920, 1080, Ppd);
            Assert.AreEqual(HalfRadius() * 0.75 * Math.Sqrt(1920.0 * 1080.0) / Ppd, r, 1e-3);

            var scale = (Vector2)Call("OverlayScaleFor", r, Ppd, 1920, 1080);
            double before = 0.75 * 1920.0 * 0.75 * 1080.0;
            double after = scale.x * 1920.0 * scale.y * 1080.0;
            Assert.AreEqual(1.0, after / before, 1e-4, "the texture covers the same area");
        }

        [Test]
        public void ConversionStaysInRange()
        {
            Assert.AreEqual(0f, (float)Call("RadiusDegFromOverlayScale", 0.75f, 1920, 1080, 0f), "no geometry");
            Assert.AreEqual(60f, (float)Call("RadiusDegFromOverlayScale", 3f, 3840, 2160, 10f), 1e-4, "capped");
        }
    }
}

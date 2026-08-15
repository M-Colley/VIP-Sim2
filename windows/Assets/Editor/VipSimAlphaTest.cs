using UnityEngine;
using UnityEditor;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Measures what an effect shader does to the ALPHA channel of a blit.
    ///
    /// VIP-Sim composites onto the desktop through DWM, from the window's own
    /// per-pixel alpha. A pixel with correct RGB and zero alpha is invisible. The
    /// effect chain is a series of Graphics.Blit calls, so every shader in it has
    /// to carry alpha through faithfully -- and the last one writes straight to
    /// the backbuffer, where getting it wrong means nothing appears at all.
    ///
    /// Rather than reason about blend equations, this feeds a known alpha ramp
    /// through the real material and reports the transfer function.
    ///
    /// Run: Unity -batchmode -executeMethod VipSim.EditorTools.VipSimAlphaTest.Run
    /// (NOT with -nographics; this needs a real device.)
    /// </summary>
    public static class VipSimAlphaTest
    {
        private const int W = 64, H = 16;

        public static void Run()
        {
            // Sweep every effect shader in the project, not a hand-picked few: a shader
            // that mangles alpha is invisible on the desktop no matter how correct its
            // RGB is, and that failure looks identical to "the effect does nothing".
            var names = new System.Collections.Generic.List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/VisualEffects/Shaders" }))
            {
                var s = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.name.StartsWith("Hidden/VisSim/")) names.Add(s.name);
            }
            names.Sort();
            foreach (var name in names) Probe(name);

            EditorApplication.Exit(0);
        }

        private static void Probe(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.Log($"ALPHATEST {shaderName} SHADER-NOT-FOUND"); return; }

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            // A white overlay means "no degradation": w = 1 - luma(white) = 0, so the
            // field-loss shaders sample mip 0 and the result is a straight passthrough.
            // Anything that is NOT a passthrough after this is the shader's own doing.
            var white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
            if (mat.HasProperty("_Overlay")) mat.SetTexture("_Overlay", white);
            if (mat.HasProperty("_MaxLODlevel")) mat.SetFloat("_MaxLODlevel", 1f);
            if (mat.HasProperty("_OverlayScale")) mat.SetFloat("_OverlayScale", 1f);

            // Source: constant mid-grey RGB, alpha ramping 0 -> 1 left to right.
            var srcTex = new Texture2D(W, H, TextureFormat.RGBA32, false, true);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    srcTex.SetPixel(x, y, new Color(0.5f, 0.5f, 0.5f, x / (float)(W - 1)));
            srcTex.Apply();

            var src = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            src.useMipMap = true; src.isPowerOfTwo = true; src.Create();
            Graphics.Blit(srcTex, src);

            // Destination starts fully transparent, exactly like the backbuffer of a
            // layered window before the final effect writes to it.
            var dst = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            dst.Create();
            var prev = RenderTexture.active;
            RenderTexture.active = dst;
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            RenderTexture.active = prev;

            Graphics.Blit(src, dst, mat, 0);

            var outTex = new Texture2D(W, H, TextureFormat.RGBA32, false, true);
            prev = RenderTexture.active;
            RenderTexture.active = dst;
            outTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            outTex.Apply();
            RenderTexture.active = prev;

            // Worst deviation from identity across the ramp. Anything beyond a couple of
            // quantisation steps (1/255) means the shader is rewriting alpha, which on a
            // DWM-composited overlay shows up as the effect being faint or absent.
            float worst = 0f; float worstIn = 0f, worstOut = 0f;
            for (int x = 0; x < W; x++)
            {
                float want = x / (float)(W - 1);
                float got = outTex.GetPixel(x, H / 2).a;
                if (Mathf.Abs(got - want) > worst) { worst = Mathf.Abs(got - want); worstIn = want; worstOut = got; }
            }
            string verdict = worst <= 0.02f ? "PASS" : "FAIL";
            Debug.Log($"ALPHATEST {verdict} {shaderName} maxAlphaError={worst:F3}" +
                      (verdict == "FAIL" ? $" (in {worstIn:F2} -> out {worstOut:F2})" : ""));

            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(white);
            Object.DestroyImmediate(srcTex);
            Object.DestroyImmediate(outTex);
            src.Release(); Object.DestroyImmediate(src);
            dst.Release(); Object.DestroyImmediate(dst);
        }
    }
}

using UnityEngine;
using UnityEditor;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Measures what each effect shader does to the ALPHA channel of a blit.
    ///
    /// VIP-Sim is composited onto the desktop from the window's own per-pixel alpha, so
    /// alpha decides whether a pixel is drawn at all. A shader with flawless RGB and
    /// mangled alpha is invisible, and that failure looks exactly like "the effect does
    /// nothing" -- which is how an alpha bug survived in the Vision Loss shaders for so
    /// long. Every effect in the Graphics.Blit chain therefore has to carry alpha through
    /// untouched, and the last one writes straight to the backbuffer, where getting it
    /// wrong means the whole simulation disappears.
    ///
    /// The probe feeds CONSTANT alpha with varying RGB. That is the important detail: an
    /// earlier version used an alpha ramp, which conflated two very different things.
    /// Effects that deliberately displace pixels (myNystagmus, myGlitch, myInpainter) move
    /// alpha along with everything else, so against a ramp they read as corrupting alpha
    /// when they are only relocating it, and the report was full of false accusations.
    /// With alpha constant across the whole source, every sample location holds the same
    /// value, so displacement cannot change the answer and any deviation is the shader
    /// genuinely rewriting alpha. Several levels are tested because some faults only show
    /// at partial alpha (a*a is identity at both 0 and 1).
    ///
    /// Run: Unity -batchmode -executeMethod VipSim.EditorTools.VipSimAlphaTest.Run
    /// (NOT with -nographics; this needs a real device.)
    /// </summary>
    public static class VipSimAlphaTest
    {
        private const int W = 64, H = 32;
        private static readonly float[] Levels = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        // A shader is allowed a little slack: the source is quantised to 8 bits and some
        // effects filter or interpolate, so a couple of 1/255 steps is not a finding.
        private const float Tolerance = 0.02f;

        public static void Run()
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/VisualEffects/Shaders" }))
            {
                var s = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.name.StartsWith("Hidden/VisSim/")) names.Add(s.name);
            }
            names.Sort();

            int pass = 0, fail = 0;
            foreach (var name in names)
            {
                if (Probe(name)) pass++; else fail++;
            }
            Debug.Log($"ALPHATEST SUMMARY {pass} pass, {fail} fail, {names.Count} shaders");

            EditorApplication.Exit(0);
        }

        private static bool Probe(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.Log($"ALPHATEST FAIL {shaderName} SHADER-NOT-FOUND"); return false; }
            if (!shader.isSupported) { Debug.Log($"ALPHATEST FAIL {shaderName} SHADER-NOT-SUPPORTED (compile error)"); return false; }

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            // A white overlay means "no degradation" for the field-loss family: w becomes
            // 1 - luma(white) = 0, so they sample mip 0 and should be a straight
            // passthrough. Anything that is not a passthrough after that is the shader.
            var white = new Texture2D(4, 4);
            var whitePx = new Color[16];
            for (int i = 0; i < 16; i++) whitePx[i] = Color.white;
            white.SetPixels(whitePx);
            white.Apply();
            foreach (var prop in new[] { "_Overlay", "_OffsetTextureX", "_OffsetTextureY", "_BlurTexture" })
                if (mat.HasProperty(prop)) mat.SetTexture(prop, white);
            if (mat.HasProperty("_MaxLODlevel")) mat.SetFloat("_MaxLODlevel", 1f);
            if (mat.HasProperty("_OverlayScale")) mat.SetFloat("_OverlayScale", 1f);

            var srcTex = new Texture2D(W, H, TextureFormat.RGBA32, false, true);
            var src = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            src.useMipMap = true; src.isPowerOfTwo = true; src.Create();
            var dst = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            dst.Create();
            var outTex = new Texture2D(W, H, TextureFormat.RGBA32, false, true);

            float worstErr = 0f, worstLevel = 0f, worstGot = 0f;

            foreach (float level in Levels)
            {
                // RGB varies across the image so a displacing shader still has something
                // to displace; alpha is flat so displacement cannot show up as an error.
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                        srcTex.SetPixel(x, y, new Color(x / (float)(W - 1), y / (float)(H - 1), 0.5f, level));
                srcTex.Apply();
                Graphics.Blit(srcTex, src);

                // The destination starts fully transparent, exactly like the backbuffer of
                // a layered window before the final effect in the chain writes to it. This
                // is the case that broke: a shader that blends rather than replaces gets
                // its (1 - a) term zeroed here and collapses.
                var prev = RenderTexture.active;
                RenderTexture.active = dst;
                GL.Clear(true, true, new Color(0, 0, 0, 0));
                RenderTexture.active = prev;

                Graphics.Blit(src, dst, mat, 0);

                prev = RenderTexture.active;
                RenderTexture.active = dst;
                outTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                outTex.Apply();
                RenderTexture.active = prev;

                var px = outTex.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    float err = Mathf.Abs(px[i].a - level);
                    if (err > worstErr) { worstErr = err; worstLevel = level; worstGot = px[i].a; }
                }
            }

            bool ok = worstErr <= Tolerance;
            Debug.Log($"ALPHATEST {(ok ? "PASS" : "FAIL")} {shaderName} maxAlphaError={worstErr:F3}" +
                      (ok ? "" : $" (constant in {worstLevel:F2} -> out {worstGot:F2})"));

            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(white);
            Object.DestroyImmediate(srcTex);
            Object.DestroyImmediate(outTex);
            RenderTexture.active = null;
            src.Release(); Object.DestroyImmediate(src);
            dst.Release(); Object.DestroyImmediate(dst);
            return ok;
        }
    }
}

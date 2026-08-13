using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisSim
{
    public class DoubleVisionEffect : LinkableBaseEffect
    {
        private string dummy = "";

        [Linkable, Range(0.0f, 0.05f), Tooltip("Displacement Amount.")]
        public float displacementAmount = 0.025f;

        private RenderTexture rt; // temporary render texture

        protected void Awake()
        {
            base.Start();

            int width_px = Screen.width;
            int height_px = Screen.height;

            if (width_px != Mathf.ClosestPowerOfTwo(width_px))
            {
                Debug.LogFormat("WARNING, width ({0}) must be a power of two. Will round upwards to {1}", width_px, Mathf.NextPowerOfTwo(width_px));
                width_px = Mathf.ClosestPowerOfTwo(width_px);
            }
            if (height_px != Mathf.ClosestPowerOfTwo(height_px))
            {
                Debug.LogFormat("WARNING, height ({0}) must be a power of two. Will round upwards to {1}", height_px, Mathf.NextPowerOfTwo(height_px));
                height_px = Mathf.ClosestPowerOfTwo(height_px);
            }

            rt = new RenderTexture(width_px, height_px, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            rt.useMipMap = true;
            rt.isPowerOfTwo = true;
            rt.Create();
        }

        public new void OnEnable()
        {
            base.OnEnable();
        }

        public new void OnDisable()
        {
            base.OnDisable();
        }

        protected override void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (Material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            Material.SetFloat("_Displacement", displacementAmount);

            RenderTexture tempTexture = RenderTexture.GetTemporary(source.width, source.height);

            // Apply the shader effect
            Graphics.Blit(source, tempTexture, Material);
            Graphics.Blit(tempTexture, destination);

            RenderTexture.ReleaseTemporary(tempTexture);
        }

        protected override void OnUpdate()
        {
            // Implement any update logic here if needed
        }

        // The intermediate RenderTexture is allocated in Awake() and lives for the
        // lifetime of the component. Without this it was never released: at
        // 2048x1024 ARGB32 with a mip chain that is ~11MB of GPU memory leaked per
        // instance, per scene load, and there is one instance per eye.
        protected void OnDestroy()
        {
            if (rt != null)
            {
                rt.Release();
                DestroyImmediate(rt);
                rt = null;
            }
        }
        protected override string GetShaderName()
        {
            return "Hidden/DoubleVision";
        }
    }
}

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

        // No intermediate render texture. One used to be allocated here -- screen-sized,
        // power-of-two, with mips, about 45 MB at 4K per eye and allocated even while the
        // effect is off -- and nothing ever read it.
        protected void Awake()
        {
            base.Start();
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

            // Straight into the destination; going through a temporary cost a full-screen copy.
            Graphics.Blit(source, destination, Material);
        }

        protected override void OnUpdate()
        {
            // Implement any update logic here if needed
        }

        protected override string GetShaderName()
        {
            return "Hidden/DoubleVision";
        }
    }
}

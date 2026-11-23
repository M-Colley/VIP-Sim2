// PJ
// TODO - get FOV and screenwidth info from the hardware directly? Or at least from the Fove Interface object?

using System;
using UnityEngine;

namespace VisSim
{
    
    public class myBlur : LinkableBaseEffect
    {

        // internal
        [Range(0.0f, 30f)]
        // amount of blurring (in acuity/cycles-per-degree units)
        public float maxCPD = 0.0f; // reminder that 1 logMar [top row of chart] is equal to about 3 cpd, 0==30, -0.2==47.5 [30/exp10(1.0)]

        // amount of blurring (in computer/kernel-size units)
        [Linkable, Range(0.001f, 10.0f)]
        public float kernalSigma = 3.0f;
        public float getkernalSigma() { return kernalSigma; } // make kernalSigma readonly

        // geometry info
        [Linkable, Range(100, 10000)]
        public int screenWidth_px = 2560;
        [Tooltip("Derive the screen width from the display actually in use.\n\n" +
                 "Leave this on. The value below was a fixed constant, so every " +
                 "degree-based parameter was silently mis-scaled on any display of a " +
                 "different width -- on a 3840px screen a constant of 1334 makes each " +
                 "degree 2.9x too small, and the simulated severity is wrong by that " +
                 "factor. Turn it off only to reproduce a specific rig.")]
        public bool autoScreenWidth = true;

        /*[Linkable, Range(0.001f, 60.0f)]
        public float screenWidth_cm = 3.0f;
        [Linkable, Range(0.001f, 60.0f)]
        public float viewingDist_cm = 3.0f;*/
        [Linkable, Range(1f, 180.0f)]
        public float viewingAngle_deg = 80.0f;

        // internal
        [Range(0, 2)]
        private int downsample = 0;
        [Range(0.0f, 10.0f)]
        private float blurSize = 1.0f;
        private const float MAX_KERNAL_SIGMA = 20f;
        private int kernelSize;
        private float widthMod = 1.0f;
        private float[] floatArray;

        private float _old_kernalSigma = 3.0f;
        private float _old_maxCPD = float.NaN;
        private int _old_screenWidth;

        // Use this for initialization
        public new void OnEnable()
        {
            // maxCPD is no longer reset here. It is the user's setting, and resetting it on
            // enable meant switching blur off and on again forgot it.
            computeBlurKernel();

            // call base method to enable effect
            base.OnEnable();
        }

        private void computeBlurKernel()
        {
            // init
            widthMod = 1.0f / (1.0f * (1 << downsample));
            
            // compute kernalSigma
            // The angular scale has to come from the display in use,
            // not a constant baked in on someone else's monitor.
            if (autoScreenWidth && Screen.width > 0) screenWidth_px = Screen.width;
            float pixel_per_dg = screenWidth_px / viewingAngle_deg; // screenWidth_dg
            //Debug.Log("pixel_per_dg: " + pixel_per_dg);

            // maxCPD is the slider's value, 0 (no loss) to 30 (the most); the acuity it stands
            // for is 30 - maxCPD cycles per degree. The conversion used to be written back into
            // maxCPD itself, so the field the UI, the profile and the saved settings all read
            // held the inverted number: a saved profile reloaded with its severity reversed,
            // and reopening the panel showed the opposite of what was running.
            double cpd = 30.0 - maxCPD;

            double sigma = 1 / (2 * Math.PI * cpd);
            sigma = Math.Sqrt(2 * Math.Log(2)) * sigma; // scale so that cutoff specifies 50 % decrement(otherwise will be targetting 60.7 %)
            sigma = sigma * pixel_per_dg; // convert to pixels
            kernalSigma = (float)Math.Min(sigma, MAX_KERNAL_SIGMA);
            //Debug.Log("kernalSigma: " + kernalSigma);

            // set Gaussian kernel. Odd, so it is centred: an even width put the image half a
            // pixel off at every blur level where 4 sigma + 1 rounded to an even number.
            kernelSize = 2 * (int)Mathf.Round(2.0f * kernalSigma) + 1;
            float[] gaussianKernel = GaussianKernel(kernalSigma, kernelSize, true);
            floatArray = new float[100];
            for (int i = 0; i < kernelSize; i++)
            {
                floatArray[i] = gaussianKernel[i];
            }

            // store values so we know whether the effect has been updated
            _old_kernalSigma = kernalSigma;
            _old_maxCPD = maxCPD;
            _old_screenWidth = Screen.width;
        }

        /// <summary>
        /// Procedural gaussian kernel. More accurate than the const ones below, but it takes time to create it.
        /// </summary>
        /// <param name="sigma">Variance, not the square</param>
        /// <param name="size">(HALF) Width or length of the kernel</param>
        /// <param name="normalize">Weither the kernel must be normalized</param>
        /// <returns>Gaussian kernel</returns>
        private static float[] GaussianKernel(float sigma, int size, bool normalize = true)
        {
            float[] kernel = new float[size];

            float sigmaSqr2 = sigma * sigma * 2f;
            float sigmaSqr2piInv = 1f / Mathf.Sqrt((float)Math.PI * sigmaSqr2);

            int k = size / 2;

            for (int i = 0; i < size; i++)
            {
                int ki = i - k;
                float exp = Mathf.Exp(-((ki*ki) / sigmaSqr2));
                kernel[i] = sigmaSqr2piInv * exp;
            }

            if (normalize)
            {
                float sum = 0f;
                foreach (float f in kernel)
                    sum += f;

                for (int i = 0; i < kernel.Length; i++)
                    kernel[i] /= sum;
            }

            return kernel;
        }

        // Update is called once per frame
		protected override void OnUpdate()
        {
        }

        // Called by camera to apply image effect
        protected override void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // update blur kernel if necessary -- including when the display changes (F3 to a
            // monitor of another width), since the kernel is in pixels per degree.
            if (kernalSigma != _old_kernalSigma || maxCPD != _old_maxCPD ||
                (autoScreenWidth && Screen.width != _old_screenWidth))
            {
                computeBlurKernel();
            }

            // set shader params
            Material.SetVector("_Parameter", new Vector4(blurSize * widthMod, -blurSize * widthMod, 0.0f, 0.0f));
            Material.SetFloatArray("_myCurve", floatArray);
            Material.SetInt("_kernelSize", kernelSize);


            // downsample
            source.filterMode = FilterMode.Bilinear;
            int rtW = source.width >> downsample;
            int rtH = source.height >> downsample;
            // One vertical pass, then one horizontal pass straight into the destination. The
            // "downsample" copy used to be made with pass 0, which is the vertical blur, and
            // pass 0 then ran again: the vertical sigma was sqrt(2) times the horizontal one,
            // so the simulated acuity was wrong vertically, at the cost of two extra blits.
            RenderTexture rt = RenderTexture.GetTemporary(rtW, rtH, 0, source.format);
            rt.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, rt, Material, 0);
            Graphics.Blit(rt, destination, Material, 1);
            RenderTexture.ReleaseTemporary(rt);
        }

		protected override string GetShaderName()
		{
			return "Hidden/VisSim/myBlur";
		}
    }
}

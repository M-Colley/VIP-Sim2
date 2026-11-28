using UnityEngine;
using System.Collections;

/*
 * todo? Don't bother with using intermediate rendertexture if using pre-generated textures (i.e., if not in AR mode) ?
 * 
 * OLD:
 * 
 * Avoiding the pink screen of doom:
 * 
 * Note that the shader isn't attached to a game object at compile time, and so won't be included in any builds.
 * 
 * To resolve this, go to Edit -> Project Settings -> Graphics and add the myMipMapBlur shader to the list of Always Included Shaders.
 * 
 * */

#if UNITY_EDITOR
using UnityEditor;
#endif

//TODO: appears to slightly darken the image?? unsure why??

namespace VisSim
{
	public class myFieldLoss : LinkableBaseEffect
    {

        private string dummy = "";
        
        // overlay texture
        private double[,] overlayRawGrid_xy;
        [LinkableAttribute]
        public Texture2D overlayTexture = null; // for display purposes only (NB: should ideally be readonly)

        // Distance from the point of gaze to the middle of the patch's soft edge, in degrees of
        // visual angle, through ViewingGeometry. It replaced overlayScale, a fraction of the
        // screen that stretched the patch to the screen's shape and changed size with it.
        [Linkable, Range(0f, MaxRadiusDeg), Tooltip("Radius of the central loss, degrees of visual angle.")]
        public float radius_deg = 5f;

        /// <summary>Largest radius the slider and a profile can set, in degrees.</summary>
        public const float MaxRadiusDeg = 60f;

        /// <summary>
        /// Radius of the default overlay (Resources/macular-degeneration.jpg) at which its blur
        /// weight is half its maximum, as a fraction of the texture's side, on the linear values
        /// the shader reads. The weight rises from 10% at 0.52 of this radius to 90% at 1.46 of
        /// it and is complete at 1.62. FieldLossGeometryTests measures it from the imported
        /// texture again.
        /// </summary>
        public const float OverlayHalfRadius = 0.1626f;

        /// <summary>
        /// The overlay's size as a fraction of an image <paramref name="width"/> by
        /// <paramref name="height"/> pixels, per axis, so that the patch is round on screen and
        /// its half-weight radius is <paramref name="radiusDeg"/> degrees of visual angle.
        /// </summary>
        public static Vector2 OverlayScaleFor(float radiusDeg, float pixelsPerDegree, int width, int height)
        {
            float side = Mathf.Max(0f, radiusDeg) * Mathf.Max(0f, pixelsPerDegree) / OverlayHalfRadius;
            return new Vector2(side / Mathf.Max(1, width), side / Mathf.Max(1, height));
        }

        /// <summary>
        /// The radius in degrees that replaces a former overlayScale. That scale stretched the
        /// overlay to the screen's shape, so it drew an ellipse; this is the radius of the circle
        /// with the same area, on a <paramref name="width"/> by <paramref name="height"/> pixel
        /// screen at <paramref name="pixelsPerDegree"/>.
        /// </summary>
        public static float RadiusDegFromOverlayScale(float overlayScale, int width, int height, float pixelsPerDegree)
        {
            if (pixelsPerDegree <= 0f) return 0f;
            float r = OverlayHalfRadius * Mathf.Max(0f, overlayScale) *
                      Mathf.Sqrt((float)Mathf.Max(1, width) * Mathf.Max(1, height)) / pixelsPerDegree;
            return Mathf.Min(r, MaxRadiusDeg);
        }

        /// <summary>A former overlayScale in degrees, at this screen and viewing geometry.</summary>
        public static float RadiusDegFromOverlayScale(float overlayScale) =>
            RadiusDegFromOverlayScale(overlayScale, Screen.width, Screen.height, ViewingGeometry.PixelsPerDegree);

        private Texture2D _oldOverlayTexture = null; // for checking for updates
        private float numLevelsOfBlur;
        private RenderTexture rt; // temporary render texture for generating mipmaps

        protected void Awake() // (NB: not Start, as Grid needs to be set even if not enabled)
        {
            base.Start();

            // The intermediate texture is allocated on first use, at the size of the image it is
            // given; see EnsureTarget. This only sets the blur depth the Material starts with.
            numLevelsOfBlur = LevelsOfBlur(Screen.width, Screen.height);
            
            // generate a default overlay
			//Texture2D defaultOverlay = (Texture2D)Resources.Load("blob2d_1024", typeof(Texture2D));
			overlayTexture = (Texture2D)Resources.Load("macular-degeneration", typeof(Texture2D));
            
			//double[,] xy = new double[,] { { -21, 9, 20.3125 }, { -21, 3, 21.1094 }, { -21, -3, -17.3438 }, { -21, -9, -16.9401 }, { -15, 15, -19.3281 }, { -15, 9, 20.0625 }, { -15, 3, 18.8281 }, { -15, -9, 16.8099 }, { -15, -15, 16.0678 }, { -9, 15, 19.8438 }, { -9, 9, 19.2031 }, { -9, 3, 21.5938 }, { -9, -3, 17.8516 }, { -9, -9, 16.4063 }, { -9, -15, 15.8073 }, { -3, 15, 19.6250 }, { -3, 9, 20.6094 }, { -3, 3, 21.7031 }, { -3, -3, 18.1771 }, { -3, -9, 17.0573 }, { -3, -15, 16.0287 }, { 3, 15, 20.3125 }, { 3, 9, 21.4844 }, { 3, 3, 21.8594 }, { 3, -3, 21.5625 }, { 3, -9, 20.6250 }, { 3, -15, 19.1875 }, { 9, 15, 20.4844 }, { 9, 9, 20.8281 }, { 9, 3, 21.3906 }, { 9, -3, 21.3125 }, { 9, -9, 20.6875 }, { 9, -15, 18.9063 }, { 15, 15, 20.1094 }, { 15, 9, 20.6875 }, { 15, 3, 20.9063 }, { 15, -3, 20.3906 }, { 15, -9, 20.4375 }, { 15, -15, 18.6094 }, { 21, 9, 20.2656 }, { 21, 3, 20.8750 }, { 21, -3, 20.7656 }, { 21, -9, 19.6094 }, { 27, 3, 20.0469 }, { 27, -3, 19.8750 } };
            //double[,] xy = new double[,] { { -21, 9, 2.2192 }, { -21, 3, 2.2161 }, { -21, -3, -9.0871 }, { -21, -9, -8.7293 }, { -15, 15, 1.7348 }, { -15, 9, 0.3692 }, { -15, 3, -1.7652 }, { -15, -3, -2.6652 }, { -15, -9, -9.9074 }, { -15, -15, -9.4527 }, { -9, 15, 1.5505 }, { -9, 9, -1.8902 }, { -9, 3, 0.3005 }, { -9, -3, -11.0824 }, { -9, -9, -12.0496 }, { -9, -15, -10.2089 }, { -3, 15, 0.9317 }, { -3, 9, -0.1839 }, { -3, 3, -0.4902 }, { -3, -3, -11.3871 }, { -3, -9, -11.1589 }, { -3, -15, -10.9761 }, { 3, 15, 2.0192 }, { 3, 9, 1.2911 }, { 3, 3, -0.1339 }, { 3, -3, -0.7308 }, { 3, -9, -0.6683 }, { 3, -15, -0.6058 }, { 9, 15, 1.0911 }, { 9, 9, 1.1348 }, { 9, 3, 0.6973 }, { 9, -3, 0.1192 }, { 9, -9, -0.6058 }, { 9, -15, -0.4870 }, { 15, 15, 1.2161 }, { 15, 9, 1.5942 }, { 15, -9, 0.8442 }, { 15, -15, -2.2839 }, { 21, 9, 1.3723 }, { 21, 3, 1.0817 }, { 21, -3, 0.7723 }, { 21, -9, -0.4839 }, { 27, 3, -0.3464 }, { 27, -3, 0.0817 } };
            //this.setGrid(xy);
        }

        public new void OnEnable()
        {
            base.OnEnable();
        }

        public new void OnDisable()
        {
            base.OnDisable();

            _oldOverlayTexture = null; // important to force regeneration of Material on re-enable
            ReleaseTarget();           // nothing to hold while switched off
        }

        private static float LevelsOfBlur(int w, int h)
        {
            return 1 + 1 + Mathf.Floor(Mathf.Log(Mathf.Max(w, h)));
        }

        /// <summary>
        /// The mip chain this effect blurs with, at the size and format of the image it is given.
        ///
        /// It used to be made once in Awake, from Screen, rounded to the closest power of two
        /// in 8-bit linear: 2560x1440 became 2048x1024, so the whole picture -- the healthy
        /// field included -- was squeezed through a smaller, anisotropic texture and came out
        /// soft, shadows banded, and the size never followed a resolution change. Mip chains
        /// on non-power-of-two textures are supported on every graphics API this ships on.
        /// Allocating on first use also stops the effect holding the texture while it is off.
        /// </summary>
        private void EnsureTarget(RenderTexture source)
        {
            if (rt != null && rt.width == source.width && rt.height == source.height &&
                rt.format == source.format) return;
            ReleaseTarget();

            var desc = source.descriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.useMipMap = true;
            desc.autoGenerateMips = true;
            rt = new RenderTexture(desc);
            rt.Create();

            numLevelsOfBlur = LevelsOfBlur(source.width, source.height);
            if (Material != null) Material.SetFloat("_MaxLODlevel", numLevelsOfBlur);
        }

        private void ReleaseTarget()
        {
            if (rt == null) return;
            rt.Release();
            DestroyImmediate(rt);
            rt = null;
        }

        protected override void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            EnsureTarget(source);

            // blit whole camera image into an intermediate rendertexture (rt), at which point mipmaps will be generated
            Graphics.Blit(source, rt);

            // Sized in pixels of the image it is drawn over, so it is set here, per image.
            Material.SetVector("_OverlayScale",
                OverlayScaleFor(radius_deg, ViewingGeometry.PixelsPerDegree, source.width, source.height));

            // Blit onto screen, at which point mipmapblur shader will be applied
			Graphics.Blit(rt, destination, Material, 0);
        }

        // Update is called once per frame
		protected override void OnUpdate()
        {
            if (_oldOverlayTexture != overlayTexture)
            {
                Material.SetFloat("_MaxLODlevel", numLevelsOfBlur);
                Material.SetTexture("_Overlay", overlayTexture);
                _oldOverlayTexture = overlayTexture;
            }

            // Gaze-contingent
            Vector2 xy_norm = GazeTracker.GetInstance.xy_norm;
            Material.SetFloat("_MouseX", 1 - xy_norm.x); // Why 1-x???
            Material.SetFloat("_MouseY", xy_norm.y);
            //Debug.Log ("Setting position: " + ((-pos.x+1f)/2f) );

            Material.SetFloat("_DarkenAmount", 1.0f);
            Material.SetFloat("_StarIntensity", 1.0f);
            Material.SetFloat("_StarSpeed", 1.0f);

        }

        
        public void setGrid(double[,] grid_xy)
        {
			this.setGrid (grid_xy, false);
        }
		public void setGrid(double[,] grid_xy, bool extrapolateEdges)
		{
			overlayRawGrid_xy = grid_xy;
			overlayTexture = GridInterpolator.Instance.interpolateGridAndMakeTexture(grid_xy, extrapolateEdges);
			Material.SetTexture("_Overlay", overlayTexture);
		}
        public double[,] getGrid()
        {
            Debug.Log(">>> " + this.overlayRawGrid_xy.Length + ": " + this.overlayRawGrid_xy[0,0]);
            return this.overlayRawGrid_xy;
        }

        protected void OnDestroy()
        {
            ReleaseTarget();
        }
        protected override string GetShaderName()
        {
            return "Hidden/VisSim/myFieldLoss";
        }

    }
}

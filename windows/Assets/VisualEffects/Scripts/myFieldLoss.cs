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

        [Linkable, Range(0.1f, 3.0f), Tooltip("Overlay Scale.")]
        public float overlayScale = 0.75f;

        private Texture2D _oldOverlayTexture = null; // for checking for updates
        private float numLevelsOfBlur;
        private RenderTexture rt; // temporary render texture for generating mipmaps

        protected void Awake() // (NB: not Start, as Grid needs to be set even if not enabled)
        {
            base.Start();

            //Create render texture (NB: we will do this rather than using a temporary rendertexture, as in Unity 5+ these don't appear to support mipmaps)
            int width_px = Screen.width;
            int height_px = Screen.height;
            //Ensure size is a power of two: NB this is extremely important, as mipmaps will only be generated for rendertextures that are a power of two!!
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
            // initialise texture
            rt = new RenderTexture(width_px, height_px, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); // MOVE TO INIT
            rt.useMipMap = true;
            rt.isPowerOfTwo = true; // round dimensions to nearest power of two size (I think?). 
            rt.Create();

            // compute number of levels of blur -- this will be set when(ever) the Material is created (onEnable)
            numLevelsOfBlur = 1 + 1 + Mathf.Floor(Mathf.Log(Mathf.Max(width_px, height_px)));
            
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
        }

        protected override void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // blit whole camera image into an intermediate rendertexture (rt), at which point mipmaps will be generated
            Graphics.Blit(source, rt);

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

            // Set the overlay scale
            Material.SetFloat("_OverlayScale", overlayScale);
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
			ApplyGrid(grid_xy, extrapolateEdges);

			// Forward to the other eye's instance.
			//
			// The eye linking copies fields marked [Linkable], and overlayTexture is
			// one -- but the grid arrives through this METHOD, and the method also
			// pushes the texture into Material, which is per-instance. So the twin
			// ended up enabled, rendering every frame, with no _Overlay bound: a
			// field-loss effect with no field loss, i.e. invisible. Since the
			// displayed camera is the one whose instance was never told, switching
			// Vision Loss on by itself appeared to do nothing, while effects
			// configured purely through [Linkable] scalars worked normally.
			//
			// ApplyGrid rather than setGrid, so the twin does not forward back.
			var twin = TwinEyeEffect as myFieldLoss;
			if (twin != null && twin != this)
			{
				twin.ApplyGrid(grid_xy, extrapolateEdges);
			}
		}

		/// <summary>Sets the grid on THIS instance only. See setGrid.</summary>
		private void ApplyGrid(double[,] grid_xy, bool extrapolateEdges)
		{
			overlayRawGrid_xy = grid_xy;
			overlayTexture = GridInterpolator.Instance.interpolateGridAndMakeTexture(grid_xy, extrapolateEdges);
			// Material lazily creates the material; guard so a call before this
			// instance is enabled cannot throw and abort the caller's own setup.
			if (Material != null)
			{
				Material.SetTexture("_Overlay", overlayTexture);
			}
		}
        public double[,] getGrid()
        {
            Debug.Log(">>> " + this.overlayRawGrid_xy.Length + ": " + this.overlayRawGrid_xy[0,0]);
            return this.overlayRawGrid_xy;
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
            return "Hidden/VisSim/myFieldLoss";
        }

    }
}

using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisSim
{
    public class myFieldLossInverted : LinkableBaseEffect
    {

        private string dummy = "";

        // overlay texture
        private double[,] overlayRawGrid_xy;
        [LinkableAttribute]
        public Texture2D overlayTexture = null; // for display purposes only (NB: should ideally be readonly)
        private Texture2D _oldOverlayTexture = null; // for checking for updates


        [Linkable, Range(0.1f, 3.0f), Tooltip("Overlay Scale.")]
        public float overlayScale = 0.5f;

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
            overlayTexture = (Texture2D)Resources.Load("macular-degeneration", typeof(Texture2D));
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
            Material.SetFloat("_MouseX", 1 - xy_norm.x); 
            Material.SetFloat("_MouseY", xy_norm.y);

            // Set the overlay scale
            Material.SetFloat("_OverlayScale", overlayScale);
        }

        public void setGrid(double[,] grid_xy)
        {
            this.setGrid(grid_xy, false);
        }

        public void setGrid(double[,] grid_xy, bool extrapolateEdges)
        {
            overlayRawGrid_xy = grid_xy;
            overlayTexture = GridInterpolator.Instance.interpolateGridAndMakeTexture(grid_xy, extrapolateEdges);
            Material.SetTexture("_Overlay", overlayTexture);
        }

        public double[,] getGrid()
        {
            Debug.Log(">>> " + this.overlayRawGrid_xy.Length + ": " + this.overlayRawGrid_xy[0, 0]);
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
            return "Hidden/VisSim/myFieldLossInverted";
        }
    }
}

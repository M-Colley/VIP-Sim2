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


        // Radius of the residual field: from the point of gaze to the middle of the soft edge
        // beyond which vision is lost, in degrees of visual angle. Conversions and the overlay's
        // measured edge are in myFieldLoss. It replaced overlayScale (see there).
        [Linkable, Range(0f, myFieldLoss.MaxRadiusDeg), Tooltip("Radius of the residual field, degrees of visual angle.")]
        public float radius_deg = 10f;

        private float numLevelsOfBlur;
        private RenderTexture rt; // temporary render texture for generating mipmaps

        protected void Awake() // (NB: not Start, as Grid needs to be set even if not enabled)
        {
            base.Start();

            // The intermediate texture is allocated on first use, at the size of the image it is
            // given; see EnsureTarget. This only sets the blur depth the Material starts with.
            numLevelsOfBlur = LevelsOfBlur(Screen.width, Screen.height);

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
                myFieldLoss.OverlayScaleFor(radius_deg, ViewingGeometry.PixelsPerDegree, source.width, source.height));

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

        protected void OnDestroy()
        {
            ReleaseTarget();
        }
        protected override string GetShaderName()
        {
            return "Hidden/VisSim/myFieldLossInverted";
        }
    }
}

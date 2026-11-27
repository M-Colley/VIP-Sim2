using UnityEngine;

namespace VisSim
{
    public class myNystagmus : LinkableBaseEffect
     {
        [Linkable, Range(0f, 1f)]
        public float foveat_d = 0.12f;                  // duration of sustained/'0'/foveation period [0.12 in C & B]
        [Linkable, Range(0f, 1f)]
        public float rise_d = 0.13f;                    // duration of rise period [~0.15 in C & B]
        [Linkable, Range(0.1f, 4f)]
        public float rise_exp = 1.75f;        	        // rise exponent(e.g, 1==linear, 2==exponential)  [~1.75 in C&B]
        [Linkable, Range(0.1f, 20f)]
        public float amp_deg = 8f;                      // total shift rightwards [8 in C & B]
        [Linkable, Range(0.1f, 4f)]
        public float baselineErr_deg = 1f / 60f * 44f;  // uniform random error in baseline(e.g., 0 to 0.7333) [0.7333 in C&B]
        [Linkable, Range(0f,359.9f)]
        public float direction_deg = 0f;  // direction of travel (currently only used in artificial/monocular mode)

        // binocular (rotate camera) or monocular (transpose image)
        // VIP-Sim shows a 2D capture through an orthographic camera, so it uses the pixel shift.
        // Rotating that camera moved the image by about 2.6 px per degree, a scale set by how far
        // the window plane sits from the camera, and left the camera rotated after the effect
        // was switched off. The pixel shift uses ViewingGeometry and leaves the camera alone.
        [Linkable, Tooltip("If artificialRotation=true then displacement will be carried out by shifting/extrapolating pixels, rather than by rotating the camera (appropriate for 2D images).")]
        public bool artificialRotation = true;

        // nulling map
        [Linkable, Range(0, 1)]
        public bool useNullingField = true;  // uniform random error in baseline(e.g., 0 to 0.7333) [0.7333 in C&B]
        [Linkable]
        public Texture2D nullTex;
        [SerializeField]
        private float _nullAmount;
        public float nullAmount { get { return _nullAmount; } }

        // internal params
        private float baselineShift_deg = 0f;
        private float timer_secs = 0f;
        private float t_cycle = 0f;
        private float t_cycle_prev = 0f;
        private float shift_deg = 0f;
        private float old_shift_deg = 0f;
        [Tooltip("Diploplia strength.")]
        private Vector2 Displace = new Vector2(0.7f, 0.0f);

        [SerializeField, Tooltip("Logs additional information when enabled for debugging.")]
        private bool enableDebugLogging = false;

        private float pixel_per_dg;

        // init
        protected new void OnEnable()
        {
            base.OnEnable();

            // set overlay texture
            nullTex = (Texture2D)Resources.Load("nystagmus_nullingmap2_512", typeof(Texture2D));
            Material.SetTexture("_NullingOverlay", nullTex);
        }

        protected new void OnDisable()
        {
            // remove any existing shift
            this.gameObject.transform.localEulerAngles = new Vector3(this.gameObject.transform.localEulerAngles.x, this.gameObject.transform.localEulerAngles.y-old_shift_deg, this.gameObject.transform.localEulerAngles.z);

            base.OnDisable();
        }

        // What the cycle actually runs on. The public fields hold the slider values, and the
        // slider runs the other way round from the durations (1 - x, and 4 - x for the
        // exponent). This used to be written back into the public fields themselves, so the
        // values the UI, profiles and saved settings read were the inverted ones: a saved
        // setting reloaded reversed, and reopening the panel showed the opposite of what was
        // running. The mapping itself is unchanged; it is just no longer stored in the field.
        private float run_foveat_d, run_rise_d, run_rise_exp;


        protected override void OnUpdate()
        {
            if (enableDebugLogging)
            {
                Debug.Log("foveat_d" + foveat_d);
            }

            // Floors keep the cycle defined at the ends of the sliders: a zero period made the
            // modulo below NaN and a zero rise divided by zero.
            run_foveat_d = Mathf.Max(0f, 1f - foveat_d);
            run_rise_d = Mathf.Max(1e-3f, 1f - rise_d);
            run_rise_exp = 4f - rise_exp;

            // From the screen's physical size and the viewing distance.
            pixel_per_dg = ViewingGeometry.PixelsPerDegree;

            //Update timer
            timer_secs += Time.deltaTime;
            // Reset the timer after a while, some GPUs don't like big numbers
            if (timer_secs > 1000f)
            {
                timer_secs -= 1000f;
            }
            
            // Gaze-contingent:
            // Get
            Vector2 xy_norm = GazeTracker.GetInstance.xy_norm;
            float x = xy_norm.x;
            float y = xy_norm.y;
            // Adjust
            //x = (x - 0.3f) / (0.7f - 0.3f); // TMP HACK!!! (to ensure actually use full range of 0 to 1, remap from ~0.3 to 7)
            //y = (y - 0.3f) / (0.7f - 0.3f); // TMP HACK!!! (to ensure actually use full range of 0 to 1, remap from ~0.3 to 7)
            // Set
            x = Mathf.Min(Mathf.Max(x, 0f), 1f);
            y = Mathf.Min(Mathf.Max(y, 0f), 1f);

            // Debugging
            //Debug.Log ("x=" + x + ",  y=" + y);


            /*------ Simulate jerk nystagmus ------ */
            // get time elapsed within current period
            float total_d = run_foveat_d + run_rise_d;
            t_cycle = timer_secs % total_d;

            // if starting a new peiod, generate a new random baseline
            if (t_cycle < t_cycle_prev)
            {
                // reset camera
                float starting_deg = this.gameObject.transform.localEulerAngles.y - old_shift_deg;
                this.gameObject.transform.localEulerAngles = new Vector3(this.gameObject.transform.localEulerAngles.x, starting_deg, this.gameObject.transform.localEulerAngles.z);
                old_shift_deg = starting_deg;
                
                baselineShift_deg = Random.Range(0f, baselineErr_deg) - starting_deg;
            }
            t_cycle_prev = t_cycle;

            // get x value, between 0 and 1 (proportion of the way through the current period)
            float shift_proportion;
            if (t_cycle <= run_foveat_d)
            {
                shift_proportion = 0;
            } else {
                shift_proportion = (t_cycle - run_foveat_d) / run_rise_d; // proportion of rise period
                shift_proportion = Mathf.Pow(shift_proportion, run_rise_exp);
            }

            // apply amplitude(scale x to be: 0 <= x <= amp_deg)
            shift_deg = shift_proportion * amp_deg;

            // apply baseline(scale x to be: baseline_deg <= x <= amp_deg)
            shift_deg = Mathf.Max(shift_deg, baselineShift_deg);

            if (!artificialRotation) // rotate camera(s)
            {
                if (useNullingField)
                {
                    Color nulling = nullTex.GetPixelBilinear(x, y);
                    _nullAmount = (nulling.r + nulling.g + nulling.b) / 3f;
                    shift_deg *= (1 - Mathf.Sqrt(_nullAmount)); // squareroot to increase the nulling effect
                }
                // compute delta
                float shift_delta_deg = shift_deg - old_shift_deg;
                old_shift_deg = shift_deg;
                // set
                //transform.Rotate(Vector3.up * shift_delta_deg);
                this.gameObject.transform.localEulerAngles = new Vector3(this.gameObject.transform.localEulerAngles.x, this.gameObject.transform.localEulerAngles.y+shift_delta_deg, this.gameObject.transform.localEulerAngles.z);
            } else {
			    Material.SetFloat("_MouseX", x); 
			    Material.SetFloat("_MouseY", y);
            }
        }

        private float deg2rad = Mathf.PI / 180f;
        protected override void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!artificialRotation) // rotate camera(s)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // artificial rotation
            //Displace = new Vector2(shift_deg * pixel_per_dg, 0f); // force horizontal
            float xOffset_deg = -shift_deg * Mathf.Cos(direction_deg * deg2rad); //???
            float yOffset_deg = shift_deg * Mathf.Sin(direction_deg * deg2rad);
            Displace = new Vector2(xOffset_deg * pixel_per_dg, yOffset_deg * pixel_per_dg);
            if (Displace == Vector2.zero)
            {
                Graphics.Blit(source, destination);
                return;
            }
            // debugging:
            //Debug.Log(Displace.x + ", " + Displace.y);
            Material.SetVector("_Displace", new Vector2(Displace.x / (float)source.width, Displace.y / (float)source.height));
            Graphics.Blit(source, destination, Material, useNullingField ? 1 : 0);
        }

				
		protected override string GetShaderName()
		{
			return "Hidden/VisSim/myNystagmus";
		}
    }
}

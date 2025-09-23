// PJ 13/09/2017
// TODO: add stereoscopic offset to cursor visualisation (?)
using UnityEngine;
//using Tobii.XR;
using System.Collections;

public class GazeTracker : MonoBehaviour
{

    public enum GazeSource
    {
        Fove = 0,
        UnitEye = 1,
        Mouse = 2,
        None = 3,
    }
    public GazeSource gazeSource = GazeSource.Mouse;

    public Camera Lcam;
    public Camera Rcam;

    public Vector2 xy_norm = new Vector2(0.5f, 0.5f);
    //public Vector2 xy_px = new Vector2(0.5f, 0.5f);

    public GameObject unitEye;

    // for visualising gaze estimate
    public bool visualiseGaze;
    public Texture2D crosshairImage; // (Texture2D)Resources.Load("crosshair");

    private float prevX = 999f;
    private float prevY = 999f;

    // Cache reference to the Gaze component instead of searching every frame
    private Gaze gazeScript;
    // Cache the active state of the unitEye object
    private bool unitEyeActive = false;

    // Singleton
    private static GazeTracker instance; // Singleton instance
    public static GazeTracker GetInstance
    {
        get
        {
            if (instance == null)
            {
                //instance = new GazeTracker();
                //instance = ScriptableObject.CreateInstance("GazeTracker") as GazeTracker;
                //GameObject go = new GameObject();
                //GameObject go = GameObject.Find("Fove Interface");
                //instance = go.AddComponent<GazeTracker>();
                instance = (GazeTracker)FindObjectOfType(typeof(GazeTracker));
            }
            return instance;
        }
    }
    private GazeTracker()
    {
    }
    
    // Use this for initialization
    void Start ()
    {
        //var settings = new TobiiXR_Settings();
        //TobiiXR.Start(settings);

        // Find and cache the gaze script once on start
        gazeScript = FindObjectOfType<Gaze>();
    }

	// Update is called once per frame
        void Update ()
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        switch (gazeSource) {
                case GazeSource.Fove:
                /*
        	// Get convergence data
			Fove.Managed.SFVR_GazeConvergenceData convergence = FoveInterface2.GetFVRHeadset ().GetGazeConvergence ();

	   		// use Ray to get world space coordinate: 
			Vector3 o = new Vector3 (convergence.ray.origin.x, convergence.ray.origin.y, convergence.ray.origin.z);
			Vector3 d = new Vector3 (convergence.ray.direction.x, convergence.ray.direction.y, convergence.ray.direction.z);
			Vector3 pos = o + d * 1.0f;

	     	//.25 to .75? & set
			xy_norm.x = (pos.x + 1f) / 2f;
			xy_norm.y = (pos.y + 1f) / 2f;

          	// finished Fove
			break;
            */
        case GazeSource.UnitEye:
                if (!unitEyeActive)
                {
                    unitEye.SetActive(true);
                    unitEyeActive = true;
                }
                xy_norm = FindGazeLocation();
                xy_norm.x = xy_norm.x / screenWidth;
                xy_norm.y = (screenHeight - xy_norm.y) / screenHeight;
            //get latest data from Tobii Eye-Tracker
                /*
                var latestdata = TobiiXR.GetEyeTrackingData(TobiiXR_TrackingSpace.Local);
                var gazepoint = latestdata.GazeRay.Direction;

                //only update when GazeRay is valid else keep old value
                if (latestdata.GazeRay.IsValid)
                {
                    Vector3 d = new Vector3(latestdata.GazeRay.Direction.x, latestdata.GazeRay.Direction.y, latestdata.GazeRay.Direction.z);

                    //.25 to .75? & set <-- no clue what this means...
                    float tmp = .5f;
                    xy_norm.x = (d.x * tmp + 0.5f) * 1f + 0.011f;
                    xy_norm.y = (d.y * tmp + 0.5f) * 1f;
                    //Debug.Log("Values -> Gazepoint: " + gazepoint + " -> D: " + d + " -> X: " + xy_norm.x + " -> Y: " + xy_norm.y);
                }
                    // only register large movements
                    float dist = Mathf.Sqrt(Mathf.Pow(xy_norm.x - prevX, 2) + Mathf.Pow(xy_norm.y - prevY, 2));
                    if (dist > 0.01f)
                    {
                        prevX = xy_norm.x;
                        prevY = xy_norm.y;
                    }
                    else
                    {
                        xy_norm.x = prevX;
                        xy_norm.y = prevY;

                    }
                */
                break;
        case GazeSource.Mouse:
            if (unitEyeActive)
            {
                unitEye.SetActive(false);
                unitEyeActive = false;
            }
                // Get raw, clip within canvas
                        float mousex = Mathf.Min (Mathf.Max (Input.mousePosition.x, 0), screenWidth);
                        float mousey = Mathf.Min (Mathf.Max (Input.mousePosition.y, 0), screenHeight);

            // Convert to norm & set
                        xy_norm.x = mousex / screenWidth;
                        xy_norm.y = mousey / screenHeight;

            //Debug.Log("Mouse --> " + xy_norm);

            // finished Mouse
            break;
                case GazeSource.None:
                // fix at centre
                        xy_norm.x = 0.5f;
                        xy_norm.y = 0.5f;

                if (unitEyeActive)
                {
                    unitEye.SetActive(false);
                    unitEyeActive = false;
                }

                // finished None
                        break;
                default:
                        throw new System.ArgumentException ("Unknown GazeSource parameter?");
                }

        // clamp within 0 to 1 range (defensive)
        xy_norm.x = Mathf.Clamp01(xy_norm.x);
        xy_norm.y = Mathf.Clamp01(xy_norm.y);
    }

    void OnGUI()
    {
        if (visualiseGaze)
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float xMin = (screenWidth / 2)*xy_norm.x - (crosshairImage.width / 2);
            float yMin = screenHeight*(1-xy_norm.y) - (crosshairImage.height / 2);
            GUI.DrawTexture(new Rect(xMin, yMin, crosshairImage.width, crosshairImage.height), crosshairImage);
            GUI.DrawTexture(new Rect(xMin + (screenWidth / 2), yMin, crosshairImage.width, crosshairImage.height), crosshairImage);
        }
    }

    private Vector2 FindGazeLocation()
    {
        // If the script reference was lost, try to locate it again
        if (gazeScript == null)
        {
            gazeScript = FindObjectOfType<Gaze>();
            if (gazeScript == null)
            {
                Debug.LogError("Gaze script not found in the scene!");
                return Vector2.zero;
            }
        }

        return gazeScript.gazeLocation;
    }
}

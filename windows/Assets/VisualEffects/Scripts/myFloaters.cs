using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VisSim
{
    public class myFloaters : LinkableBaseEffect
    {
        public enum FloaterType
        {
            Dark = 0,
            Light = 1,
            Scintillating = 2,
        }
        public FloaterType floaterType = FloaterType.Dark;

        [Linkable, Range(0.0f, 1.0f)]
        public float intensity = 1.0f;

        [Linkable, Range(1, 10f)]
        public float floaterSize = 1;

        [Linkable, Range(0.0f, 2500f)]
        public float floaterDensity = 500f;

        [Linkable, Range(50f, 250f)]
        public float circleRadius = 150f;

        [Linkable]
        public bool center = true;

        // Shader params
        public Texture2D texture = null;

        // Scintillation params
        private float ScStrength = 1f;
        private float ScLumContribution = 1f;

        [Linkable, Range(0.0f, 3.0f), Tooltip("Wave animation speed.")]
        public float Speed = 1f;
        [Linkable, Tooltip("Wave frequency (higher means more waves).")]
        private float Frequency = 12f;
        [Linkable, Tooltip("Wave amplitude (higher means bigger waves).")]
        private float Amplitude = 0.01f;


        // Store previous values to track changes
        //private float previousIntensity;
        private float previousFloaterSize;
        private float previousFloaterDensity;
        private float previousRadius;
        private bool previousCenter;

        // Boolean to track if values changed
        //private bool valuesChanged = false;

        // internal
        private float Timer = 0f;
        //private float _old_floaterSize = 1;
        //private float _old_floaterDensity = 0.5f;

        private const int OverlayWidth = 1024;
        private const int OverlayHeight = 1024;
        private const int MaxFloaterCount = OverlayWidth * OverlayHeight / 10;

        private Color[] floaterPixels;
        private bool overlayRegenerationQueued;

        // cellular automata (Game of Life) params

        /*
        private static bool[,] golBoard; // Holds the current state of the board.
        private static int golBoardWidth = 256; // The width of the board in n-cells.
        private static int golBoardHeight = 256; // The height of the board in n-cells.
        private static bool golLoopEdges = false; // True if cell rules can loop around edges.
        private static float[,] smoothedBoard; // Holds the current state of the board.
        */

        public new void OnEnable()
        {
            //previousIntensity = intensity;
            previousFloaterSize = floaterSize;
            previousFloaterDensity = floaterDensity;
            previousCenter = center;
            previousRadius = circleRadius;
            overlayRegenerationQueued = false;
            // init floater texture
            generateOverlayTexture();

            // call base method to enable effect
            base.OnEnable();
        }

        // Update is called once per frame
        protected override void OnUpdate()
        {
            // Reset the timer after a while, some GPUs don't like big numbers
            if (Timer > 1000f)
                Timer -= 1000f;

            // Increment timer
            Timer += Speed * Time.deltaTime;

            if (floaterSize != previousFloaterSize || floaterDensity != previousFloaterDensity || previousRadius != circleRadius || previousCenter != center)
            {
                // Set flag if values have changed
                //valuesChanged = true;
                if (!overlayRegenerationQueued)
                {
                    Debug.Log("Values have changed! Regenerating texture...");
                }

                RequestOverlayRegeneration();

                // Update the previous values to the new ones
                //previousIntensity = intensity;
                previousFloaterSize = floaterSize;
                previousFloaterDensity = floaterDensity;
                previousCenter = center;
                previousRadius = circleRadius;
            }
            else
            {
                //valuesChanged = false; // No changes detected
            }

            // Pass mouse position to shader
            Vector2 xy_norm = GazeTracker.GetInstance.xy_norm;
            Material.SetFloat("_MouseX", 1 - xy_norm.x); 
            Material.SetFloat("_MouseY", 1 - xy_norm.y);

        }

        private void LateUpdate()
        {
            if (!overlayRegenerationQueued)
            {
                return;
            }

            overlayRegenerationQueued = false;
            generateOverlayTexture();
        }

        private void RequestOverlayRegeneration()
        {
            overlayRegenerationQueued = true;
        }

        // Called by camera to apply image effect
        protected override void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Vector4 UV_Transform = new Vector4(1, 0, 0, 1);

#if UNITY_WP8
	    	    // WP8 has no OS support for rotating screen with device orientation,
	    	    // so we do those transformations ourselves.
			    if (Screen.orientation == ScreenOrientation.LandscapeLeft) {
				    UV_Transform = new Vector4(0, -1, 1, 0);
			    }
			    if (Screen.orientation == ScreenOrientation.LandscapeRight) {
				    UV_Transform = new Vector4(0, 1, -1, 0);
			    }
			    if (Screen.orientation == ScreenOrientation.PortraitUpsideDown) {
				    UV_Transform = new Vector4(-1, 0, 0, -1);
			    }
#endif


            // set params
            Material.SetVector("_UV_Transform", UV_Transform);
            Material.SetFloat("_Intensity", intensity);
            Material.SetTexture("_Overlay", texture);
            Material.SetVector("_WarpParams", new Vector3(Frequency, Amplitude, Timer));

            // update params
            Material.SetVector("_ScintillateParams", new Vector3(Timer, ScStrength, ScLumContribution));

            // Blit
            switch (floaterType)
            {
                case FloaterType.Dark:
                    Graphics.Blit(source, destination, Material, 0);
                    break;
                case FloaterType.Light:
                    Graphics.Blit(source, destination, Material, 1);
                    break;
                case FloaterType.Scintillating:
                    Graphics.Blit(source, destination, Material, 2);
                    break;
                default:
                    Console.WriteLine("??????");
                    break;
            };
        }


        //private float minGradient = 0.1f; // Minimum gradient value
        //private float maxGradient = 0.95f; // Maximum gradient value


        private void generateOverlayTexture()
        {
            Debug.Log("Generating Floaters with custom parameters");

            if (texture == null || texture.width != OverlayWidth || texture.height != OverlayHeight)
            {
                if (texture != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(texture);
                    }
                    else
                    {
                        DestroyImmediate(texture);
                    }
                }

                texture = new Texture2D(OverlayWidth, OverlayHeight, TextureFormat.RGBA32, false);
            }

            int pixelCount = OverlayWidth * OverlayHeight;
            if (floaterPixels == null || floaterPixels.Length != pixelCount)
            {
                floaterPixels = new Color[pixelCount];
            }
            else
            {
                Array.Clear(floaterPixels, 0, floaterPixels.Length);
            }

            int numFloaters = Mathf.FloorToInt(floaterDensity * 100f); // Adjustable density
            if (numFloaters > MaxFloaterCount)
            {
                Debug.LogWarning($"Floater density {floaterDensity} generates {numFloaters} floaters. Clamping to {MaxFloaterCount} to avoid long stalls.");
                numFloaters = MaxFloaterCount;
            }

            for (int i = 0; i < numFloaters; i++)
            {
                AddFloater(floaterPixels, OverlayWidth, OverlayHeight, floaterSize);
            }

            texture.SetPixels(floaterPixels);
            texture.Apply();

            Debug.Log($"Floaters generated successfully! ({numFloaters} floaters)");
        }

        private void AddFloater(Color[] pixels, int width, int height, float floaterSize)
        {
            // Define the circle parameters
            Vector2 circleCenter = new Vector2(width / 2, height / 2);
            //float circleRadius = Mathf.Min(width, height) / 4; // Adjust radius as needed

            // Random position and size
            int floaterWidth = Mathf.FloorToInt(floaterSize * UnityEngine.Random.Range(0.5f, 1.5f));
            int floaterHeight = floaterWidth;


            int xPos;
            int yPos;

            if (center)
            {
                // Find a random position inside the circle
                do
                {
                    xPos = UnityEngine.Random.Range(0, width - floaterWidth);
                    yPos = UnityEngine.Random.Range(0, height - floaterHeight);
                } while (!IsInsideCircle(new Vector2(xPos + floaterWidth / 2, yPos + floaterHeight / 2), circleCenter, circleRadius));
            }
            else
            {
                // Standard random position
                xPos = UnityEngine.Random.Range(0, width - floaterWidth);
                yPos = UnityEngine.Random.Range(0, height - floaterHeight);
            }

            // Use Perlin noise or random noise for organic blob shape
            for (int y = 0; y < floaterHeight; y++)
            {
                for (int x = 0; x < floaterWidth; x++)
                {
                    float perlinValue = Mathf.PerlinNoise(x * 0.1f, y * 0.1f); // Control shape using Perlin noise
                    if (perlinValue > 0.5f)
                    {
                        pixels[(xPos + x) + (yPos + y) * width] = Color.white; // White for the floaters
                    }
                }
            }
        }

        private bool IsInsideCircle(Vector2 point, Vector2 circleCenter, float radius)
        {
            // Calculate the distance between the point and the center of the circle
            float distance = Vector2.Distance(point, circleCenter);

            // Calculate the distance from the edge of the circle
            float distanceFromEdge = distance - radius;

            // If the point is inside the circle, there is no hard edge
            if (distance <= radius)
            {
                return true;
            }
            else
            {
                if (UnityEngine.Random.Range(0f, 1f) < 0.01)
                    return true;
            }

            // The probability that the point is returned as "inside" decreases the further the point is from the edge
            float probability = 1.0f - distanceFromEdge / radius;
            return UnityEngine.Random.Range(0f, 1f) < probability;
        }



        // Utility function to get the reposition probability based on distance from center
        private float GetRepositionProbability(Vector2 circleCenter, float circleRadius)
        {
            // Calculate distance from the center (0 to circleRadius)
            float distanceFromCenter = Vector2.Distance(circleCenter, circleCenter) / circleRadius;
            // Normalize distance to range between minGradient and maxGradient
            float normalizedDistance = Mathf.Clamp01(distanceFromCenter / circleRadius);
            //float gradientValue = Mathf.Lerp(minGradient, maxGradient, Mathf.Pow(normalizedDistance, 20));
            return normalizedDistance;
        }

        /*
        private void generateOverlayTexture()
        {
            Debug.Log("Generating Floaters with custom parameters");

            // Create a texture
            int width = 512;
            int height = 512;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // Initialize with white background
            Color[] pixels = new Color[width * height];

            // Generate floaters
            int numFloaters = Mathf.FloorToInt(floaterDensity * 100); // Adjustable density
            for (int i = 0; i < numFloaters; i++)
            {
                AddFloater(pixels, width, height, floaterSize);
            }

            // Apply texture
            texture.SetPixels(pixels);
            texture.Apply();

            // Apply Gaussian blur for a natural look
            //ApplyGaussianBlur(texture, 3); // Adjustable blur amount

            // Create material and set texture
            Material material = new Material(Shader.Find("Hidden/BlurEffectConeTap"));
            material.mainTexture = texture;

            Debug.Log("Floaters generated successfully!");
        }

        private void AddFloater(Color[] pixels, int width, int height, float floaterSize)
        {
            // Random position and size
            int floaterWidth = Mathf.FloorToInt(floaterSize * UnityEngine.Random.Range(0.5f, 1.5f));
            int floaterHeight = floaterWidth;

            int xPos = UnityEngine.Random.Range(0, width - floaterWidth);
            int yPos = UnityEngine.Random.Range(0, height - floaterHeight);

            // Use Perlin noise or random noise for organic blob shape
            for (int y = 0; y < floaterHeight; y++)
            {
                for (int x = 0; x < floaterWidth; x++)
                {
                    float perlinValue = Mathf.PerlinNoise(x * 0.1f, y * 0.1f); // Control shape using Perlin noise
                    if (perlinValue > 0.5f)
                    {
                        //pixels[(xPos + x) + (yPos + y) * width] = new Color(0, 0, 0, UnityEngine.Random.Range(0.3f, 0.7f));
                        pixels[(xPos + x) + (yPos + y) * width] = Color.white; // Black for the floaters
                    }
                }
            }
        }

        private void ApplyGaussianBlur(Texture2D texture, int blurRadius)
        {
            // Perform a simple box blur (can be improved with a Gaussian blur kernel)
            Color[] pixels = texture.GetPixels();
            Color[] blurredPixels = new Color[pixels.Length];

            int width = texture.width;
            int height = texture.height;

            for (int y = blurRadius; y < height - blurRadius; y++)
            {
                for (int x = blurRadius; x < width - blurRadius; x++)
                {
                    Color sum = new Color(0, 0, 0, 0);
                    int count = 0;

                    // Simple box kernel for blur
                    for (int ky = -blurRadius; ky <= blurRadius; ky++)
                    {
                        for (int kx = -blurRadius; kx <= blurRadius; kx++)
                        {
                            sum += pixels[(x + kx) + (y + ky) * width];
                            count++;
                        }
                    }

                    blurredPixels[x + y * width] = sum / count;
                }
            }

            // Apply blurred pixels back to the texture
            texture.SetPixels(blurredPixels);
            texture.Apply();
        }

        /*
        // GENERATING VITREOUS-FLOATERS OVERLAY TEXTURE
        private void generateOverlayTexture()
        {
            Debug.Log("Initialising Game Of Life" + UnityEngine.Random.Range(-10.0f, 10.0f));

            // generate golBoard
            runGameOfLife();

            // use golBoard to generate a texture
            var tex = new Texture2D(golBoardWidth, golBoardHeight, TextureFormat.RGB24, false); // Create a new texture RGB24 (24 bit without alpha) and no mipmaps
            Color[] imgMatrix = new Color[golBoardWidth * golBoardHeight];
            for (int x = 0; x < golBoardWidth; x++)
            {
                for (int y = 0; y < golBoardHeight; y++)
                {
                    imgMatrix[x + y * golBoardWidth] = golBoard[x, y] ? Color.black : Color.white;
                }
            }
            tex.SetPixels(imgMatrix);
            tex.Apply(false); // actually apply all SetPixels, don't recalculate mip levels

            // smooth and set
            Material material = new Material(Shader.Find("Hidden/BlurEffectConeTap"));
            RenderTexture rt = RenderTexture.GetTemporary(64, 64); // downsample to blur
            Graphics.Blit(tex, rt, material);
            // Create a new Texture2D and read the RenderTexture image into it
            RenderTexture.active = rt; // Set the supplied RenderTexture as the active one
            texture = new Texture2D(rt.width, rt.height);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply(false);

            // if in editor store values so know whether effect has been updated
            _old_floaterSize = floaterSize;
            _old_floaterDensity = floaterDensity;

            // done
            Debug.Log("Game Of Life Ready!" + UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        private void runGameOfLife()
        {
            // init params
            int nPhase1iterations = 1;
            int nPhase2iterations = 2;

            // Generate a  new randomly seeded map
            initializeRandomBoard();

            // Phase 1
            for (var i = 0; i < nPhase1iterations; i++)
            {
                updateBoard1();
            }

            // Phase 2
            for (var i = 0; i < nPhase2iterations; i++)
            {
                updateBoard2();
            }
        }

        private void initializeRandomBoard()
        {
            // compute param(s) based on user input
            double probCellStartsOn = 0.0075 + (0.02 - 0.0075) * floaterDensity; // 0.0075 to 0.02[Controlling Density]

            // init
            var rand = new System.Random();

            // iterate through board, turning on cells randomly
            golBoard = new bool[golBoardWidth, golBoardHeight];
            for (var y = 0; y < golBoardHeight; y++)
            {
                for (var x = 0; x < golBoardWidth; x++)
                {
                    golBoard[x, y] = rand.NextDouble() < probCellStartsOn;
                }
            }
        }

        private void updateBoard1()
        {
            // compute param(s) based on user input
            float padCheckEmpty = floaterSize + 1;

            // A temp variable to hold the next state while it's being calculated.
            bool[,] newBoard = new bool[golBoardWidth, golBoardHeight];

            for (var y = 0; y < golBoardHeight; y++)
            {
                for (var x = 0; x < golBoardWidth; x++)
                {
                    var nSurroundingOn1 = countLiveNeighbors(x, y, 1);
                    var nSurroundingOn2 = countLiveNeighbors(x, y, padCheckEmpty);
                    newBoard[x, y] = (nSurroundingOn1 >= 5) || (nSurroundingOn2 <= 1);
                }
            }

            golBoard = newBoard;
        }

        private void updateBoard2()
        {
            bool[,] newBoard = new bool[golBoardWidth, golBoardHeight];

            for (var y = 0; y < golBoardHeight; y++)
            {
                for (var x = 0; x < golBoardWidth; x++)
                {
                    var nSurroundingOn1 = countLiveNeighbors(x, y, 1);
                    newBoard[x, y] = nSurroundingOn1 >= 5;
                }
            }

            golBoard = newBoard;
        }

        private int countLiveNeighbors(int x, int y, float n)
        {
            int value = 0;
            int nvalues = 0;

            for (var j = -n; j <= n; j++)
            {
                if (!golLoopEdges && y + j < 0 || y + j >= golBoardHeight)
                {
                    continue;
                }

                float k = (y + j + golBoardHeight) % golBoardHeight;

                for (var i = -n; i <= n; i++)
                {
                    if (!golLoopEdges && x + i < 0 || x + i >= golBoardWidth)
                    {
                        continue;
                    }

                    float h = (x + i + golBoardWidth) % golBoardWidth;

                    value += golBoard[(int)h, (int)k] ? 1 : 0;
                    nvalues++;
                }
            }

            value -= (golBoard[x, y] ? 1 : 0);
            if (nvalues <= 5)
            {
                value = 9;
            }
            return value;
        }
        */
        protected override string GetShaderName()
        {
            return "Hidden/VisSim/myFloaters";
        }
        
    }
}

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace VisSim
{
    
    public class myNoise : LinkableBaseEffect
    {

        [Linkable, Range(0.0f, 1.0f)]
        public float intensity = 1.0f;

        // FastNoise params
        private const String tweaklabel = "z Advanced: Complex Noise";
        [Linkable, Range(0.0f, 1.0f)]
        public float frequency = 0.01f;
        [Linkable, Range(0, 1)]
        public FastNoise.Interp interp = FastNoise.Interp.Quintic;
        [Linkable, Range(0, 1)]
        public FastNoise.NoiseType noiseType = FastNoise.NoiseType.Simplex;
        [Linkable, Range(0, 20)]
        public int octaves = 3;
        [Linkable, Range(0.0f, 10.0f)]
        public float lacunarity = 2.0f;
        [Linkable, Range(0.0f, 2.0f)]
        public float gain = 0.5f;
        public FastNoise.FractalType fractalType = FastNoise.FractalType.FBM;

        // internal
        private static Texture2D[] tex;
        private static bool texturesGenerated = false;
        private static int texWidth;
        private static int texHeight;
        private int counter = 0;

        // Use this for initialization
        public new void OnEnable() {
            base.OnEnable();

            int width_px = Screen.width;
            int height_px = Screen.height;
            const int N = 10;

            if (!texturesGenerated || texWidth != width_px || texHeight != height_px)
            {
                GenerateTextures(width_px, height_px, N);
            }
        }

        private void GenerateTextures(int width_px, int height_px, int N)
        {
            texWidth = width_px;
            texHeight = height_px;
            tex = new Texture2D[N];

            var tasks = new Task<Color32[]>[N];
            var seeds = new int[N];
            for (int i = 0; i < N; i++)
            {
                seeds[i] = UnityEngine.Random.Range(0, 1000);
                int idx = i;
                tasks[i] = Task.Run(() => BuildPixels(width_px, height_px, seeds[idx]));
            }

            for (int i = 0; i < N; i++)
            {
                var pixels = tasks[i].Result;
                tex[i] = new Texture2D(width_px, height_px);
                tex[i].SetPixels32(pixels);
                tex[i].Apply(false);
            }

            texturesGenerated = true;
        }

        private Color32[] BuildPixels(int width_px, int height_px, int seed)
        {
            FastNoise fNoise = new FastNoise();
            fNoise.SetFrequency(frequency);
            fNoise.SetInterp(interp);
            fNoise.SetNoiseType(noiseType);
            fNoise.SetFractalOctaves(octaves);
            fNoise.SetFractalLacunarity(lacunarity);
            fNoise.SetFractalGain(gain);
            fNoise.SetFractalType(fractalType);
            fNoise.SetSeed(seed);

            Color32[] pixels = new Color32[width_px * height_px];
            for (int y = 0; y < height_px; y++)
            {
                float y2 = y * 2f;
                int row = y * width_px;
                for (int x = 0; x < width_px; x++)
                {
                    byte noise = (byte)Mathf.Clamp(fNoise.GetNoise(x * 2f, y2) * 127.5f + 127.5f, 0f, 255f);
                    pixels[row + x] = new Color32(noise, noise, noise, 255);
                }
            }
            return pixels;
        }

        // Called by camera to apply image effect
        //Vector4 UV_Transform = new Vector4(1, 0, 0, 1);
        float tween = 0f;
        int counter1 = 1;
        [Range(0.0f, 1.0f)]
        public float speed = 1f;

        private float wTimer = 0f;
        public float wSpeed = 1f;
        public float wFrequency = 12f;
        public float wAmplitude = 0.01f;


        // Update is called once per frame
		protected override void OnUpdate()
        {
            // Reset the timer after a while, some GPUs don't like big numbers
            if (wTimer > 1000f)
                wTimer -= 1000f;

            // Increment timer
            wTimer += wSpeed * Time.deltaTime;
        }

        // Called by camera to apply image effect
        protected override void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            tween += speed * Time.deltaTime;

            if (tween >= 1f)
            {
                counter = (counter + 1) % tex.Length;
                counter1 = (counter1 + 1) % tex.Length;
                tween = 0f;
            }

            // set params
            //Material.SetVector("_UV_Transform", UV_Transform);
            //Material.SetFloat("_Intensity", intensity);
            Material.SetTexture("_NoiseTex", tex[counter]);
            //Material.SetTexture("_MainTex", source);

            Material.SetFloat("_Intensity", intensity);

            
            Material.SetTexture("_NoiseTex1", tex[counter1]);
            Material.SetFloat("_Tween", tween);

            Material.SetVector("_WarpParams", new Vector3(wFrequency, wAmplitude, wTimer));

            // Blit
            Graphics.Blit(source, destination, Material, 0);
        }

				
		protected override string GetShaderName()
		{
			return "Hidden/VisSim/myNoise";
		}
    }
}

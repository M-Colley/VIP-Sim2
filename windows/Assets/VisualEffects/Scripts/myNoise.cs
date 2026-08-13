using System;
using System.Collections;
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
        private static Color32[][] pixelBuffers;
        private static bool texturesGenerated = false;
        private static int texWidth;
        private static int texHeight;
        private const int TextureCount = 10;
        private static Coroutine generationCoroutine;
        private static myNoise generationOwner;
        private int counter = 0;

        // Use this for initialization
        public new void OnEnable()
        {
            base.OnEnable();

            RequestTextureGeneration(Screen.width, Screen.height, TextureCount);
        }

        private void RequestTextureGeneration(int width_px, int height_px, int count)
        {
            if (width_px <= 0 || height_px <= 0)
            {
                return;
            }

            if (texturesGenerated && texWidth == width_px && texHeight == height_px)
            {
                return;
            }

            if (generationCoroutine != null)
            {
                if (generationOwner == this)
                {
                    return;
                }

                return;
            }

            generationOwner = this;
            generationCoroutine = StartCoroutine(GenerateTexturesCoroutine(width_px, height_px, count));
        }

        private IEnumerator GenerateTexturesCoroutine(int width_px, int height_px, int count)
        {
            texturesGenerated = false;

            bool resolutionChanged = tex == null || tex.Length != count || texWidth != width_px || texHeight != height_px;
            texWidth = width_px;
            texHeight = height_px;

            if (resolutionChanged)
            {
                ReleaseTextures();
                tex = new Texture2D[count];
            }

            EnsurePixelBuffers(count, width_px * height_px);
            EnsureTextures(count, width_px, height_px);

            float localFrequency = frequency;
            FastNoise.Interp localInterp = interp;
            FastNoise.NoiseType localNoiseType = noiseType;
            int localOctaves = octaves;
            float localLacunarity = lacunarity;
            float localGain = gain;
            FastNoise.FractalType localFractalType = fractalType;

            var tasks = new Task[count];
            var seeds = new int[count];
            for (int i = 0; i < count; i++)
            {
                seeds[i] = UnityEngine.Random.Range(0, 1000);
                int idx = i;
                tasks[i] = Task.Run(() => BuildPixels(pixelBuffers[idx], width_px, height_px, seeds[idx], localFrequency, localInterp, localNoiseType, localOctaves, localLacunarity, localGain, localFractalType));
            }

            var allTask = Task.WhenAll(tasks);
            while (!allTask.IsCompleted)
            {
                yield return null;
            }

            if (allTask.IsFaulted)
            {
                Debug.LogException(allTask.Exception);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    tex[i].SetPixels32(pixelBuffers[i]);
                    tex[i].Apply(false);
                }

                counter = 0;
                counter1 = tex.Length > 0 ? 1 % tex.Length : 0;
                texturesGenerated = true;
            }

            generationCoroutine = null;
            generationOwner = null;
        }

        private void OnDisable()
        {
            if (generationOwner == this)
            {
                if (generationCoroutine != null)
                {
                    StopCoroutine(generationCoroutine);
                }

                generationOwner = null;
                generationCoroutine = null;
            }
        }

        private void EnsurePixelBuffers(int count, int totalPixels)
        {
            if (pixelBuffers == null || pixelBuffers.Length != count)
            {
                pixelBuffers = new Color32[count][];
            }

            for (int i = 0; i < count; i++)
            {
                if (pixelBuffers[i] == null || pixelBuffers[i].Length != totalPixels)
                {
                    pixelBuffers[i] = new Color32[totalPixels];
                }
            }
        }

        private void EnsureTextures(int count, int width, int height)
        {
            if (tex == null || tex.Length != count)
            {
                tex = new Texture2D[count];
            }

            for (int i = 0; i < count; i++)
            {
                if (tex[i] == null)
                {
                    tex[i] = CreateTexture(width, height);
                }
                else if (tex[i].width != width || tex[i].height != height)
                {
                    DestroyTexture(tex[i]);
                    tex[i] = CreateTexture(width, height);
                }
            }
        }

        private Texture2D CreateTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            return texture;
        }

        private void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        private void ReleaseTextures()
        {
            if (tex == null)
            {
                return;
            }

            for (int i = 0; i < tex.Length; i++)
            {
                if (tex[i] != null)
                {
                    DestroyTexture(tex[i]);
                    tex[i] = null;
                }
            }

            texturesGenerated = false;
            pixelBuffers = null;
        }

        private void BuildPixels(Color32[] pixels, int width_px, int height_px, int seed, float localFrequency, FastNoise.Interp localInterp, FastNoise.NoiseType localNoiseType, int localOctaves, float localLacunarity, float localGain, FastNoise.FractalType localFractalType)
        {
            if (pixels == null || pixels.Length < width_px * height_px)
            {
                return;
            }

            FastNoise fNoise = new FastNoise();
            fNoise.SetFrequency(localFrequency);
            fNoise.SetInterp(localInterp);
            fNoise.SetNoiseType(localNoiseType);
            fNoise.SetFractalOctaves(localOctaves);
            fNoise.SetFractalLacunarity(localLacunarity);
            fNoise.SetFractalGain(localGain);
            fNoise.SetFractalType(localFractalType);
            fNoise.SetSeed(seed);

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
            if (generationCoroutine == null)
            {
                int screenWidth = Screen.width;
                int screenHeight = Screen.height;
                if (!texturesGenerated || texWidth != screenWidth || texHeight != screenHeight)
                {
                    RequestTextureGeneration(screenWidth, screenHeight, TextureCount);
                }
            }

            // Reset the timer after a while, some GPUs don't like big numbers
            if (wTimer > 1000f)
            {
                wTimer -= 1000f;
            }

            // Increment timer
            wTimer += wSpeed * Time.deltaTime;
        }

        // Called by camera to apply image effect
        protected override void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!texturesGenerated || tex == null || tex.Length == 0)
            {
                Graphics.Blit(source, destination);
                return;
            }

            int textureCount = tex.Length;

            tween += speed * Time.deltaTime;

            if (tween >= 1f)
            {
                counter = (counter + 1) % textureCount;
                counter1 = (counter1 + 1) % textureCount;
                tween = 0f;
            }

            counter %= textureCount;
            counter1 %= textureCount;

            // set params
            //Material.SetVector("_UV_Transform", UV_Transform);
            //Material.SetFloat("_Intensity", intensity);
            Texture2D primaryTexture = tex[counter];
            Texture2D secondaryTexture = tex[counter1];
            if (primaryTexture == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (secondaryTexture == null)
            {
                secondaryTexture = primaryTexture;
            }

            Material.SetTexture("_NoiseTex", primaryTexture);
            //Material.SetTexture("_MainTex", source);

            Material.SetFloat("_Intensity", intensity);

            Material.SetTexture("_NoiseTex1", secondaryTexture);
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

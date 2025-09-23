// Example C# script to generate a noise texture in Unity
using UnityEngine;

public class NoiseTextureGenerator : MonoBehaviour
{
    public int width = 256;
    public int height = 256;
    public float scale = 20.0f;

    void Start()
    {
        Texture2D noiseTex = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float xCoord = (float)x / width * scale;
                float yCoord = (float)y / height * scale;
                float sample = Mathf.PerlinNoise(xCoord, yCoord);
                pixels[x + y * width] = new Color(sample, sample, sample);
            }
        }

        noiseTex.SetPixels(pixels);
        noiseTex.Apply();
        GetComponent<Renderer>().material.SetTexture("_NoiseTex", noiseTex);
    }
}


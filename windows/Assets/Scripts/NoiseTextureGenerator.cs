// Example C# script to generate a noise texture in Unity
using UnityEngine;

public class NoiseTextureGenerator : MonoBehaviour
{
    public int width = 256;
    public int height = 256;
    public float scale = 20.0f;

    void Start()
    {
        // This component is attached to objects that do not always carry a
        // Renderer, and the unguarded GetComponent<Renderer>().material below
        // threw a NullReferenceException on every startup of the built player.
        var renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning($"[NoiseTextureGenerator] No Renderer on '{name}'; " +
                             "skipping noise texture generation.", this);
            enabled = false;
            return;
        }

        Texture2D noiseTex = new Texture2D(width, height);
        Color[] colors = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float xCoord = (float)x / width * scale;
                float yCoord = (float)y / height * scale;
                float sample = Mathf.PerlinNoise(xCoord, yCoord);
                colors[y * width + x] = new Color(sample, sample, sample);
            }
        }

        noiseTex.SetPixels(colors);
        noiseTex.Apply();
        renderer.material.SetTexture("_NoiseTex", noiseTex);
    }
}


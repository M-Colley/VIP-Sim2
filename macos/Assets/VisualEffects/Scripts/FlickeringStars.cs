using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class FlickeringStars : MonoBehaviour
{
    public Shader starShader;

    [Range(0, 15)]
    public int numCoordinates = 15;
    [Range(0, 0.5f)]
    public float radius = 0.25f;
    [Range(0, 0.005f)]
    public float starRadius = 0.0025f;
    [Range(0, 5f)]
    public float fadeInDuration, fadeOutDuration = 2f;

    private Material starMaterial;
    private Vector4[] coordinates;
    private float fade = 1.0f;
    private float[] fadeInStartTimes;
    private float[] fadeOutEndTimes;

    void Start()
    {
        if (starShader == null)
        {
            Debug.LogError("Shader not assigned!");
            return;
        }

        starMaterial = new Material(starShader);
        coordinates = new Vector4[numCoordinates];
        fadeInStartTimes = new float[numCoordinates];
        fadeOutEndTimes = new float[numCoordinates];
        GenerateRandomCoordinates();
        InvokeRepeating(nameof(GenerateRandomCoordinates), 5f, 5f);
    }

    void Update()
    {
        float currentTime = Time.time;

        // Update fade states for each coordinate
        for (int i = 0; i < numCoordinates; i++)
        {
            float timeSinceFadeInStart = currentTime - fadeInStartTimes[i];
            float timeSinceFadeOutEnd = currentTime - fadeOutEndTimes[i];

            // Determine fade factor based on current time
            float fade = 0.0f;
            if (timeSinceFadeInStart < fadeInDuration)
            {
                fade = Mathf.Clamp01(timeSinceFadeInStart / fadeInDuration);
            }
            else if (timeSinceFadeOutEnd < fadeOutDuration)
            {
                fade = Mathf.Clamp01(1.0f - timeSinceFadeOutEnd / fadeOutDuration);
            }

            coordinates[i].w = fade; // Store fade value in the 'w' component
        }
    }

    void OnDisable()
    {
        CancelInvoke(nameof(GenerateRandomCoordinates));
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (starMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        // Set shader properties
        starMaterial.SetFloat("_Radius", radius);
        starMaterial.SetFloat("_StarRadius", starRadius);
        starMaterial.SetFloat("_Fade", fade);

        // Set coordinates
        for (int i = 0; i < numCoordinates; i++)
        {
            starMaterial.SetVector("_Coordinate" + i, coordinates[i]);
        }

        // Apply shader and render
        Graphics.Blit(source, destination, starMaterial);
    }

    void GenerateRandomCoordinates()
    {
        Vector2 center = new Vector2(0.5f, 0.5f); // Center of the screen, normalized
        for (int i = 0; i < numCoordinates; i++)
        {
            Vector2 randomPoint = center + Random.insideUnitCircle * radius;
            coordinates[i] = new Vector4(randomPoint.x, randomPoint.y, starRadius, 0.0f); // Initialize with 0 visibility

            // Set random start times for fade-in and fade-out
            float randomDelay = Random.Range(0.0f, 5.0f);
            fadeInStartTimes[i] = Time.time + randomDelay;
            fadeOutEndTimes[i] = fadeInStartTimes[i] + fadeInDuration; // Fade-out starts after fade-in
        }
    }
}

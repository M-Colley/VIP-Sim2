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

    // The shader has fifteen fixed slots.
    private const int MaxCoordinates = 15;
    private static readonly int[] CoordinateIds = BuildCoordinateIds();
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int StarRadiusId = Shader.PropertyToID("_StarRadius");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    private Material starMaterial;
    private Vector4[] coordinates;
    private float fade = 1.0f;
    private float[] fadeInStartTimes;
    private float[] fadeOutEndTimes;

    private static int[] BuildCoordinateIds()
    {
        var ids = new int[MaxCoordinates];
        for (int i = 0; i < MaxCoordinates; i++) ids[i] = Shader.PropertyToID("_Coordinate" + i);
        return ids;
    }

    private bool EnsureSetup()
    {
        if (starShader == null)
        {
            Debug.LogError("Shader not assigned!");
            return false;
        }
        if (starMaterial == null) starMaterial = new Material(starShader);

        int n = Mathf.Clamp(numCoordinates, 0, MaxCoordinates);
        if (coordinates == null || coordinates.Length != n)
        {
            coordinates = new Vector4[n];
            fadeInStartTimes = new float[n];
            fadeOutEndTimes = new float[n];
        }
        return true;
    }

    // OnEnable, not Start. OnDisable cancels the repeating regeneration, and Start never runs a
    // second time -- so after one switch off and on, nothing scheduled new stars, every fade
    // ran out within seconds, and the effect showed nothing while reporting itself enabled.
    void OnEnable()
    {
        if (!EnsureSetup()) return;
        GenerateRandomCoordinates();
        InvokeRepeating(nameof(GenerateRandomCoordinates), 5f, 5f);
    }

    void OnDestroy()
    {
        if (starMaterial != null) DestroyImmediate(starMaterial);
    }

    void Update()
    {
        if (coordinates == null) return;
        float currentTime = Time.time;

        // Update fade states for each coordinate
        for (int i = 0; i < coordinates.Length; i++)
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

            coordinates[i].z = starRadius; // live, so the slider takes effect now, not at the next regeneration
            coordinates[i].w = fade; // Store fade value in the 'w' component
        }
    }

    void OnDisable()
    {
        CancelInvoke(nameof(GenerateRandomCoordinates));
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (starMaterial == null || coordinates == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        // Set shader properties
        starMaterial.SetFloat(RadiusId, radius);
        starMaterial.SetFloat(StarRadiusId, starRadius);
        starMaterial.SetFloat(FadeId, fade);

        // Set coordinates. Cached property ids: building "_Coordinate" + i allocated fifteen
        // strings a frame. Unused slots are zeroed, or they would keep stars from before.
        for (int i = 0; i < MaxCoordinates; i++)
        {
            starMaterial.SetVector(CoordinateIds[i], i < coordinates.Length ? coordinates[i] : Vector4.zero);
        }

        // Apply shader and render
        Graphics.Blit(source, destination, starMaterial);
    }

    void GenerateRandomCoordinates()
    {
        Vector2 center = new Vector2(0.5f, 0.5f); // Center of the screen, normalized
        if (coordinates == null) return;
        for (int i = 0; i < coordinates.Length; i++)
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

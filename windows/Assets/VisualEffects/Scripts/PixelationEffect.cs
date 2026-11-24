using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class PixelationEffect : MonoBehaviour
{
    public Shader pixelationShader;

    [Range(10, 1000f)]
    public float pixelRadius = 10f;

    private Material pixelationMaterial;
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private const float MinCells = 8f;

    void OnDestroy()
    {
        if (pixelationMaterial != null) DestroyImmediate(pixelationMaterial);
    }

    void Start()
    {
        if (pixelationShader == null)
        {
            Debug.LogError("Vortex shader not assigned!");
            enabled = false;
            return;
        }

        pixelationMaterial = new Material(pixelationShader);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (pixelationMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        // At least MinCells across. At the slider's maximum (1000) this was 0, and the shader
        // divides by it; floored at 1, one sampled pixel filled the whole screen, desktop included.
        pixelationMaterial.SetFloat(PixelSizeId, Mathf.Max(MinCells, 1000f - pixelRadius));


        Graphics.Blit(source, destination, pixelationMaterial);
    }
}

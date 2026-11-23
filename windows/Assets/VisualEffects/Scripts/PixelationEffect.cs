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

        // Floored at 1: at the slider's maximum (1000) this was 0, and the shader divides by it.
        pixelationMaterial.SetFloat(PixelSizeId, Mathf.Max(1f, 1000f - pixelRadius));


        Graphics.Blit(source, destination, pixelationMaterial);
    }
}

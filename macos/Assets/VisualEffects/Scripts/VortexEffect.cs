using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class VortexEffect : MonoBehaviour
{
    public Shader vortexShader;

    [Range(0, 0.5f)]
    public float vortexRadius = 0.27f;
    [Range(0, 2)]
    public float suctionStrength = 1f;
    [Range(0, 0.25f)]
    public float innerCircleRadius = 0.01f;
    [Range(0, 0.1f)]
    public float noiseAmount = 0.05f;

    private Material vortexMaterial;

    void Start()
    {
        if (vortexShader == null)
        {
            Debug.LogError("Vortex shader not assigned!");
            enabled = false;
            return;
        }

        vortexMaterial = new Material(vortexShader);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (vortexMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        //Vector2 mousePos = Input.mousePosition;
        //mousePos.x /= Screen.width;
        //mousePos.y /= Screen.height;

        Vector2 xy_norm = GazeTracker.GetInstance.xy_norm;
        vortexMaterial.SetFloat("_MouseX", xy_norm.x);
        vortexMaterial.SetFloat("_MouseY", xy_norm.y);
        vortexMaterial.SetFloat("_VortexRadius", vortexRadius);
        vortexMaterial.SetFloat("_SuctionStrength", suctionStrength);
        vortexMaterial.SetFloat("_InnerCircleRadius", innerCircleRadius);
        vortexMaterial.SetFloat("_NoiseScale" , 10f);
        vortexMaterial.SetFloat("_NoiseAmount", noiseAmount);
        vortexMaterial.SetInt("_ScreenHeight", Screen.height);
        vortexMaterial.SetInt("_ScreenWidth", Screen.width);


        Graphics.Blit(source, destination, vortexMaterial);
    }
}

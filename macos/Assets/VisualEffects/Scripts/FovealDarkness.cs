using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class FovealDarkness : MonoBehaviour
{
    public Shader darknessShader;

    [Range(0, 0.25f)]
    public float innerCircleRadius = 0.01f;
    [Range(0, 0.1f)]
    public float fadeWidth = 0.05f;
    [Range(0, 1f)]
    public float opacity = 0.5f;

    private Material darknessMaterial;

    void Start()
    {
        if (darknessShader == null)
        {
            Debug.LogError("Vortex shader not assigned!");
            enabled = false;
            return;
        }

        darknessMaterial = new Material(darknessShader);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (darknessMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        //Vector2 mousePos = Input.mousePosition;
        //mousePos.x /= Screen.width;
        //mousePos.y /= Screen.height;

        Vector2 xy_norm = GazeTracker.GetInstance.xy_norm;
        darknessMaterial.SetFloat("_MouseX", xy_norm.x);
        darknessMaterial.SetFloat("_MouseY", xy_norm.y);
        darknessMaterial.SetFloat("_InnerCircleRadius", innerCircleRadius);
        darknessMaterial.SetFloat("_Opacity", opacity);
        darknessMaterial.SetFloat("_FadeWidth", fadeWidth);
        darknessMaterial.SetInt("_ScreenHeight", Screen.height);
        darknessMaterial.SetInt("_ScreenWidth", Screen.width);


        Graphics.Blit(source, destination, darknessMaterial);
    }
}

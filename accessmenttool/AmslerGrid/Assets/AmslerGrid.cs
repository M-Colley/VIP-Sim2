using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AmslerGrid : MonoBehaviour
{
    [SerializeField]
    private float distance = 40f; // Distance to the screen in cm

    [SerializeField]
    private string aspectRatio = "16:9"; // Aspect ratio of the screen as a string

    [SerializeField]
    private float diameter = 10f; // Diameter of the screen diagonal in cm

    [SerializeField]
    private Vector2 screenResolution = new Vector2(1920, 1080); // Screen resolution in pixels

    [SerializeField]
    private Material lineMaterial; // Material for the LineRenderer

    [SerializeField]
    public Image circle;

    private float cmPerPixel;
    private float screenWidth;
    private float screenHeight;

    private List<GameObject> drawnObjects = new List<GameObject>();
    private bool activeState = true;

    void Start()
    {
        // Calculate the aspect ratio
        float aspectRatioValue = ParseAspectRatio(aspectRatio);

        // Calculate screen width and height based on the diagonal and aspect ratio
        CalculateDimensions(aspectRatioValue, diameter);

        // Calculate cm per pixel based on screen width
        cmPerPixel = screenWidth / screenResolution.x;

        // Calculate the distance per line in the grid
        float distancePerLineCm = distance * Mathf.Tan(1 * Mathf.Deg2Rad); // Distance per line in cm
        float distancePerLinePx = distancePerLineCm / cmPerPixel; // Distance per line in pixels

        // Draw the grid
        DrawGrid(distancePerLinePx);

    }

    private void Update()
    {
        CheckVisibility();
    }

    float ParseAspectRatio(string aspectRatio)
    {
        // Split the string by ':' and convert the parts to float
        string[] parts = aspectRatio.Split(':');
        if (parts.Length != 2)
        {
            Debug.LogError("Invalid aspect ratio format. Use the format 'width:height'.");
            return 1.0f; // Default value if the format is incorrect
        }

        if (float.TryParse(parts[0], out float width) && float.TryParse(parts[1], out float height))
        {
            return width / height;
        }
        else
        {
            Debug.LogError("Invalid values in the aspect ratio. Ensure both parts are numbers.");
            return 1.0f; // Default value if conversion fails
        }
    }

    void CalculateDimensions(float aspectRatioValue, float diagonal)
    {
        // Calculate the aspect ratio width and height
        float aspectRatioHeight = 1.0f;
        float aspectRatioWidth = aspectRatioValue;

        // Calculate the diagonal of the aspect ratio
        float aspectRatioDiagonal = Mathf.Sqrt(Mathf.Pow(aspectRatioWidth, 2) + Mathf.Pow(aspectRatioHeight, 2));

        // Calculate the factor to scale the aspect ratio to the diagonal
        float factor = diagonal / aspectRatioDiagonal;

        // Calculate the screen width and height
        screenWidth = aspectRatioWidth * factor;
        screenHeight = aspectRatioHeight * factor;

        Debug.Log($"Screen Width: {screenWidth} cm, Screen Height: {screenHeight} cm");
    }

    void DrawGrid(float distancePerLinePx)
    {
        // Calculate the number of lines based on the screen resolution
        int numberOfVerticalLines = Mathf.CeilToInt(screenResolution.x / distancePerLinePx);
        int numberOfHorizontalLines = Mathf.CeilToInt(screenResolution.y / distancePerLinePx);

        // Expand the grid by half the size on each side
        numberOfVerticalLines = Mathf.CeilToInt(1.5f * numberOfVerticalLines);
        numberOfHorizontalLines = Mathf.CeilToInt(1.5f * numberOfHorizontalLines);

        // Convert screen resolution to world units
        Camera cam = Camera.main;
        float screenWidthWorld = cam.orthographicSize * cam.aspect * 2;
        float screenHeightWorld = cam.orthographicSize * 2;

        // Calculate pixel to world unit ratio
        float pixelsToWorldX = screenWidthWorld / screenResolution.x;
        float pixelsToWorldY = screenHeightWorld / screenResolution.y;

        // Total line count (each line has two vertices)
        int totalLines = numberOfVerticalLines + numberOfHorizontalLines;
        Vector3[] vertices = new Vector3[totalLines * 2];
        int[] indices = new int[totalLines * 2];
        int vertIndex = 0;

        // Store vertical line vertices
        for (int i = -numberOfVerticalLines / 2; i <= numberOfVerticalLines / 2; i++)
        {
            float xPos = i * distancePerLinePx * pixelsToWorldX;
            vertices[vertIndex] = new Vector3(xPos, -screenHeightWorld, 0);
            indices[vertIndex] = vertIndex;
            vertIndex++;
            vertices[vertIndex] = new Vector3(xPos, screenHeightWorld, 0);
            indices[vertIndex] = vertIndex;
            vertIndex++;
        }

        // Store horizontal line vertices
        for (int i = -numberOfHorizontalLines / 2; i <= numberOfHorizontalLines / 2; i++)
        {
            float yPos = i * distancePerLinePx * pixelsToWorldY;
            vertices[vertIndex] = new Vector3(-screenWidthWorld, yPos, 0);
            indices[vertIndex] = vertIndex;
            vertIndex++;
            vertices[vertIndex] = new Vector3(screenWidthWorld, yPos, 0);
            indices[vertIndex] = vertIndex;
            vertIndex++;
        }

        // Create mesh for the grid
        Mesh gridMesh = new Mesh();
        gridMesh.vertices = vertices;
        gridMesh.SetIndices(indices, MeshTopology.Lines, 0);

        GameObject gridObj = new GameObject("Grid");
        gridObj.transform.parent = transform;
        MeshFilter mf = gridObj.AddComponent<MeshFilter>();
        MeshRenderer mr = gridObj.AddComponent<MeshRenderer>();
        mf.mesh = gridMesh;
        mr.material = lineMaterial;

        drawnObjects.Add(gridObj);
    }

    void CheckVisibility()
    {
        if (Input.GetKeyDown(KeyCode.V)) //USD or European layout
        {
            ToggleLineVisibility();
        }
    }

    public void ToggleLineVisibility()
    {
        activeState = !activeState;
        foreach (GameObject lineObj in drawnObjects)
        {
            lineObj.SetActive(activeState);
        }
    }
}

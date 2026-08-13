using UnityEngine;
using UnityEngine.UI;
using TMPro; // Correct namespace for TextMeshPro
using mcDesktopCapture;

/// <summary>
/// macOS window picker and capture driver, backed by ScreenCaptureKit
/// (DesktopCapture2).
///
/// Fixes applied while auditing the macOS path:
///
///  1. Update() latched `setTexture` after the first frame and never queried the
///     plugin again. ScreenCaptureKit reallocates its backing surface when the
///     captured window is resized, so the overlay froze on whatever frame it
///     happened to grab first. The texture instance is now compared each frame
///     and reassigned when it changes.
///
///  2. StopCapture() overwrote planeRenderer.material with transparentMaterial,
///     discarding the capture material permanently. Starting a second capture
///     then wrote mainTexture onto the transparent material, so capture worked
///     exactly once per launch. Both materials are now cached and swapped.
///
///  3. The window list appended a synthetic "Stop" entry and then skipped it in
///     the very same loop that builds the buttons, so the Stop control never
///     existed. It is now built explicitly, first in the list.
///
///  4. OnDisable() tore down the native capture session but left isInit true, so
///     re-enabling the component ran Update() against a destroyed session.
///
///  5. lastWindowID was static, so two instances (or a scene reload) shared
///     toggle state. It is now per-instance, and the toggle logic is explicit.
/// </summary>
public class MacCapture : MonoBehaviour
{
    [SerializeField]
    private ScrollRect scrollView; // The ScrollView that holds the window list.
    [SerializeField]
    private GameObject buttonPrefab; // The prefab for a single Button (not Toggle).

    private WindowProperty[] list = { };

    public bool isRunning = false;
    private bool isInit = false;

    [SerializeField]
    public Material transparentMaterial;

    public Renderer planeRenderer;

    public FirestoreRESTManager logger;

    // Material the plane was authored with; restored whenever capture starts.
    private Material captureMaterial;
    private Texture currentTexture;
    private int currentWindowID = -1;

    private void Awake()
    {
        if (planeRenderer != null)
            captureMaterial = planeRenderer.sharedMaterial;
    }

    public void Init()
    {
        Application.targetFrameRate = 60;

        DesktopCapture2.Init();

        if (!DesktopCapture2.HasScreenRecordingPermission())
        {
            Debug.LogError(
                "VIP-Sim has no Screen Recording permission. Enable it under " +
                "System Settings > Privacy & Security > Screen & System Audio Recording, " +
                "then QUIT AND REOPEN VIP-Sim -- macOS only applies a new grant to a fresh process.");
        }

        list = DesktopCapture2.WindowList ?? new WindowProperty[0];

        // Clear previous buttons before adding new ones
        foreach (Transform child in scrollView.content)
        {
            Destroy(child.gameObject);
        }

        int i = 0;

        // Stop control first. Previously a synthetic "Stop" WindowProperty was
        // appended to the list and then filtered out by the same loop, so it
        // never appeared.
        CreateButton("Stop capture", i++, () =>
        {
            StopCapture();
            if (logger != null) logger.OnProgramClick("Stop");
        });

        foreach (var window in list)
        {
            if (window == null || window.owningApplication == null) continue;

            var appName = window.owningApplication.applicationName;
            if (!window.isOnScreen
                || string.IsNullOrEmpty(appName)
                || appName.ToLower().Replace("_", "").Contains("vipsim"))
            {
                continue;
            }

            var captured = window; // capture per iteration for the closure
            CreateButton(appName, i++, () =>
            {
                OnButtonClicked(captured);
                if (logger != null) logger.OnProgramClick(captured.owningApplication.applicationName);
            });
        }

        isInit = true;
        this.enabled = true;
    }

    private void CreateButton(string label, int index, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObj = Instantiate(buttonPrefab, scrollView.content);

        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        if (buttonRect == null)
        {
            Debug.LogError("RectTransform not found on buttonPrefab!");
            return;
        }

        buttonRect.anchoredPosition = new Vector2(
            buttonRect.anchoredPosition.x, -(index * buttonRect.sizeDelta.y));

        Button button = buttonObj.GetComponentInChildren<Button>();
        if (button == null)
        {
            Debug.LogError("Button component not found in prefab!");
            return;
        }

        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>();
        if (tmpText == null)
        {
            Debug.LogError("TMP_Text component not found inside the Button!");
            return;
        }

        tmpText.text = label;
        button.onClick.AddListener(onClick);
    }

    // Called when a window button is clicked. Clicking the active window stops it.
    private void OnButtonClicked(WindowProperty window)
    {
        bool sameWindow = isRunning && window.windowID == currentWindowID;

        if (isRunning) StopCapture();

        if (sameWindow) return; // clicking the running window is a toggle-off

        StartCapture(window);
    }

    // Start capture for a selected window
    private void StartCapture(WindowProperty window)
    {
        if (planeRenderer != null && captureMaterial != null)
            planeRenderer.material = captureMaterial;

        currentTexture = null;
        DesktopCapture2.StartCaptureWithWindowID(window.windowID, window.frame.width, window.frame.height, true);
        currentWindowID = window.windowID;
        isRunning = true;

        if (planeRenderer != null) planeRenderer.enabled = true;
        Debug.Log($"Started capture for window {window.windowID} ({window.owningApplication.applicationName})");
    }

    // Update is called once per frame
    void Update()
    {
        if (!isInit || !isRunning)
        {
            if (planeRenderer != null) planeRenderer.enabled = false;
            return;
        }

        // Re-query every frame. The plugin hands back a different Texture2D when
        // the captured window is resized; latching the first one froze the overlay.
        var texture = DesktopCapture2.GetTexture2D();
        if (texture == null || ReferenceEquals(texture, currentTexture)) return;

        currentTexture = texture;
        if (planeRenderer != null) planeRenderer.material.mainTexture = texture;
    }

    void OnDisable()
    {
        StopCapture();
        DesktopCapture2.Destroy();
        // Must clear, or a later OnEnable would drive Update() against a
        // destroyed native session.
        isInit = false;
    }

    // Stop capture manually
    public void StopCapture()
    {
        DesktopCapture2.StopCapture();
        currentTexture = null;
        currentWindowID = -1;
        isRunning = false;

        if (planeRenderer != null)
        {
            if (transparentMaterial != null) planeRenderer.material = transparentMaterial;
            planeRenderer.enabled = false;
        }
    }
}

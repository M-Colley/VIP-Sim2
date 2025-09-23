using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using static VisSim.myBloom;

public class DropdownManager : MonoBehaviour
{
    public TMP_Dropdown dropdown; // Reference to the TextMeshPro Dropdown
    public Transform uiContainer; // Container where UI elements will be instantiated
    public List<MonoBehaviour> scripts; // List of scripts to be toggled

    [SerializeField] private float fontSize = 14f;
    [SerializeField] private float labelWidth = 200f;
    [SerializeField] private float itemWidth = 200f;
    [SerializeField] private Color labelColor = Color.black;
    [SerializeField] private Color backgroundColor = Color.white;
    [SerializeField] private Color handleColor = Color.white;
    [SerializeField] private Color fillColor = Color.green;
    [SerializeField] private Color toggleOnColor = Color.green;
    [SerializeField] private Color toggleOffColor = Color.red;
    [SerializeField] private Sprite handleSprite;
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite fillSprite;
    [SerializeField] private Sprite checkmarkSprite;

    [SerializeField] private FirestoreRESTManager logger;

    private MonoBehaviour currentScript;
    private List<GameObject> uiElements = new List<GameObject>();

    private List<String> fixedInputs = new List<String>();

    void Start()
    {
        // Add listener to handle dropdown value change
        dropdown.onValueChanged.AddListener(delegate { DropdownValueChanged(); });

        fixedInputs.Add("LinkEyes");
        fixedInputs.Add("Foveat_d");
        fixedInputs.Add("Rise_d");
        fixedInputs.Add("Rise_exp");
        fixedInputs.Add("Amp_deg");
        fixedInputs.Add("kernalSigma");
        fixedInputs.Add("viewingAngle_deg");
        fixedInputs.Add("AutomaticTimer");
        fixedInputs.Add("useBrightness");
        fixedInputs.Add("useContrast");
        fixedInputs.Add("inpainterTexture");
        fixedInputs.Add("useFieldTexture");
        fixedInputs.Add("useComputeShader");
        fixedInputs.Add("baselineErr_deg");
        fixedInputs.Add("direction_deg");
        fixedInputs.Add("artificialRotation");
        fixedInputs.Add("useNullingField");
        fixedInputs.Add("LumContribution");
        fixedInputs.Add("blurType");
        fixedInputs.Add("gazeContingent");

        List<string> options = new List<string>();
        /*
        foreach (var script in scripts)
        {
            string scriptName = script.GetType().Name;
            scriptName = scriptName.Replace("My", "").Replace("my", "").Replace("my ", "").Replace("2","");

            options.Add(scriptName);
            script.enabled = false; // Disable all scripts initially
        }*/

        // Dictionary to map original script names to new names
        var scriptNameMapping = new Dictionary<string, string>
        {
            { "FieldLoss", "Vision loss, central" },
            { "Blur", "Hyperopia" },
            { "Recolour", "Color vision deficiency" },
            { "BrightnessContrastGamma", "Contrast Sensitivity" },
            { "DistortionMap", "Metamorphopsia" },
            { "Nystagmus", "Nystagmus" },
            { "Floaters", "Retinopathy" },
            { "Teichopsia", "Teichopsia" },
            { "Wiggle", "Metamorphopsia2" },
            { "Bloom", "Glare Vision/photophobia" },
            { "FieldLossInverted", "Vision loss, peripheral" },
            { "Cataract", "Cataract" },
            { "Inpainter", "In-Filling" },
        };

        foreach (var script in scripts)
        {
            string scriptName = script.GetType().Name;
            scriptName = scriptName.Replace("My", "").Replace("my", "").Replace("my ", "").Replace("2", "");

            if (scriptNameMapping.TryGetValue(scriptName, out var newScriptName))
            {
                options.Add(newScriptName);
            }
            else
            {
                // If the script name is not found in the dictionary, add the original name
                options.Add(scriptName);
            }

            script.enabled = false; // Disable all scripts initially
        }


        dropdown.AddOptions(options);

        // Enable the initially selected script and create UI for its public variables
        //DropdownValueChanged();
    }

    public void DropdownValueChanged()
    {
        // Disable the current script if it exists
        if (currentScript != null)
        {
            currentScript.enabled = false;
        }

        // Get the selected script based on dropdown value
        currentScript = scripts[dropdown.value];
        currentScript.enabled = true;



        foreach (var element in uiElements)
        {
            // Check if the element's name is "DropdownDummy"
            if (element.name == "DropdownDummy")
            {
                // Disable the dropdown by setting it inactive
                element.SetActive(false);
            }
            else
            {
                // Destroy other UI elements
                Destroy(element);
            }
        }

        // Clear only non-dropdown elements from the list
        uiElements.RemoveAll(element => element.name != "DropdownDummy");


        // Create UI elements for public variables of the selected script
        CreateUIElementsForScript(currentScript);
    }

    public void ToggleVisionImpairment(string name)
    {
        MonoBehaviour script = GetScriptByName(name);

        logger.OnImpairmentClick(name, -1);

        script.enabled = !script.enabled;
        currentScript = script;
    }

    public void LoadUIElements(string name)
    {

        MonoBehaviour script = GetScriptByName(name);

        if (!script.isActiveAndEnabled)
        {
            return;
        }

        foreach (var element in uiElements)
        {
            // Check if the element's name is "DropdownDummy"
            if (element.name == "DropdownDummy")
            {
                // Disable the dropdown by setting it inactive
                element.SetActive(false);
            }
            else
            {
                // Destroy other UI elements
                Destroy(element);
            }
        }

        // Clear only non-dropdown elements from the list
        uiElements.RemoveAll(element => element.name != "DropdownDummy");


        // Create UI elements for public variables of the selected script
        CreateUIElementsForScript(script);
    }

    // Method to find a script by its name
    public MonoBehaviour GetScriptByName(string scriptName)
    {
        // Using LINQ to find the script with the matching name
        return scripts.FirstOrDefault(script => script.GetType().Name == (scriptName));
    }

    void CreateUIElementsForScript(MonoBehaviour script)
    {
        Type scriptType = script.GetType();
        FieldInfo[] fields = scriptType.GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            // Skip fields that are not supported
            if (field.FieldType != typeof(float) &&
                field.FieldType != typeof(bool) &&
                !field.FieldType.IsEnum
                || fixedInputs.Contains(field.Name))
            {
                continue;
            }

            // Create a label for the field
            GameObject labelObject = new GameObject(field.Name + "Label");
            labelObject.transform.SetParent(uiContainer, false);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
            label.text = renameToReadableParameter(field.Name);
            label.fontSize = fontSize;
            label.color = labelColor;
            label.rectTransform.sizeDelta = new Vector2(labelWidth, 30); // Adjust size as needed
            uiElements.Add(labelObject);

            // Create appropriate UI element based on the field type
            if (field.FieldType == typeof(float))
            {
                CreateSlider(field, script);
            }
            else if (field.FieldType == typeof(bool))
            {
                CreateToggle(field, script);
            }
            else if (field.FieldType.IsEnum)
            {
                CreateDropdown(field, script, label.gameObject.transform.GetSiblingIndex());
            }
        }
    }

    private static readonly Dictionary<string, string> Mappings = new Dictionary<string, string>()
    {
        { "overlayScale", "Scale (-/+)" },
        { "maxCPD", "Cycles per degree (-/+)" },
        { "anomType", "Type" },
        { "Brightness", "Brightness (-/+)" },
        { "Contrast", "Contrast (-/+)" },
        { "Gamma", "Gamma (-/+)" },
        { "severityIndex", "Severity (-/+)" },
        { "foveat_d", "Frequency (-/+)" },
        { "rise_d", "Speed (-/+)" },
        { "rise_exp", "Fade (-/+)" },
        { "amp_deg", "Range (-/+)" },
        { "floaterSize", "Floater Size (-/+)" },
        { "floaterDensity", "Floater Density (-/+)" },
        { "circleRadius", "Radius (-/+)" },
        { "Speed", "Speed (-/+)" },
        { "displacementAmount", "Severity (-/+)" },
        { "vortexRadius", "Radius (-/+)" },
        { "suctionStrength", "Strength (-/+)" },
        { "innerCircleRadius", "Radius (-/+)" },
        { "noiseAmount", "Noise (-/+)" },
        { "fadeWidth", "Fade (-/+)" },
        { "opacity", "Opacity (-/+)" },
        { "radius", "Spawn Radius (-/+)" },
        { "starRadius", "Star Size (-/+)" },
        { "fadeInDuration", "Fade-in Duration (-/+)" },
        { "fadeOutDuration", "Fade-out Duration (-/+)" },
        { "pixelRadius", "Strength (-/+)" },
        { "intensity", "Intensity (-/+)" },
        { "blurSize", "Size (-/+)" },
        { "Strength", "Strength (-/+)" },
        { "Frequency", "Frequency (-/+)" },
        { "Amplitude", "Amplitude (-/+)" },
        { "threshold", "Threshold (-/+)" },
        { "gazeContingent", "Follow Gaze" },
        { "useFrosting", "Frosting" }
    };
    private string renameToReadableParameter(string name)
    {
        if (Mappings.TryGetValue(name, out string rightValue))
        {
            return rightValue;
        }
        else
        {
            return name;
        }
    }

    void CreateSlider(FieldInfo field, MonoBehaviour script)
    {
        // Check if the field has a RangeAttribute
        RangeAttribute rangeAttribute = field.GetCustomAttribute<RangeAttribute>();
        float minValue = 0;
        float maxValue = 100;

        if (rangeAttribute != null)
        {
            minValue = rangeAttribute.min;
            maxValue = rangeAttribute.max;
        }

        GameObject sliderObject = new GameObject(field.Name + "Slider");
        sliderObject.transform.SetParent(uiContainer, false);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = Convert.ToSingle(field.GetValue(script));
        slider.onValueChanged.AddListener(delegate (float value) { OnSliderValueChanged(field, script, value); });

        // Add background image
        GameObject background = new GameObject("Background");
        background.transform.SetParent(sliderObject.transform, false);
        Image bgImage = background.AddComponent<Image>();
        bgImage.color = backgroundColor; // Set background color
        bgImage.sprite = backgroundSprite;
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.25f);
        bgRect.anchorMax = new Vector2(1, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Add fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1, 0.75f);
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        // Add fill image
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = fillColor; // Set fill color
        fillImage.sprite = fillSprite;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        slider.fillRect = fillRect;

        // Add handle slide area
        GameObject handleSlideArea = new GameObject("Handle Slide Area");
        handleSlideArea.transform.SetParent(sliderObject.transform, false);
        RectTransform handleSlideAreaRect = handleSlideArea.AddComponent<RectTransform>();
        handleSlideAreaRect.anchorMin = new Vector2(0, 0);
        handleSlideAreaRect.anchorMax = new Vector2(1, 1);
        handleSlideAreaRect.offsetMin = Vector2.zero;
        handleSlideAreaRect.offsetMax = Vector2.zero;

        // Add handle image
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleSlideArea.transform, false);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = handleColor; // Set handle color
        // TODO: Add knob texture
        handleImage.sprite = handleSprite;

        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(30, 0);
        slider.targetGraphic = handleImage;
        slider.handleRect = handleRect;

        // Set the size of the slider object
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(itemWidth, 30);

        uiElements.Add(sliderObject);
    }

    void CreateToggle(FieldInfo field, MonoBehaviour script)
    {
        // Create the main toggle object
        GameObject toggleObject = new GameObject(field.Name + "Toggle");
        toggleObject.transform.SetParent(uiContainer, false);

        // Configure the RectTransform
        RectTransform toggleRect = toggleObject.AddComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(30, 30); // Adjust size as needed

        // Add background image to toggle
        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(toggleObject.transform, false);
        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = backgroundColor;

        // Configure the RectTransform for the background
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0, 0);
        backgroundRect.anchorMax = new Vector2(1, 1);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        // Create the checkmark object
        GameObject checkmarkObject = new GameObject("Checkmark");
        checkmarkObject.transform.SetParent(backgroundObject.transform, false);
        Image checkmarkImage = checkmarkObject.AddComponent<Image>();
        // Set checkmark sprite and color
        checkmarkImage.sprite = checkmarkSprite; // Assuming you have a checkmark sprite
        

        // Configure the RectTransform for the checkmark
        RectTransform checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.1f, 0.1f);
        checkmarkRect.anchorMax = new Vector2(0.9f, 0.9f);
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;

        // Add the toggle component
        Toggle toggle = toggleObject.AddComponent<Toggle>();
        toggle.graphic = checkmarkImage;

        // Set toggle value and add listener for changes
        toggle.isOn = (bool)field.GetValue(script);
        toggle.onValueChanged.AddListener((value) =>
        {
            OnToggleValueChanged(field, script, value);
            checkmarkImage.color = toggleOnColor;
        });


        // Add the toggle object to the list of UI elements
        uiElements.Add(toggleObject);
    }


    void CreateDropdown(FieldInfo field, MonoBehaviour script, int siblingsIndex)
    {
        // Find the existing dropdown in the container
        TMP_Dropdown tmpDropdown = uiContainer.GetComponentInChildren<TMP_Dropdown>(true);

        if (tmpDropdown == null)
        {
            Debug.LogError("No TMP_Dropdown found in the UI container.");
            return;
        }

        // Activate the dropdown if it is not active
        if (!tmpDropdown.gameObject.activeSelf)
        {
            tmpDropdown.gameObject.SetActive(true);
        }

        // Clear existing options
        tmpDropdown.ClearOptions();

        // Add new options based on the enum values
        foreach (var value in Enum.GetValues(field.FieldType))
        {
            tmpDropdown.options.Add(new TMP_Dropdown.OptionData(value.ToString()));
        }

        // Set the current value
        tmpDropdown.value = Array.IndexOf(Enum.GetValues(field.FieldType), field.GetValue(script));

        // Add listener to handle value changes
        tmpDropdown.onValueChanged.RemoveAllListeners();
        tmpDropdown.onValueChanged.AddListener(delegate (int value)
        {
            OnDropdownValueChanged(field, script, value);
        });

        // Adjust dropdown UI properties
        tmpDropdown.captionText.fontSize = fontSize;
        tmpDropdown.captionText.color = Color.gray;
        tmpDropdown.itemText.fontSize = fontSize;
        tmpDropdown.itemText.color = Color.gray;
        tmpDropdown.GetComponent<RectTransform>().sizeDelta = new Vector2(itemWidth, 30);

        tmpDropdown.transform.SetSiblingIndex(siblingsIndex + 1);

        uiElements.Add(tmpDropdown.gameObject);
    }

    void OnSliderValueChanged(FieldInfo field, MonoBehaviour script, float value)
    {
        logger.OnImpairmentClick(field.ToString(), value);
        field.SetValue(script, value);
    }

    void OnToggleValueChanged(FieldInfo field, MonoBehaviour script, bool value)
    {
        logger.OnImpairmentClick(script.name, value ? 1 : 0);
        field.SetValue(script, value);
    }

    void OnDropdownValueChanged(FieldInfo field, MonoBehaviour script, int value)
    {
        object enumValue = Enum.GetValues(field.FieldType).GetValue(value);

        logger.OnImpairmentClick(script.name, Array.IndexOf(Enum.GetValues(field.FieldType), value));
        field.SetValue(script, enumValue);
    }
}

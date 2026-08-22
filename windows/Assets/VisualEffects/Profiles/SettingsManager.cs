using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json; // Für JSON (installiere NewtonSoft.Json aus dem Unity Asset Store oder per NuGet)
using Newtonsoft.Json.Linq;
using System.Reflection;
using MathNet.Numerics.Interpolation;
using System.Threading.Tasks;
using VisSim;
using static VisSim.myRecolour;
using static VisSim.myFloaters;
using SimpleFileBrowser; // Für Reflection, um alle Public Variablen zu aktualisieren

public class SettingsManager : MonoBehaviour
{
    // Referenzen zu den Buttons
    public Button loadButton;
    public Button saveButton;

    // Referenz zu den aktuellen Einstellungen
    public AppSettings appSettings = new AppSettings();

    [SerializeField]
    public myFieldLoss myFieldLoss;

    [SerializeField]
    public myBlur myBlur;

    [SerializeField]
    public myRecolour myRecolour;

    [SerializeField]
    public myBrightnessContrastGamma myBrightnessContrastGamma;

    [SerializeField]
    public myDistortionMap myDistortionMap;

    [SerializeField]
    public myNystagmus myNystagmus;

    [SerializeField]
    public myFloaters myFloaters;

    [SerializeField]
    public myTeichopsia myTeichopsia;

    [SerializeField]
    public myWiggle myWiggle;

    [SerializeField]
    public myBloom myBloom;

    [SerializeField]
    public myFieldLossInverted myFieldLossInverted;

    [SerializeField]
    public myCataract myCataract;

    [SerializeField]
    public myInpainter2 myInpainter2;

    [SerializeField]
    public DoubleVisionEffect myDoubleVision;

    [SerializeField]
    public VortexEffect myVortexEffect;

    [SerializeField]
    public FovealDarkness myFovealDarkness;

    [SerializeField]
    public FlickeringStars myFlickeringStars;

    [SerializeField]
    public PixelationEffect myPixelationEffect;

    private void Start()
    {
        // Event Listener für die Buttons hinzufügen
        loadButton.onClick.AddListener(OpenFileBrowser);
        saveButton.onClick.AddListener(SaveSettings);
        InvokeRepeating("SyncSettings", 5.0f, 5.0f);
    }

    // Öffnet einen Dateibrowser, um die Datei zu laden
    //
    // The callback form, which is the one that works. WaitForLoadDialog returns an
    // IEnumerator that has to be driven as a coroutine; called from an async method that
    // never awaited it, it was simply discarded -- so the dialog never appeared, the stale
    // FileBrowser.Success was read, and the Load button did nothing at all. It is the same
    // call the macOS project already makes.
    private void OpenFileBrowser()
    {
        SetProfileFilters();
        FileBrowser.ShowLoadDialog(
            (paths) =>
            {
                if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
                    LoadSettings(paths[0]);
            },
            () => { Debug.Log("[SettingsManager] file selection cancelled"); },
            FileBrowser.PickMode.Files,
            false, null, null,
            "Load a profile or settings file", "Load");
    }

    /// <summary>
    /// Both kinds of file this build can read.
    ///
    /// Condition profiles are .json; the older settings files are .profile. Filtering to
    /// .profile alone -- which is what this did -- shows the user an empty folder when they
    /// navigate to their profiles, which is indistinguishable from the profiles not being
    /// there.
    /// </summary>
    private static void SetProfileFilters()
    {
        FileBrowser.SetFilters(true,
            new FileBrowser.Filter("Profiles and settings", ".json", ".profile"),
            new FileBrowser.Filter("Condition profiles", ".json"),
            new FileBrowser.Filter("Settings files", ".profile"));
        FileBrowser.SetDefaultFilter(".json");
    }

    // Speichert die aktuellen Einstellungen in eine Datei
    private void SaveSettings()
    {
        SetProfileFilters();
        FileBrowser.ShowSaveDialog(
            (paths) =>
            {
                if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
                    WriteProfile(paths[0]);
            },
            () => { Debug.Log("[SettingsManager] save cancelled"); },
            FileBrowser.PickMode.Files,
            false, null, "profile.json",
            "Save this simulation as a profile", "Save");
    }

    private void WriteProfile(string path)
    {

        // Written as a condition profile: which effects are on, and their parameters in the
        // effects' own units. That is the format the Load button reads back, so a profile
        // saved here reloads exactly, and it is the same shape as the authored profiles --
        // which is what lets this application be used to tune one rather than only to view
        // it. Nothing writes a severity: severity belongs to the pipeline the authored
        // profiles came from, and this end knows the concrete values.
        var profile = ProfileBinder.Capture(this, Path.GetFileNameWithoutExtension(path), "");
        File.WriteAllText(path, profile.ToString(Formatting.Indented));

        int count = (profile["filters"] as JArray)?.Count ?? 0;
        Debug.Log($"[SettingsManager] saved a profile with {count} active effect(s) to {path}");
    }

    // Lädt Einstellungen aus einer JSON-Datei
    private void LoadSettings(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("File not found: " + path);
            return;
        }

        string json = File.ReadAllText(path);

        // Check the file is the shape we expect before applying any of it.
        //
        // Newtonsoft ignores properties it does not recognise, so a JSON file with entirely
        // different keys deserializes without error into an AppSettings whose every field is
        // the C# default. UpdatePublicFields then assigns those defaults unconditionally to
        // all eighteen effects and every light in the scene -- so loading the wrong file did
        // not fail, it silently reset the whole simulation to zero and logged success. A
        // condition profile is exactly such a file: it nests its values under "filters",
        // and shares not one top-level key with AppSettings.
        JObject probe;
        try
        {
            probe = JObject.Parse(json);
        }
        catch (JsonException e)
        {
            Debug.LogError($"[SettingsManager] {Path.GetFileName(path)} is not valid JSON: {e.Message}");
            return;
        }

        // A condition profile: which effects, with parameters per effect. Applied through
        // ProfileBinder, which reports by name everything it could not honour -- these
        // profiles were authored against a different filter pipeline and describe more than
        // this build can draw, and the difference should never be something the user has to
        // infer from the picture.
        if (probe["filters"] != null)
        {
            string why;
            var profile = ConditionProfile.Parse(probe, out why);
            if (profile == null)
            {
                Debug.LogError($"[SettingsManager] {Path.GetFileName(path)}: {why}. Nothing was changed.");
                return;
            }

            var report = ProfileBinder.Apply(this, profile);
            Debug.Log(report.Summary(profile.Id));
            if (!string.IsNullOrEmpty(profile.Caveat))
                Debug.LogWarning($"[ConditionProfile] {profile.Id} says of itself: {profile.Caveat}");
            return;
        }

        int known = 0;
        foreach (var prop in probe.Properties())
            if (typeof(AppSettings).GetField(prop.Name) != null) known++;

        if (known == 0)
        {
            Debug.LogError($"[SettingsManager] {Path.GetFileName(path)} has no setting this " +
                           "version recognises, so loading it would reset every effect to zero. " +
                           "Nothing was changed.");
            return;
        }

        appSettings = JsonConvert.DeserializeObject<AppSettings>(json);
        ApplySettings();
        Debug.Log($"[SettingsManager] loaded {known} settings from {path}");
    }

    // Übernimmt die geladenen Einstellungen und aktualisiert alle relevanten Public Variablen in anderen Skripten
    private void ApplySettings()
    {
        // Aktualisiere die referenzierten Objekte mit Public Variablen
        UpdatePublicFields();

        // Beispiel: wende Licht-Intensität an
        Light[] lights = FindObjectsByType<Light>();
        foreach (var light in lights)
        {
            light.intensity = appSettings.intensity;
        }
    }

    // Aktualisiert die Public Variablen eines MonoBehaviour anhand der aktuellen AppSettings
    private void UpdatePublicFields()
    {
        // vision loss central
        myFieldLoss.overlayScale = appSettings.overlayscale;

        // Hyperopia
        myBlur.maxCPD = appSettings.maxCPD;
        // Color Vision Deficiency
        myRecolour.anomType = appSettings.anomType;
        myRecolour.severityIndex = appSettings.severityIndex;
        //  Contrast Sensitivty
        myBrightnessContrastGamma.Contrast = appSettings.contrast;
        myBrightnessContrastGamma.Brightness = appSettings.brightness;
        myBrightnessContrastGamma.Gamma = appSettings.gamma;
        //Nystagmus
        myNystagmus.foveat_d = appSettings.foveat_d;
        myNystagmus.rise_d = appSettings.rise_d;
        myNystagmus.rise_exp = appSettings.rise_exp;
        myNystagmus.amp_deg = appSettings.amp_deg;
        //Retinopathy
        myFloaters.floaterType = appSettings.type;
        myFloaters.intensity = appSettings.intensity;
        myFloaters.floaterSize = appSettings.floaterSize;
        myFloaters.floaterDensity =  appSettings.floaterDensity;
        myFloaters.circleRadius = appSettings.circleRadius;
        myFloaters.center = appSettings.center;
        myFloaters.Speed = appSettings.r_speed; //!!!!
        //Teichopisa
        myTeichopsia.Strength = appSettings.strength;
        myTeichopsia.LumContribution = appSettings.lumContribution;
        myTeichopsia.gazeContingent = appSettings.gazeContingent;
        //Metamorphosia
        myWiggle.Timer = appSettings.timer;
        myWiggle.Speed = appSettings.speed;
        myWiggle.Frequency = appSettings.frequency;
        myWiggle.Amplitude = appSettings.amplitute;
        //Photophobia
        myBloom.intensity = appSettings.p_intensity; //!!!!
        myBloom.threshold = appSettings.threshold;
        myBloom.blurSize = appSettings.blurSize;
        //Vision Loss peripheral
        myFieldLossInverted.overlayScale = appSettings.overlayscale;
        //Cataract
        myCataract.severityIndex = appSettings.c_severityIndex; //!!!!
        myCataract.useFrosting = appSettings.useFrosting;
        myCataract.Gamma = appSettings.c_gamma; //!!!!
        //In-Filling
        myInpainter2.threshold = appSettings.i_threshold; //!!!!
        //Double Vision
        myDoubleVision.displacementAmount = appSettings.displacementAmount;
        //Distortion
        myVortexEffect.vortexRadius = appSettings.vortexRadius;
        myVortexEffect.suctionStrength = appSettings.suctionStrength;
        myVortexEffect.innerCircleRadius = appSettings.innerCircleRadius;
        myVortexEffect.noiseAmount = appSettings.noiseAmount;
        //Foveal Darkness
        myFovealDarkness.innerCircleRadius = appSettings.innnerCircleRadius;
        myFovealDarkness.fadeWidth = appSettings.fadWidth;
        myFovealDarkness.opacity = appSettings.opacity;
        //Flickering Stars
        myFlickeringStars.radius = appSettings.radius;
        myFlickeringStars.starRadius = appSettings.starRadius;
        myFlickeringStars.fadeInDuration = appSettings.fadeInDuration;
        myFlickeringStars.fadeOutDuration = appSettings.fadeOutDuration;
        //Detail Loss
        myPixelationEffect.pixelRadius = appSettings.pixelRadius;

}

    // Synchronisiert die AppSettings, wenn sich eine Public Variable ändert
    public void SyncSettings()
    {
        // vision loss central
        appSettings.overlayscale = myFieldLoss.overlayScale;

        // Hyperopia
        appSettings.maxCPD = myBlur.maxCPD;
        // Color Vision Deficiency
        appSettings.anomType = myRecolour.anomType;
        appSettings.severityIndex = myRecolour.severityIndex;
        // Contrast Sensitivity
        appSettings.contrast = myBrightnessContrastGamma.Contrast;
        appSettings.brightness = myBrightnessContrastGamma.Brightness;
        appSettings.gamma = myBrightnessContrastGamma.Gamma;
        // Nystagmus
        appSettings.foveat_d = myNystagmus.foveat_d;
        appSettings.rise_d = myNystagmus.rise_d;
        appSettings.rise_exp = myNystagmus.rise_exp;
        appSettings.amp_deg = myNystagmus.amp_deg;
        // Retinopathy
        appSettings.type = myFloaters.floaterType;
        appSettings.intensity = myFloaters.intensity;
        appSettings.floaterSize = myFloaters.floaterSize;
        appSettings.floaterDensity = myFloaters.floaterDensity;
        appSettings.circleRadius = myFloaters.circleRadius;
        appSettings.center = myFloaters.center;
        appSettings.r_speed = myFloaters.Speed;
        // Teichopisa
        appSettings.strength = myTeichopsia.Strength;
        appSettings.lumContribution = myTeichopsia.LumContribution;
        appSettings.gazeContingent = myTeichopsia.gazeContingent;
        // Metamorphosia
        appSettings.timer = myWiggle.Timer;
        appSettings.speed = myWiggle.Speed;
        appSettings.frequency = myWiggle.Frequency;
        appSettings.amplitute = myWiggle.Amplitude;
        // Photophobia
        appSettings.p_intensity = myBloom.intensity;
        appSettings.threshold = myBloom.threshold;
        appSettings.blurSize = myBloom.blurSize;
        // Vision Loss peripheral
        appSettings.overlayscale = myFieldLossInverted.overlayScale;
        // Cataract
        appSettings.c_severityIndex = myCataract.severityIndex;
        appSettings.useFrosting = myCataract.useFrosting;
        appSettings.c_gamma = myCataract.Gamma;
        // In-Filling
        appSettings.i_threshold = myInpainter2.threshold;
        // Double Vision
        appSettings.displacementAmount = myDoubleVision.displacementAmount;
        // Distortion
        appSettings.vortexRadius = myVortexEffect.vortexRadius;
        appSettings.suctionStrength = myVortexEffect.suctionStrength;
        appSettings.innerCircleRadius = myVortexEffect.innerCircleRadius;
        appSettings.noiseAmount = myVortexEffect.noiseAmount;
        // Foveal Darkness
        appSettings.innnerCircleRadius = myFovealDarkness.innerCircleRadius;
        appSettings.fadWidth = myFovealDarkness.fadeWidth;
        appSettings.opacity = myFovealDarkness.opacity;
        // Flickering Stars
        appSettings.radius = myFlickeringStars.radius;
        appSettings.starRadius = myFlickeringStars.starRadius;
        appSettings.fadeInDuration = myFlickeringStars.fadeInDuration;
        appSettings.fadeOutDuration = myFlickeringStars.fadeOutDuration;
        // Detail Loss
        appSettings.pixelRadius = myPixelationEffect.pixelRadius;

    }

    // Methoden für den Standalone File Browser
    /*
    private string OpenFilePanel()
    {
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Select a Settings File", "", "profile", false);
        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            return paths[0];
        }
        return null;
    }

    private string SaveFilePanel()
    {
        string path = StandaloneFileBrowser.SaveFilePanel("Save Settings File", "", "settings", "profile");
        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }
        return null;
    }
    */

}

// Klasse, die alle Einstellungen enthält
[System.Serializable]
public class AppSettings
{
    
    public string playerName = "Player";
    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    public bool fullscreen = true;
    public float volume = 0.5f;

    // Füge hier weitere Einstellungen hinzu, die gespeichert werden sollen

    // vision loss central
    public float overlay_scale;
    // Hyperopia
    public float maxCPD;
    // Color Vision Deficiency
    public AnomolyType anomType;
    // Contrast Sensitivty
    public float severityIndex;
    //Metamorphosia
    public float brightness;
    public float contrast;
    public float gamma;
    //Nystagmus
    public float foveat_d;
    public float rise_d;
    public float rise_exp;
    public float amp_deg;
    //Retinopathy
    public FloaterType type;
    public float intensity;
    public float floaterSize;
    public float floaterDensity;
    public float circleRadius;
    public bool center;
    public float r_speed; //!!!!
    //Teichopisa
    public float strength;
    public float lumContribution;
    public bool gazeContingent;
    //Metamorphosia
    public float timer;
    public float speed;
    public float frequency;
    public float amplitute;
    //Photophobia
    public float p_intensity; //!!!!
    public float threshold;
    public float blurSize;
    //Vision Loss peripheral
    public float overlayscale;
    //Cataract
    public float c_severityIndex; //!!!!
    public bool useFrosting;
    public float c_gamma; //!!!!
    //In-Filling
    public float i_threshold; //!!!!
    //Double Vision
    public float displacementAmount;
    //Distortion
    public float vortexRadius;
    public float suctionStrength;
    public float innerCircleRadius;
    public float noiseAmount;
    //Foveal Darkness
    public float innnerCircleRadius;
    public float fadWidth;
    public float opacity;
    //Flickering Stars
    public float radius;
    public float starRadius;
    public float fadeInDuration;
    public float fadeOutDuration;
    //Detail Loss
    public float pixelRadius;
}

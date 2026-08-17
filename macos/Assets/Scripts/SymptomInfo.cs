using UnityEngine;

/// <summary>
/// A scrollable reference explaining every symptom VIP-Sim simulates.
///
/// The effect list gives eighteen names and no explanation. A designer can now find the
/// right effect by its plain-language label, but still has no way to learn what the
/// condition actually is, which is half of what an accessibility tool is for.
///
/// Drawn with IMGUI rather than as uGUI objects, deliberately. The alternative was an info
/// affordance on each of eighteen rows, and those rows have no room -- the Enable bar spans
/// -150 to +80 and the gear sits at 105 inside a 230-wide menu. IMGUI needs no scene
/// geometry at all, which matters on a panel where two layout changes have already had to
/// be reverted. It also paints over everything, which for a modal reference is what you
/// want rather than a drawback.
///
/// Content mirrors docs/EFFECTS.md. Kept in code rather than loaded from the markdown so
/// there is nothing to ship alongside the binary and nothing to fail to find at runtime.
/// </summary>
public class SymptomInfo : MonoBehaviour
{
    [Tooltip("Also opens and closes the panel, for when the toolbar button is unreachable.")]
    public KeyCode toggleKey = KeyCode.F1;

    private bool _open;
    private Vector2 _scroll;
    private GUIStyle _title, _term, _body, _group;

    /// <summary>Wired to the toolbar button. Public so the Button onClick can find it.</summary>
    public void Toggle() => SetOpen(!_open);

    public void Close() => SetOpen(false);

    /// <summary>
    /// Opening the panel has to switch the overlay out of click-through, or it cannot be
    /// used at all: the wheel and the clicks pass straight through to whatever application
    /// is behind it, so the scroll view never receives them and the Close button cannot be
    /// pressed. The uGUI hover test that normally handles this is blind to IMGUI, which is
    /// why the panel needs to say so explicitly.
    /// </summary>
    private void SetOpen(bool open)
    {
        if (_open == open) return;
        _open = open;

        var window = FindAnyObjectByType<TransparentWindow>(FindObjectsInactive.Include);
        if (window == null) return;

        if (open) window.enableInfoState();
        else window.disableInfoState();
    }

    private void OnDisable()
    {
        // Never leave the overlay stuck non-click-through. If this component is disabled
        // while the panel is open, the flag would stay set and the whole overlay would
        // keep swallowing clicks with nothing on screen to explain why.
        if (_open) SetOpen(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) Toggle();

        // Escape closes, which is what people try first on a modal panel.
        if (_open && Input.GetKeyDown(KeyCode.Escape)) _open = false;
    }

    private void OnGUI()
    {
        if (!_open) return;

        EnsureStyles();

        // Sized as a share of the screen rather than in pixels: VIP-Sim runs full-screen on
        // whatever the display is, and this is read on 4K panels as often as 1080p ones.
        float w = Mathf.Min(Screen.width * 0.55f, 1100f);
        float h = Screen.height * 0.78f;
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        // Dim the rest of the screen. Without this the text sits on top of the simulation
        // it is describing, which is unreadable by construction -- the whole point of the
        // effects is to degrade whatever is behind them.
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Box(panel, GUIContent.none);
        GUILayout.BeginArea(new Rect(panel.x + 24, panel.y + 20, panel.width - 48, panel.height - 40));

        GUILayout.Label("Vision symptoms", _title);
        GUILayout.Label("Each effect approximates ONE symptom, not a whole diagnosis. Real " +
                        "conditions combine several, vary enormously between individuals, and " +
                        "change over time.", _body);
        GUILayout.Space(10);

        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (var entry in Entries)
        {
            if (entry.isGroup)
            {
                GUILayout.Space(12);
                GUILayout.Label(entry.label, _group);
                continue;
            }
            GUILayout.Label($"{entry.label}   ({entry.term})", _term);
            GUILayout.Label(entry.description, _body);
            GUILayout.Space(6);
        }
        GUILayout.EndScrollView();

        GUILayout.Space(8);
        if (GUILayout.Button("Close  (Esc)", GUILayout.Height(34))) _open = false;

        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (_title != null) return;

        // Scaled from a 1080p baseline. Fixed-point IMGUI text is unreadable at 4K, which is
        // the same mistake UnitEye's debug overlay made before it was corrected.
        float s = Mathf.Max(1f, Screen.height / 1080f);

        _title = new GUIStyle(GUI.skin.label)
        { fontSize = Mathf.RoundToInt(30 * s), fontStyle = FontStyle.Bold, wordWrap = true };
        _group = new GUIStyle(GUI.skin.label)
        { fontSize = Mathf.RoundToInt(21 * s), fontStyle = FontStyle.Bold, wordWrap = true };
        _term = new GUIStyle(GUI.skin.label)
        { fontSize = Mathf.RoundToInt(18 * s), fontStyle = FontStyle.Bold, wordWrap = true };
        _body = new GUIStyle(GUI.skin.label)
        { fontSize = Mathf.RoundToInt(17 * s), wordWrap = true };
        _group.normal.textColor = new Color(1f, 0.62f, 0.16f);
    }

    private struct Entry
    {
        public string label, term, description;
        public bool isGroup;

        public static Entry Group(string name) => new Entry { label = name, isGroup = true };
        public static Entry Of(string label, string term, string description) =>
            new Entry { label = label, term = term, description = description };
    }

    // Mirrors docs/EFFECTS.md, in the same order the effect list is grouped.
    private static readonly Entry[] Entries =
    {
        Entry.Group("Central vision"),
        Entry.Of("Vision Loss, Central", "central scotoma",
                 "A blind or blurred patch in the middle of vision. Reading, faces and fine " +
                 "detail go; the periphery stays usable."),
        Entry.Of("Central Dark Spot", "foveal darkness",
                 "Darkening at the precise centre of gaze, which moves with the eye."),
        Entry.Of("Detail Loss", "reduced acuity",
                 "Fine detail is lost everywhere, without the image blurring uniformly."),

        Entry.Group("Peripheral vision"),
        Entry.Of("Vision Loss, Peripheral", "peripheral scotoma",
                 "Loss around the edges, progressing inwards. Central detail is preserved " +
                 "until late, so it is easily unnoticed -- characteristic of glaucoma."),
        Entry.Of("In-Filling", "perceptual filling-in",
                 "The brain completes missing regions with surrounding texture, so gaps are " +
                 "not perceived as gaps."),

        Entry.Group("Distortion"),
        Entry.Of("Wavy Distortion", "metamorphopsia",
                 "Straight lines bend or ripple. A common early sign of macular disease."),
        Entry.Of("Wavy Distortion II", "metamorphopsia (variant)",
                 "A second implementation, with a different distortion field."),
        Entry.Of("Distortion", "geometric distortion",
                 "General warping of the image."),

        Entry.Group("Blur and refraction"),
        Entry.Of("Farsightedness", "hyperopia",
                 "Near objects are out of focus; distance is clearer."),
        Entry.Of("Cataract", "cataract",
                 "Clouding of the lens. Hazy vision, dulled colour, and light scattering " +
                 "into glare."),

        Entry.Group("Colour and contrast"),
        Entry.Of("Color Vision Deficiency", "dyschromatopsia",
                 "Reduced ability to distinguish colours, most often red from green. " +
                 "Sharpness is unaffected."),
        Entry.Of("Contrast Sensitivity", "reduced contrast sensitivity",
                 "Low-contrast edges become hard to separate. Text on a tinted background " +
                 "disappears first."),

        Entry.Group("Light"),
        Entry.Of("Glare Vision / Photophobia", "photophobia",
                 "Bright regions bloom and become painful to look at."),

        Entry.Group("Eye movement"),
        Entry.Of("Eye Tremor", "nystagmus",
                 "Involuntary rhythmic eye movement; the image drifts and jerks."),
        Entry.Of("Double Vision", "diplopia",
                 "Two offset copies of the image."),

        Entry.Group("Transient and floating"),
        Entry.Of("Retinopathy / Floaters", "vitreous floaters",
                 "Dark shapes drifting across vision, moving with the eye and settling slowly."),
        Entry.Of("Flickering Specks", "photopsia",
                 "Small flickering points of light."),
        Entry.Of("Visual Aura", "teichopsia",
                 "A shimmering, often geometric disturbance that expands across the field. " +
                 "Associated with migraine."),
    };
}

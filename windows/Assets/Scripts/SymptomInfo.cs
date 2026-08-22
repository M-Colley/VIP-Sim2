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

    // The UIST'25 paper. Resolved through doi.org rather than a publisher URL so it keeps
    // working if the hosting moves.
    private const string PaperUrl = "https://doi.org/10.1145/3746059.3747704";

    private bool _open;
    private Vector2 _scroll;

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

    private void Start()
    {
        // Re-apply the display this machine used last session. Delayed, because
        // TransparentWindow.Start restores full-screen geometry on the PRIMARY display
        // and both writes land on the same window -- whoever writes last wins, and it
        // has to be this one.
        StartCoroutine(RestoreDisplayWhenSettled());

        // The first-run tutorial is attached AT RUNTIME rather than serialized into the
        // scene: a component that exists only in code cannot drift between the two
        // platform projects, and scene surgery is what has repeatedly broken this UI.
        if (GetComponent<FirstRunTutorial>() == null)
            gameObject.AddComponent<FirstRunTutorial>();

        UpdateChecker.Install(gameObject);
        VipSimAccessibility.Install(gameObject);
        DisplayNotice.Install(gameObject);
        CaptureNotice.Install(gameObject);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // Ask the capture plugin for a method that can see a GPU-rendered window; its
        // default cannot, and the failure looks exactly like the simulation being off.
        WindowCaptureMode.Install(gameObject);
#endif

#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
        // Linux gets its overlay from a separate presenter process and its capture from
        // the desktop portal; both are attached here for the same reason as the components
        // above -- a component that exists only in code cannot drift between the two
        // platform projects, and neither can be added to a scene the other platforms share.
        LinuxPresenter.Install(gameObject);
        LinuxCapture.Install(gameObject);
        LinuxCaptureSurface.Install(gameObject);
#endif
    }

    private System.Collections.IEnumerator RestoreDisplayWhenSettled()
    {
        yield return new WaitForSeconds(1.5f);
        DisplaySwitcher.ApplySaved();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) Toggle();

        // Escape closes, which is what people try first on a modal panel.
        if (_open && Input.GetKeyDown(KeyCode.Escape)) _open = false;
    }

    /// <summary>
    /// Which section of the panel is showing.
    ///
    /// This panel had accumulated everything that had nowhere else to go: a reference to
    /// eighteen symptoms, a paper link, four navigation buttons, two rows of accessibility
    /// controls with their own paragraph of keyboard help, three support buttons and an
    /// update status line -- all on one screen at once. The reference the panel exists for
    /// was the hardest thing on it to read, and the footer alone held nine controls.
    ///
    /// Three sections, one purpose each, and a footer with one button in it. Nothing was
    /// removed; it is the simultaneity that was the problem.
    /// </summary>
    private enum Tab { Symptoms, Display, Help }

    private Tab _tab = Tab.Symptoms;
    private Tab _pendingTab = Tab.Symptoms;

    // Read once per frame, during the layout pass, and used for the rest of it.
    //
    // IMGUI runs OnGUI several times per frame -- once to lay out, again to handle the
    // event, again to paint -- and every pass must produce the SAME set of controls. A
    // value that can change in between (the tab the user just clicked, whether an update
    // has finished downloading, whether a monitor was just plugged in) would add or remove
    // a control mid-frame and throw a mismatched-layout-group error, which takes the whole
    // panel down rather than just misdrawing it. Latching them at Layout is what makes the
    // passes agree.
    private bool _updateAvailable;
    private int _displays = 1;

    // One scroll position per section, so leaving a section and coming back does not
    // silently return the reader to the top of it.
    private readonly Vector2[] _scrolls = new Vector2[3];

    private void OnGUI()
    {
        if (!_open) return;

        VipSimSkin.Ensure();
        float s = VipSimSkin.Scale;
        float bh = VipSimSkin.ControlHeight;

        if (Event.current.type == EventType.Layout)
        {
            _tab = _pendingTab;
            _updateAvailable = UpdateChecker.UpdateAvailable;
            _displays = DisplaySwitcher.DisplayCount;
        }

        // Sized as a share of the screen rather than in pixels: VIP-Sim runs full-screen on
        // whatever the display is, and this is read on 4K panels as often as 1080p ones.
        float w = Mathf.Min(Screen.width * 0.72f, 1600f);
        float h = Screen.height * 0.82f;
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        // Dim the rest of the screen. Without this the text sits on top of the simulation
        // it is describing, which is unreadable by construction -- the whole point of the
        // effects is to degrade whatever is behind them.
        VipSimSkin.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.76f));

        GUI.Box(panel, GUIContent.none, VipSimSkin.Panel);
        GUILayout.BeginArea(new Rect(panel.x + 34 * s, panel.y + 30 * s,
                                     panel.width - 68 * s, panel.height - 60 * s));

        GUILayout.Label($"<color=#{VipSimSkin.AccentHex}>VIP-SIM</color>", VipSimSkin.Body);
        GUILayout.Space(4 * s);
        GUILayout.Label(TitleFor(_tab), VipSimSkin.Title);
        GUILayout.Space(12 * s);

        GUILayout.BeginHorizontal();
        TabButton(Tab.Symptoms, "Symptoms", bh);
        TabButton(Tab.Display, "Display & text", bh);
        TabButton(Tab.Help, "Help & updates", bh);
        GUILayout.EndHorizontal();

        GUILayout.Space(12 * s);
        VipSimSkin.Separator(0f);
        GUILayout.Space(10 * s);

        int i = (int)_tab;
        _scrolls[i] = GUILayout.BeginScrollView(_scrolls[i]);
        switch (_tab)
        {
            case Tab.Display: DrawDisplaySection(s, bh); break;
            case Tab.Help: DrawHelpSection(s, bh); break;
            default: DrawSymptomsSection(s); break;
        }
        GUILayout.EndScrollView();

        GUILayout.Space(14 * s);
        VipSimSkin.Separator(0f);
        GUILayout.Space(14 * s);

        // One button. Everything else that used to live down here belongs to a section and
        // is now inside it.
        if (GUILayout.Button("Close  (Esc)", VipSimSkin.Primary, GUILayout.Height(bh))) SetOpen(false);

        GUILayout.EndArea();
    }

    private static string TitleFor(Tab tab)
    {
        switch (tab)
        {
            case Tab.Display: return "Display and text";
            case Tab.Help: return "Help and updates";
            default: return "Vision symptoms";
        }
    }

    /// <summary>
    /// A tab. The current one is drawn in the primary style, so which section you are in is
    /// visible without relying on position or colour alone.
    /// </summary>
    private void TabButton(Tab tab, string label, float bh)
    {
        var style = _tab == tab ? VipSimSkin.Primary : VipSimSkin.Secondary;
        if (GUILayout.Button(label, style, GUILayout.Height(bh))) _pendingTab = tab;
    }

    private void DrawSymptomsSection(float s)
    {
        GUILayout.Label("Each effect approximates <b>one symptom</b>, not a whole diagnosis. Real " +
                        "conditions combine several, vary enormously between individuals, and " +
                        "change over time.", VipSimSkin.Body);
        GUILayout.Space(12 * s);

        foreach (var entry in Entries)
        {
            if (entry.isGroup)
            {
                GUILayout.Space(14 * s);
                GUILayout.Label(entry.label.ToUpperInvariant(), VipSimSkin.Heading);
                continue;
            }
            GUILayout.Label($"{entry.label}   <color=#{VipSimSkin.MutedHex}>{entry.term}</color>", VipSimSkin.Term);
            GUILayout.Label(entry.description, VipSimSkin.Body);
            GUILayout.Space(10 * s);
        }
    }

    /// <summary>
    /// Which screen the overlay is on, and how big its own text is.
    ///
    /// These two belong together: both answer "I cannot see this properly", which is the
    /// only reason anyone opens this section. The display control lives here as well as on
    /// F3 because this panel forces the overlay interactive, so the button ALWAYS works --
    /// F3, like every hotkey on a click-through overlay, only fires while VIP-Sim happens
    /// to hold keyboard focus, which is rarely.
    /// </summary>
    private void DrawDisplaySection(float s, float bh)
    {
        GUILayout.Label("WHICH SCREEN", VipSimSkin.Heading);
        GUILayout.Space(6 * s);

        if (_displays > 1)
        {
            GUILayout.Label($"VIP-Sim is on {DisplaySwitcher.Summary}. It simulates the screen it " +
                            "is on, so a window on another screen will not appear.", VipSimSkin.Body);
            GUILayout.Space(8 * s);
            if (GUILayout.Button("Move to the next screen  (F3)", VipSimSkin.Secondary, GUILayout.Height(bh)))
                DisplaySwitcher.MoveToNext();
        }
        else
        {
            GUILayout.Label("One screen is connected, so there is nowhere else to move to.",
                            VipSimSkin.Body);
        }

        GUILayout.Space(16 * s);
        VipSimSkin.Separator(0f);
        GUILayout.Space(16 * s);

        GUILayout.Label("TEXT AND CONTRAST", VipSimSkin.Heading);
        GUILayout.Space(6 * s);

        // No fixed widths on the controls. The first version put all four on one line with
        // fixed pixel widths and it overflowed the panel at 120% text, clipping the last
        // button off the right edge -- a control row that breaks when the text is enlarged
        // is precisely the defect this feature exists to prevent.
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Text size  {Mathf.RoundToInt(VipSimSkin.UserScale * 100f)}%", VipSimSkin.Term);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("A -", VipSimSkin.Secondary, GUILayout.Height(bh), GUILayout.MinWidth(90 * s)))
            VipSimSkin.UserScale = VipSimSkin.UserScale - 0.1f;
        if (GUILayout.Button("A +", VipSimSkin.Secondary, GUILayout.Height(bh), GUILayout.MinWidth(90 * s)))
            VipSimSkin.UserScale = VipSimSkin.UserScale + 0.1f;
        GUILayout.EndHorizontal();

        GUILayout.Space(8 * s);

        GUILayout.BeginHorizontal();

        // Labelled with its state in words, not by colour alone.
        bool hc = VipSimSkin.HighContrast;
        if (GUILayout.Button(hc ? "High contrast: ON" : "High contrast: OFF",
                             hc ? VipSimSkin.Primary : VipSimSkin.Secondary, GUILayout.Height(bh)))
        {
            VipSimSkin.HighContrast = !hc;
        }

        if (GUILayout.Button("Keyboard focus", VipSimSkin.Secondary, GUILayout.Height(bh)))
            VipSimAccessibility.FocusFirst();

        GUILayout.EndHorizontal();

        GUILayout.Space(8 * s);
        GUILayout.Label("Tab and Shift+Tab move between controls; arrow keys move within them; " +
                        "Enter activates. VIP-Sim must have keyboard focus first -- click its " +
                        "panel once. Screen readers are not yet supported; see ACCESSIBILITY.md.",
                        VipSimSkin.Body);
    }

    /// <summary>
    /// The paper, the tutorial, and the answer to "something is wrong, now what" that is
    /// not "email the author and hope": where to report, which file to attach, and whether
    /// the build is current -- the first question any support reply would have asked.
    /// </summary>
    private void DrawHelpSection(float s, float bh)
    {
        GUILayout.Label("ABOUT VIP-SIM", VipSimSkin.Heading);
        GUILayout.Space(6 * s);
        GUILayout.Label("VIP-Sim is described in the UIST'25 paper, which covers how the symptoms " +
                        "were chosen, how the simulation was built with and for people with " +
                        "visual impairments, and what it was evaluated on.", VipSimSkin.Body);
        GUILayout.Space(8 * s);

        // A link, not just a printed DOI: nobody types a DOI by hand. Rendered as a button
        // so it is obviously clickable, since IMGUI has no anchor element.
        var linkStyle = new GUIStyle(VipSimSkin.Body) { normal = { textColor = new Color(0.45f, 0.72f, 1f) } };
        if (GUILayout.Button(PaperUrl, linkStyle)) Application.OpenURL(PaperUrl);

        GUILayout.Space(10 * s);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Open the paper", VipSimSkin.Secondary, GUILayout.Height(bh)))
            Application.OpenURL(PaperUrl);

        // Close this panel first: both are modal IMGUI surfaces and would stack.
        if (GUILayout.Button("Show the tutorial", VipSimSkin.Secondary, GUILayout.Height(bh)))
        {
            SetOpen(false);
            FirstRunTutorial.Open();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(16 * s);
        VipSimSkin.Separator(0f);
        GUILayout.Space(16 * s);

        GUILayout.Label("IF SOMETHING IS WRONG", VipSimSkin.Heading);
        GUILayout.Space(6 * s);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Report a problem", VipSimSkin.Secondary, GUILayout.Height(bh)))
            Application.OpenURL(UpdateChecker.SupportUrl);

        // Copies rather than opens: the folder differs per platform, and a path on the
        // clipboard is what a user can paste into a bug report.
        if (GUILayout.Button("Copy diagnostics path", VipSimSkin.Secondary, GUILayout.Height(bh)))
        {
            GUIUtility.systemCopyBuffer = Application.persistentDataPath;
            Debug.Log($"[SymptomInfo] Diagnostics path copied: {Application.persistentDataPath}");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(14 * s);
        GUILayout.Label(UpdateChecker.Status, VipSimSkin.Muted);
        if (_updateAvailable)
        {
            GUILayout.Space(8 * s);
            if (GUILayout.Button("Get the update", VipSimSkin.Primary, GUILayout.Height(bh)))
                Application.OpenURL(UpdateChecker.ReleasesUrl);
        }
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

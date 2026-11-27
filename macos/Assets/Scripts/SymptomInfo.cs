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

        // Notes are typed here, so leaving the panel is when they are complete.
        if (!open) ReviewRecorder.WriteIfDirty();

        var window = FindAnyObjectByType<TransparentWindow>(FindObjectsInactive.Include);
        if (window == null) return;

        if (open) window.enableInfoState();
        else window.disableInfoState();
    }

    private void OnApplicationQuit() => ReviewRecorder.WriteIfDirty();

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

        // An organisation's policy.json, if there is one: geometry, report folder, start profile.
        VipSimPolicy.ApplyAtStart(this);

        // The first-run tutorial is attached AT RUNTIME rather than serialized into the
        // scene: a component that exists only in code cannot drift between the two
        // platform projects, and scene surgery is what has repeatedly broken this UI.
        if (GetComponent<FirstRunTutorial>() == null)
            gameObject.AddComponent<FirstRunTutorial>();

        UpdateChecker.Install(gameObject);
        VipSimAccessibility.Install(gameObject);
        DisplayNotice.Install(gameObject);

#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
        // Linux gets its overlay from a separate presenter process and its capture from
        // the desktop portal; both are attached here for the same reason as the components
        // above -- a component that exists only in code cannot drift between the two
        // platform projects, and neither can be added to a scene the other platforms share.
        LinuxPresenter.Install(gameObject);
        LinuxCapture.Install(gameObject);
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

        // Escape closes, which is what people try first on a modal panel. Through SetOpen:
        // clearing the flag alone left the overlay in its non-click-through info state, so
        // after Esc the whole screen swallowed clicks with nothing visible on it.
        if (_open && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
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
    private enum Tab { Symptoms, Display, Review, Help }

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
    private readonly Vector2[] _scrolls = new Vector2[4];

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
            ApplyPendingReviewActions();
            _reviewCount = ReviewRecorder.HasFindings ? ReviewRecorder.Session.Findings.Count : 0;
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
        TabButton(Tab.Review, "Review", bh);
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
            case Tab.Review: DrawReviewSection(s, bh); break;
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
            case Tab.Review: return "Review and report";
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

    // What is typed into the two geometry fields. A value is applied once it parses and is
    // in range; until then the field keeps the text and a line below says what is accepted.
    private string _distanceText, _widthText;
    private bool _distanceBad, _widthBad;
    private GUIStyle _field;
    private const string WidthControl = "vipsim-screen-width";
    private const string DistanceControl = "vipsim-viewing-distance";

    /// <summary>
    /// Viewing distance and screen width. Blur and eye tremor are set in
    /// degrees of visual angle, and these two numbers convert degrees to pixels. The width is
    /// read from the monitor where it reports one. The distance cannot be measured, so it is
    /// asked for here; without it those effects are only as accurate as the 60 cm default.
    /// </summary>
    private void DrawGeometrySection(float s, float bh)
    {
        GUILayout.Label("VIEWING DISTANCE AND SCREEN SIZE", VipSimSkin.Heading);
        GUILayout.Space(6 * s);
        GUILayout.Label("Farsightedness (blur) and eye tremor (nystagmus) are set in degrees of visual " +
                        "angle. Their size on screen depends on how far your eyes are from the screen and " +
                        "how wide the screen is. Measure both in centimetres.", VipSimSkin.Body);
        GUILayout.Space(8 * s);

        if (_field == null || _field.fontSize != VipSimSkin.Body.fontSize)
            _field = new GUIStyle(GUI.skin.textField)
            {
                fontSize = VipSimSkin.Body.fontSize,
                alignment = TextAnchor.MiddleLeft,
            };

        string focused = GUI.GetNameOfFocusedControl();
        var geometry = VisSim.ViewingGeometry.Source;
        if (focused != DistanceControl && !_distanceBad)
            _distanceText = Cm(VisSim.ViewingGeometry.DistanceCm);
        if (focused != WidthControl && !_widthBad)
            _widthText = geometry == VisSim.ViewingGeometry.WidthSource.Entered ? Cm(VisSim.ViewingGeometry.WidthCm) : "";

        GUILayout.BeginHorizontal();
        GUILayout.Label("Viewing distance, cm", VipSimSkin.Term);
        GUILayout.FlexibleSpace();
        GUI.SetNextControlName(DistanceControl);
        // A value fixed by the organisation's policy is shown in a disabled field.
        GUI.enabled = !VisSim.ViewingGeometry.DistanceFixed;
        string distance = GUILayout.TextField(_distanceText ?? "", 6, _field, GUILayout.Width(130 * s), GUILayout.Height(bh));
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        if (VisSim.ViewingGeometry.DistanceFixed)
            GUILayout.Label("The viewing distance is set by your organisation.", VipSimSkin.Body);
        if (distance != _distanceText)
        {
            _distanceText = distance;
            _distanceBad = !TryCm(distance, VisSim.ViewingGeometry.MinDistanceCm, VisSim.ViewingGeometry.MaxDistanceCm, out float d);
            if (!_distanceBad) VisSim.ViewingGeometry.DistanceCm = d;
        }
        if (_distanceBad)
            GUILayout.Label($"<color=#{VipSimSkin.AccentHex}>Enter a distance from " +
                            $"{VisSim.ViewingGeometry.MinDistanceCm:0} to {VisSim.ViewingGeometry.MaxDistanceCm:0} cm.</color>",
                            VipSimSkin.Body);

        GUILayout.Space(6 * s);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Screen width, cm (leave empty to use the monitor's value)", VipSimSkin.Term);
        GUILayout.FlexibleSpace();
        GUI.SetNextControlName(WidthControl);
        GUI.enabled = !VisSim.ViewingGeometry.WidthFixed;
        string width = GUILayout.TextField(_widthText ?? "", 6, _field, GUILayout.Width(130 * s), GUILayout.Height(bh));
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        if (width != _widthText)
        {
            _widthText = width;
            if (string.IsNullOrWhiteSpace(width))
            {
                _widthBad = false;
                VisSim.ViewingGeometry.EnteredWidthCm = 0f;
            }
            else
            {
                _widthBad = !TryCm(width, VisSim.ViewingGeometry.MinWidthCm, VisSim.ViewingGeometry.MaxWidthCm, out float w);
                if (!_widthBad) VisSim.ViewingGeometry.EnteredWidthCm = w;
            }
        }
        if (_widthBad)
            GUILayout.Label($"<color=#{VipSimSkin.AccentHex}>Enter a width from " +
                            $"{VisSim.ViewingGeometry.MinWidthCm:0} to {VisSim.ViewingGeometry.MaxWidthCm:0} cm, " +
                            "or leave it empty.</color>", VipSimSkin.Body);

        float monitor = VisSim.ViewingGeometry.MonitorWidthCm;
        string widthNote;
        switch (VisSim.ViewingGeometry.Source)
        {
            case VisSim.ViewingGeometry.WidthSource.Monitor:
                widthNote = $"The monitor reports {monitor:0.0} cm.";
                break;
            case VisSim.ViewingGeometry.WidthSource.Entered when VisSim.ViewingGeometry.WidthFixed:
                widthNote = "The screen width is set by your organisation.";
                break;
            case VisSim.ViewingGeometry.WidthSource.Entered:
                widthNote = monitor > 0f ? $"Using the width entered. The monitor reports {monitor:0.0} cm."
                                         : "Using the width entered. The monitor does not report its size.";
                break;
            default:
                widthNote = $"The monitor does not report its size, so {VisSim.ViewingGeometry.AssumedWidthCm:0.0} cm " +
                            "(a 24-inch screen) is assumed. Measure the width of the picture and enter it.";
                break;
        }
        GUILayout.Space(6 * s);
        GUILayout.Label(widthNote, VipSimSkin.Body);
        GUILayout.Label($"One degree is {VisSim.ViewingGeometry.PixelsPerDegree:0.0} px at the centre of this " +
                        $"screen, and the screen spans {VisSim.ViewingGeometry.ScreenSpanDeg:0} degrees of your view.",
                        VipSimSkin.Body);
    }

    private static string Cm(float v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    // Accepts a comma as the decimal mark too, since half the users type one.
    private static bool TryCm(string text, float min, float max, out float value)
    {
        bool ok = float.TryParse((text ?? "").Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out value);
        return ok && value >= min && value <= max;
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

        DrawGeometrySection(s, bh);

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
    // ------------------------------------------------------------------ review

    // Latched at Layout like the values above: deleting a finding or starting a new review
    // mid-frame would change the number of controls between IMGUI passes.
    private int _reviewCount;
    private VipSim.Review.Finding _pendingDelete;
    private bool _pendingNewReview;
    private GUIStyle _area, _label;

    private void ApplyPendingReviewActions()
    {
        if (_pendingDelete != null) ReviewRecorder.Delete(_pendingDelete);
        if (_pendingNewReview) ReviewRecorder.Start();
        _pendingDelete = null;
        _pendingNewReview = false;
    }

    /// <summary>
    /// The review in progress. Findings come from the camera button in the toolbar; this
    /// section names the product, describes each finding, rates it and links it to the
    /// requirements it concerns. Everything is written to disk as it changes.
    /// </summary>
    private void DrawReviewSection(float s, float bh)
    {
        EnsureFieldStyles();

        GUILayout.Label("REVIEW", VipSimSkin.Heading);
        GUILayout.Space(6 * s);
        GUILayout.Label("Each press of the <b>camera button</b> in the toolbar adds a finding. A finding " +
                        "keeps the window with the symptoms applied, the same window without them, and the " +
                        "settings that reproduce the simulation. Describe each finding below. VIP-Sim writes " +
                        "a report for people, a CSV file for Jira or Azure DevOps, and a JSON file to this folder:",
                        VipSimSkin.Body);
        GUILayout.Label(ReviewRecorder.Folder, _label);
        GUILayout.Space(10 * s);

        var session = ReviewRecorder.Session;
        GUILayout.Label("Product or page reviewed", _label);
        ReviewRecorder.SetProduct(GUILayout.TextField(session.Product ?? "", 200, _field,
                                                      GUILayout.MaxWidth(720 * s), GUILayout.Height(bh)));
        GUILayout.Space(6 * s);
        GUILayout.Label("Reviewer", _label);
        ReviewRecorder.SetReviewer(GUILayout.TextField(session.Reviewer ?? "", 120, _field,
                                                       GUILayout.MaxWidth(720 * s), GUILayout.Height(bh)));
        GUILayout.Space(8 * s);

        bool titles = ReviewRecorder.IncludeWindowTitles;
        if (GUILayout.Button(titles ? "[x] Record window titles" : "[ ] Record window titles",
                             titles ? VipSimSkin.Primary : VipSimSkin.Secondary,
                             GUILayout.Height(bh), GUILayout.ExpandWidth(false)))
            ReviewRecorder.IncludeWindowTitles = !titles;
        GUILayout.Label("Off by default. A window title can name a document, a customer or an account, and it " +
                        "is copied into the report and the CSV file.", _label);

        GUILayout.Space(14 * s);
        VipSimSkin.Separator(0f);
        GUILayout.Space(10 * s);

        if (_reviewCount == 0)
        {
            GUILayout.Label("No findings yet. Choose the symptoms, then press the camera button in the toolbar.",
                            VipSimSkin.Body);
            return;
        }

        for (int i = 0; i < _reviewCount && i < session.Findings.Count; i++)
            DrawFinding(session.Findings[i], s, bh);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Open the report", VipSimSkin.Primary, GUILayout.Height(bh)))
            ReviewRecorder.OpenReport();
        if (GUILayout.Button("Start a new review", VipSimSkin.Secondary, GUILayout.Height(bh)))
            _pendingNewReview = true;
        GUILayout.EndHorizontal();
        GUILayout.Label("A new review starts in a new folder. This review's files stay where they are.",
                        _label);
    }

    private void DrawFinding(VipSim.Review.Finding f, float s, float bh)
    {
        GUILayout.Label($"{f.Id}   <color=#{VipSimSkin.MutedHex}>{f.Captured:HH:mm}  {f.SymptomList()}</color>",
                        VipSimSkin.Term);
        GUILayout.Space(4 * s);

        GUILayout.Label("What is the problem, and for whom?", _label);
        string note = GUILayout.TextArea(f.Note ?? "", 4000, _area, GUILayout.MinHeight(bh * 2.2f));
        if (note != f.Note) { f.Note = note; ReviewRecorder.MarkDirty(); }
        GUILayout.Space(6 * s);

        GUILayout.Label("Impact", _label);
        GUILayout.BeginHorizontal();
        foreach (VipSim.Review.Impact impact in System.Enum.GetValues(typeof(VipSim.Review.Impact)))
        {
            bool on = f.Impact == impact;
            if (GUILayout.Button((on ? "[x] " : "[ ] ") + impact, on ? VipSimSkin.Primary : VipSimSkin.Secondary,
                                 GUILayout.Height(bh)) && !on)
            {
                f.Impact = impact;
                ReviewRecorder.MarkDirty();
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(6 * s);

        GUILayout.Label("Requirements (EN 301 549 and WCAG 2.2)", _label);
        var criteria = VipSim.Review.ReviewCatalog.Criteria;
        const int perRow = 2;
        for (int r = 0; r < criteria.Count; r += perRow)
        {
            GUILayout.BeginHorizontal();
            for (int c = r; c < r + perRow && c < criteria.Count; c++)
            {
                var criterion = criteria[c];
                bool on = f.Criteria.Contains(criterion.Id);
                string label = (on ? "[x] " : "[ ] ") + criterion.Name +
                               (criterion.Level.Length > 0 ? " (" + criterion.Level + ")" : "");
                if (GUILayout.Button(label, on ? VipSimSkin.Primary : VipSimSkin.Secondary, GUILayout.Height(bh)))
                {
                    if (on) f.Criteria.Remove(criterion.Id);
                    else f.Criteria.Add(criterion.Id);
                    ReviewRecorder.MarkDirty();
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(6 * s);

        if (GUILayout.Button($"Delete {f.Id} and its images", VipSimSkin.Secondary,
                             GUILayout.Height(bh), GUILayout.ExpandWidth(false)))
            _pendingDelete = f;

        GUILayout.Space(12 * s);
        VipSimSkin.Separator(0f);
        GUILayout.Space(10 * s);
    }

    /// <summary>Text field and text area styles at the panel's body size.</summary>
    private void EnsureFieldStyles()
    {
        if (_field == null || _field.fontSize != VipSimSkin.Body.fontSize)
            _field = new GUIStyle(GUI.skin.textField)
            {
                fontSize = VipSimSkin.Body.fontSize,
                alignment = TextAnchor.MiddleLeft,
            };
        // VipSimSkin.Muted is centred for status lines; labels sit flush with their fields.
        if (_label == null || _label.fontSize != VipSimSkin.Muted.fontSize)
            _label = new GUIStyle(VipSimSkin.Muted) { alignment = TextAnchor.UpperLeft, wordWrap = true };
        if (_area == null || _area.fontSize != VipSimSkin.Body.fontSize)
            _area = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = VipSimSkin.Body.fontSize,
                wordWrap = true,
            };
    }

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

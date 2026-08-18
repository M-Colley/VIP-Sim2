using UnityEngine;

/// <summary>
/// A four-page walkthrough shown once, the first time VIP-Sim is started.
///
/// VIP-Sim had no onboarding at all: a first-time user faces a borderless overlay,
/// an unlabelled window list, eighteen symptom toggles and a set of hotkeys nothing
/// announces. The panel is discoverable only if you already know it is there.
///
/// IMGUI, like the symptom reference, and for the same reasons: it needs no scene
/// geometry on a panel where layout changes have repeatedly broken shipping UI, and
/// it paints over the whole screen, which a modal walkthrough wants. It is added AT
/// RUNTIME by SymptomInfo rather than serialized into the scenes -- a component that
/// exists only in code cannot drift between the two platform projects. Its look comes
/// from VipSimSkin, shared with the symptom reference.
///
/// While open it sets TransparentWindow's tutorialState, without which its own
/// buttons would be unreachable: the overlay is click-through, and clicks on a
/// click-through window land in whatever application is underneath it.
/// </summary>
public class FirstRunTutorial : MonoBehaviour
{
    private const string DonePref = "vipsim.tutorial.done";

    private static FirstRunTutorial _instance;

    private bool _open;
    private int _page;

    /// <summary>Reopen from the symptom panel's footer, whether or not it ran before.</summary>
    public static void Open()
    {
        if (_instance == null) return;
        _instance._page = 0;
        _instance.SetOpen(true);
    }

    private void Awake()
    {
        _instance = this;
    }

    private void Start()
    {
        if (PlayerPrefs.GetInt(DonePref, 0) == 1) return;

        // First run. Deferred a few seconds so the overlay has finished restoring its
        // geometry and the user sees the tutorial over a settled screen, not one that is
        // still resizing itself.
        Invoke(nameof(OpenFirstTime), 3f);
    }

    private void OpenFirstTime()
    {
        if (!_open) { _page = 0; SetOpen(true); }
    }

    private void Update()
    {
        if (_open && Input.GetKeyDown(KeyCode.Escape)) Finish();
    }

    private void SetOpen(bool open)
    {
        if (_open == open) return;
        _open = open;

        var window = FindAnyObjectByType<TransparentWindow>(FindObjectsInactive.Include);
        if (window == null) return;
        if (open) window.enableTutorialState();
        else window.disableTutorialState();
    }

    /// <summary>Closing by any route marks the tutorial done; it never nags twice.</summary>
    private void Finish()
    {
        PlayerPrefs.SetInt(DonePref, 1);
        PlayerPrefs.Save();
        SetOpen(false);
    }

    private void OnDisable()
    {
        // Never leave the overlay stuck non-click-through with nothing on screen.
        if (_open) SetOpen(false);
    }

    private void OnGUI()
    {
        if (!_open) return;
        VipSimSkin.Ensure();

        float s = VipSimSkin.Scale;
        float w = Mathf.Min(Screen.width * 0.46f, 1100f * s);
        float h = Mathf.Min(Screen.height * 0.56f, 820f * s);
        var panel = new Rect(Mathf.Round((Screen.width - w) * 0.5f),
                             Mathf.Round((Screen.height - h) * 0.5f), w, h);

        // Dim everything behind the walkthrough. The desktop underneath is arbitrary and
        // usually busy; without this the panel competes with whatever is on screen.
        VipSimSkin.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.76f));

        GUI.Box(panel, GUIContent.none, VipSimSkin.Panel);
        GUILayout.BeginArea(new Rect(panel.x + 34 * s, panel.y + 30 * s,
                                     panel.width - 68 * s, panel.height - 60 * s));

        // Eyebrow line: tells the user what this is, so the first page is not just a
        // title floating on a dark rectangle.
        GUILayout.Label($"<color=#{VipSimSkin.AccentHex}>GETTING STARTED</color>   " +
                        $"<color=#{VipSimSkin.MutedHex}>{_page + 1} of {Pages.Length}</color>",
                        VipSimSkin.Body);
        GUILayout.Space(6 * s);
        GUILayout.Label(Pages[_page].title, VipSimSkin.Title);
        GUILayout.Space(10 * s);
        VipSimSkin.Separator(0f);
        GUILayout.Space(16 * s);
        GUILayout.Label(Pages[_page].body, VipSimSkin.Body);

        GUILayout.FlexibleSpace();

        // Progress dots, drawn rather than written: at a glance they say "four short
        // pages, you are on the second" without the reader parsing a fraction.
        DrawDots(s);
        GUILayout.Space(14 * s);

        float bh = VipSimSkin.ControlHeight;
        GUILayout.BeginHorizontal();
        if (_page > 0 && GUILayout.Button("Back", VipSimSkin.Secondary, GUILayout.Height(bh))) _page--;
        if (_page < Pages.Length - 1)
        {
            if (GUILayout.Button("Skip", VipSimSkin.Secondary, GUILayout.Height(bh))) Finish();
            if (GUILayout.Button("Next", VipSimSkin.Primary, GUILayout.Height(bh))) _page++;
        }
        else if (GUILayout.Button("Get started", VipSimSkin.Primary, GUILayout.Height(bh))) Finish();
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    private void DrawDots(float s)
    {
        float d = 8f * s, gap = 8f * s;
        var row = GUILayoutUtility.GetRect(1f, d, GUILayout.ExpandWidth(true));
        float total = Pages.Length * d + (Pages.Length - 1) * gap;
        float x = row.x + (row.width - total) * 0.5f;

        for (int i = 0; i < Pages.Length; i++)
        {
            var c = i == _page ? VipSimSkin.Accent : new Color(1f, 1f, 1f, 0.22f);
            VipSimSkin.Fill(new Rect(x, row.y, d, d), c);
            x += d + gap;
        }
    }

    private struct Page { public string title, body; }

    private static readonly Page[] Pages =
    {
        new Page
        {
            title = "Welcome to VIP-Sim",
            body  = "VIP-Sim overlays a simulation of vision impairments on a real " +
                    "application, so you can experience your own design the way someone " +
                    "with a visual impairment might.\n\n" +
                    "The overlay is <b>click-through</b>: you keep clicking, scrolling and " +
                    "typing in the application underneath while the simulation runs on top. " +
                    "Only VIP-Sim's own panel catches the mouse."
        },
        new Page
        {
            title = "1 - Pick a window",
            body  = "Choose the application you want to simulate from the list in the " +
                    "panel on the right.\n\n" +
                    "Its window is captured live and redrawn with the simulation applied. " +
                    "Everything else on your screen stays untouched."
        },
        new Page
        {
            title = "2 - Turn on symptoms",
            body  = "Switch symptoms on and off from the list -- they are grouped by the " +
                    "part of vision they affect, and several can run at once.\n\n" +
                    "The <b>gear</b> beside each symptom opens its settings, severity " +
                    "first among them.\n\n" +
                    "Not sure what a symptom is? The <b>(i)</b> button in the toolbar -- " +
                    "or <b>F1</b> -- explains every one of them in plain language."
        },
        new Page
        {
            title = "3 - Gaze, monitors, and the way out",
            body  = "The simulation follows your <b>mouse</b> by default. The eye icon in " +
                    "the toolbar switches to <b>webcam eye tracking</b>, so it follows " +
                    "where you look instead -- run the crosshair button (<b>F9</b>) once " +
                    "first to calibrate.\n\n" +
                    "More than one monitor? Move VIP-Sim between them from the (i) panel, " +
                    "or with <b>F3</b>.\n\n" +
                    "<b>Ctrl+Alt+Q always quits</b>, whatever else is happening."
        },
    };
}

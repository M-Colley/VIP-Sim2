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
/// exists only in code cannot drift between the two platform projects.
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
    private GUIStyle _title, _body, _progress, _button;

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
        EnsureStyles();

        float w = Mathf.Min(Screen.width * 0.46f, 1000f);
        float h = Mathf.Min(Screen.height * 0.52f, 760f);
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Box(panel, GUIContent.none);
        GUILayout.BeginArea(new Rect(panel.x + 28, panel.y + 24, panel.width - 56, panel.height - 48));

        GUILayout.Label(Pages[_page].title, _title);
        GUILayout.Space(8);
        GUILayout.Label(Pages[_page].body, _body);

        GUILayout.FlexibleSpace();
        GUILayout.Label($"{_page + 1} / {Pages.Length}", _progress);
        GUILayout.Space(6);

        // Button height and font both scale with the display; IMGUI's defaults are
        // authored for 1080p and shrink to slivers at 4K.
        float bh = 40f * Mathf.Max(1f, Screen.height / 1080f);
        GUILayout.BeginHorizontal();
        if (_page > 0 && GUILayout.Button("Back", _button, GUILayout.Height(bh))) _page--;
        if (_page < Pages.Length - 1)
        {
            if (GUILayout.Button("Next", _button, GUILayout.Height(bh))) _page++;
            if (GUILayout.Button("Skip", _button, GUILayout.Height(bh))) Finish();
        }
        else if (GUILayout.Button("Get started", _button, GUILayout.Height(bh))) Finish();
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (_title != null) return;
        float s = Mathf.Max(1f, Screen.height / 1080f);
        _title = new GUIStyle(GUI.skin.label)
        { fontSize = Mathf.RoundToInt(28 * s), fontStyle = FontStyle.Bold, wordWrap = true };
        _body = new GUIStyle(GUI.skin.label)
        { fontSize = Mathf.RoundToInt(18 * s), wordWrap = true, richText = true };
        _progress = new GUIStyle(GUI.skin.label)
        { fontSize = Mathf.RoundToInt(15 * s), alignment = TextAnchor.MiddleCenter };
        _progress.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
        _button = new GUIStyle(GUI.skin.button)
        { fontSize = Mathf.RoundToInt(18 * s) };
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

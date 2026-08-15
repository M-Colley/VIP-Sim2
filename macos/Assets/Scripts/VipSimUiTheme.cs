using UnityEngine;

/// <summary>
/// The panel's layout and colour values, as an editable asset.
///
/// These used to be `private const` fields inside UiRefreshSetup, an Editor script that
/// mutated the scene from a menu item. That worked, but it is the wrong shape: the values
/// lived in C# and the scene was the output, so the two drifted, and changing a colour
/// meant editing code, recompiling and re-running a menu command. Nobody without a C#
/// editor could touch the design.
///
/// Several of these numbers are load-bearing in ways that are not obvious from looking at
/// them, and the reasons are recorded here rather than in a commit nobody will read:
///
///  - Effect row spacing is NEGATIVE. Each row is a 100-tall RectTransform holding about
///    32px of visible content, so the layout group pulls them back together. A
///    reasonable-looking positive value stretches an 18-row list to roughly 2000px.
///  - The webcam row's x of 0 depends on the row being sized to its contents. It was -240
///    to compensate for a 100-wide rect whose layout group overflowed; once that was
///    fixed, the same offset pushed the control off the panel edge.
///  - Panel height is NOT the height of the window list. The area beneath it is the
///    backdrop for the effect list, which only appears once a window is selected. Sizing
///    the panel to the list leaves the effect list with no background.
///  - Gear size is wider but NOT taller than the original. Extra height forces extra row
///    spacing, which pushes the 18-row list past the bottom of the panel.
///
/// Create via: Assets > Create > VIP-Sim > UI Theme
/// </summary>
[CreateAssetMenu(fileName = "VipSimUiTheme", menuName = "VIP-Sim/UI Theme")]
public class VipSimUiTheme : ScriptableObject
{
    [Header("Panel")]
    [Tooltip("Not the height of the window list. The space beneath the list is the " +
             "backdrop for the effect list, which only appears after a window is selected.")]
    public float panelHeight = 1240f;

    [Tooltip("Leave at 374. Growing this runs the window list through the effect list, " +
             "which is drawn in the same area once a window has been selected.")]
    public float windowListHeight = 374f;

    public Color surface = new Color(0.098f, 0.102f, 0.114f, 0.965f);

    [Header("Effect rows")]
    [Tooltip("NEGATIVE by design. Rows are 100-tall rects holding ~32px of content, so the " +
             "layout group pulls them together. A positive value stretches the list to ~2000px.")]
    public float rowSpacing = -61f;

    [Tooltip("Wider than the original 35 for an easier pointer target, but deliberately " +
             "NOT taller: extra height forces extra row spacing and overflows the panel.")]
    public Vector2 gearSize = new Vector2(44f, 32f);

    [Tooltip("The Enable bar is 230 wide centred at -35, so it ends at +80. A gear left at " +
             "95 overlaps it.")]
    public float gearX = 105f;

    public Color gearIdle = Color.white;

    [Tooltip("The open gear is separated by HUE, not brightness. An Image tint multiplies, " +
             "so it can only darken -- a brighter selected colour would do nothing.")]
    public Color gearActive = new Color(1f, 0.58f, 0.11f, 1f);

    [Header("Window cards")]
    [Tooltip("Same accent as an open gear, so the app has one signal for 'this is what you " +
             "are acting on'.")]
    public Color itemSelected = new Color(0.72f, 0.42f, 0.09f, 1f);

    [Tooltip("Not pure black: that crushes the card sprite and loses the row edges.")]
    public Color itemIdle = new Color(0.16f, 0.16f, 0.18f, 1f);

    [Header("Settings panel")]
    public float settingsPanelX = 190f;
    public float settingsPanelWidth = 349f;

    [Tooltip("Slider and dropdown width, applied where DropdownManager builds them at " +
             "runtime. There is nothing in the scene to resize -- the widgets do not exist " +
             "until an effect's settings are opened.")]
    public float itemWidth = 220f;

    [Tooltip("Positive here, unlike the effect rows: these are ordinary-height rows. 16 was " +
             "the original and pushed a six-parameter effect off the bottom of the panel.")]
    public float settingsSpacing = 2f;

    [Header("Webcam footer")]
    [Tooltip("Depends on the row being sized to its contents. Was -240 to compensate for a " +
             "layout group overflowing a 100-wide rect; with that fixed, 0 centres it.")]
    public float webcamX = 0f;
    public float webcamBottomInset = 34f;
    public Vector2 webcamRowSize = new Vector2(500f, 62f);
    public float camLabelWidth = 360f;
    public Color footerSurface = new Color(0.13f, 0.13f, 0.15f, 1f);

    [Header("Toolbar")]
    [Tooltip("The exit button was FF4D00, the loudest thing on an overlay whose job is to " +
             "sit over someone else's work without competing with it.")]
    public Color destructive = new Color(0.788f, 0.263f, 0.263f, 1f);

    /// <summary>
    /// The theme asset, or a default instance if none exists yet. Returning defaults rather
    /// than null keeps UiRefreshSetup working on a checkout where the asset has not been
    /// created, which matters because the values it carries are the ones that make the
    /// panel usable at all.
    /// </summary>
    public static VipSimUiTheme LoadOrDefault()
    {
#if UNITY_EDITOR
        var guids = UnityEditor.AssetDatabase.FindAssets("t:VipSimUiTheme");
        if (guids.Length > 0)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<VipSimUiTheme>(path);
            if (asset != null) return asset;
        }
#endif
        return CreateInstance<VipSimUiTheme>();
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes VIP-Sim's own interface operable without a mouse, and visible to someone who
/// needs it larger or higher-contrast.
///
/// A tool for finding visual-accessibility failures that is itself unusable by a low-vision
/// designer is the first thing an accessibility organisation will notice. Two concrete
/// defects were measured in the scene before this existed:
///
///   - the EventSystem's m_FirstSelected was null, so nothing was ever selected and
///     keyboard navigation had no entry point at all -- the 77 Selectables were all on
///     Automatic navigation and none of it was reachable;
///   - Selectable m_SelectedColor was 0.96 grey against a 1.0 white normal colour, about a
///     4% luminance difference, so even once focus existed it was invisible.
///
/// Both are fixed here in code rather than in the scenes: the two platform projects hold
/// separate copies of every scene, and scene edits are what have repeatedly diverged.
///
/// WHAT THIS DOES NOT DO: screen readers. Unity's accessibility module targets iOS
/// VoiceOver and Android TalkBack; it does not expose a UI Automation or NSAccessibility
/// tree on desktop, so NVDA, JAWS and macOS VoiceOver see VIP-Sim as one blank window. That
/// needs a native plugin per platform and cannot be claimed until it is tested with a real
/// screen reader. docs/ACCESSIBILITY.md states this plainly rather than leaving it implied.
/// </summary>
public class VipSimAccessibility : MonoBehaviour
{
    [Tooltip("Opens the accessibility settings inside the F1 panel.")]
    public KeyCode settingsKey = KeyCode.F2;

    private static VipSimAccessibility _instance;

    private readonly List<Selectable> _order = new List<Selectable>();
    private float _nextRebuild;

    // Base CanvasScaler values, captured once so repeated scaling compounds from the
    // authored layout rather than from the last scaled result.
    private readonly Dictionary<CanvasScaler, Vector2> _baseReference = new Dictionary<CanvasScaler, Vector2>();
    private readonly Dictionary<CanvasScaler, float> _baseFactor = new Dictionary<CanvasScaler, float>();
    private float _appliedScale = -1f;

    /// <summary>True once the user has begun navigating by keyboard.</summary>
    public static bool KeyboardMode { get; private set; }

    /// <summary>Whether the accessibility section of the F1 panel is expanded.</summary>
    public static bool SettingsOpen { get; set; }

    public static void Install(GameObject host)
    {
        if (host.GetComponent<VipSimAccessibility>() == null)
            host.AddComponent<VipSimAccessibility>();
    }

    private void Awake() => _instance = this;

    private void Start() => ApplyUiScale();

    private void Update()
    {
        // The text-size setting is changed from IMGUI buttons, which have no change event,
        // so the scale is polled. Cheap: a float compare, and the work only runs on change.
        if (!Mathf.Approximately(_appliedScale, VipSimSkin.UserScale)) ApplyUiScale();

        if (Input.GetKeyDown(settingsKey))
        {
            SettingsOpen = !SettingsOpen;
            Debug.Log($"[Accessibility] settings panel {(SettingsOpen ? "opened" : "closed")}");
        }

        // Tab moves through the interface in reading order. Unity's navigation handles the
        // arrow keys but not Tab, which is the key most people reach for first -- and it is
        // the only one that gives a predictable order rather than a spatial guess.
        bool tab = Input.GetKeyDown(KeyCode.Tab);
        bool arrow = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                     Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow);

        if (tab || arrow)
        {
            if (!KeyboardMode)
            {
                KeyboardMode = true;
                Debug.Log("[Accessibility] keyboard navigation active; focus ring shown.");
            }

            // The entry point the scene never had. Without this first selection Unity's
            // navigation has nothing to move FROM and every key press does nothing.
            if (Current() == null)
            {
                FocusFirst();
                return;
            }

            if (tab) Step(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
        }

        // A click puts the mouse back in charge and retires the focus ring, so it does not
        // sit on screen confusing a mouse user.
        if (KeyboardMode && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            KeyboardMode = false;
    }


    /// <summary>
    /// Apply the user's text size to the scene UI -- the toolbar, window list and effect
    /// list -- not only to the IMGUI panels.
    ///
    /// Those are uGUI on scaled canvases, so the lever is the CanvasScaler rather than a
    /// font size. For a canvas that scales with screen size, a SMALLER reference resolution
    /// means each authored pixel covers more of the screen, so the reference is divided by
    /// the user's scale; for a constant-pixel canvas the scale factor is multiplied. Both
    /// are computed from the authored base values, captured on the first pass, so stepping
    /// the size up and down repeatedly always lands back on the original layout rather than
    /// drifting.
    /// </summary>
    public void ApplyUiScale()
    {
        float scale = VipSimSkin.UserScale;
        _appliedScale = scale;

        int touched = 0;
        foreach (var cs in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cs == null) continue;

            if (cs.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                if (!_baseReference.ContainsKey(cs)) _baseReference[cs] = cs.referenceResolution;
                cs.referenceResolution = _baseReference[cs] / scale;
                touched++;
            }
            else if (cs.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                if (!_baseFactor.ContainsKey(cs)) _baseFactor[cs] = cs.scaleFactor;
                cs.scaleFactor = _baseFactor[cs] * scale;
                touched++;
            }
        }

        if (touched > 0)
            Debug.Log($"[Accessibility] UI scale {scale:0.00} applied to {touched} canvas(es).");
    }

    private static GameObject CurrentGo() =>
        EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

    private static Selectable Current()
    {
        var go = CurrentGo();
        if (go == null || !go.activeInHierarchy) return null;
        var s = go.GetComponent<Selectable>();
        return s != null && s.IsInteractable() ? s : null;
    }

    /// <summary>Select the first control in reading order.</summary>
    public static void FocusFirst()
    {
        if (_instance == null) return;
        _instance.RebuildOrder(force: true);
        if (_instance._order.Count == 0)
        {
            Debug.Log("[Accessibility] nothing focusable on screen yet.");
            return;
        }
        _instance.Select(_instance._order[0]);
    }

    private void Step(int direction)
    {
        RebuildOrder(force: false);
        if (_order.Count == 0) return;

        int i = _order.IndexOf(Current());
        i = i < 0 ? 0 : (i + direction + _order.Count) % _order.Count;
        Select(_order[i]);
    }

    private void Select(Selectable s)
    {
        if (EventSystem.current == null || s == null) return;
        EventSystem.current.SetSelectedGameObject(s.gameObject);
        Debug.Log($"[Accessibility] focus -> {Path(s.transform)}");
    }

    private static string Path(Transform t)
    {
        var name = t.name;
        // One level of parent is usually what identifies a row; the leaf is often "Button".
        return t.parent != null ? t.parent.name + "/" + name : name;
    }

    /// <summary>
    /// Reading order: top to bottom, then left to right, from actual screen positions.
    ///
    /// Not the scene hierarchy order, which reflects how the UI was assembled rather than
    /// how it is read, and not Unity's Automatic navigation, which picks the nearest
    /// neighbour in a direction and so cannot produce a stable full traversal.
    /// </summary>
    private void RebuildOrder(bool force)
    {
        if (!force && Time.unscaledTime < _nextRebuild) return;
        _nextRebuild = Time.unscaledTime + 0.5f;

        _order.Clear();
        foreach (var s in FindObjectsByType<Selectable>(FindObjectsSortMode.None))
        {
            if (s == null || !s.gameObject.activeInHierarchy || !s.IsInteractable()) continue;
            if (s.navigation.mode == Navigation.Mode.None) continue;
            _order.Add(s);
        }

        _order.Sort((a, b) =>
        {
            var ra = ScreenRect(a);
            var rb = ScreenRect(b);
            // Rows first: treat centres within half a row height as the same row.
            float rowTolerance = Mathf.Max(ra.height, rb.height) * 0.5f;
            float dy = rb.center.y - ra.center.y;          // screen y is up; higher first
            if (Mathf.Abs(dy) > rowTolerance) return dy > 0 ? 1 : -1;
            return ra.center.x.CompareTo(rb.center.x);
        });
    }

    /// <summary>A Selectable's rect in screen pixels, origin bottom-left.</summary>
    private static Rect ScreenRect(Selectable s)
    {
        var rt = s.transform as RectTransform;
        if (rt == null) return new Rect(0, 0, 0, 0);

        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        var canvas = s.GetComponentInParent<Canvas>();
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    /// <summary>
    /// Draw the focus ring.
    ///
    /// Drawn here rather than configured per-Selectable: there are 77 of them across two
    /// projects, their m_SelectedColor was effectively invisible, and a colour tint alone
    /// signals focus by colour only -- which is precisely what this is meant to avoid. An
    /// outline is a shape cue, so it survives greyscale and colour vision deficiency.
    /// </summary>
    private void OnGUI()
    {
        if (!KeyboardMode) return;
        var s = Current();
        if (s == null) return;

        VipSimSkin.Ensure();
        var r = ScreenRect(s);
        if (r.width <= 0f || r.height <= 0f) return;

        // Screen space is y-up from the bottom; IMGUI is y-down from the top.
        var gui = new Rect(r.x, Screen.height - r.y - r.height, r.width, r.height);
        VipSimSkin.FocusRing(gui, 3f * Mathf.Max(1f, Screen.height / 1080f));
    }
}

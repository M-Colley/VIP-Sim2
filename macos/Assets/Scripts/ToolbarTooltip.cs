using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hover help for the toolbar icons.
///
/// The toolbar is six unlabelled 60x55 glyphs. Two of them (the gaze-source
/// toggle and calibration) now look similar, and none of them say what they do,
/// which for a tool aimed at designers learning about vision impairment is its
/// own small accessibility problem.
///
/// Deliberately not IMGUI. UnitEye's debug preview and VIP-Sim's diagnostics
/// overlay are both IMGUI and both paint over the entire simulation; a tooltip
/// that did the same would obscure the thing the user is inspecting. This drives
/// a normal uGUI TMP label instead, so it composites with the rest of the panel
/// and respects the overlay's own draw order.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ToolbarTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Text shown while the pointer is over this control.")]
    [TextArea(2, 4)]
    public string message = "";

    /// <summary>
    /// Shared label all tooltips write into. Static so a single label serves the
    /// whole toolbar rather than one per button, and so it survives buttons being
    /// added or removed.
    /// </summary>
    private static TMP_Text _label;
    private static GameObject _labelRoot;

    public static void Register(TMP_Text label)
    {
        _label = label;
        _labelRoot = label != null ? label.gameObject : null;
        if (_labelRoot != null) _labelRoot.SetActive(false);
    }

    private void Awake()
    {
        // Late binding: the label is created by the editor setup and may be resolved after
        // this component's Awake, so look it up by name once.
        //
        // NOT GameObject.Find. That skips inactive objects, and the label is inactive by
        // design -- it is only shown while the pointer is over a button, and Register()
        // deactivates it immediately. So the lookup returned null every time, _label stayed
        // null, and OnPointerEnter bailed out on its first line: hover help was set on every
        // button and never appeared on any of them.
        if (_label == null)
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (candidate == null || candidate.name != "ToolbarTooltipLabel") continue;

                // Exclude assets and prefab contents; only take the instance in the loaded
                // scene, which FindObjectsOfTypeAll does not filter for.
                if (!candidate.gameObject.scene.IsValid()) continue;

                Register(candidate);
                break;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_label == null || string.IsNullOrEmpty(message)) return;
        _label.text = message;
        if (_labelRoot != null) _labelRoot.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_labelRoot != null) _labelRoot.SetActive(false);
    }

    private void OnDisable()
    {
        if (_labelRoot != null) _labelRoot.SetActive(false);
    }
}

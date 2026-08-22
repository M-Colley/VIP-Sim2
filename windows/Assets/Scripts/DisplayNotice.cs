using UnityEngine;

/// <summary>
/// Say which screen the overlay is on, when it is not the only one.
///
/// VIP-Sim remembers the display it was last used on and moves there at startup. That is
/// the right behaviour and it is also disconcerting: the simulation appears on a monitor
/// the user may not be looking at, over applications that are not the ones they meant, and
/// nothing on screen says what happened or how to undo it. The control to move it exists --
/// F3, and a button in the F1 panel -- but neither is anywhere the user would look while
/// wondering why the overlay is on the wrong screen.
///
/// So the overlay says it, once, for a few seconds, on the screen it has moved to.
/// </summary>
public class DisplayNotice : MonoBehaviour
{
    private const float ShowForSeconds = 6f;

    private float _until;
    private string _text;
    private GUIStyle _style;

    public static void Install(GameObject host)
    {
        if (host.GetComponent<DisplayNotice>() == null)
            host.AddComponent<DisplayNotice>();
    }

    /// <summary>Show the notice again -- called after a deliberate move, too.</summary>
    public static void Show()
    {
        var n = FindAnyObjectByType<DisplayNotice>(FindObjectsInactive.Include);
        if (n != null) n.Begin();
    }

    private void Start()
    {
        // Only worth saying on a machine with somewhere else to be.
        if (DisplaySwitcher.DisplayCount > 1) Begin();
        else enabled = false;
    }

    private void Begin()
    {
        _text = $"VIP-Sim is on {DisplaySwitcher.Summary}.   Press F3 to move it to the next screen.";
        _until = Time.unscaledTime + ShowForSeconds;
        enabled = true;
    }

    private void OnGUI()
    {
        if (Time.unscaledTime > _until) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.018f, 14f, 34f)),
                wordWrap = false,
            };
            _style.normal.textColor = Color.white;
        }

        var size = _style.CalcSize(new GUIContent(_text));
        float pad = size.y * 0.8f;
        float w = size.x + pad * 2f;
        float h = size.y + pad;

        // Top centre: away from the toolbar, and the one place on a fullscreen overlay that
        // is not covering the thing the user is trying to look at.
        var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.04f, w, h);

        // Fades out rather than vanishing, so it does not read as a glitch.
        float left = _until - Time.unscaledTime;
        float alpha = Mathf.Clamp01(left / 1.5f);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f * alpha);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(rect, _text, _style);
        GUI.color = prev;
    }
}

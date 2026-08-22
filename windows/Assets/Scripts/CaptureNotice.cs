using UnityEngine;

/// <summary>
/// Say when the window being captured is not on the screen VIP-Sim is overlaying.
///
/// The simulation draws the captured window where that window actually is. If it is on
/// another monitor, "where it actually is" is off the edge of the overlay, so the correct
/// result of a correct capture is a blank screen -- and there is nothing anywhere to say
/// why. It reads exactly like the capture being broken, and that is how it was reported:
/// two windows showing "nothing" and a third that appeared but wrong, all in one session,
/// on a two-monitor machine.
///
/// The remedy is not to force the image on screen anyway. Drawing a window over a screen it
/// is not on would misplace every click relative to the content, which is the fault the 1:1
/// placement exists to prevent. What is missing is the sentence, so this is that sentence.
///
/// Shown for as long as the condition holds, rather than as a timed toast: it describes a
/// state the user is still in, and a message that vanishes while the screen is still blank
/// is worse than none.
/// </summary>
public class CaptureNotice : MonoBehaviour
{
    /// <summary>Below this share of the window overlapping the overlay, say something.</summary>
    private const float OffScreenBelow = 0.15f;

    /// <summary>
    /// How long the condition must hold before the notice appears. Dragging a window
    /// between monitors passes through this state, and a message that flashes during a
    /// drag is noise.
    /// </summary>
    private const float SettleSeconds = 1f;

    private static string _title;
    private static float _fraction = 1f;
    private static float _lastReport = -999f;

    private float _offScreenSince = -1f;
    private GUIStyle _style;

    public static void Install(GameObject host)
    {
        if (host.GetComponent<CaptureNotice>() == null)
            host.AddComponent<CaptureNotice>();
    }

    /// <summary>
    /// Called by the capture placement every frame it positions a window. Not calling it --
    /// because nothing is captured -- lets the notice expire on its own, so there is no
    /// separate "stop" to forget.
    /// </summary>
    public static void Report(string title, float fractionOnScreen)
    {
        _title = title;
        _fraction = fractionOnScreen;
        _lastReport = Time.unscaledTime;
    }

    private void Update()
    {
        // Stale means nothing is being captured any more.
        bool live = Time.unscaledTime - _lastReport < 0.5f;
        bool off = live && _fraction < OffScreenBelow;

        if (!off) _offScreenSince = -1f;
        else if (_offScreenSince < 0f) _offScreenSince = Time.unscaledTime;
    }

    private void OnGUI()
    {
        if (_offScreenSince < 0f || Time.unscaledTime - _offScreenSince < SettleSeconds) return;

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

        string name = string.IsNullOrEmpty(_title) ? "That window" : $"'{_title}'";
        string text = DisplaySwitcher.DisplayCount > 1
            ? $"{name} is on another screen.   VIP-Sim is on {DisplaySwitcher.Summary} — press F3 to move it there."
            : $"{name} is off screen, so there is nothing to simulate.";

        var size = _style.CalcSize(new GUIContent(text));
        float pad = size.y * 0.8f;
        float w = size.x + pad * 2f;
        float h = size.y + pad;

        // Below the display notice, which occupies the same corner of the screen for its
        // first few seconds. Two messages stacked read as one; two overlapping read as a bug.
        var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.11f, w, h);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(rect, text, _style);
        GUI.color = prev;
    }
}

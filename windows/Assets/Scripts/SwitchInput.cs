using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toolbar button that switches the gaze source between the webcam eye tracker and
/// the mouse.
///
/// The icon is a readout of GazeTracker, never a second copy of the state. It used to
/// be both: onButtonClick decided what to switch to by asking which sprite was
/// currently showing, and nothing ever reconciled that with the tracker. The scene
/// ships with gazeSource = UnitEye and the mouse sprite assigned to the button, so
/// VIP-Sim started up displaying "mouse" while gaze tracking was actually live, and
/// the first press only corrected the icon -- you had to press the button twice to
/// change anything.
/// </summary>
public class SwitchInput : MonoBehaviour
{
    [SerializeField]
    private Sprite _spriteMouse;
    [SerializeField]
    private Sprite _spriteEye;
    [SerializeField]
    private Image _image;
    [SerializeField]
    private GazeTracker _tracker;
    [SerializeField]
    private FirestoreRESTManager logger;

    // Deliberately not a valid GazeSource, so the first LateUpdate always syncs.
    private GazeTracker.GazeSource _shown = (GazeTracker.GazeSource)(-1);

    /// <summary>
    /// Keep the icon honest. Polling rather than syncing once at startup because the
    /// gaze source can also change without anyone pressing this button: GazeTracker
    /// falls back to the mouse on its own when the webcam cannot be opened, and an
    /// icon still claiming eye tracking after that would be actively misleading.
    /// </summary>
    private void LateUpdate()
    {
        if (_tracker != null && _tracker.gazeSource != _shown) Refresh();
    }

    public void onButtonClick()
    {
        if (_tracker == null) return;

        bool useEye = _tracker.gazeSource != GazeTracker.GazeSource.UnitEye;
        _tracker.gazeSource = useEye ? GazeTracker.GazeSource.UnitEye : GazeTracker.GazeSource.Mouse;
        Refresh();

        // Null-guarded: the logger is optional, and an unassigned reference used to
        // throw here and take the whole toggle down with it.
        if (logger != null) logger.OnButtonClick(useEye ? "eye" : "mouse");
    }

    private void Refresh()
    {
        if (_image == null || _tracker == null) return;

        _shown = _tracker.gazeSource;
        _image.sprite = _shown == GazeTracker.GazeSource.UnitEye ? _spriteEye : _spriteMouse;
    }
}

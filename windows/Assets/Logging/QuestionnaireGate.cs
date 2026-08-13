using UnityEngine;

/// <summary>
/// Keeps the end-of-session questionnaire panel closed unless study
/// instrumentation is switched on.
///
/// Why a component on the panel rather than a reference from FirestoreRESTManager:
/// the panel is activated by a UnityEvent wired in the scene (a button's onClick
/// calling SetActive on this GameObject). Nothing in code performs that
/// activation, so there is no call site to guard -- a check in
/// FirestoreRESTManager.Start() runs long before the button is ever pressed and
/// cannot stop it. Sitting on the panel and vetoing its own OnEnable intercepts
/// every activation path, whoever triggers it.
///
/// Placed on the panel that is currently named "Feedback" in VIP_SIM.unity.
/// </summary>
[DisallowMultipleComponent]
public class QuestionnaireGate : MonoBehaviour
{
    [Tooltip("The manager that owns the enableSessionQuestionnaire setting. " +
             "Resolved automatically if left empty.")]
    [SerializeField] private FirestoreRESTManager manager;

    [Tooltip("Restores overlay click-through when the panel is vetoed, so the app " +
             "does not stay in its non-click-through feedback state.")]
    [SerializeField] private TransparentWindow transparentWindow;

    private void OnEnable()
    {
        if (manager == null)
            manager = FindAnyObjectByType<FirestoreRESTManager>(FindObjectsInactive.Include);

        // If the manager is genuinely absent, fail open: better to show the
        // questionnaire than to silently swallow it in a study session.
        if (manager == null || manager.QuestionnaireEnabled) return;

        if (transparentWindow == null)
            transparentWindow = FindAnyObjectByType<TransparentWindow>(FindObjectsInactive.Include);

        // The button that opens this panel also puts the overlay into its
        // non-click-through "feedback" state. Undo that, or the whole overlay
        // would keep swallowing clicks with nothing visible to click on.
        if (transparentWindow != null)
            transparentWindow.disableFeedbackState();

        gameObject.SetActive(false);

        // Then finish what the user actually asked for.
        //
        // This panel is the end-of-session screen, and the application's exit path
        // runs THROUGH it: the quit button opens this panel, and Application.Quit()
        // only happens once the questionnaire is submitted. Simply hiding the panel
        // therefore severed the only way to close the app -- it had to be killed
        // from Task Manager, because it is a topmost click-through overlay with no
        // title bar to close.
        //
        // With the questionnaire disabled, OnEndSessionButtonPressed() collects and
        // uploads nothing and just exits, so this preserves the intent of pressing
        // the button rather than swallowing it.
        Debug.Log("[QuestionnaireGate] Questionnaire disabled; exiting without it.");
        manager.OnEndSessionButtonPressed();
    }
}

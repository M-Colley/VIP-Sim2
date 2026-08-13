using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Adds a "Calibrate Eye Tracking" button to the settings UI.
    ///
    /// Calibration was reachable only via the F9 hotkey, which nobody discovers.
    /// It is the single biggest lever on gaze quality -- without it the pipeline
    /// falls back to raw, uncalibrated gaze -- so it needs to be visible.
    ///
    /// The button is created by CLONING the existing NextCam button rather than
    /// being built from scratch. NextCam already lives in the settings panel and
    /// already carries the project's button styling, font, sizing and anchoring,
    /// so cloning inherits all of it. Building a button from first principles
    /// would mean guessing at colours, fonts and RectTransform values, which is
    /// exactly the kind of blind UI work that produces something misaligned.
    ///
    /// Idempotent.
    /// </summary>
    public static class CalibrationButtonSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";
        private const string ButtonName = "CalibrateGazeButton";
        private const string TemplateName = "NextCam";
        // Calibration belongs beside the gaze-source toggle, not buried in the
        // webcam sub-menu: WebcamMenu is only meaningful once eye tracking is
        // selected, so a calibration button parented there is invisible exactly
        // when someone is looking for it. MouseEyeSwitch is the control that
        // turns eye tracking on, which is the moment calibration becomes relevant.
        private const string PreferredNeighbourName = "MouseEyeSwitch";

        [MenuItem("VIP-Sim/Add calibration button to settings")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var all = Resources.FindObjectsOfTypeAll<Transform>()
                               .Where(t => t.gameObject.scene == scene)
                               .Select(t => t.gameObject)
                               .ToList();

            // Repair rather than skip: an existing button may be in the wrong
            // parent from an earlier run, and "already present" is useless if the
            // user cannot see it. Removing and recreating re-applies the current
            // placement rules.
            foreach (var stale in all.Where(g => g.name == ButtonName).ToList())
            {
                Debug.Log($"CALIBRATION_BUTTON: removing existing button under " +
                          $"'{stale.transform.parent?.name}' to re-place it.");
                Object.DestroyImmediate(stale);
            }
            all = all.Where(g => g != null).ToList();

            var template = all.FirstOrDefault(g => g.name == TemplateName);
            if (template == null)
            {
                Debug.LogError($"CALIBRATION_BUTTON_FAILED: template '{TemplateName}' not found.");
                return;
            }

            var tracker = Object.FindObjectsByType<GazeTracker>(FindObjectsInactive.Include).FirstOrDefault();
            if (tracker == null)
            {
                Debug.LogError("CALIBRATION_BUTTON_FAILED: no GazeTracker to bind to.");
                return;
            }

            // Style comes from NextCam (fonts, colours, sizing); placement comes
            // from the gaze-source toggle, so the two concerns stay separate.
            var neighbour = all.FirstOrDefault(g => g.name == PreferredNeighbourName);
            var parent = neighbour != null ? neighbour.transform.parent : template.transform.parent;

            var clone = Object.Instantiate(template, parent);
            clone.name = ButtonName;

            // Sit directly below the template rather than on top of it. Offsetting
            // by the template's own height keeps whatever layout convention the
            // panel already uses instead of inventing coordinates.
            var anchorSrc = (neighbour != null ? neighbour : template).GetComponent<RectTransform>();
            var dstRect = clone.GetComponent<RectTransform>();
            if (anchorSrc != null && dstRect != null)
            {
                // Inherit the neighbour's anchoring so the button follows the same
                // resolution behaviour as the control it sits under, then drop one
                // row using its height rather than an invented offset.
                dstRect.anchorMin = anchorSrc.anchorMin;
                dstRect.anchorMax = anchorSrc.anchorMax;
                dstRect.pivot = anchorSrc.pivot;
                dstRect.anchoredPosition = anchorSrc.anchoredPosition
                                           - new Vector2(0f, anchorSrc.rect.height * 1.15f);
            }

            // Label sized to the button. MouseEyeSwitch lives in the title bar and
            // is a ~51px icon-sized control, so the clone inherits those dimensions
            // and a full "Calibrate Eye Tracking" string would simply overflow.
            // Short label on the button, full description in the tooltip-equivalent
            // name so it is still discoverable in the hierarchy.
            var rect = clone.GetComponent<RectTransform>();
            bool tiny = rect != null && rect.rect.width < 120f;
            string label = tiny ? "Cal" : "Calibrate Eye Tracking";

            foreach (var tmp in clone.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.text = label;
                if (tiny) tmp.enableAutoSizing = true;
            }
            foreach (var txt in clone.GetComponentsInChildren<Text>(true))
            {
                txt.text = label;
                if (tiny) txt.resizeTextForBestFit = true;
            }

            var button = clone.GetComponent<Button>() ?? clone.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                Debug.LogError("CALIBRATION_BUTTON_FAILED: clone has no Button component.");
                Object.DestroyImmediate(clone);
                return;
            }

            // The clone inherited NextCam's listeners; drop them before binding ours,
            // or pressing Calibrate would also cycle the webcam.
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            button.onClick.RemoveAllListeners();

            UnityEventTools.AddPersistentListener(button.onClick, tracker.StartCalibration);

            EditorUtility.SetDirty(clone);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            var r = clone.GetComponent<RectTransform>();
            Debug.Log($"CALIBRATION_BUTTON_OK: '{ButtonName}' under '{clone.transform.parent?.name}' " +
                      $"at anchoredPosition {r?.anchoredPosition} size {r?.rect.size}, " +
                      $"bound to GazeTracker.StartCalibration. " +
                      $"If it is not where you want it, drag it in the Editor -- only the position " +
                      $"needs changing, the binding is already correct.");
        }
    }
}

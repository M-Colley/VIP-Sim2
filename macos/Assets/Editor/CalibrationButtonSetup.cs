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

        [MenuItem("VIP-Sim/Add calibration button to settings")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var all = Resources.FindObjectsOfTypeAll<Transform>()
                               .Where(t => t.gameObject.scene == scene)
                               .Select(t => t.gameObject)
                               .ToList();

            if (all.Any(g => g.name == ButtonName))
            {
                Debug.Log("CALIBRATION_BUTTON_SKIPPED: already present.");
                return;
            }

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

            var clone = Object.Instantiate(template, template.transform.parent);
            clone.name = ButtonName;

            // Sit directly below the template rather than on top of it. Offsetting
            // by the template's own height keeps whatever layout convention the
            // panel already uses instead of inventing coordinates.
            var srcRect = template.GetComponent<RectTransform>();
            var dstRect = clone.GetComponent<RectTransform>();
            if (srcRect != null && dstRect != null)
            {
                dstRect.anchoredPosition = srcRect.anchoredPosition
                                           - new Vector2(0f, srcRect.rect.height * 1.2f);
            }

            // Label: cover both TMP and legacy Text so this does not depend on
            // which the template happens to use.
            foreach (var tmp in clone.GetComponentsInChildren<TMP_Text>(true))
                tmp.text = "Calibrate Eye Tracking";
            foreach (var txt in clone.GetComponentsInChildren<Text>(true))
                txt.text = "Calibrate Eye Tracking";

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

            Debug.Log($"CALIBRATION_BUTTON_OK: cloned '{TemplateName}' as '{ButtonName}' under " +
                      $"'{clone.transform.parent?.name}', bound to GazeTracker.StartCalibration.");
        }
    }
}

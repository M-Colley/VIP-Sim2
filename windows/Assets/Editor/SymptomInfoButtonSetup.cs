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
    /// Adds an info button to the toolbar that opens the symptom reference.
    ///
    /// Cloned from MouseEyeSwitch rather than built from scratch, the same approach the
    /// calibration button uses: the clone inherits the toolbar's styling, sizing and
    /// anchoring instead of having them guessed at. Building from first principles is what
    /// put an earlier button at (0,-63), outside a 220x100 bar and clipped from view.
    ///
    /// The inherited SwitchInput is stripped. That component keeps its icon in sync with
    /// GazeTracker every frame, so left in place it would repaint this button's glyph with
    /// the eye sprite -- exactly what happened to the calibration button's crosshair.
    ///
    /// Idempotent.
    /// </summary>
    public static class SymptomInfoButtonSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";
        private const string ButtonName = "SymptomInfoButton";
        private const string TemplateName = "MouseEyeSwitch";

        [MenuItem("VIP-Sim/Add symptom info button")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var all = Resources.FindObjectsOfTypeAll<Transform>()
                               .Where(t => t.gameObject.scene == scene)
                               .Select(t => t.gameObject)
                               .ToList();

            foreach (var stale in all.Where(g => g.name == ButtonName).ToList())
                Object.DestroyImmediate(stale);
            all = all.Where(g => g != null).ToList();

            var template = all.FirstOrDefault(g => g.name == TemplateName);
            if (template == null)
            {
                Debug.LogError($"SYMPTOM_INFO_FAILED: template '{TemplateName}' not found.");
                return;
            }

            // The panel component lives on whatever carries VipSimDiagnostics -- an
            // always-present object with an OnGUI already, so no new scene object is needed.
            var host = all.FirstOrDefault(g => g.GetComponent<VipSimDiagnostics>() != null)
                       ?? all.FirstOrDefault(g => g.name == "Canvas");
            if (host == null)
            {
                Debug.LogError("SYMPTOM_INFO_FAILED: no host object for the panel component.");
                return;
            }

            var info = host.GetComponent<SymptomInfo>() ?? host.AddComponent<SymptomInfo>();

            var clone = Object.Instantiate(template, template.transform.parent);
            clone.name = ButtonName;
            clone.transform.SetSiblingIndex(0); // leftmost: it explains what the rest does

            foreach (var stray in clone.GetComponentsInChildren<SwitchInput>(true))
                Object.DestroyImmediate(stray);

            foreach (var tmp in clone.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.text = "?";
                tmp.enableAutoSizing = true;
            }
            foreach (var txt in clone.GetComponentsInChildren<Text>(true))
            {
                txt.text = "?";
                txt.resizeTextForBestFit = true;
            }

            // The clone carries MouseEyeSwitch's icon; clear it so the "?" is legible rather
            // than sitting on top of an eye.
            var img = clone.GetComponent<Image>();
            if (img != null) img.sprite = null;

            var button = clone.GetComponent<Button>() ?? clone.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                Debug.LogError("SYMPTOM_INFO_FAILED: clone has no Button.");
                Object.DestroyImmediate(clone);
                return;
            }

            // Drop the inherited listeners before binding: otherwise pressing this would
            // also toggle the gaze source.
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            button.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(button.onClick, info.Toggle);

            var tip = clone.GetComponent<ToolbarTooltip>();
            if (tip != null) tip.message = "What do these symptoms mean? (F1)";

            EditorUtility.SetDirty(clone);
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"SYMPTOM_INFO_OK: '{ButtonName}' added under " +
                      $"'{clone.transform.parent?.name}', bound to SymptomInfo.Toggle on " +
                      $"'{host.name}'. F1 also opens it.");
        }

        public static void Run() { Setup(); EditorApplication.Exit(0); }

        /// <summary>
        /// Remove the toolbar button, keeping the SymptomInfo panel itself.
        ///
        /// TitleBarB is a fixed-width HorizontalLayoutGroup sized for six buttons. A seventh
        /// pushed the EXIT button off the end of the row -- the same fault that put the X
        /// "too far to the right" when the calibration button was added, reproduced exactly.
        /// Losing the exit control on a borderless always-on-top overlay is far worse than
        /// having no info button, even with Ctrl+Alt+Q as a fallback.
        ///
        /// The panel is unaffected: it is IMGUI on an existing object and needs no toolbar
        /// space at all. F1 opens it. Putting a button back means widening TitleBarB or
        /// moving a control out of it, and that should be done with eyes on the result.
        /// </summary>
        [MenuItem("VIP-Sim/Remove symptom info button")]
        public static void Remove()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var stale = Resources.FindObjectsOfTypeAll<Transform>()
                                 .Where(t => t.gameObject.scene == scene && t.name == ButtonName)
                                 .Select(t => t.gameObject)
                                 .ToList();

            foreach (var g in stale) Object.DestroyImmediate(g);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"SYMPTOM_INFO_REMOVED: {stale.Count} button(s); the F1 panel is unaffected.");
        }

        public static void RunRemove() { Remove(); EditorApplication.Exit(0); }
    }
}

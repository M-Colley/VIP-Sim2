using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Attaches <see cref="QuestionnaireGate"/> to the end-of-session questionnaire
    /// panel and wires FirestoreRESTManager.questionnairePanel at it.
    ///
    /// The panel is opened by a UnityEvent configured in the scene, so it cannot be
    /// gated from code without a component sitting on the panel itself. This does
    /// that setup once, in the scene, rather than asking every future maintainer to
    /// remember it. Idempotent.
    /// </summary>
    public static class QuestionnaireGateSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";

        // The panel's name in VIP_SIM.unity. Kept as a list so a rename does not
        // silently turn this into a no-op.
        private static readonly string[] PanelNames = { "Feedback", "SessionFeedback", "Questionnaire" };

        [MenuItem("VIP-Sim/Gate the session questionnaire panel")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var manager = Object.FindObjectsByType<FirestoreRESTManager>(FindObjectsInactive.Include)
                                .FirstOrDefault();
            if (manager == null)
            {
                Debug.LogError("QUESTIONNAIRE_GATE_FAILED: no FirestoreRESTManager in the scene.");
                return;
            }

            // Include inactive: the panel is inactive until the button opens it.
            var panel = Resources.FindObjectsOfTypeAll<Transform>()
                .Where(t => t.gameObject.scene == scene)
                .Select(t => t.gameObject)
                .FirstOrDefault(go => PanelNames.Contains(go.name));

            if (panel == null)
            {
                Debug.LogError("QUESTIONNAIRE_GATE_FAILED: no panel named any of: " +
                               string.Join(", ", PanelNames) + ". Was it renamed?");
                return;
            }

            int changed = 0;

            if (panel.GetComponent<QuestionnaireGate>() == null)
            {
                panel.AddComponent<QuestionnaireGate>();
                EditorUtility.SetDirty(panel);
                changed++;
            }

            var so = new SerializedObject(manager);
            var prop = so.FindProperty("questionnairePanel");
            if (prop != null && prop.objectReferenceValue != panel)
            {
                prop.objectReferenceValue = panel;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
                changed++;
            }

            if (changed == 0)
            {
                Debug.Log("QUESTIONNAIRE_GATE_SKIPPED: already wired.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"QUESTIONNAIRE_GATE_OK: gated '{panel.name}' ({changed} change(s)).");
        }
    }
}

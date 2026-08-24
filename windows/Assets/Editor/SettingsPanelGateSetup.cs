using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Mark which HideImpairmentSelection gates the per-effect parameter panel.
    ///
    /// There are two of them and they are configured identically apart from what they show:
    /// one shows the panel of parameters for the selected effect, the other shows the whole
    /// effect list. They also read the same slider -- the master Enable switch -- which is
    /// why closing a parameter panel used to switch the simulation off and take the list
    /// with it. Now only the parameter panel answers to the panel's own state, and this
    /// says which one that is.
    ///
    /// Decided from the object each instance shows rather than from the order they happen to
    /// sit on the GameObject, because component order is not a thing anyone should have to
    /// preserve by hand.
    /// </summary>
    public static class SettingsPanelGateSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";

        /// <summary>Objects that hold an effect's parameters, in either project.</summary>
        private static readonly string[] ParameterPanels = { "UICointainer", "UIContainer" };

        [MenuItem("VIP-Sim/Clean up/Mark the settings-panel gate")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int changed = 0, seen = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var gate in root.GetComponentsInChildren<HideImpairmentSelection>(true))
                {
                    seen++;
                    var so = new SerializedObject(gate);
                    var target = so.FindProperty("targetGameObject");
                    var flag = so.FindProperty("gatesSettingsPanel");
                    if (flag == null)
                    {
                        Debug.LogError("[SettingsPanelGateSetup] no gatesSettingsPanel field; " +
                                       "the script did not compile or is out of date.");
                        return;
                    }

                    var shown = target != null ? target.objectReferenceValue as GameObject : null;
                    string name = shown != null ? shown.name : "(nothing)";
                    bool wanted = shown != null && ParameterPanels.Contains(shown.name);

                    Debug.Log($"[SettingsPanelGateSetup] {gate.gameObject.name} shows '{name}' " +
                              $"-> gatesSettingsPanel={wanted}");

                    if (flag.boolValue == wanted) continue;
                    flag.boolValue = wanted;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed++;
                }
            }

            if (seen == 0)
            {
                Debug.LogError("[SettingsPanelGateSetup] found no HideImpairmentSelection at all.");
                return;
            }

            if (changed == 0)
            {
                Debug.Log("[SettingsPanelGateSetup] already correct; nothing written.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SettingsPanelGateSetup] set {changed} of {seen} instance(s) and saved.");
        }
    }
}

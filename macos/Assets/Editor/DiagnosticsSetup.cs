using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Puts <see cref="VipSimDiagnostics"/> in the scene so the shipped build can
    /// be measured without a Unity Profiler attachment.
    ///
    /// Attached to the GazeTracker's GameObject rather than a new one, so it is
    /// alongside the thing most likely to be under suspicion and cannot be lost
    /// by someone tidying up loose objects. Overlay defaults to off; F10 shows it,
    /// F11 runs the effect-chain A/B. Idempotent.
    /// </summary>
    public static class DiagnosticsSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";

        [MenuItem("VIP-Sim/Add performance diagnostics to scene")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (Object.FindObjectsByType<VipSimDiagnostics>(FindObjectsInactive.Include).Any())
            {
                Debug.Log("DIAGNOSTICS_SETUP_SKIPPED: already present.");
                return;
            }

            var host = Object.FindObjectsByType<GazeTracker>(FindObjectsInactive.Include)
                             .FirstOrDefault()?.gameObject;
            if (host == null)
            {
                Debug.LogError("DIAGNOSTICS_SETUP_FAILED: no GazeTracker to attach to.");
                return;
            }

            host.AddComponent<VipSimDiagnostics>();
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"DIAGNOSTICS_SETUP_OK: added VipSimDiagnostics to '{host.name}'.");
        }
    }
}

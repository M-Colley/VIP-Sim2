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

            var existing = Object.FindObjectsByType<VipSimDiagnostics>(FindObjectsInactive.Include)
                                 .FirstOrDefault();

            GameObject host;
            if (existing != null)
            {
                host = existing.gameObject;
            }
            else
            {
                host = Object.FindObjectsByType<GazeTracker>(FindObjectsInactive.Include)
                             .FirstOrDefault()?.gameObject;
                if (host == null)
                {
                    Debug.LogError("DIAGNOSTICS_SETUP_FAILED: no GazeTracker to attach to.");
                    return;
                }
                existing = host.AddComponent<VipSimDiagnostics>();
            }

            // Enforce the serialized values rather than relying on the C# field
            // initialisers. Unity serialises a component's values when it is added,
            // so changing a default later does NOT update instances already in the
            // scene -- which is exactly why periodic logging stayed off after the
            // default was changed from 0 to 5.
            if (existing.logIntervalSeconds <= 0f)
                existing.logIntervalSeconds = 5f;
            existing.showOverlay = false;

            // FrameRateController must actually be in the scene to own the frame
            // rate. Removing the three competing targetFrameRate assignments left
            // nothing setting it at all, so the player reported "target 30 fps"
            // (a leftover serialized value) and only hit 60 because vSync happened
            // to be enabled. A component that is written but never attached does
            // nothing.
            if (!Object.FindObjectsByType<FrameRateController>(FindObjectsInactive.Include).Any())
            {
                host.AddComponent<FrameRateController>();
                Debug.Log("DIAGNOSTICS_SETUP: added FrameRateController (was missing from the scene).");
            }

            EditorUtility.SetDirty(existing);
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"DIAGNOSTICS_SETUP_OK: added VipSimDiagnostics to '{host.name}'.");
        }
    }
}

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitEye;
using Mediapipe.Unity.FaceMesh;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Turns off UnitEye's debug drawing in the VIP-Sim scene.
    ///
    /// FaceMeshSolution draws a FULL-SCREEN mirrored webcam preview through IMGUI
    /// (`_drawPreview`, which defaults to true). IMGUI paints on top of everything
    /// cameras render, so in VIP-Sim that preview covered the entire simulation:
    /// the user saw their own face instead of the symptom overlay, with no way to
    /// dismiss it -- UnitEye's own toggle button is unclickable because the overlay
    /// is click-through everywhere outside VIP-Sim's panel.
    ///
    /// Doing this at runtime is not enough. GazeTracker sets IsRendering=false when
    /// it enables gaze, but HomulerGaze sets IsRendering=true again on its own
    /// (lines ~993 and ~1069, when calibration or the gaze UI finishes), which
    /// re-enables the preview. `_drawPreview` is the only switch nothing re-sets.
    ///
    /// It is a private [SerializeField], so it is written via SerializedObject and
    /// persisted in the scene -- deliberately, rather than patching the vendored
    /// uniteye package, so the package stays a clean upstream copy.
    /// </summary>
    public static class UnitEyeOverlaySettings
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";

        [MenuItem("VIP-Sim/Disable UnitEye debug overlays in scene")]
        public static void DisableDebugOverlays()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int changed = 0;

            // 1. The full-screen webcam preview.
            foreach (var fm in Object.FindObjectsByType<FaceMeshSolution>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(fm);
                var prop = so.FindProperty("_drawPreview");
                if (prop == null)
                {
                    Debug.LogError("UNITEYE_OVERLAY_FAILED: _drawPreview not found on FaceMeshSolution " +
                                   "(field renamed upstream?)");
                    return;
                }
                if (prop.boolValue)
                {
                    prop.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(fm);
                    changed++;
                }
            }

            // 2. HomulerGaze's own thumbnails, dot and IMGUI panel. These are public
            //    fields, so a plain assignment persists.
            foreach (var gaze in Object.FindObjectsByType<HomulerGaze>(FindObjectsInactive.Include))
            {
                if (!gaze.showEyes && !gaze.showFaceMesh && !gaze.drawDot &&
                    !gaze.showGazeUI && !gaze.visualizeAOI) continue;

                gaze.showEyes = false;       // webcam eye crops in the screen corners
                gaze.showFaceMesh = false;   // landmark dot overlay
                gaze.drawDot = false;        // UnitEye's gaze dot; VIP-Sim has visualiseGaze
                gaze.showGazeUI = false;     // IMGUI button panel
                gaze.visualizeAOI = false;   // AOI debug boxes
                EditorUtility.SetDirty(gaze);
                changed++;
            }

            if (changed == 0)
            {
                Debug.Log("UNITEYE_OVERLAY_SKIPPED: debug drawing already disabled.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"UNITEYE_OVERLAY_OK: disabled debug drawing on {changed} component(s).");
        }
    }
}

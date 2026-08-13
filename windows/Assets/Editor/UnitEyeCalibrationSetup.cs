using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitEye;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Makes gaze calibration possible in VIP-Sim, and reduces gaze latency.
    ///
    /// Calibration
    /// -----------
    /// HomulerGaze.LoadCalibration() bails immediately unless its serialized
    /// _calibrationScript is assigned, and VIP-Sim's scene never had a
    /// HomulerGazeCalibration component at all -- it came from UnitEye's own demo
    /// scenes, which VIP-Sim does not use. So calibration could not be started, and
    /// the pipeline logged "No RidgeRegression calibration found; using raw
    /// (uncalibrated) gaze" and ran on raw model output. Raw gaze is roughly
    /// head-pose-driven and drifts badly; almost all of UnitEye's accuracy comes
    /// from the per-user ridge regression fitted during calibration.
    ///
    /// Latency
    /// -------
    /// _asyncGpuReadback defaults to false, which makes every frame stall waiting
    /// for the eye-crop textures to come back from the GPU before inference can
    /// run. Enabling it pipelines the readback: gaze is published one frame later
    /// but the stall disappears. For a 60fps overlay that trade is clearly worth
    /// it -- a 16ms pipeline bubble every frame costs far more than 16ms of
    /// pipelining, because the bubble also blocks the simulation's own rendering.
    ///
    /// Idempotent.
    /// </summary>
    public static class UnitEyeCalibrationSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";

        [MenuItem("VIP-Sim/Set up gaze calibration + low latency")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var gaze = Object.FindObjectsByType<HomulerGaze>(FindObjectsInactive.Include).FirstOrDefault();
            if (gaze == null)
            {
                Debug.LogError("CALIBRATION_SETUP_FAILED: no HomulerGaze in the scene.");
                return;
            }

            int changed = 0;
            var so = new SerializedObject(gaze);

            // --- 1. Calibration component -------------------------------------
            var calibProp = so.FindProperty("_calibrationScript");
            if (calibProp == null)
            {
                Debug.LogError("CALIBRATION_SETUP_FAILED: _calibrationScript not found (renamed upstream?).");
                return;
            }

            if (calibProp.objectReferenceValue == null)
            {
                var calib = gaze.GetComponent<HomulerGazeCalibration>()
                            ?? gaze.gameObject.AddComponent<HomulerGazeCalibration>();

                // The calibration routine drives itself once enabled, so it must
                // start disabled or it would run the moment the scene loads.
                calib.enabled = false;

                if (calib.calibrationDot == null)
                {
                    // Shipped by UnitEye; loaded by name so this does not depend on
                    // a GUID that could change when the package is updated.
                    var dot = AssetDatabase.FindAssets("CalibrationDot t:Texture2D")
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Where(p => p.Contains("uniteye"))
                        .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                        .FirstOrDefault(t => t != null);

                    if (dot == null)
                        Debug.LogWarning("CALIBRATION_SETUP: no CalibrationDot texture found; " +
                                         "the calibration target will be invisible until one is assigned.");
                    else
                        calib.calibrationDot = dot;
                }

                calibProp.objectReferenceValue = calib;
                EditorUtility.SetDirty(calib);
                changed++;
            }

            // --- 2. Async GPU readback: force OFF -----------------------------
            // Enabling this looked like the obvious latency win and is actively
            // broken in UnitEye 1.1. With _asyncGpuReadback = true the player
            // throws every single frame:
            //
            //   InvalidOperationException: Cannot access the data as it is not available
            //     at AsyncGPUReadbackRequest.GetData[T]
            //     at UnitEye.HomulerEyeMURunner.PerformInference
            //     at UnitEye.NativeGazeProvider.Tick
            //
            // PerformInference reads the readback before it has completed, so gaze
            // stops updating entirely and the exception spam costs far more than
            // the stall it was meant to remove. Pinned to false here so the
            // setting cannot be flipped back on hopefully, and this comment
            // survives as the reason.
            var asyncProp = so.FindProperty("_asyncGpuReadback");
            if (asyncProp != null && asyncProp.boolValue)
            {
                asyncProp.boolValue = false;
                changed++;
            }

            if (changed > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gaze);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }

            var check = new SerializedObject(gaze);
            Debug.Log($"CALIBRATION_SETUP_OK: changes={changed} " +
                      $"calibrationScript={(check.FindProperty("_calibrationScript").objectReferenceValue != null ? "wired" : "MISSING")} " +
                      $"asyncGpuReadback={check.FindProperty("_asyncGpuReadback")?.boolValue}");
        }
    }
}

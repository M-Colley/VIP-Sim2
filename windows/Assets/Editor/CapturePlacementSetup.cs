using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Puts CaptureWindowPlacement on the WindowManager, beside the component it
    /// takes the camera from, and wires it to the same camera.
    ///
    /// It attaches to AlignBoxColliderWithCamera, not FitPlaneToCameraView: the
    /// latter has zero references in any scene or prefab, so an earlier version of
    /// this setup aimed at dead code and found nothing.
    /// </summary>
    public static class CapturePlacementSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";

        [MenuItem("VIP-Sim/Setup 1:1 capture placement")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var align = Resources.FindObjectsOfTypeAll<AlignBoxColliderWithCamera>()
                                 .FirstOrDefault(x => x.gameObject.scene == scene);
            if (align == null)
            {
                Debug.LogError("CAPTURE_PLACEMENT_FAILED: no AlignBoxColliderWithCamera in the scene.");
                return;
            }

            int changed = 0;
            var placement = align.GetComponent<CaptureWindowPlacement>();
            if (placement == null)
            {
                placement = align.gameObject.AddComponent<CaptureWindowPlacement>();
                changed++;
            }
            if (placement.targetCamera != align.camera)
            {
                placement.targetCamera = align.camera;
                changed++;
            }

            if (changed == 0)
            {
                Debug.Log("CAPTURE_PLACEMENT_SKIPPED: already configured.");
                return;
            }

            EditorUtility.SetDirty(placement);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"CAPTURE_PLACEMENT_OK: on '{align.gameObject.name}', camera " +
                      $"'{(placement.targetCamera != null ? placement.targetCamera.name : "<none>")}'.");
        }
    }
}

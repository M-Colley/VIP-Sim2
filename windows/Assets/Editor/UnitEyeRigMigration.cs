using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitEye;

namespace VipSim.EditorTools
{
    /// <summary>
    /// One-shot migration of the in-scene UnitEye rig from the Barracuda-era
    /// layout to the UnitEye 1.1 layout.
    ///
    /// Remapping the Gaze component's script GUID to HomulerGaze was enough to make
    /// the project compile, but not enough to make it run: HomulerGaze introduced a
    /// serialized field, _mediaPipeGO, that must point at a GameObject carrying
    /// FaceMeshSolution + WebCamSource. The old rig has no such child -- under
    /// Barracuda the inference ran inside HolisticPipeline with no scene
    /// representation -- so the built player started up and then logged:
    ///
    ///   UnitEye: gaze provider setup failed, disabling HomulerGaze on 'UnitEye'.
    ///   UnitEye: HomulerGaze._mediaPipeGO is not assigned.
    ///
    /// Rather than hand-editing scene YAML to fabricate that subtree, this replaces
    /// the whole rig with the upstream UnitEyeUsingHomulerMediapipe prefab, which
    /// already has it wired, and repoints GazeTracker.unitEye at the replacement.
    ///
    /// Idempotent: it does nothing if the rig is already correctly wired.
    /// </summary>
    public static class UnitEyeRigMigration
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";
        private const string PrefabGuid = "24a5d1cccb5291249a020c0f69815cf6"; // UnitEyeUsingHomulerMediapipe

        [MenuItem("VIP-Sim/Migrate UnitEye rig to 1.1")]
        public static void Migrate()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var gaze = Object.FindObjectsByType<HomulerGaze>(FindObjectsInactive.Include)
                             .FirstOrDefault();
            if (gaze == null)
            {
                Debug.LogError("UNITEYE_MIGRATION_FAILED: no HomulerGaze in the scene.");
                return;
            }

            var so = new SerializedObject(gaze);
            var mediaPipeProp = so.FindProperty("_mediaPipeGO");
            if (mediaPipeProp == null)
            {
                Debug.LogError("UNITEYE_MIGRATION_FAILED: _mediaPipeGO not found on HomulerGaze.");
                return;
            }

            if (mediaPipeProp.objectReferenceValue != null)
            {
                Debug.Log("UNITEYE_MIGRATION_SKIPPED: _mediaPipeGO already assigned.");
                return;
            }

            var prefabPath = AssetDatabase.GUIDToAssetPath(PrefabGuid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"UNITEYE_MIGRATION_FAILED: prefab not found at guid {PrefabGuid}.");
                return;
            }

            var oldRig = gaze.gameObject;
            var oldName = oldRig.name;
            var parent = oldRig.transform.parent;
            var localPos = oldRig.transform.localPosition;
            var localRot = oldRig.transform.localRotation;
            var localScale = oldRig.transform.localScale;
            var wasActive = oldRig.activeSelf;
            int siblingIndex = oldRig.transform.GetSiblingIndex();

            var newRig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            newRig.name = oldName;
            newRig.transform.SetParent(parent, false);
            newRig.transform.localPosition = localPos;
            newRig.transform.localRotation = localRot;
            newRig.transform.localScale = localScale;

            // Repoint every GazeTracker that referenced the old rig, so the
            // enable/disable of eye tracking still targets the right object.
            int repointed = 0;
            foreach (var tracker in Object.FindObjectsByType<GazeTracker>(FindObjectsInactive.Include))
            {
                if (tracker.unitEye != oldRig) continue;
                Undo.RecordObject(tracker, "Repoint UnitEye rig");
                tracker.unitEye = newRig;
                EditorUtility.SetDirty(tracker);
                repointed++;
            }

            Object.DestroyImmediate(oldRig);

            newRig.transform.SetSiblingIndex(siblingIndex);
            newRig.SetActive(wasActive);

            var newGaze = newRig.GetComponentInChildren<HomulerGaze>(true);
            var check = newGaze != null
                ? new SerializedObject(newGaze).FindProperty("_mediaPipeGO")?.objectReferenceValue
                : null;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            if (check == null)
            {
                Debug.LogError("UNITEYE_MIGRATION_FAILED: replacement rig still has _mediaPipeGO unassigned.");
                return;
            }

            Debug.Log($"UNITEYE_MIGRATION_OK: replaced rig '{oldName}', " +
                      $"_mediaPipeGO -> '{check.name}', repointed {repointed} GazeTracker reference(s).");
        }
    }
}

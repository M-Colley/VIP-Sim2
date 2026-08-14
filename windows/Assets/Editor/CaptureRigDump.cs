using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using uWindowCapture;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Prints the capture rig: which component drives the camera, the camera's
    /// projection, and the prefab the window textures are spawned from.
    ///
    /// FitPlaneToCameraView turned out to have zero references in any scene or
    /// prefab -- reasoning from its name would have aimed the "windows are always
    /// maximized" fix at dead code.
    /// </summary>
    public static class CaptureRigDump
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";

        [MenuItem("VIP-Sim/Dump capture rig")]
        public static void Dump()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var sb = new StringBuilder("CAPTURE_RIG_BEGIN\n");

            foreach (var a in Resources.FindObjectsOfTypeAll<AlignBoxColliderWithCamera>()
                                       .Where(x => x.gameObject.scene == scene))
            {
                sb.AppendLine($"AlignBoxColliderWithCamera on '{Path(a.transform)}' " +
                              $"active={a.gameObject.activeInHierarchy} enabled={a.enabled}");
                var cam = a.camera;
                sb.AppendLine($"   camera={(cam == null ? "<NULL>" : Path(cam.transform))}" +
                              (cam == null ? "" : $" ortho={cam.orthographic} size={cam.orthographicSize} " +
                                                  $"fov={cam.fieldOfView} pos={cam.transform.position}"));
                var box = a.GetComponentInChildren<BoxCollider>(true);
                sb.AppendLine($"   boxCollider={(box == null ? "<none>" : Path(box.transform) + " size=" + box.size)}");
                sb.AppendLine($"   children={a.transform.childCount}");
            }

            foreach (var m in Resources.FindObjectsOfTypeAll<UwcWindowTextureManager>()
                                       .Where(x => x.gameObject.scene == scene))
            {
                sb.AppendLine($"UwcWindowTextureManager on '{Path(m.transform)}' " +
                              $"active={m.gameObject.activeInHierarchy}");
                var so = new SerializedObject(m);
                var prefab = so.FindProperty("windowPrefab")?.objectReferenceValue as GameObject;
                sb.AppendLine($"   windowPrefab={(prefab == null ? "<NULL>" : AssetDatabase.GetAssetPath(prefab))}");
                if (prefab != null)
                {
                    var wt = prefab.GetComponent<UwcWindowTexture>();
                    if (wt != null)
                        sb.AppendLine($"   UwcWindowTexture scaleControl={wt.scaleControlType} " +
                                      $"scalePer1000Pixel={wt.scalePer1000Pixel} " +
                                      $"updateScaleForcely={wt.updateScaleForcely}");
                    var mf = prefab.GetComponent<MeshFilter>();
                    sb.AppendLine($"   mesh={(mf == null || mf.sharedMesh == null ? "<none>" : mf.sharedMesh.name + " bounds=" + mf.sharedMesh.bounds.size)}");
                    sb.AppendLine($"   prefab localScale={prefab.transform.localScale}");
                }
            }

            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"CAMERA '{Path(c.transform)}' ortho={c.orthographic} size={c.orthographicSize} " +
                              $"fov={c.fieldOfView} depth={c.depth} active={c.gameObject.activeInHierarchy}");

            sb.AppendLine("CAPTURE_RIG_END");
            Debug.Log(sb.ToString());
        }

        private static string Path(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}

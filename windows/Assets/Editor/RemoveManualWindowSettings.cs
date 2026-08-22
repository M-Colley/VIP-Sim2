using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Delete the manual window-size Settings dialog.
    ///
    /// It existed for one reason, printed on its own face: "If the automatic detection of
    /// the window size was unsuccessful, you can adjust it manually." Automatic detection
    /// is no longer unsuccessful -- the capture is placed 1:1 from the window's own painted
    /// bounds, and now in the right coordinate space on a second monitor -- so the dialog is
    /// a workaround for a fault that no longer exists.
    ///
    /// It is not merely redundant, it is harmful. ZoomSettings sets settingsOpen in Load()
    /// and clears it only in Abort(), while Apply closes the window without clearing it --
    /// so after a single Apply, Update() keeps calling Save() every 0.1s for the rest of the
    /// session, rewriting WindowManager's position and the capture plane's collider from
    /// input fields nobody can see. Every automatic placement is then overwritten ten times
    /// a second by a value the user set once and cannot get back to. A user log showed
    /// exactly that: a stale x offset of -1.28 world units, 1280 pixels, still being applied.
    ///
    /// Done as an editor script rather than by hand because the removal has to be atomic.
    /// feedbackState is what suppresses click-through, and in this scene it is SET by the
    /// toolbar gear and CLEARED only from inside the dialog it opens. Remove the dialog and
    /// keep the button and the overlay latches non-click-through the first time anyone
    /// presses it -- the whole desktop stops accepting clicks, with nothing left on screen
    /// to undo it. Both halves go, or neither does.
    /// </summary>
    public static class RemoveManualWindowSettings
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";

        /// <summary>Root objects to delete outright, by their path from the scene root.</summary>
        private static readonly string[] Doomed =
        {
            "Canvas/SettingsMenu",           // the dialog itself
            "Canvas/SettingsWarning",        // its unsaved-changes modal
            "Canvas/Menu/TitleBar/TitleBarB/settings",  // Windows: the toolbar gear
            "Canvas/Menu/TitleBar/TitleBarB/Settings",  // macOS: same button, capital S
        };

        /// <summary>
        /// Components to strip, by type name. Named rather than typed so this still compiles
        /// and runs after the scripts themselves have been deleted from the project.
        /// </summary>
        private static readonly string[] DoomedComponents =
        {
            "ZoomSettings",     // Windows: drives the three fields and nothing else
            "LoadZoomSettings", // the old serialized type name, in case anything still has it

            // NOT MacScale. It looks like the macOS equivalent and it is not: its Update
            // writes the capture plane's localScale every frame, with a NEGATIVE x, and the
            // plane is authored at (1,1,1). That flip is the only thing standing between
            // macOS and a mirror-imaged capture, so the component stays and only its
            // dependency on the deleted input fields is removed. There is no Mac here to
            // check a change to it on, which is exactly why it is not being made.
        };

        [MenuItem("VIP-Sim/Clean up/Remove the manual window-size dialog")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int removed = 0;

            foreach (var path in Doomed)
            {
                var go = Find(scene.GetRootGameObjects(), path);
                if (go == null) continue;
                Debug.Log($"[RemoveManualWindowSettings] deleting {path}");
                Object.DestroyImmediate(go);
                removed++;
            }

            // Components second. Destroying the objects above can take some of these with
            // them, so this only catches what lives elsewhere -- ZoomSettings sits on
            // OverlayManager, not inside the dialog.
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true).ToList())
                {
                    if (mb == null) continue;
                    if (!DoomedComponents.Contains(mb.GetType().Name)) continue;
                    Debug.Log($"[RemoveManualWindowSettings] removing {mb.GetType().Name} " +
                              $"from {mb.gameObject.name}");
                    Object.DestroyImmediate(mb);
                    removed++;
                }
            }

            if (removed == 0)
            {
                Debug.Log("[RemoveManualWindowSettings] nothing to remove; already clean.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RemoveManualWindowSettings] removed {removed} object(s)/component(s) " +
                      $"and saved {ScenePath}.");
        }

        /// <summary>
        /// Resolve a slash-separated path from the scene roots. Transform.Find does not
        /// search inactive children of the root it is called on in every Unity version, and
        /// both of these panels ship inactive, so the walk is explicit.
        /// </summary>
        private static GameObject Find(IEnumerable<GameObject> roots, string path)
        {
            var parts = path.Split('/');
            var current = roots.FirstOrDefault(r => r.name == parts[0]);
            if (current == null) return null;

            for (int i = 1; i < parts.Length; i++)
            {
                Transform next = null;
                foreach (Transform child in current.transform)
                {
                    if (child.name != parts[i]) continue;
                    next = child;
                    break;
                }
                if (next == null) return null;
                current = next.gameObject;
            }
            return current;
        }
    }
}

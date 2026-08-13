using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Dumps the UI hierarchy with active state, screen rect and interactability.
    ///
    /// Placing the calibration button by reasoning about names failed three times:
    /// WebcamMenu turned out to be a sub-menu, TitleBarB turned out to be a 51px
    /// icon strip. Guessing at a fourth location would be the same mistake again.
    /// This prints the ground truth instead -- which containers actually exist,
    /// which are active, and where they sit on screen -- so the decision is made
    /// from data rather than from names.
    /// </summary>
    public static class UiHierarchyDump
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";

        [MenuItem("VIP-Sim/Dump UI hierarchy")]
        public static void Dump()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine("UI_DUMP_BEGIN");

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)
                                         .OrderBy(c => c.name))
            {
                sb.AppendLine($"CANVAS '{canvas.name}' active={canvas.gameObject.activeInHierarchy}");
                Walk(canvas.transform, 1, sb);
            }

            sb.AppendLine("UI_DUMP_END");
            Debug.Log(sb.ToString());
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            // Deep hierarchies are mostly label/icon children that add noise.
            if (depth > 4) return;

            foreach (Transform child in t)
            {
                var go = child.gameObject;
                var rt = child as RectTransform;
                var btn = go.GetComponent<Button>();
                var hasText = go.GetComponentInChildren<TMPro.TMP_Text>(true) != null;

                // Only report containers and interactive things; plain images and
                // label children are not candidate parents for a new control.
                bool interesting = btn != null || child.childCount > 0 || hasText;
                if (interesting)
                {
                    string size = rt != null ? $"{rt.rect.width:F0}x{rt.rect.height:F0}" : "-";
                    string pos = rt != null ? $"({rt.anchoredPosition.x:F0},{rt.anchoredPosition.y:F0})" : "-";
                    sb.AppendLine($"{new string(' ', depth * 2)}{go.name}" +
                                  $"  active={go.activeSelf}/{go.activeInHierarchy}" +
                                  $"  size={size} pos={pos}" +
                                  $"  children={child.childCount}" +
                                  (btn != null ? "  [BUTTON]" : ""));
                }

                Walk(child, depth + 1, sb);
            }
        }
    }
}

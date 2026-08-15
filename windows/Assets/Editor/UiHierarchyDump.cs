using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Prints the UI hierarchy with sizes, so layout work is done against what is
    /// actually in the scene rather than against a guess. Blind UI editing is what
    /// produced the earlier round of misplaced buttons.
    /// </summary>
    public static class UiHierarchyDump
    {
        [MenuItem("VIP-Sim/Dump UI hierarchy")]
        public static void Dump()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/VIP_SIM.unity", OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var sb = new StringBuilder("UIDUMP\n");
            foreach (var r in roots)
                foreach (var c in r.GetComponentsInChildren<Canvas>(true))
                    Walk(c.transform, 0, sb);
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            if (depth > 6) return;
            var rt = t as RectTransform;
            string size = rt != null ? $"{rt.rect.width:F0}x{rt.rect.height:F0}@{rt.anchoredPosition.x:F0},{rt.anchoredPosition.y:F0}" : "-";
            var bits = t.GetComponents<Component>()
                        .Where(c => c != null && !(c is RectTransform) && !(c is CanvasRenderer))
                        .Select(c => c.GetType().Name);
            sb.AppendLine($"UIDUMP {new string(' ', depth * 2)}{t.name} [{size}] active={t.gameObject.activeSelf} {{{string.Join(",", bits)}}}");
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), depth + 1, sb);
        }
    }
}

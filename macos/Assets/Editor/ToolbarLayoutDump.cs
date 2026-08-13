using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Prints the toolbar row's real geometry and the clickthrough panel reference.
    ///
    /// UiHierarchyDump showed every TitleBarB child sitting at (0,0) with a 220px
    /// parent, which means a layout component is driving them and the sizes in that
    /// dump are not the whole story. Adding a seventh button pushed the exit button
    /// out of the row, and the same overflow can put it outside the rectangle
    /// TransparentWindow uses to decide where clicks are captured -- so the panel
    /// reference is printed here too rather than assumed.
    /// </summary>
    public static class ToolbarLayoutDump
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";

        [MenuItem("VIP-Sim/Dump toolbar layout")]
        public static void Dump()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Canvas.ForceUpdateCanvases();

            var sb = new StringBuilder();
            sb.AppendLine("TOOLBAR_DUMP_BEGIN");

            foreach (var tw in Object.FindObjectsByType<TransparentWindow>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(tw);
                sb.AppendLine($"TransparentWindow on '{Path(tw.transform)}'");
                foreach (var f in new[] { "maincam", "canvasRectTransform", "panelRectTransform" })
                {
                    var p = so.FindProperty(f);
                    var obj = p?.objectReferenceValue;
                    sb.Append($"  {f} = {(obj == null ? "<NULL>" : Path(((Component)obj).transform))}");
                    if (obj is RectTransform r)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(r);
                        sb.Append($"  rect={Fmt(r)}");
                    }
                    sb.AppendLine();
                }
            }

            foreach (var name in new[] { "TitleBar", "TitleBarA", "TitleBarB", "Panel", "Menu" })
            {
                foreach (var t in Object.FindObjectsByType<RectTransform>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.name == name))
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(t);
                    sb.AppendLine($"'{Path(t)}' active={t.gameObject.activeInHierarchy}");
                    sb.AppendLine($"   {Fmt(t)}");
                    foreach (var c in t.GetComponents<Component>())
                    {
                        if (c is RectTransform) continue;
                        sb.Append($"   + {c.GetType().Name}");
                        if (c is HorizontalOrVerticalLayoutGroup g)
                        {
                            sb.Append($" spacing={g.spacing} pad=({g.padding.left},{g.padding.right}) " +
                                      $"childCtrlW={g.childControlWidth} childExpandW={g.childForceExpandWidth} " +
                                      $"align={g.childAlignment}");
                        }
                        else if (c is ContentSizeFitter f)
                        {
                            sb.Append($" horiz={f.horizontalFit} vert={f.verticalFit}");
                        }
                        else if (c is LayoutElement le)
                        {
                            sb.Append($" pref=({le.preferredWidth},{le.preferredHeight}) min=({le.minWidth},{le.minHeight})");
                        }
                        sb.AppendLine();
                    }
                    foreach (RectTransform child in t)
                    {
                        var le = child.GetComponent<LayoutElement>();
                        sb.AppendLine($"     - {child.name} {Fmt(child)}" +
                                      (le != null ? $" [LayoutElement pref={le.preferredWidth}]" : ""));
                    }
                }
            }

            sb.AppendLine("TOOLBAR_DUMP_END");
            Debug.Log(sb.ToString());
        }

        private static string Fmt(RectTransform r)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            return $"size={r.rect.width:F0}x{r.rect.height:F0} pos=({r.anchoredPosition.x:F0},{r.anchoredPosition.y:F0}) " +
                   $"sizeDelta=({r.sizeDelta.x:F0},{r.sizeDelta.y:F0}) " +
                   $"anchors=({r.anchorMin.x:F2},{r.anchorMin.y:F2})-({r.anchorMax.x:F2},{r.anchorMax.y:F2}) " +
                   $"pivot=({r.pivot.x:F2},{r.pivot.y:F2}) " +
                   $"world=({c[0].x:F0},{c[0].y:F0})-({c[2].x:F0},{c[2].y:F0})";
        }

        private static string Path(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}

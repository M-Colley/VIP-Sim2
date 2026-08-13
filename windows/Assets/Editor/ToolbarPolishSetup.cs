using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Gives the calibration button its own icon and adds hover help to the toolbar.
    ///
    /// The calibration button was cloned from MouseEyeSwitch and so inherited the
    /// eye glyph, leaving two visually identical buttons sitting next to each other
    /// doing different things. UnitEye already ships a crosshair texture, which is
    /// exactly the right symbol for "calibrate to a target" and keeps the toolbar
    /// consistent with the calibration screen itself.
    ///
    /// The toolbar is otherwise six unlabelled glyphs with no way to discover what
    /// they do. Idempotent.
    /// </summary>
    public static class ToolbarPolishSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";
        private const string LabelName = "ToolbarTooltipLabel";

        // Keyed by GameObject name in TitleBarB.
        private static readonly Dictionary<string, string> Help = new()
        {
            ["settings"] = "Settings — offsets, zoom and display options",
            ["Load"] = "Load a saved symptom profile",
            ["Save"] = "Save the current symptom profile",
            ["MouseEyeSwitch"] = "Switch gaze source between mouse and eye tracking",
            ["CalibrateGazeButton"] = "Calibrate eye tracking (F9).\nFollow the dot; Escape or right-click aborts.",
            ["MinimizeButton"] = "Minimise VIP-Sim",
            ["ExitButton"] = "Exit VIP-Sim (Ctrl+Alt+Q always works)",
            ["PrevCam"] = "Previous webcam",
            ["NextCam"] = "Next webcam",
        };

        [MenuItem("VIP-Sim/Polish toolbar (icon + hover help)")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var all = Resources.FindObjectsOfTypeAll<Transform>()
                               .Where(t => t.gameObject.scene == scene)
                               .Select(t => t.gameObject)
                               .ToList();

            int changed = 0;

            // --- 1. Calibration icon -------------------------------------------
            var calib = all.FirstOrDefault(g => g.name == "CalibrateGazeButton");
            if (calib != null)
            {
                // UnitEye draws Crosshair.png with GUI.DrawTexture, so it is imported
                // as a plain Texture and LoadAssetAtPath<Sprite> returns null. Convert
                // it to a Sprite first — uGUI Image can only take sprites.
                var path = AssetDatabase.FindAssets("Crosshair")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => p.Contains("uniteye") && p.EndsWith(".png"));

                Sprite sprite = null;
                if (path != null)
                {
                    if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                        (importer.textureType != TextureImporterType.Sprite ||
                         importer.spriteImportMode != SpriteImportMode.Single))
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        // Multiple mode slices the texture into sub-sprites and
                        // LoadAssetAtPath<Sprite> then returns null for the asset
                        // itself, which is why the first conversion still failed.
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.SaveAndReimport();
                        Debug.Log($"TOOLBAR_POLISH: reimported {path} as a single Sprite.");
                    }
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }

                if (sprite == null)
                {
                    Debug.LogWarning("TOOLBAR_POLISH: no Crosshair sprite found; the calibration " +
                                     "button keeps the inherited eye icon. Check that Crosshair.png " +
                                     "in the uniteye package is imported as a Sprite.");
                }
                else
                {
                    // Prefer a child Image (icon-over-background layouts), else the
                    // button's own Image — these toolbar buttons carry the glyph on
                    // the root, which an earlier "skip the root" guess got wrong.
                    var images = calib.GetComponentsInChildren<Image>(true);
                    var target = images.FirstOrDefault(i => i.gameObject != calib)
                                 ?? images.FirstOrDefault();
                    if (target != null && target.sprite != sprite)
                    {
                        target.sprite = sprite;
                        EditorUtility.SetDirty(target);
                        changed++;
                        Debug.Log($"TOOLBAR_POLISH: icon set on '{target.gameObject.name}'.");
                    }
                }
            }

            // --- 2. Make the button row fit its buttons -------------------------
            //
            // TitleBarB is 220px wide but has always been laid out by a
            // HorizontalLayoutGroup whose children overflow it: six 60px buttons
            // needed 360. That happened to line up, because the parent's
            // force-expand pushed TitleBarB's left edge to exactly 360px short of
            // the title bar's right edge. Adding a seventh button broke the
            // coincidence and pushed the exit button 60px past the right edge of
            // the screen -- and past the panel rect TransparentWindow uses to
            // decide where clicks are captured, so it stopped being clickable as
            // well as visible.
            //
            // Sizing the box to its content and pinning it to the title bar's
            // right edge removes the coincidence instead of re-tuning it. As a
            // side effect the group's existing UpperRight alignment finally does
            // something: with the box the right size, a hidden button leaves the
            // rest flush right instead of leaving a hole.
            var bar = all.FirstOrDefault(g => g.name == "TitleBarB" &&
                                              g.GetComponentsInChildren<Button>(true)
                                               .Any(b => b.name == "CalibrateGazeButton"));
            if (bar != null)
            {
                var rt = (RectTransform)bar.transform;
                var group = bar.GetComponent<HorizontalLayoutGroup>();

                float content = 0f;
                int buttons = 0;
                foreach (RectTransform child in rt)
                {
                    if (child.GetComponent<Button>() == null) continue;
                    content += child.sizeDelta.x;
                    buttons++;
                }
                if (group != null)
                    content += group.spacing * Mathf.Max(0, buttons - 1) + group.padding.horizontal;

                // ignoreLayout takes TitleBarB out of the parent row's
                // force-expand maths, which is what made its position depend on
                // its own width in the first place.
                var element = bar.GetComponent<LayoutElement>() ?? bar.AddComponent<LayoutElement>();
                bool touched = !element.ignoreLayout;
                element.ignoreLayout = true;

                var anchor = new Vector2(1f, 1f);
                if (rt.anchorMin != anchor || rt.anchorMax != anchor || rt.pivot != anchor ||
                    !Mathf.Approximately(rt.sizeDelta.x, content) ||
                    rt.anchoredPosition != Vector2.zero)
                {
                    rt.anchorMin = anchor;
                    rt.anchorMax = anchor;
                    rt.pivot = anchor;
                    rt.sizeDelta = new Vector2(content, rt.sizeDelta.y);
                    rt.anchoredPosition = Vector2.zero;
                    touched = true;
                }

                if (touched)
                {
                    EditorUtility.SetDirty(bar);
                    changed++;
                    Debug.Log($"TOOLBAR_POLISH: row sized to {buttons} button(s) ({content:F0}px) " +
                              "and pinned to the title bar's right edge.");
                }
            }
            else
            {
                Debug.LogWarning("TOOLBAR_POLISH: no TitleBarB containing CalibrateGazeButton; " +
                                 "row geometry left alone.");
            }

            // --- 3. Shared tooltip label ---------------------------------------
            var label = all.FirstOrDefault(g => g.name == LabelName);
            if (label == null)
            {
                // Clone an existing TMP label so font, material and scaling match
                // the rest of the UI instead of being invented.
                var template = all.Select(g => g.GetComponent<TMP_Text>())
                                  .FirstOrDefault(t => t != null && t.gameObject.activeInHierarchy);
                if (template == null)
                {
                    Debug.LogError("TOOLBAR_POLISH_FAILED: no TMP_Text to clone for the tooltip label.");
                    return;
                }

                var clone = Object.Instantiate(template.gameObject, template.transform.parent);
                clone.name = LabelName;
                var txt = clone.GetComponent<TMP_Text>();
                txt.text = "";
                txt.enableWordWrapping = true;
                txt.alignment = TextAlignmentOptions.TopRight;
                txt.raycastTarget = false; // must never eat clicks meant for the toolbar

                clone.SetActive(false);
                label = clone;
                EditorUtility.SetDirty(clone);
                changed++;
            }

            // Anchor it under the title bar, whatever it was cloned from.
            //
            // The first version copied TitleBarB's anchors and pivot but left the
            // clone parented to whichever object happened to own the TMP_Text it
            // cloned -- which was CamLabel, under WebcamMenu. Anchors resolve
            // against the parent, so the help text rendered down beside the webcam
            // arrows at the bottom of the panel rather than under the button it
            // was describing.
            var bar2 = all.FirstOrDefault(g => g.name == "TitleBar" && g.activeInHierarchy);
            if (bar2 == null)
            {
                Debug.LogWarning("TOOLBAR_POLISH: no active TitleBar; tooltip label left where it is.");
            }
            else
            {
                var lrt = (RectTransform)label.transform;
                var anchor = new Vector2(1f, 0f);      // bottom-right of the title bar
                var pivot = new Vector2(1f, 1f);       // hang downwards from there
                var offset = new Vector2(0f, -4f);
                var size = new Vector2(420f, 60f);

                bool moved = lrt.parent != bar2.transform;
                if (moved) lrt.SetParent(bar2.transform, false);

                // TitleBar runs a HorizontalLayoutGroup, which would otherwise
                // treat the label as a third column and shove TitleBarA aside.
                var le = label.GetComponent<LayoutElement>() ?? label.AddComponent<LayoutElement>();
                if (!le.ignoreLayout) { le.ignoreLayout = true; moved = true; }

                if (lrt.anchorMin != anchor || lrt.anchorMax != anchor || lrt.pivot != pivot ||
                    lrt.anchoredPosition != offset || lrt.sizeDelta != size)
                {
                    lrt.anchorMin = anchor;
                    lrt.anchorMax = anchor;
                    lrt.pivot = pivot;
                    lrt.anchoredPosition = offset;
                    lrt.sizeDelta = size;
                    moved = true;
                }

                if (moved)
                {
                    EditorUtility.SetDirty(label);
                    changed++;
                    Debug.Log("TOOLBAR_POLISH: tooltip label anchored under the title bar.");
                }
            }

            // --- 4. Attach help to each button ---------------------------------
            foreach (var kv in Help)
            {
                foreach (var go in all.Where(g => g != null && g.name == kv.Key))
                {
                    if (go.GetComponent<Button>() == null) continue;

                    var tip = go.GetComponent<ToolbarTooltip>() ?? go.AddComponent<ToolbarTooltip>();
                    if (tip.message != kv.Value)
                    {
                        tip.message = kv.Value;
                        EditorUtility.SetDirty(go);
                        changed++;
                    }
                }
            }

            if (changed == 0)
            {
                Debug.Log("TOOLBAR_POLISH_SKIPPED: already configured.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"TOOLBAR_POLISH_OK: {changed} change(s); tooltip label '{LabelName}' ready.");
        }
    }
}

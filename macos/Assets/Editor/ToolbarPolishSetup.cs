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

            // --- 2. Shared tooltip label ---------------------------------------
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
                txt.alignment = TextAlignmentOptions.TopLeft;
                txt.raycastTarget = false; // must never eat clicks meant for the toolbar

                var rt = clone.GetComponent<RectTransform>();
                var toolbar = all.FirstOrDefault(g => g.name == "TitleBarB" && g.activeInHierarchy);
                if (toolbar != null)
                {
                    var trt = toolbar.GetComponent<RectTransform>();
                    rt.anchorMin = trt.anchorMin;
                    rt.anchorMax = trt.anchorMax;
                    rt.pivot = trt.pivot;
                    // Just below the toolbar row.
                    rt.anchoredPosition = trt.anchoredPosition - new Vector2(0f, trt.rect.height * 0.6f);
                    rt.sizeDelta = new Vector2(420f, 60f);
                }

                clone.SetActive(false);
                label = clone;
                EditorUtility.SetDirty(clone);
                changed++;
            }

            // --- 3. Attach help to each button ---------------------------------
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

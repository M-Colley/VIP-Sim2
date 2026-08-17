using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Adds an info button to the toolbar that opens the symptom reference.
    ///
    /// Cloned from MouseEyeSwitch rather than built from scratch, the same approach the
    /// calibration button uses: the clone inherits the toolbar's styling, sizing and
    /// anchoring instead of having them guessed at. Building from first principles is what
    /// put an earlier button at (0,-63), outside a 220x100 bar and clipped from view.
    ///
    /// The inherited SwitchInput is stripped. That component keeps its icon in sync with
    /// GazeTracker every frame, so left in place it would repaint this button's glyph with
    /// the eye sprite -- exactly what happened to the calibration button's crosshair.
    ///
    /// Idempotent.
    /// </summary>
    public static class SymptomInfoButtonSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";
        private const string ButtonName = "SymptomInfoButton";
        private const string TemplateName = "MouseEyeSwitch";

        // Every button in TitleBarB is 60 wide; the bar has to grow by exactly one.
        private const float ButtonWidth = 60f;

        [MenuItem("VIP-Sim/Add symptom info button")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var all = Resources.FindObjectsOfTypeAll<Transform>()
                               .Where(t => t.gameObject.scene == scene)
                               .Select(t => t.gameObject)
                               .ToList();

            foreach (var stale in all.Where(g => g.name == ButtonName).ToList())
                Object.DestroyImmediate(stale);
            all = all.Where(g => g != null).ToList();

            var template = all.FirstOrDefault(g => g.name == TemplateName);
            if (template == null)
            {
                Debug.LogError($"SYMPTOM_INFO_FAILED: template '{TemplateName}' not found.");
                return;
            }

            // The panel component lives on whatever carries VipSimDiagnostics -- an
            // always-present object with an OnGUI already, so no new scene object is needed.
            var host = all.FirstOrDefault(g => g.GetComponent<VipSimDiagnostics>() != null)
                       ?? all.FirstOrDefault(g => g.name == "Canvas");
            if (host == null)
            {
                Debug.LogError("SYMPTOM_INFO_FAILED: no host object for the panel component.");
                return;
            }

            var info = host.GetComponent<SymptomInfo>() ?? host.AddComponent<SymptomInfo>();

            // Make room BEFORE adding. TitleBarB is a fixed-width HorizontalLayoutGroup and
            // was already full: adding a button without widening it pushed the EXIT button
            // off the end of the row, which on a borderless always-on-top overlay removes
            // the only visible way out. One button's worth of width, no more.
            var bar = template.transform.parent;

            var clone = Object.Instantiate(template, template.transform.parent);
            clone.name = ButtonName;
            // Immediately after the template, NOT at index 0. TitleBarB sits to the right of
            // the title, so index 0 placed the icon on top of the "VIP-Sim" wordmark rather
            // than at the start of the button row.
            clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

            foreach (var stray in clone.GetComponentsInChildren<SwitchInput>(true))
                Object.DestroyImmediate(stray);

            foreach (var tmp in clone.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.text = "?";
                tmp.enableAutoSizing = true;
            }
            foreach (var txt in clone.GetComponentsInChildren<Text>(true))
            {
                txt.text = "?";
                txt.resizeTextForBestFit = true;
            }

            // A real icon, not a text glyph. Clearing the sprite and putting "?" in the
            // label left the button looking empty -- the text is white on a transparent
            // background, so there was nothing to see and nothing to click towards.
            //
            // Generated rather than sourced: the project has no info icon, and a drawn
            // circle-with-an-i is a handful of lines against finding, licensing and
            // importing artwork for one 60px button.
            var img = clone.GetComponent<Image>();
            if (img != null) img.sprite = GetOrCreateInfoIcon();

            // Clear the inherited label so it does not sit on top of the icon.
            foreach (var tmp in clone.GetComponentsInChildren<TMP_Text>(true)) tmp.text = "";
            foreach (var txt in clone.GetComponentsInChildren<Text>(true)) txt.text = "";

            var button = clone.GetComponent<Button>() ?? clone.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                Debug.LogError("SYMPTOM_INFO_FAILED: clone has no Button.");
                Object.DestroyImmediate(clone);
                return;
            }

            // Drop the inherited listeners before binding: otherwise pressing this would
            // also toggle the gaze source.
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            button.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(button.onClick, info.Toggle);

            var tip = clone.GetComponent<ToolbarTooltip>();
            if (tip != null) tip.message = "What do these symptoms mean? (F1)";

            // Size the bar from its final child count, NOT by adding a button's width.
            // Incrementing is not idempotent: re-running the setup grew TitleBarB every time,
            // 420 to 480 to 540. Deriving the width means the result is the same however many
            // times this runs, and it self-corrects a bar that has already been over-grown.
            var barRect = bar != null ? bar.GetComponent<RectTransform>() : null;
            if (barRect != null)
            {
                float want = bar.childCount * ButtonWidth;
                if (Mathf.Abs(barRect.rect.width - want) > 0.5f)
                {
                    Debug.Log($"SYMPTOM_INFO: '{bar.name}' width {barRect.rect.width:F0} -> {want:F0} " +
                              $"for {bar.childCount} buttons, so Exit is not pushed off the row.");
                    barRect.sizeDelta = new Vector2(barRect.sizeDelta.x + (want - barRect.rect.width),
                                                    barRect.sizeDelta.y);
                    EditorUtility.SetDirty(barRect);
                }

                // A LayoutElement preferred width would override the rect and undo the above.
                var le = bar.GetComponent<LayoutElement>();
                if (le != null && le.preferredWidth > 0f && Mathf.Abs(le.preferredWidth - want) > 0.5f)
                {
                    le.preferredWidth = want;
                    EditorUtility.SetDirty(le);
                }
            }

            EditorUtility.SetDirty(clone);
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"SYMPTOM_INFO_OK: '{ButtonName}' added under " +
                      $"'{clone.transform.parent?.name}', bound to SymptomInfo.Toggle on " +
                      $"'{host.name}'. F1 also opens it.");
        }

        private const string IconPath = "Assets/UI/info_icon.png";

        /// <summary>
        /// A circle-with-an-i, drawn once and saved as a project asset.
        ///
        /// Black on transparent, matching the other toolbar glyphs (folder, save, eye) which
        /// are black line art on the light button background. Anti-aliased by sampling the
        /// distance to the circle edge rather than testing inside/outside, because a hard
        /// 64px circle looks visibly jagged scaled onto a 60px button on a 4K display.
        /// </summary>
        private static Sprite GetOrCreateInfoIcon()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (existing != null) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];

            float c = (size - 1) * 0.5f;
            float rOuter = size * 0.46f;
            float ring = size * 0.075f;

            // The "i": a dot above a stem, both as rounded rectangles in normalised space.
            float stemW = size * 0.085f;
            float dotR = size * 0.062f;
            float dotCy = c - size * 0.19f;
            float stemTop = c - size * 0.055f;
            float stemBot = c + size * 0.235f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // Signed distance to the ring, negative inside the stroke.
                    float ringSd = Mathf.Abs(d - (rOuter - ring * 0.5f)) - ring * 0.5f;

                    float dotSd = Mathf.Sqrt((x - c) * (x - c) + (y - dotCy) * (y - dotCy)) - dotR;

                    float stemSd = Mathf.Max(Mathf.Abs(x - c) - stemW * 0.5f,
                                             Mathf.Max(stemTop - y, y - stemBot));

                    float sd = Mathf.Min(ringSd, Mathf.Min(dotSd, stemSd));

                    // One pixel of coverage either side of the edge.
                    float a = Mathf.Clamp01(0.5f - sd);
                    px[y * size + x] = new Color(0f, 0f, 0f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply();

            System.IO.File.WriteAllBytes(IconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);

            // Must be a Sprite, single mode. uGUI Image cannot take a plain Texture, and
            // Multiple mode makes LoadAssetAtPath<Sprite> return null for the asset itself --
            // the same trap the calibration crosshair hit.
            if (AssetImporter.GetAtPath(IconPath) is TextureImporter imp)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            Debug.Log($"SYMPTOM_INFO: generated {IconPath} ({(sprite != null ? "ok" : "FAILED to import as Sprite")}).");
            return sprite;
        }

        public static void Run() { Setup(); EditorApplication.Exit(0); }

        /// <summary>
        /// Remove the toolbar button, keeping the SymptomInfo panel itself.
        ///
        /// TitleBarB is a fixed-width HorizontalLayoutGroup sized for six buttons. A seventh
        /// pushed the EXIT button off the end of the row -- the same fault that put the X
        /// "too far to the right" when the calibration button was added, reproduced exactly.
        /// Losing the exit control on a borderless always-on-top overlay is far worse than
        /// having no info button, even with Ctrl+Alt+Q as a fallback.
        ///
        /// The panel is unaffected: it is IMGUI on an existing object and needs no toolbar
        /// space at all. F1 opens it. Putting a button back means widening TitleBarB or
        /// moving a control out of it, and that should be done with eyes on the result.
        /// </summary>
        [MenuItem("VIP-Sim/Remove symptom info button")]
        public static void Remove()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var stale = Resources.FindObjectsOfTypeAll<Transform>()
                                 .Where(t => t.gameObject.scene == scene && t.name == ButtonName)
                                 .Select(t => t.gameObject)
                                 .ToList();

            foreach (var g in stale) Object.DestroyImmediate(g);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"SYMPTOM_INFO_REMOVED: {stale.Count} button(s); the F1 panel is unaffected.");
        }

        public static void RunRemove() { Remove(); EditorApplication.Exit(0); }
    }
}

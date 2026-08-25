using System.IO;
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
    /// Add a toolbar button that saves an image of the simulation.
    ///
    /// Cloned from MouseEyeSwitch, the same way the info and calibration buttons were added:
    /// the clone inherits the toolbar's styling, sizing and anchoring instead of having them
    /// guessed at. Building one from first principles is what once put a button at (0,-63),
    /// outside a 220x100 bar and clipped from view.
    ///
    /// A camera, not a second floppy disk. The toolbar already has a Save, and that one saves
    /// a profile -- two disk icons side by side, one writing settings and one writing a
    /// picture, is a guessing game.
    ///
    /// Idempotent.
    /// </summary>
    public static class ExportButtonSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";
        private const string ButtonName = "ExportButton";
        private const string TemplateName = "MouseEyeSwitch";
        // 48, not 60. The toolbar was tuned to exactly this -- UiRefreshSetup's own
        // comment reads "60 -> 48. This alone is what makes eight buttons fit" -- and adding
        // a ninth at 60 took 108px from the title, clipping the VIP-Sim wordmark.
        private const float ButtonWidth = 48f;

        [MenuItem("VIP-Sim/Add the save-image button")]
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
                Debug.LogError($"EXPORT_FAILED: template '{TemplateName}' not found.");
                return;
            }

            // The same always-present host the other code-only components use.
            var host = all.FirstOrDefault(g => g.GetComponent<VipSimDiagnostics>() != null)
                       ?? all.FirstOrDefault(g => g.name == "Canvas");
            if (host == null)
            {
                Debug.LogError("EXPORT_FAILED: no host object for the export component.");
                return;
            }

            var export = host.GetComponent<VipSimExport>() ?? host.AddComponent<VipSimExport>();

            var bar = template.transform.parent;
            var clone = Object.Instantiate(template, template.transform.parent);
            clone.name = ButtonName;
            clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

            // SwitchInput repaints its button's glyph from GazeTracker every frame; left on
            // the clone it would replace this icon with the eye sprite, which is exactly what
            // happened to the calibration button's crosshair.
            foreach (var stray in clone.GetComponentsInChildren<SwitchInput>(true))
                Object.DestroyImmediate(stray);

            foreach (var tmp in clone.GetComponentsInChildren<TMP_Text>(true)) tmp.text = "";
            foreach (var txt in clone.GetComponentsInChildren<Text>(true)) txt.text = "";

            // Onto the Glyph child if there is one, not the root.
            //
            // The root Image is the button BACKGROUND, and on this template it has already
            // been cleared to alpha 0 by the toolbar refresh, which moved the glyph to a
            // child. Writing the icon to an invisible graphic is why the first version of
            // this button shipped completely blank: the sprite was assigned, and nothing
            // drew it. MouseEyeSwitch's own glyph is assigned at runtime by SwitchInput,
            // which this clone strips, so the inherited child is empty too.
            var glyphHolder = clone.transform.Find("Glyph");
            var img = glyphHolder != null ? glyphHolder.GetComponent<Image>()
                                          : clone.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = GetOrCreateCameraIcon();
                img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);
                img.enabled = true;
                EditorUtility.SetDirty(img);

                // So the hover tint darkens the glyph, the way it does on every sibling.
                var b = clone.GetComponent<Button>();
                if (b != null && glyphHolder != null) b.targetGraphic = img;
            }

            var button = clone.GetComponent<Button>() ?? clone.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                Debug.LogError("EXPORT_FAILED: clone has no Button.");
                Object.DestroyImmediate(clone);
                return;
            }

            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            button.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(button.onClick, export.SaveSimulatedView);

            var tip = clone.GetComponent<ToolbarTooltip>();
            if (tip != null)
                tip.message = "Save an image of the simulation, with a profile that reproduces it";

            // Width derived from the final child count, never incremented: incrementing is not
            // idempotent and grew this bar 420 -> 480 -> 540 across re-runs, pushing Exit off
            // the end of a row that is the only visible way to quit.
            var barRect = bar != null ? bar.GetComponent<RectTransform>() : null;
            if (barRect != null)
            {
                float want = bar.childCount * ButtonWidth;
                if (Mathf.Abs(barRect.rect.width - want) > 0.5f)
                {
                    barRect.sizeDelta = new Vector2(barRect.sizeDelta.x + (want - barRect.rect.width),
                                                    barRect.sizeDelta.y);
                    EditorUtility.SetDirty(barRect);
                }

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

            Debug.Log($"EXPORT_OK: '{ButtonName}' added under '{clone.transform.parent?.name}', " +
                      $"bound to VipSimExport.SaveSimulatedView on '{host.name}'.");
        }

        private const string IconPath = "Assets/UI/camera_icon.png";

        /// <summary>
        /// A camera: body, lens and viewfinder bump, drawn once and saved as a project asset.
        ///
        /// Black on transparent to match the other toolbar glyphs, and anti-aliased by
        /// sampling the distance to each edge rather than testing inside/outside -- a hard
        /// 128px shape looks visibly jagged scaled onto a 60px button on a 4K display.
        /// </summary>
        private static Sprite GetOrCreateCameraIcon()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (existing != null) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];

            float c = (size - 1) * 0.5f;
            float bodyHalfW = size * 0.40f;
            float bodyTop = c - size * 0.20f;
            float bodyBot = c + size * 0.26f;

            float bumpHalfW = size * 0.13f;
            float bumpTop = c - size * 0.31f;

            float lensOuter = size * 0.175f;
            float lensRing = size * 0.055f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Body and viewfinder as rectangles, unioned.
                    float body = Mathf.Max(Mathf.Abs(x - c) - bodyHalfW,
                                           Mathf.Max(bodyTop - y, y - bodyBot));
                    float bump = Mathf.Max(Mathf.Abs(x - (c - size * 0.16f)) - bumpHalfW,
                                           Mathf.Max(bumpTop - y, y - bodyTop));
                    float shell = Mathf.Min(body, bump);

                    // The lens is cut OUT of the body, so the glyph reads as an outline
                    // rather than a solid blob at button size.
                    float lensCy = c + size * 0.03f;
                    float lens = Mathf.Sqrt((x - c) * (x - c) + (y - lensCy) * (y - lensCy)) - lensOuter;
                    float lensStroke = Mathf.Abs(lens + lensRing * 0.5f) - lensRing * 0.5f;

                    float sd = Mathf.Max(shell, -lens);       // body minus the lens hole
                    sd = Mathf.Min(sd, lensStroke);           // plus the lens ring itself

                    float a = Mathf.Clamp01(0.5f - sd);
                    px[y * size + x] = new Color(0f, 0f, 0f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            File.WriteAllBytes(IconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);

            // Sprite, SINGLE mode. Leaving spriteImportMode alone is why this button shipped
            // blank: the PNG was written and imported perfectly well, and
            // LoadAssetAtPath<Sprite> still returned null, so a null sprite was assigned to a
            // real Image. The info icon's generator carries the same note -- it is the trap
            // the calibration crosshair hit before it.
            if (AssetImporter.GetAtPath(IconPath) is TextureImporter imp)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            Debug.Log($"EXPORT: generated {IconPath} " +
                      $"({(sprite != null ? "ok" : "FAILED to import as Sprite")}).");
            return sprite;
        }
    }
}

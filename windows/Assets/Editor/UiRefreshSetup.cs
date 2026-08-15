using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace VipSim.EditorTools
{
    /// <summary>
    /// Tightens the VIP-Sim panel: drops uWindowCapture's debug readout from the window
    /// cards, collapses the empty space under the list, and settles on one dark surface
    /// colour instead of several near-misses.
    ///
    /// The panel was 625x1240 holding a 374-tall window list and a webcam row, which left
    /// roughly two thirds of it as flat black. That reads as unfinished rather than
    /// minimal, and it is the single biggest reason the overlay looked rough.
    ///
    /// Idempotent, and every change is logged with its before value so the result can be
    /// checked against the scene rather than taken on trust.
    /// </summary>
    public static class UiRefreshSetup
    {
        private const string ScenePath = "Assets/Scenes/VIP_SIM.unity";
        private const string ItemPrefab = "Assets/uWindowCapture/Samples/Window List/uWC Window List Item.prefab";

        // Panel content is the title bar (55), the window list (374) and the webcam row,
        // plus breathing room. The effects list (VerticalMenu, 460) also lives here and is
        // shown in place of the window list, so the panel has to clear that too.
        private const float PanelHeight = 560f;

        // Only the coordinate strip, NOT its parent "Window Info" -- that also holds the
        // window's Title, and hiding the whole block left cards showing a bare icon with
        // no way to tell which window was which.
        private const string DebugRowName = "Window Position and Scale";

        // The webcam row was already anchored bottom-centre; its x of -240 is load-bearing
        // because the HorizontalLayoutGroup lays its three children out from a 100-wide
        // rect and overflows to the right. Re-centring it at 0 pushed the label off the
        // panel edge. Only the y is ours to set, to lift it clear of the bottom border.
        private const float WebcamX = -240f;
        private const float WebcamBottomInset = 34f;

        private static readonly Color Surface = new Color(0.098f, 0.102f, 0.114f, 0.965f);
        private static readonly Color Destructive = new Color(0.788f, 0.263f, 0.263f, 1f);

        [MenuItem("VIP-Sim/Refresh panel layout")]
        public static void Setup()
        {
            int changed = 0;

            // --- 1. Drop the debug readout from the window cards --------------------
            //
            // "X: -11  Y: -11  Z: 4  W: 3840  H: 2088  Status: Zoomed" is uWindowCapture's
            // sample-scene diagnostics, not something a study participant or clinician
            // needs in order to pick a window. Removing it also halves the height of every
            // card, so more windows fit without scrolling.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefab);
            if (prefab == null)
            {
                Debug.LogWarning($"UIREFRESH: window list item prefab not found at {ItemPrefab}; " +
                                 "the debug row will still be visible on the cards.");
            }
            else
            {
                var root = PrefabUtility.LoadPrefabContents(ItemPrefab);

                // Repair an earlier pass of this script that hid "Window Info" wholesale.
                // That is the parent block and it carries the window's Title, so the cards
                // ended up as bare icons with nothing identifying them.
                bool repaired = false;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Window Info" && !t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(true);
                        repaired = true;
                        Debug.Log("UIREFRESH: re-enabled 'Window Info'; it holds the window title.");
                    }
                }

                var info = root.GetComponentsInChildren<Transform>(true)
                               .FirstOrDefault(t => t.name == DebugRowName);
                if (repaired) { PrefabUtility.SaveAsPrefabAsset(root, ItemPrefab); changed++; }
                if (info == null)
                {
                    Debug.LogWarning("UIREFRESH: no 'Window Info' child in the card prefab; leaving it alone.");
                }
                else if (!info.gameObject.activeSelf)
                {
                    Debug.Log("UIREFRESH: card debug row already hidden.");
                }
                else
                {
                    // Deactivated rather than deleted: it is a vendor prefab, the values are
                    // occasionally useful when working out which window actually got
                    // captured, and re-enabling one flag is easier than restoring a subtree.
                    info.gameObject.SetActive(false);
                    PrefabUtility.SaveAsPrefabAsset(root, ItemPrefab);
                    changed++;
                    Debug.Log("UIREFRESH: hid 'Window Info' debug row on the window cards.");
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var all = Resources.FindObjectsOfTypeAll<Transform>()
                               .Where(t => t.gameObject.scene == scene)
                               .Select(t => t.gameObject)
                               .ToList();

            // Any card instance already sitting in the scene needs the same treatment --
            // the template under Window List is a scene object, not a prefab instance.
            foreach (var item in all.Where(g => g.name.StartsWith("uWC Window List Item")))
            {
                foreach (var t in item.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Window Info" && !t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(true);
                        changed++;
                        Debug.Log($"UIREFRESH: re-enabled 'Window Info' on scene instance '{item.name}'.");
                    }
                }

                var info = item.GetComponentsInChildren<Transform>(true)
                               .FirstOrDefault(t => t.name == DebugRowName);
                if (info != null && info.gameObject.activeSelf)
                {
                    info.gameObject.SetActive(false);
                    changed++;
                    Debug.Log($"UIREFRESH: hid debug row on scene instance '{item.name}'.");
                }
            }

            // --- 2. Collapse the dead space ----------------------------------------
            var panel = all.FirstOrDefault(g => g.name == "Panel" && g.transform.parent?.name == "Menu");
            if (panel == null)
            {
                Debug.LogWarning("UIREFRESH: no Menu/Panel found; layout left as-is.");
            }
            else
            {
                var prt = panel.GetComponent<RectTransform>();
                if (Mathf.Abs(prt.rect.height - PanelHeight) > 0.5f)
                {
                    float before = prt.rect.height;
                    prt.sizeDelta = new Vector2(prt.sizeDelta.x, prt.sizeDelta.y - (before - PanelHeight));
                    EditorUtility.SetDirty(prt);
                    changed++;
                    Debug.Log($"UIREFRESH: panel height {before:F0} -> {prt.rect.height:F0}.");
                }

                var img = panel.GetComponent<Image>();
                if (img != null && img.color != Surface)
                {
                    Debug.Log($"UIREFRESH: panel surface {ColorUtility.ToHtmlStringRGBA(img.color)} -> " +
                              $"{ColorUtility.ToHtmlStringRGBA(Surface)}.");
                    img.color = Surface;
                    EditorUtility.SetDirty(img);
                    changed++;
                }

                // Pin the webcam row to the bottom of the panel. Anchoring it rather than
                // nudging its position means it keeps sitting correctly if the panel height
                // is tuned again later, instead of needing a matching manual offset.
                var webcam = all.FirstOrDefault(g => g.name == "WebcamMenu");
                if (webcam != null)
                {
                    var wrt = webcam.GetComponent<RectTransform>();
                    var want = new Vector2(WebcamX, WebcamBottomInset);
                    if ((wrt.anchoredPosition - want).sqrMagnitude > 0.25f)
                    {
                        Debug.Log($"UIREFRESH: webcam row {wrt.anchoredPosition} -> {want} " +
                                  $"(anchors {wrt.anchorMin}/{wrt.anchorMax} left alone).");
                        wrt.anchoredPosition = want;
                        EditorUtility.SetDirty(wrt);
                        changed++;
                    }
                }
            }

            // --- 3. Soften the destructive action ----------------------------------
            //
            // The exit button was near-saturated orange, the loudest thing on screen by a
            // wide margin, on an overlay whose whole job is to sit over someone else's
            // work without shouting.
            // Every ExitButton, not the first one found: there is one in the main toolbar
            // and another in the settings menu, and picking whichever the scene happened to
            // enumerate first recoloured the hidden one while the visible orange stayed put.
            foreach (var exit in all.Where(g => g.name == "ExitButton" && g.GetComponent<Image>() != null))
            {
                var eimg = exit.GetComponent<Image>();
                if (eimg.color != Destructive)
                {
                    Debug.Log($"UIREFRESH: exit button under '{exit.transform.parent?.parent?.name}' " +
                              $"{ColorUtility.ToHtmlStringRGBA(eimg.color)} -> " +
                              $"{ColorUtility.ToHtmlStringRGBA(Destructive)}.");
                    eimg.color = Destructive;
                    EditorUtility.SetDirty(eimg);
                    changed++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"UIREFRESH_OK: {changed} change(s).");
        }

        public static void Run()
        {
            Setup();
            EditorApplication.Exit(0);
        }
    }
}

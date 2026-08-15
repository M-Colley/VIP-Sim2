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
        // The panel is NOT sized to the window list. An earlier pass cut it from 1240 to
        // 560 on the grounds that the space under the list was dead, which was wrong: that
        // area is the backdrop for VerticalMenu, the effects list, which only appears once
        // a window has been selected. Shrinking the panel left the effects list hanging off
        // the bottom with no background behind it and overlapping the webcam row. The
        // mistake was invisible in screenshots because synthetic clicks cannot select a
        // window, so every screenshot showed the pre-selection state only.
        //
        // The emptiness is removed instead by growing the window list to fill the panel,
        // which also means more windows are visible without scrolling.
        private const float PanelHeight = 1240f;

        // 374, the original. Do not grow this. The area below the window list is where
        // VerticalMenu, the effect list, is drawn, and BOTH lists are on screen at once
        // once a window has been selected -- the window list stays up so you can switch
        // capture target. Stretching the list to 1000 to "use the empty space" ran it
        // straight through the effect list and left the two overlapping.
        //
        // That empty region is reserved, not wasted, and this is the second attempt to
        // reclaim it that broke the layout: first by shrinking the panel out from under
        // the effect list, then by expanding the window list into it. Making the panel
        // genuinely fit its content needs the two lists to resize against each other at
        // runtime, which is a real change and should be done with the effect list visible
        // on screen, not inferred from a pre-selection screenshot.
        private const float WindowListHeight = 374f;

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

        // 44x36 rather than the original 35x32, and nudged right to 105. The Enable bar is
        // 230 wide centred at -35, so it ends at +80: a 50-wide gear left at x=95 would
        // have overlapped it by 10px, turning a fiddly control into a broken one. At 105
        // the gear clears it by 3px.
        // Wider but NOT taller. 44x32 keeps the original row height, so the gear is an
        // easier pointer target without needing extra spacing between rows -- and it was
        // that extra spacing which pushed the 18-row list past the bottom of the panel and
        // over the webcam controls.
        private static readonly Vector2 GearSize = new Vector2(44f, 32f);
        private const float GearX = 105f;

        // Spacing here is NEGATIVE and has to stay that way. Each effect row is a 100-tall
        // RectTransform carrying about 32px of visible content, so the layout group pulls
        // them back together with -61 to get a 39px step. Setting a "sensible" positive 12
        // would have made the step 112 and stretched an 18-row list to roughly 2000px.
        // -54 gives a 46px step: 7px more air than before, which is what the 36-tall gear
        // needs, without touching the rest of the geometry.
        private const float RowSpacing = -61f;

        // 155 originally, which put the sliders' handles over the gears. +35 is roughly one
        // handle radius; deliberately small, because the panel is only 625 wide and the
        // settings content already reaches close to its right edge.
        private const float SettingsPanelX = 190f;

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

                // Grow the window list (and the Scroll View inside it) to occupy the space
                // that used to be blank. This is what actually answers "no dead space":
                // the region is filled with something useful rather than cut away from
                // under a sibling that needed it.
                foreach (var name in new[] { "Window List", "Scroll View" })
                {
                    var target = all.FirstOrDefault(g => g.name == name &&
                                                        g.GetComponentInParent<Transform>() != null &&
                                                        IsUnder(g.transform, panel.transform));
                    if (target == null) continue;
                    var trt = target.GetComponent<RectTransform>();
                    if (trt == null || Mathf.Abs(trt.rect.height - WindowListHeight) <= 0.5f) continue;

                    float before = trt.rect.height;
                    trt.sizeDelta = new Vector2(trt.sizeDelta.x, trt.sizeDelta.y - (before - WindowListHeight));
                    EditorUtility.SetDirty(trt);
                    changed++;
                    Debug.Log($"UIREFRESH: '{name}' height {before:F0} -> {trt.rect.height:F0}.");
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

            // --- 3b. Stop the settings panel colliding with the gear column ---------
            //
            // The per-effect settings (Mode, Timer, Speed, sliders) sit immediately to the
            // right of the gear column, and a uGUI slider centres its handle ON the start
            // of its track -- so the handle sticks out half its own width to the left of
            // where the panel appears to begin and lands on top of the gears. Shifting the
            // container right by the handle radius clears it without reflowing anything.
            var settingsPanel = all.FirstOrDefault(g => g.name == "UICointainer");
            if (settingsPanel != null)
            {
                var srt = settingsPanel.GetComponent<RectTransform>();
                if (srt != null && Mathf.Abs(srt.anchoredPosition.x - SettingsPanelX) > 0.5f)
                {
                    Debug.Log($"UIREFRESH: settings panel x {srt.anchoredPosition.x:F0} -> {SettingsPanelX:F0}.");
                    srt.anchoredPosition = new Vector2(SettingsPanelX, srt.anchoredPosition.y);
                    EditorUtility.SetDirty(srt);
                    changed++;
                }
            }

            // --- 4. Make the per-effect settings gear hittable ----------------------
            //
            // Each effect row is a wide "Enable" bar with a 35x32 gear crammed against its
            // right edge. That is below the ~44px minimum anyone recommends for a pointer
            // target, it sits hard against a much larger button that does something else
            // entirely, and it reads as decoration rather than a control -- people do not
            // realise that is where per-effect settings live. Widening it is the part that
            // can be done safely from here; the affordance problem needs a visual
            // treatment that should be looked at on screen rather than guessed at.
            var gears = all.Where(g => g.name == "Settings" &&
                                       g.GetComponent<Button>() != null &&
                                       g.transform.parent != null &&
                                       g.transform.parent.parent != null &&
                                       g.transform.parent.parent.name == "VerticalMenu").ToList();
            foreach (var gear in gears)
            {
                var grt = gear.GetComponent<RectTransform>();
                if (grt == null || Mathf.Abs(grt.rect.width - GearSize.x) <= 0.5f) continue;

                var before = grt.rect.size;
                grt.sizeDelta += GearSize - before;
                grt.anchoredPosition = new Vector2(GearX, grt.anchoredPosition.y);
                EditorUtility.SetDirty(grt);
                changed++;
            }
            if (gears.Count > 0)
                Debug.Log($"UIREFRESH: enlarged {gears.Count} settings gear(s) to " +
                          $"{GearSize.x}x{GearSize.y} at x={GearX}.");

            var menu = all.FirstOrDefault(g => g.name == "VerticalMenu");
            var vlg = menu != null ? menu.GetComponent<VerticalLayoutGroup>() : null;
            if (vlg != null && Mathf.Abs(vlg.spacing - RowSpacing) > 0.5f)
            {
                Debug.Log($"UIREFRESH: effect row spacing {vlg.spacing:F0} -> {RowSpacing:F0}.");
                vlg.spacing = RowSpacing;
                EditorUtility.SetDirty(vlg);
                changed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"UIREFRESH_OK: {changed} change(s).");
        }

        private static bool IsUnder(Transform t, Transform ancestor)
        {
            for (var p = t; p != null; p = p.parent) if (p == ancestor) return true;
            return false;
        }

        public static void Run()
        {
            Setup();
            EditorApplication.Exit(0);
        }
    }
}

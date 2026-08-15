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
        // 0, not the -240 this used to need. That offset existed only to compensate for a
        // 100-wide rect whose HorizontalLayoutGroup overflowed rightwards; now the rect is
        // sized to its contents, the same offset pushed the whole control off the left edge
        // of the panel. A correctly sized row just centres.
        private const float WebcamX = 0f;
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
        // 16 originally. -18 was tried and is far too tight: these rows are around 60 tall,
        // so a negative gap overlapped the labels with the sliders above them and the panel
        // became unreadable. 2 keeps every row clear while still saving about 14px each,
        // which is roughly one extra control visible on a six-parameter effect.
        private const float SettingsSpacing = 2f;
        // Back to the original 349. Narrowing this container was the wrong lever and did
        // nothing: the sliders are not stretched to it. Their width is set explicitly in
        // DropdownManager when they are constructed, which is what ItemWidth below fixes.
        private const float SettingsPanelWidth = 349f;

        // DropdownManager.itemWidth, the explicit width every runtime-built slider and
        // dropdown is given. 200 ran them past the right edge of the panel; 150 leaves a
        // margin. This is the only place that controls it -- there is nothing in the scene
        // to resize, because the widgets do not exist until the effect's settings open.
        // 300 in the scene originally, which overran the panel edge; 150 was too far the
        // other way and left the sliders stubby. 220 fits inside the panel with a margin.
        private const float ItemWidth = 220f;

        // Idle stays at full brightness. Dimming it was tried and is wrong: the gears are
        // already easy to miss, and knocking all sixteen back to 55% made the control less
        // discoverable to fix a problem with the state that only one of them is ever in.
        // The open one is separated by HUE instead -- a saturated amber against the pale
        // idle sprite, which differs in colour and value at once and needs no new art.
        private static readonly Color GearIdle = Color.white;
        private static readonly Color GearActive = new Color(1f, 0.58f, 0.11f, 1f);

        // Wide enough for a real device name on one line -- "HD Pro Webcam C920" still
        // wrapped at 290, and the second line is what collided with the effect list.
        private static readonly Vector2 WebcamRowSize = new Vector2(500f, 62f);
        private const float CamLabelWidth = 360f;
        private static readonly Color FooterSurface = new Color(0.13f, 0.13f, 0.15f, 1f);

        private static readonly Color ItemSelected = new Color(0.72f, 0.42f, 0.09f, 1f);
        private static readonly Color ItemIdle = new Color(0.16f, 0.16f, 0.18f, 1f);

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
                // Selected vs not was two near-identical greys, so which window is being
                // captured was hard to pick out of the list. Selected now uses the same
                // amber as an open settings gear, giving the app one consistent signal for
                // "this is the thing you are acting on".
                // Matched by type NAME rather than referenced directly: UwcWindowListItem
                // is uWindowCapture, which only exists in the Windows project, and this
                // script runs against both. A direct reference fails to compile on macOS.
                foreach (var listItem in root.GetComponentsInChildren<Component>(true))
                {
                    if (listItem == null || listItem.GetType().Name != "UwcWindowListItem") continue;
                    var lso = new SerializedObject(listItem);
                    var sel = lso.FindProperty("selected");
                    var notSel = lso.FindProperty("notSelected");
                    if (sel == null || notSel == null) continue;
                    if (sel.colorValue == ItemSelected && notSel.colorValue == ItemIdle) continue;

                    Debug.Log($"UIREFRESH: window card selected {ColorUtility.ToHtmlStringRGB(sel.colorValue)} -> " +
                              $"{ColorUtility.ToHtmlStringRGB(ItemSelected)}, idle " +
                              $"{ColorUtility.ToHtmlStringRGB(notSel.colorValue)} -> {ColorUtility.ToHtmlStringRGB(ItemIdle)}.");
                    sel.colorValue = ItemSelected;
                    notSel.colorValue = ItemIdle;
                    lso.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, ItemPrefab);
                    changed++;
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

            // --- 3a. Turn the webcam row into a readable footer control -------------
            //
            // Three problems at once. The label wrapped onto a second line and that line
            // landed on top of the bottom of the effect list, so it read as broken. The
            // prev/next arrows sat at the extreme left and right edges of a 100-wide rect
            // that its HorizontalLayoutGroup overflowed, far away from the name they act
            // on, so nothing suggested they were a stepper for it. And the row had no
            // background, so it floated over whatever happened to be behind it.
            //
            // Sizing the rect to its contents pulls the arrows in against the label, and a
            // surface behind it separates it from the list as a distinct footer.
            var webcamRow = all.FirstOrDefault(g => g.name == "WebcamMenu");
            if (webcamRow != null)
            {
                var wrt = webcamRow.GetComponent<RectTransform>();
                if (wrt != null && Mathf.Abs(wrt.rect.width - WebcamRowSize.x) > 0.5f)
                {
                    Debug.Log($"UIREFRESH: webcam row size {wrt.rect.size} -> {WebcamRowSize}.");
                    wrt.sizeDelta += WebcamRowSize - wrt.rect.size;
                    EditorUtility.SetDirty(wrt);
                    changed++;
                }

                var hlg = webcamRow.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null && (hlg.childAlignment != TextAnchor.MiddleCenter || hlg.spacing != 10f))
                {
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.spacing = 10f;
                    hlg.childForceExpandWidth = false;
                    EditorUtility.SetDirty(hlg);
                    changed++;
                    Debug.Log("UIREFRESH: webcam row centred, arrows pulled in beside the name.");
                }

                // Widen the label so a typical device name fits on one line instead of
                // wrapping into the effect list.
                var camLabel = webcamRow.GetComponentsInChildren<Transform>(true)
                                        .FirstOrDefault(t => t.name == "CamLabel");
                if (camLabel != null)
                {
                    var crt = camLabel.GetComponent<RectTransform>();
                    if (crt != null && Mathf.Abs(crt.rect.width - CamLabelWidth) > 0.5f)
                    {
                        Debug.Log($"UIREFRESH: camera label width {crt.rect.width:F0} -> {CamLabelWidth:F0}.");
                        crt.sizeDelta = new Vector2(crt.sizeDelta.x + (CamLabelWidth - crt.rect.width), crt.sizeDelta.y);
                        EditorUtility.SetDirty(crt);
                        changed++;
                    }
                }

                var bg = webcamRow.GetComponent<Image>();
                if (bg == null)
                {
                    bg = webcamRow.AddComponent<Image>();
                    Debug.Log("UIREFRESH: added a background to the webcam row so it stops floating over the list.");
                    changed++;
                }
                if (bg.color != FooterSurface)
                {
                    bg.color = FooterSurface;
                    EditorUtility.SetDirty(bg);
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

            // --- 3c. Fit more settings in, and stop the sliders hitting the edge ----
            //
            // Effects with a lot of parameters (Retinopathy has six) run off the bottom of
            // the panel, so the last control cannot be reached at all. Tightening the row
            // spacing buys back roughly a control's worth of height per three rows without
            // touching any of the individual widgets.
            //
            // The sliders also ran flush to the panel's right edge, which leaves the handle
            // half off the surface at maximum and reads as clipped rather than deliberate.
            if (settingsPanel != null)
            {
                var svlg = settingsPanel.GetComponent<VerticalLayoutGroup>();
                if (svlg != null && Mathf.Abs(svlg.spacing - SettingsSpacing) > 0.5f)
                {
                    Debug.Log($"UIREFRESH: settings row spacing {svlg.spacing:F0} -> {SettingsSpacing:F0}.");
                    svlg.spacing = SettingsSpacing;
                    EditorUtility.SetDirty(svlg);
                    changed++;
                }

                // Narrow the CONTAINER rather than the sliders. The settings widgets are
                // built at runtime, not stored in the scene, so there is nothing here to
                // resize individually -- an earlier attempt to walk the sliders matched
                // nothing at all. They stretch to this container, so pulling its right edge
                // in pulls theirs in too, and it keeps working for widgets created later.
                var prt2 = settingsPanel.GetComponent<RectTransform>();
                if (prt2 != null && Mathf.Abs(prt2.rect.width - SettingsPanelWidth) > 0.5f)
                {
                    Debug.Log($"UIREFRESH: settings panel width {prt2.rect.width:F0} -> {SettingsPanelWidth:F0} " +
                              "so the sliders stop running into the panel edge.");
                    prt2.sizeDelta = new Vector2(prt2.sizeDelta.x + (SettingsPanelWidth - prt2.rect.width),
                                                 prt2.sizeDelta.y);
                    EditorUtility.SetDirty(prt2);
                    changed++;
                }
            }

            // Narrow the runtime-built widgets at their source.
            foreach (var dm in Resources.FindObjectsOfTypeAll<DropdownManager>())
            {
                if (dm == null || dm.gameObject.scene != scene) continue;
                var dso = new SerializedObject(dm);
                var iw = dso.FindProperty("itemWidth");
                if (iw == null || Mathf.Abs(iw.floatValue - ItemWidth) < 0.5f) continue;

                Debug.Log($"UIREFRESH: slider/dropdown width {iw.floatValue:F0} -> {ItemWidth:F0}.");
                iw.floatValue = ItemWidth;
                dso.ApplyModifiedPropertiesWithoutUndo();
                changed++;
            }

            // --- 3d. Stop UnitEye drawing a second cursor --------------------------
            //
            // HomulerGaze paints a crosshair at the gaze point with GUI.DrawTexture on
            // every frame. On a full-screen transparent overlay that lands on top of the
            // real desktop, so the user sees their own mouse pointer AND a second cursor
            // -- and because it marks where the eye tracker thinks they are looking rather
            // than where the pointer is, the two do not coincide. That is what the
            // "cursor is not properly aligned" report was: not a coordinate bug, a debug
            // overlay that was never meant to ship switched on.
            //
            // The crosshair is a development aid for checking the gaze pipeline. It stays
            // in the build and can be re-enabled on the component when diagnosing tracking.
            foreach (var comp in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (comp == null || comp.gameObject.scene != scene) continue;
                if (comp.GetType().Name != "HomulerGaze") continue;

                var gso = new SerializedObject(comp);
                var dd = gso.FindProperty("drawDot");
                if (dd == null || !dd.boolValue) continue;

                dd.boolValue = false;
                gso.ApplyModifiedPropertiesWithoutUndo();
                changed++;
                Debug.Log("UIREFRESH: turned off UnitEye's gaze crosshair (drawDot); it was " +
                          "drawing a second cursor over the desktop.");
            }

            // --- 3e. Make camera render order deterministic -------------------------
            //
            // Both cameras sat at depth 0, so which one wrote the backbuffer last was
            // undefined -- and since both CLEAR, the second one wipes the first. Only one
            // was ever contributing; which one was luck.
            //
            // The orthographic camera is the one that reaches the screen: it is what
            // AlignBoxColliderWithCamera drives, and the 1:1 capture placement applied to
            // it is visibly correct, which could not be true if its output were being
            // discarded. It is given the higher depth so it renders last, deliberately.
            //
            // Disabling the other camera previously removed the overlay entirely, which
            // looked like evidence it was the one rendering. It is not: LinkableBaseEffect
            // disables itself when it cannot find its opposite-eye twin, so removing that
            // camera switched every effect off. That distinction is what makes deleting it
            // safe, and it has to be handled in the same change.
            foreach (var cam in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (cam == null || cam.gameObject.scene != scene) continue;

                float want = cam.orthographic ? 1f : 0f;
                if (Mathf.Abs(cam.depth - want) < 0.01f) continue;

                Debug.Log($"UIREFRESH: camera '{cam.name}' depth {cam.depth:F1} -> {want:F1} " +
                          $"(ortho={cam.orthographic}, clear={cam.clearFlags}).");
                cam.depth = want;
                EditorUtility.SetDirty(cam);
                changed++;
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

            // Give the open effect's gear a visible state.
            //
            // Selected vs not was only settingONBG vs settingsOffBG, two shades of the same
            // cream, so the row whose settings panel was showing looked the same as the
            // fifteen that were not. Tinting is done by DIMMING the unselected gears rather
            // than brightening the selected one: an Image tint multiplies, so it can only
            // darken, and a "brighter" selected colour would have had no effect at all.
            // Unselected at 55% against full brightness is an unmistakable difference and
            // needs no new art.
            foreach (var gear in gears)
            {
                var so = new SerializedObject(gear.GetComponent<ChangeButtonAppearance>());
                var p1 = so.FindProperty("imageColor1");
                var p2 = so.FindProperty("imageColor2");
                if (p1 == null || p2 == null) continue;
                if (p1.colorValue == GearIdle && p2.colorValue == GearActive) continue;

                p1.colorValue = GearIdle;
                p2.colorValue = GearActive;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed++;
            }
            if (gears.Count > 0)
                Debug.Log($"UIREFRESH: tinted {gears.Count} gear(s) -- idle " +
                          $"{ColorUtility.ToHtmlStringRGB(GearIdle)}, open {ColorUtility.ToHtmlStringRGB(GearActive)}.");

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

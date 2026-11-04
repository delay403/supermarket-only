#if UNITY_EDITOR
using System;
using Game.Hud;
using Game.SupermarketOrganize;
using Game.SupermarketOrganize.Config;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Game.EditorTools
{
    public static class SupermarketOrganizeSceneSetupUtility
    {
        const string MenuRoot = "Game/Supermarket Organize/";
        const string InteractionHudRootName = "SupermarketOrganizeInteractionHud";
        const string SkillHudRootName = "SupermarketOrganizeSkillHud";
        const string UpgradePanelRootName = "SupermarketOrganizeUpgradePanel";
        const string SweepRobotPanelRootName = "SupermarketSweepRobotPanel";
        const string HomePanelRootName = "SupermarketOrganizeHomePanel";
        const string ResultPanelRootName = "SupermarketOrganizeResultPanel";
        const string HudSectionRootName = "SupermarketOrganizeHudSection";

        [MenuItem(MenuRoot + "Setup Selected Container Zone")]
        static void SetupSelectedContainerZone()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("超市收纳", "请先选中货架根物体。", "确定");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(go, "Setup Container Zone");
            var zone = EnsureComponent<SupermarketContainerZone>(go);
            EnsureTriggerCollider(go);

            if (go.transform.Find("GridOrigin") == null)
            {
                var origin = new GameObject("GridOrigin");
                Undo.RegisterCreatedObjectUndo(origin, "Create GridOrigin");
                origin.transform.SetParent(go.transform, false);
            }

            if (go.transform.Find("TipObject") == null)
            {
                var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tip.name = "TipObject";
                Undo.RegisterCreatedObjectUndo(tip, "Create TipObject");
                tip.transform.SetParent(go.transform, false);
                tip.transform.localScale = Vector3.one * 0.2f;
                UnityEngine.Object.DestroyImmediate(tip.GetComponent<Collider>());
            }

            EditorUtility.SetDirty(zone);
            MarkSceneDirty();
        }

        [MenuItem(MenuRoot + "Fix Player For Supermarket Organize")]
        static void FixPlayerForSupermarketOrganize()
        {
            var motor = UnityEngine.Object.FindObjectOfType<PlayerMotor>();
            if (motor == null)
            {
                EditorUtility.DisplayDialog("超市收纳", "场景中未找到 PlayerMotor。", "确定");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(motor.gameObject, "Fix Player For Supermarket Organize");
            SupermarketOrganizePlayerSetup.ConfigurePlayerForSceneSetup(motor.gameObject, Camera.main);
            DisableThirdPersonCameraInScene(Camera.main);

            MarkSceneDirty();
        }

        [MenuItem(MenuRoot + "Switch Main Camera To First Person")]
        static void SwitchMainCameraToFirstPerson()
        {
            var motor = UnityEngine.Object.FindObjectOfType<PlayerMotor>();
            if (motor == null)
            {
                EditorUtility.DisplayDialog("超市收纳", "场景中未找到 PlayerMotor。", "确定");
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                EditorUtility.DisplayDialog("超市收纳", "场景中未找到 Main Camera。", "确定");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(cam.gameObject, "Switch Main Camera To First Person");
            SupermarketOrganizePlayerSetup.ConfigureFirstPersonCamera(motor.gameObject, cam);
            DisableThirdPersonCameraInScene(cam);
            MarkSceneDirty();
        }

        static void DisableThirdPersonCameraInScene(Camera cam)
        {
            if (cam == null)
                return;

            var thirdPerson = cam.GetComponent<ThirdPersonCamera>();
            if (thirdPerson != null)
                thirdPerson.enabled = false;
        }

        [MenuItem(MenuRoot + "Ensure Interaction HUD (Crosshair + Buttons)")]
        static void EnsureInteractionHud()
        {
            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var existing = canvas.GetComponentInChildren<SupermarketOrganizeInteractionHud>(true);
            if (existing != null)
            {
                EditorUtility.DisplayDialog("超市收纳", "场景中已存在 Interaction HUD。", "确定");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Ensure Interaction HUD");
            var root = CreateUiObject(InteractionHudRootName, canvas.transform);
            StretchFull(root.GetComponent<RectTransform>());

            var crosshairGo = CreateUiObject("Crosshair", root.transform);
            var crosshairRt = crosshairGo.GetComponent<RectTransform>();
            crosshairRt.anchorMin = crosshairRt.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRt.sizeDelta = new Vector2(14f, 14f);
            crosshairGo.AddComponent<Image>().color = Color.white;
            var crosshairUi = crosshairGo.AddComponent<SupermarketCrosshairUi>();

            var hintGo = CreateUiObject("AimHint", root.transform);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0.5f);
            hintRt.anchoredPosition = new Vector2(0f, -48f);
            hintRt.sizeDelta = new Vector2(520f, 48f);
            var hintText = hintGo.AddComponent<Text>();
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = Color.white;
            hintText.fontSize = 22;
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintText.verticalOverflow = VerticalWrapMode.Overflow;
            hintText.text = string.Empty;
            hintGo.AddComponent<SupermarketAimHintUi>();

            var shelfGo = CreateUiObject("ShelfFeedback", root.transform);
            var shelfRt = shelfGo.GetComponent<RectTransform>();
            shelfRt.anchorMin = new Vector2(0.5f, 0.5f);
            shelfRt.anchorMax = new Vector2(0.5f, 0.5f);
            shelfRt.anchoredPosition = new Vector2(0f, 120f);
            shelfRt.sizeDelta = new Vector2(420f, 40f);
            var shelfText = shelfGo.AddComponent<Text>();
            shelfText.alignment = TextAnchor.MiddleCenter;
            shelfText.color = Color.white;
            shelfText.fontSize = 26;
            shelfText.text = string.Empty;
            shelfGo.SetActive(false);
            var shelfUi = shelfGo.AddComponent<SupermarketShelfFeedbackUi>();

            var interactButton = CreateActionButton(root.transform, "InteractButton", "拾取", new Vector2(0f, -220f), new Color(0.25f, 0.55f, 0.95f, 0.92f));
            var switchButton = CreateActionButton(root.transform, "SwitchItemButton", "切换", new Vector2(160f, -220f), new Color(0.55f, 0.35f, 0.85f, 0.92f));
            switchButton.gameObject.SetActive(false);

            var heldNameGo = CreateUiObject("CurrentHeldItemName", root.transform);
            var heldNameRt = heldNameGo.GetComponent<RectTransform>();
            heldNameRt.anchorMin = heldNameRt.anchorMax = new Vector2(0.5f, 0.5f);
            heldNameRt.anchoredPosition = new Vector2(160f, -280f);
            heldNameRt.sizeDelta = new Vector2(320f, 40f);
            var heldNameText = heldNameGo.AddComponent<Text>();
            heldNameText.alignment = TextAnchor.MiddleCenter;
            heldNameText.color = new Color(0.25f, 0.95f, 0.45f, 1f);
            heldNameText.fontSize = 24;
            heldNameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            heldNameText.verticalOverflow = VerticalWrapMode.Overflow;
            heldNameText.text = string.Empty;
            heldNameGo.SetActive(false);

            var hud = root.AddComponent<SupermarketOrganizeInteractionHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("crosshairUi").objectReferenceValue = crosshairUi;
            hudSo.FindProperty("aimHintUi").objectReferenceValue = hintGo.GetComponent<SupermarketAimHintUi>();
            hudSo.FindProperty("shelfFeedbackUi").objectReferenceValue = shelfUi;
            hudSo.FindProperty("interactButton").objectReferenceValue = interactButton;
            hudSo.FindProperty("interactButtonLabel").objectReferenceValue = interactButton.GetComponentInChildren<Text>();
            hudSo.FindProperty("switchItemButton").objectReferenceValue = switchButton;
            hudSo.FindProperty("currentHeldItemNameText").objectReferenceValue = heldNameText;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var shelfSo = new SerializedObject(shelfUi);
            shelfSo.FindProperty("root").objectReferenceValue = shelfGo;
            shelfSo.FindProperty("messageText").objectReferenceValue = shelfText;
            shelfSo.ApplyModifiedPropertiesWithoutUndo();

            var aimSo = new SerializedObject(hintGo.GetComponent<SupermarketAimHintUi>());
            aimSo.FindProperty("hintText").objectReferenceValue = hintText;
            aimSo.FindProperty("root").objectReferenceValue = hintGo;
            aimSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            MarkSceneDirty();
        }

        [MenuItem(MenuRoot + "Ensure Skill HUD + Upgrade Panel")]
        static void EnsureSkillHudAndUpgradePanel()
        {
            var canvas = EnsureOverlayCanvas();
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Ensure Supermarket Skill HUD + Upgrade");

            var skillHud = EnsureSkillHud(canvas.transform);
            var upgradePanel = EnsureUpgradePanel(canvas.transform);
            var sweepPanel = EnsureSweepRobotPanel(canvas.transform);
            var homePanel = EnsureHomePanel(canvas.transform);
            var resultPanel = EnsureResultPanel(canvas.transform);
            var hudSection = EnsureHudSection(canvas.transform);

            Selection.activeGameObject = skillHud;
            EditorUtility.DisplayDialog(
                "超市收纳",
                "已创建/确认：技能 HUD、升级面板、扫地机器人面板、主页、结算面板、HUD 信息区。\n" +
                "若升级面板仍是旧版纵向列表，请执行 Rebuild Upgrade Panel。",
                "确定");
            MarkSceneDirty();
        }

        [MenuItem(MenuRoot + "Ensure Result Panel")]
        static void EnsureResultPanelMenuItem()
        {
            var canvas = EnsureOverlayCanvas();
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Ensure Supermarket Result Panel");
            var panel = EnsureResultPanel(canvas.transform);
            Selection.activeGameObject = panel;
            MarkSceneDirty();
        }

        static Canvas EnsureOverlayCanvas()
        {
            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (canvas != null)
                return canvas;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }

        static GameObject EnsureSkillHud(Transform canvasTransform)
        {
            var existing = UnityEngine.Object.FindObjectOfType<SupermarketOrganizeSkillHud>(true);
            if (existing != null)
            {
                WireSkillHud(existing);
                return existing.gameObject;
            }

            var root = CreateUiObject(SkillHudRootName, canvasTransform);
            StretchFull(root.GetComponent<RectTransform>());

            var bar = CreateUiObject("SkillBar", root.transform);
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = barRt.anchorMax = new Vector2(0.5f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = new Vector2(0f, 118f);
            barRt.sizeDelta = new Vector2(420f, 92f);

            var slots = new SupermarketOrganizeSkillSlotUi[4];
            var skillIds = new[]
            {
                SupermarketSkillId.Scanner,
                SupermarketSkillId.Magnet,
                SupermarketSkillId.XRay,
                SupermarketSkillId.AutoStocker,
            };
            var skillColors = new[]
            {
                new Color(0.95f, 0.72f, 0.18f, 0.95f),
                new Color(0.62f, 0.38f, 0.92f, 0.95f),
                new Color(0.22f, 0.78f, 0.92f, 0.95f),
                new Color(0.28f, 0.82f, 0.42f, 0.95f),
            };

            for (var i = 0; i < skillIds.Length; i++)
            {
                var x = -135f + i * 90f;
                slots[i] = CreateSkillSlot(bar.transform, skillIds[i], skillColors[i], new Vector2(x, 0f));
            }

            var hud = root.AddComponent<SupermarketOrganizeSkillHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("sectionRoot").objectReferenceValue = root;
            hudSo.FindProperty("skillSlots").arraySize = slots.Length;
            for (var i = 0; i < slots.Length; i++)
                hudSo.FindProperty("skillSlots").GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        static void WireSkillHud(SupermarketOrganizeSkillHud hud)
        {
            var so = new SerializedObject(hud);
            if (so.FindProperty("sectionRoot").objectReferenceValue == null)
                so.FindProperty("sectionRoot").objectReferenceValue = hud.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static SupermarketOrganizeSkillSlotUi CreateSkillSlot(
            Transform parent,
            SupermarketSkillId skillId,
            Color iconColor,
            Vector2 anchoredPos)
        {
            var slotGo = CreateUiObject(skillId.ToString(), parent);
            var slotRt = slotGo.GetComponent<RectTransform>();
            slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.anchoredPosition = anchoredPos;
            slotRt.sizeDelta = new Vector2(76f, 76f);

            var iconGo = CreateUiObject("Icon", slotGo.transform);
            StretchFull(iconGo.GetComponent<RectTransform>());
            var iconImage = iconGo.AddComponent<Image>();
            iconImage.color = iconColor;

            var cooldownGo = CreateUiObject("CooldownFill", slotGo.transform);
            StretchFull(cooldownGo.GetComponent<RectTransform>());
            var cooldownFill = cooldownGo.AddComponent<Image>();
            cooldownFill.color = new Color(0f, 0f, 0f, 0.58f);
            ConfigureRadial360Fill(cooldownFill);

            var cooldownTextGo = CreateUiObject("CooldownText", slotGo.transform);
            StretchFull(cooldownTextGo.GetComponent<RectTransform>());
            var cooldownText = cooldownTextGo.AddComponent<Text>();
            cooldownText.alignment = TextAnchor.MiddleCenter;
            cooldownText.fontSize = 18;
            cooldownText.color = Color.white;
            cooldownText.raycastTarget = false;
            cooldownTextGo.SetActive(false);

            var lockedGo = CreateUiObject("Locked", slotGo.transform);
            StretchFull(lockedGo.GetComponent<RectTransform>());
            var lockedImage = lockedGo.AddComponent<Image>();
            lockedImage.color = new Color(0f, 0f, 0f, 0.35f);
            lockedGo.SetActive(false);

            var lockButton = lockedGo.AddComponent<Button>();
            lockButton.targetGraphic = lockedImage;

            var button = slotGo.AddComponent<Button>();
            button.targetGraphic = iconImage;

            var slot = slotGo.AddComponent<SupermarketOrganizeSkillSlotUi>();
            slot.Configure(skillId, button, iconImage, cooldownFill, cooldownText, lockedGo, lockButton);
            return slot;
        }

        [MenuItem(MenuRoot + "Rebuild Upgrade Panel (Left-Right Layout)")]
        static void RebuildUpgradePanel()
        {
            var existing = UnityEngine.Object.FindObjectOfType<SupermarketOrganizeUpgradePanel>(true);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            var canvas = EnsureOverlayCanvas();
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Rebuild Upgrade Panel");
            EnsureUpgradePanel(canvas.transform);
            MarkSceneDirty();
            EditorUtility.DisplayDialog("超市收纳", "升级面板已按左图标右详情布局重建。", "确定");
        }

        [MenuItem(MenuRoot + "Ensure Sweep Robot Panel")]
        static void EnsureSweepRobotPanelMenu()
        {
            var canvas = EnsureOverlayCanvas();
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Ensure Sweep Robot Panel");
            EnsureSweepRobotPanel(canvas.transform);
            MarkSceneDirty();
        }

        [MenuItem(MenuRoot + "Ensure Home Panel")]
        static void EnsureHomePanelMenu()
        {
            var canvas = EnsureOverlayCanvas();
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Ensure Home Panel");
            EnsureHomePanel(canvas.transform);
            MarkSceneDirty();
        }

        static GameObject EnsureUpgradePanel(Transform canvasTransform)
        {
            var existing = UnityEngine.Object.FindObjectOfType<SupermarketOrganizeUpgradePanel>(true);
            if (existing != null)
                return existing.gameObject;

            var root = CreateUiObject(UpgradePanelRootName, canvasTransform);
            StretchFull(root.GetComponent<RectTransform>());

            var mask = CreateUiObject("Mask", root.transform);
            StretchFull(mask.GetComponent<RectTransform>());
            var maskImage = mask.AddComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.55f);
            var maskButton = mask.AddComponent<Button>();
            maskButton.transition = Selectable.Transition.None;

            var panel = CreateUiObject("MainPanel", root.transform);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(920f, 620f);
            panel.AddComponent<Image>().color = new Color(0.95f, 0.93f, 0.88f, 0.98f);

            var closeButton = CreateHeaderButton(panel.transform, "CloseButton", "×", new Vector2(-16f, -16f));

            var leftGo = CreateUiObject("LeftColumn", panel.transform);
            var leftRt = leftGo.GetComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0f, 0f);
            leftRt.anchorMax = new Vector2(0.58f, 1f);
            leftRt.offsetMin = new Vector2(20f, 20f);
            leftRt.offsetMax = new Vector2(-8f, -60f);

            var skillLabelGo = CreateUiObject("SkillLabel", leftGo.transform);
            var skillLabelRt = skillLabelGo.GetComponent<RectTransform>();
            skillLabelRt.anchorMin = skillLabelRt.anchorMax = new Vector2(0.5f, 1f);
            skillLabelRt.pivot = new Vector2(0.5f, 1f);
            skillLabelRt.anchoredPosition = new Vector2(0f, 0f);
            skillLabelRt.sizeDelta = new Vector2(200f, 28f);
            var skillLabel = skillLabelGo.AddComponent<Text>();
            skillLabel.text = "能力";
            skillLabel.fontSize = 22;
            skillLabel.alignment = TextAnchor.MiddleCenter;

            var skillGridGo = CreateUiObject("SkillGrid", leftGo.transform);
            var skillGridRt = skillGridGo.GetComponent<RectTransform>();
            skillGridRt.anchorMin = new Vector2(0f, 0.55f);
            skillGridRt.anchorMax = new Vector2(1f, 1f);
            skillGridRt.offsetMin = new Vector2(0f, 0f);
            skillGridRt.offsetMax = new Vector2(0f, -34f);
            var skillGrid = skillGridGo.AddComponent<GridLayoutGroup>();
            skillGrid.cellSize = new Vector2(96f, 120f);
            skillGrid.spacing = new Vector2(12f, 8f);
            skillGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            skillGrid.constraintCount = 4;

            var attrLabelGo = CreateUiObject("AttributeLabel", leftGo.transform);
            var attrLabelRt = attrLabelGo.GetComponent<RectTransform>();
            attrLabelRt.anchorMin = attrLabelRt.anchorMax = new Vector2(0.5f, 0.5f);
            attrLabelRt.pivot = new Vector2(0.5f, 0.5f);
            attrLabelRt.anchoredPosition = new Vector2(0f, 8f);
            attrLabelRt.sizeDelta = new Vector2(200f, 28f);
            var attrLabel = attrLabelGo.AddComponent<Text>();
            attrLabel.text = "强化道具";
            attrLabel.fontSize = 22;
            attrLabel.alignment = TextAnchor.MiddleCenter;

            var attrGridGo = CreateUiObject("AttributeGrid", leftGo.transform);
            var attrGridRt = attrGridGo.GetComponent<RectTransform>();
            attrGridRt.anchorMin = new Vector2(0f, 0f);
            attrGridRt.anchorMax = new Vector2(1f, 0.5f);
            attrGridRt.offsetMin = new Vector2(0f, 0f);
            attrGridRt.offsetMax = new Vector2(0f, -8f);
            var attrGrid = attrGridGo.AddComponent<GridLayoutGroup>();
            attrGrid.cellSize = new Vector2(96f, 120f);
            attrGrid.spacing = new Vector2(12f, 8f);
            attrGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            attrGrid.constraintCount = 3;

            var rightGo = CreateUiObject("RightColumn", panel.transform);
            var rightRt = rightGo.GetComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(0.58f, 0f);
            rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.offsetMin = new Vector2(8f, 20f);
            rightRt.offsetMax = new Vector2(-20f, -20f);
            rightGo.AddComponent<Image>().color = new Color(0.88f, 0.9f, 0.94f, 0.65f);

            var detailUi = CreateUpgradeDetailUi(rightGo.transform);
            var itemPrefab = CreateUpgradeIconCellPrefab();

            var panelComponent = root.AddComponent<SupermarketOrganizeUpgradePanel>();
            var panelSo = new SerializedObject(panelComponent);
            panelSo.FindProperty("root").objectReferenceValue = root;
            panelSo.FindProperty("mainPanel").objectReferenceValue = panelRt;
            panelSo.FindProperty("maskCloseButton").objectReferenceValue = maskButton;
            panelSo.FindProperty("closeButton").objectReferenceValue = closeButton;
            panelSo.FindProperty("skillGridContent").objectReferenceValue = skillGridRt;
            panelSo.FindProperty("attributeGridContent").objectReferenceValue = attrGridRt;
            panelSo.FindProperty("itemPrefab").objectReferenceValue = itemPrefab;
            panelSo.FindProperty("detailUi").objectReferenceValue = detailUi;
            panelSo.ApplyModifiedPropertiesWithoutUndo();
            itemPrefab.gameObject.SetActive(false);
            return root;
        }

        static SupermarketOrganizeUpgradeDetailUi CreateUpgradeDetailUi(Transform parent)
        {
            var detailGo = CreateUiObject("Detail", parent);
            StretchFull(detailGo.GetComponent<RectTransform>());

            var iconGo = CreateUiObject("Icon", detailGo.transform);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0f, -16f);
            iconRt.sizeDelta = new Vector2(96f, 96f);
            var iconImage = iconGo.AddComponent<Image>();

            var nameGo = CreateUiObject("Name", detailGo.transform);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, -120f);
            nameRt.sizeDelta = new Vector2(280f, 32f);
            var nameText = nameGo.AddComponent<Text>();
            nameText.fontSize = 24;
            nameText.alignment = TextAnchor.MiddleCenter;

            var segments = new Image[4];
            var segRoot = CreateUiObject("LevelSegments", detailGo.transform);
            var segRootRt = segRoot.GetComponent<RectTransform>();
            segRootRt.anchorMin = segRootRt.anchorMax = new Vector2(0.5f, 1f);
            segRootRt.pivot = new Vector2(0.5f, 1f);
            segRootRt.anchoredPosition = new Vector2(0f, -160f);
            segRootRt.sizeDelta = new Vector2(280f, 16f);
            var segLayout = segRoot.AddComponent<HorizontalLayoutGroup>();
            segLayout.spacing = 6f;
            segLayout.childForceExpandWidth = true;
            segLayout.childForceExpandHeight = true;
            for (var i = 0; i < segments.Length; i++)
            {
                var segGo = CreateUiObject($"Seg{i}", segRoot.transform);
                segGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
                segments[i] = segGo.AddComponent<Image>();
                segments[i].color = new Color(0.75f, 0.78f, 0.82f, 0.35f);
            }

            var levelGo = CreateUiObject("Level", detailGo.transform);
            var levelRt = levelGo.GetComponent<RectTransform>();
            levelRt.anchorMin = levelRt.anchorMax = new Vector2(0.5f, 1f);
            levelRt.pivot = new Vector2(0.5f, 1f);
            levelRt.anchoredPosition = new Vector2(0f, -186f);
            levelRt.sizeDelta = new Vector2(280f, 24f);
            var levelText = levelGo.AddComponent<Text>();
            levelText.fontSize = 18;
            levelText.alignment = TextAnchor.MiddleCenter;

            var descGo = CreateUiObject("Description", detailGo.transform);
            var descRt = descGo.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.08f, 0.34f);
            descRt.anchorMax = new Vector2(0.92f, 0.72f);
            descRt.offsetMin = descRt.offsetMax = Vector2.zero;
            var descText = descGo.AddComponent<Text>();
            descText.fontSize = 16;
            descText.alignment = TextAnchor.UpperLeft;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var effectGo = CreateUiObject("Effect", detailGo.transform);
            var effectRt = effectGo.GetComponent<RectTransform>();
            effectRt.anchorMin = new Vector2(0.08f, 0.26f);
            effectRt.anchorMax = new Vector2(0.92f, 0.34f);
            effectRt.offsetMin = effectRt.offsetMax = Vector2.zero;
            var effectText = effectGo.AddComponent<Text>();
            effectText.fontSize = 17;
            effectText.color = new Color(0.2f, 0.7f, 0.45f, 1f);

            var costGo = CreateUiObject("Cost", detailGo.transform);
            var costRt = costGo.GetComponent<RectTransform>();
            costRt.anchorMin = new Vector2(0.08f, 0.16f);
            costRt.anchorMax = new Vector2(0.92f, 0.24f);
            costRt.offsetMin = costRt.offsetMax = Vector2.zero;
            var costText = costGo.AddComponent<Text>();
            costText.fontSize = 20;
            costText.alignment = TextAnchor.MiddleLeft;

            var maxBadgeGo = CreateUiObject("MaxBadge", detailGo.transform);
            var maxBadgeRt = maxBadgeGo.GetComponent<RectTransform>();
            maxBadgeRt.anchorMin = maxBadgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            maxBadgeRt.sizeDelta = new Vector2(120f, 36f);
            var maxBadgeText = maxBadgeGo.AddComponent<Text>();
            maxBadgeText.text = "满级";
            maxBadgeText.fontSize = 22;
            maxBadgeText.alignment = TextAnchor.MiddleCenter;
            maxBadgeGo.SetActive(false);

            var upgradeGo = CreateUiObject("UpgradeButton", detailGo.transform);
            var upgradeRt = upgradeGo.GetComponent<RectTransform>();
            upgradeRt.anchorMin = upgradeRt.anchorMax = new Vector2(0.5f, 0f);
            upgradeRt.pivot = new Vector2(0.5f, 0f);
            upgradeRt.anchoredPosition = new Vector2(0f, 16f);
            upgradeRt.sizeDelta = new Vector2(220f, 56f);
            var upgradeImage = upgradeGo.AddComponent<Image>();
            upgradeImage.color = new Color(0.25f, 0.58f, 0.95f, 0.95f);
            var upgradeButton = upgradeGo.AddComponent<Button>();
            upgradeButton.targetGraphic = upgradeImage;
            var upgradeLabelGo = CreateUiObject("Text", upgradeGo.transform);
            StretchFull(upgradeLabelGo.GetComponent<RectTransform>());
            var upgradeLabel = upgradeLabelGo.AddComponent<Text>();
            upgradeLabel.text = "升级";
            upgradeLabel.fontSize = 22;
            upgradeLabel.alignment = TextAnchor.MiddleCenter;

            var detail = detailGo.AddComponent<SupermarketOrganizeUpgradeDetailUi>();
            var detailSo = new SerializedObject(detail);
            detailSo.FindProperty("iconImage").objectReferenceValue = iconImage;
            detailSo.FindProperty("nameText").objectReferenceValue = nameText;
            detailSo.FindProperty("levelText").objectReferenceValue = levelText;
            detailSo.FindProperty("descriptionText").objectReferenceValue = descText;
            detailSo.FindProperty("effectText").objectReferenceValue = effectText;
            detailSo.FindProperty("costText").objectReferenceValue = costText;
            detailSo.FindProperty("upgradeButton").objectReferenceValue = upgradeButton;
            detailSo.FindProperty("upgradeButtonLabel").objectReferenceValue = upgradeLabel;
            detailSo.FindProperty("maxLevelBadgeRoot").objectReferenceValue = maxBadgeGo;
            detailSo.FindProperty("levelSegmentFills").arraySize = segments.Length;
            for (var i = 0; i < segments.Length; i++)
                detailSo.FindProperty("levelSegmentFills").GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
            detailSo.ApplyModifiedPropertiesWithoutUndo();
            return detail;
        }

        static SupermarketOrganizeUpgradeRowUi CreateUpgradeIconCellPrefab()
        {
            var cellGo = CreateUiObject("UpgradeIconCellPrefab", null);
            var cellRt = cellGo.GetComponent<RectTransform>();
            cellRt.sizeDelta = new Vector2(96f, 120f);
            var bg = cellGo.AddComponent<Image>();
            bg.color = new Color(0.95f, 0.72f, 0.2f, 0.35f);
            var button = cellGo.AddComponent<Button>();
            button.targetGraphic = bg;

            var iconGo = CreateUiObject("Icon", cellGo.transform);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(56f, 56f);
            iconRt.anchoredPosition = new Vector2(0f, 8f);
            var iconImage = iconGo.AddComponent<Image>();

            var levelGo = CreateUiObject("Level", cellGo.transform);
            var levelRt = levelGo.GetComponent<RectTransform>();
            levelRt.anchorMin = new Vector2(0f, 0f);
            levelRt.anchorMax = new Vector2(0.55f, 0.35f);
            levelRt.offsetMin = new Vector2(4f, 4f);
            levelRt.offsetMax = new Vector2(0f, 0f);
            var levelText = levelGo.AddComponent<Text>();
            levelText.fontSize = 14;
            levelText.alignment = TextAnchor.LowerLeft;

            var nameGo = CreateUiObject("Name", cellGo.transform);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0f, 0f);
            nameRt.sizeDelta = new Vector2(0f, 24f);
            var nameText = nameGo.AddComponent<Text>();
            nameText.fontSize = 13;
            nameText.alignment = TextAnchor.MiddleCenter;

            var maxGo = CreateUiObject("MaxBadge", cellGo.transform);
            var maxRt = maxGo.GetComponent<RectTransform>();
            maxRt.anchorMin = maxRt.anchorMax = new Vector2(1f, 1f);
            maxRt.anchoredPosition = new Vector2(-6f, -6f);
            maxRt.sizeDelta = new Vector2(36f, 20f);
            maxGo.AddComponent<Text>().text = "满";
            maxGo.SetActive(false);

            var frameGo = CreateUiObject("SelectionFrame", cellGo.transform);
            StretchFull(frameGo.GetComponent<RectTransform>());
            var frameImage = frameGo.AddComponent<Image>();
            frameImage.color = new Color(0.2f, 0.45f, 0.95f, 0.95f);
            frameImage.raycastTarget = false;
            frameGo.SetActive(false);

            var row = cellGo.AddComponent<SupermarketOrganizeUpgradeRowUi>();
            var rowSo = new SerializedObject(row);
            rowSo.FindProperty("cellButton").objectReferenceValue = button;
            rowSo.FindProperty("backgroundImage").objectReferenceValue = bg;
            rowSo.FindProperty("iconImage").objectReferenceValue = iconImage;
            rowSo.FindProperty("levelText").objectReferenceValue = levelText;
            rowSo.FindProperty("nameText").objectReferenceValue = nameText;
            rowSo.FindProperty("maxLevelBadgeRoot").objectReferenceValue = maxGo;
            rowSo.FindProperty("selectionFrame").objectReferenceValue = frameGo.GetComponent<RectTransform>();
            rowSo.ApplyModifiedPropertiesWithoutUndo();
            return row;
        }

        static GameObject EnsureSweepRobotPanel(Transform canvasTransform)
        {
            var existing = UnityEngine.Object.FindObjectOfType<SupermarketSweepRobotPanel>(true);
            if (existing != null)
                return existing.gameObject;

            var root = CreateUiObject(SweepRobotPanelRootName, canvasTransform);
            StretchFull(root.GetComponent<RectTransform>());

            var mask = CreateUiObject("Mask", root.transform);
            StretchFull(mask.GetComponent<RectTransform>());
            mask.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var maskButton = mask.AddComponent<Button>();
            maskButton.transition = Selectable.Transition.None;

            var panel = CreateUiObject("MainPanel", root.transform);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(560f, 360f);
            panel.AddComponent<Image>().color = new Color(0.95f, 0.93f, 0.88f, 0.98f);

            var titleGo = CreateUiObject("Title", panel.transform);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -20f);
            titleRt.sizeDelta = new Vector2(400f, 40f);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "[清理机器人]";
            titleText.fontSize = 26;
            titleText.alignment = TextAnchor.MiddleCenter;

            var descGo = CreateUiObject("Description", panel.transform);
            var descRt = descGo.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.08f, 0.45f);
            descRt.anchorMax = new Vector2(0.92f, 0.78f);
            descRt.offsetMin = descRt.offsetMax = Vector2.zero;
            var descText = descGo.AddComponent<Text>();
            descText.fontSize = 18;
            descText.alignment = TextAnchor.MiddleCenter;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var remainGo = CreateUiObject("Remaining", panel.transform);
            var remainRt = remainGo.GetComponent<RectTransform>();
            remainRt.anchorMin = new Vector2(0.08f, 0.3f);
            remainRt.anchorMax = new Vector2(0.92f, 0.42f);
            remainRt.offsetMin = remainRt.offsetMax = Vector2.zero;
            var remainText = remainGo.AddComponent<Text>();
            remainText.fontSize = 20;
            remainText.alignment = TextAnchor.MiddleCenter;

            var continueBtn = CreateActionButton(panel.transform, "ContinueButton", "继续游戏", new Vector2(-110f, 24f), new Color(0.2f, 0.75f, 0.35f, 0.95f));
            var claimBtn = CreateActionButton(panel.transform, "ClaimButton", "立即领取", new Vector2(110f, 24f), new Color(0.95f, 0.72f, 0.18f, 0.95f));

            var panelComponent = root.AddComponent<SupermarketSweepRobotPanel>();
            var panelSo = new SerializedObject(panelComponent);
            panelSo.FindProperty("root").objectReferenceValue = root;
            panelSo.FindProperty("mainPanel").objectReferenceValue = panelRt;
            panelSo.FindProperty("maskCloseButton").objectReferenceValue = maskButton;
            panelSo.FindProperty("titleText").objectReferenceValue = titleText;
            panelSo.FindProperty("descriptionText").objectReferenceValue = descText;
            panelSo.FindProperty("remainingTimeText").objectReferenceValue = remainText;
            panelSo.FindProperty("continueButton").objectReferenceValue = continueBtn;
            panelSo.FindProperty("claimButton").objectReferenceValue = claimBtn;
            panelSo.FindProperty("claimButtonLabel").objectReferenceValue = claimBtn.GetComponentInChildren<Text>();
            panelSo.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return root;
        }

        static GameObject EnsureResultPanel(Transform canvasTransform)
        {
            var existing = UnityEngine.Object.FindObjectOfType<SupermarketOrganizeResultPanel>(true);
            if (existing != null)
                return existing.gameObject;

            var root = CreateUiObject(ResultPanelRootName, canvasTransform);
            StretchFull(root.GetComponent<RectTransform>());

            var mask = CreateUiObject("Mask", root.transform);
            StretchFull(mask.GetComponent<RectTransform>());
            var maskImage = mask.AddComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.55f);

            var panel = CreateUiObject("MainPanel", root.transform);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(640f, 520f);
            panel.AddComponent<Image>().color = new Color(0.98f, 0.96f, 0.92f, 0.98f);

            var titleGo = CreateUiObject("Title", panel.transform);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -24f);
            titleRt.sizeDelta = new Vector2(420f, 48f);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "整理完毕";
            titleText.fontSize = 34;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;

            var messageGo = CreateUiObject("Message", panel.transform);
            var messageRt = messageGo.GetComponent<RectTransform>();
            messageRt.anchorMin = new Vector2(0.08f, 0.58f);
            messageRt.anchorMax = new Vector2(0.92f, 0.72f);
            messageRt.offsetMin = messageRt.offsetMax = Vector2.zero;
            var messageText = messageGo.AddComponent<Text>();
            messageText.text = "超市整理顺利完毕！";
            messageText.fontSize = 24;
            messageText.alignment = TextAnchor.MiddleCenter;

            var shelfGo = CreateUiObject("ShelfCount", panel.transform);
            var shelfRt = shelfGo.GetComponent<RectTransform>();
            shelfRt.anchorMin = new Vector2(0.08f, 0.42f);
            shelfRt.anchorMax = new Vector2(0.92f, 0.52f);
            shelfRt.offsetMin = shelfRt.offsetMax = Vector2.zero;
            var shelfText = shelfGo.AddComponent<Text>();
            shelfText.fontSize = 24;
            shelfText.alignment = TextAnchor.MiddleCenter;
            shelfText.supportRichText = true;

            var itemGo = CreateUiObject("ItemCount", panel.transform);
            var itemRt = itemGo.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0.08f, 0.3f);
            itemRt.anchorMax = new Vector2(0.92f, 0.4f);
            itemRt.offsetMin = itemRt.offsetMax = Vector2.zero;
            var itemText = itemGo.AddComponent<Text>();
            itemText.fontSize = 24;
            itemText.alignment = TextAnchor.MiddleCenter;
            itemText.supportRichText = true;

            var returnBtn = CreateActionButton(
                panel.transform,
                "ReturnMapButton",
                "返回地图",
                new Vector2(0f, 28f),
                new Color(0.2f, 0.75f, 0.35f, 0.95f));

            var panelComponent = root.AddComponent<SupermarketOrganizeResultPanel>();
            var panelSo = new SerializedObject(panelComponent);
            panelSo.FindProperty("root").objectReferenceValue = root;
            panelSo.FindProperty("mainPanel").objectReferenceValue = panelRt;
            panelSo.FindProperty("maskImage").objectReferenceValue = maskImage;
            panelSo.FindProperty("titleText").objectReferenceValue = titleText;
            panelSo.FindProperty("messageText").objectReferenceValue = messageText;
            panelSo.FindProperty("shelfCountText").objectReferenceValue = shelfText;
            panelSo.FindProperty("itemCountText").objectReferenceValue = itemText;
            panelSo.FindProperty("returnMapButton").objectReferenceValue = returnBtn;
            panelSo.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return root;
        }

        static GameObject EnsureHomePanel(Transform canvasTransform)
        {
            var existing = UnityEngine.Object.FindObjectOfType<SupermarketOrganizeHomePanel>(true);
            if (existing != null)
                return existing.gameObject;

            var root = CreateUiObject(HomePanelRootName, canvasTransform);
            StretchFull(root.GetComponent<RectTransform>());
            var rootGroup = root.AddComponent<CanvasGroup>();

            var staminaGo = CreateUiObject("Stamina", root.transform);
            var staminaRt = staminaGo.GetComponent<RectTransform>();
            staminaRt.anchorMin = staminaRt.anchorMax = new Vector2(1f, 1f);
            staminaRt.pivot = new Vector2(1f, 1f);
            staminaRt.anchoredPosition = new Vector2(-24f, -24f);
            staminaRt.sizeDelta = new Vector2(180f, 40f);
            var staminaText = staminaGo.AddComponent<Text>();
            staminaText.fontSize = 22;
            staminaText.alignment = TextAnchor.MiddleRight;
            staminaText.text = "5/5";

            var buttonParent = CreateUiObject("DifficultyButtons", root.transform);
            StretchFull(buttonParent.GetComponent<RectTransform>());

            var detailMask = CreateUiObject("DetailMask", root.transform);
            StretchFull(detailMask.GetComponent<RectTransform>());
            detailMask.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            var detailMaskButton = detailMask.AddComponent<Button>();
            detailMaskButton.transition = Selectable.Transition.None;
            detailMask.SetActive(false);

            var detailPopupGo = CreateUiObject("DetailPopup", buttonParent.transform);
            var detailPopupRt = detailPopupGo.GetComponent<RectTransform>();
            detailPopupRt.anchorMin = detailPopupRt.anchorMax = new Vector2(0.5f, 0.5f);
            detailPopupRt.pivot = new Vector2(0f, 0.5f);
            detailPopupRt.sizeDelta = new Vector2(280f, 360f);
            detailPopupGo.AddComponent<Image>().color = new Color(0.95f, 0.93f, 0.88f, 0.98f);
            detailPopupGo.SetActive(false);

            var mapNameGo = CreateUiObject("MapName", detailPopupGo.transform);
            var mapNameRt = mapNameGo.GetComponent<RectTransform>();
            mapNameRt.anchorMin = mapNameRt.anchorMax = new Vector2(0.5f, 1f);
            mapNameRt.pivot = new Vector2(0.5f, 1f);
            mapNameRt.anchoredPosition = new Vector2(0f, -20f);
            mapNameRt.sizeDelta = new Vector2(240f, 36f);
            var mapNameText = mapNameGo.AddComponent<Text>();
            mapNameText.fontSize = 24;
            mapNameText.alignment = TextAnchor.MiddleCenter;

            var shelfGo = CreateUiObject("ShelfCount", detailPopupGo.transform);
            var shelfRt = shelfGo.GetComponent<RectTransform>();
            shelfRt.anchorMin = new Vector2(0.1f, 0.62f);
            shelfRt.anchorMax = new Vector2(0.9f, 0.72f);
            shelfRt.offsetMin = shelfRt.offsetMax = Vector2.zero;
            var shelfText = shelfGo.AddComponent<Text>();
            shelfText.fontSize = 18;

            var itemGo = CreateUiObject("ItemCount", detailPopupGo.transform);
            var itemRt = itemGo.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0.1f, 0.5f);
            itemRt.anchorMax = new Vector2(0.9f, 0.6f);
            itemRt.offsetMin = itemRt.offsetMax = Vector2.zero;
            var itemText = itemGo.AddComponent<Text>();
            itemText.fontSize = 18;

            var diffGo = CreateUiObject("Difficulty", detailPopupGo.transform);
            var diffRt = diffGo.GetComponent<RectTransform>();
            diffRt.anchorMin = new Vector2(0.1f, 0.38f);
            diffRt.anchorMax = new Vector2(0.9f, 0.48f);
            diffRt.offsetMin = diffRt.offsetMax = Vector2.zero;
            var diffText = diffGo.AddComponent<Text>();
            diffText.fontSize = 18;

            var startBtn = CreateActionButton(detailPopupGo.transform, "StartButton", "开始游戏", new Vector2(0f, 28f), new Color(0.25f, 0.58f, 0.95f, 0.95f));

            var detailPopup = detailPopupGo.AddComponent<SupermarketOrganizeHomeDetailPopup>();
            var popupSo = new SerializedObject(detailPopup);
            popupSo.FindProperty("root").objectReferenceValue = detailPopupGo;
            popupSo.FindProperty("popupRect").objectReferenceValue = detailPopupRt;
            popupSo.FindProperty("mapNameText").objectReferenceValue = mapNameText;
            popupSo.FindProperty("shelfCountText").objectReferenceValue = shelfText;
            popupSo.FindProperty("itemCountText").objectReferenceValue = itemText;
            popupSo.FindProperty("difficultyText").objectReferenceValue = diffText;
            popupSo.FindProperty("startButton").objectReferenceValue = startBtn;
            popupSo.ApplyModifiedPropertiesWithoutUndo();

            var difficultyPrefabGo = CreateUiObject("DifficultyButtonPrefab", null);
            var diffBtnRt = difficultyPrefabGo.GetComponent<RectTransform>();
            diffBtnRt.anchorMin = diffBtnRt.anchorMax = new Vector2(0.5f, 0.5f);
            diffBtnRt.pivot = new Vector2(0.5f, 0.5f);
            diffBtnRt.sizeDelta = new Vector2(120f, 120f);
            var diffBtnImage = difficultyPrefabGo.AddComponent<Image>();
            diffBtnImage.color = new Color(1f, 0.75f, 0.2f, 0.35f);
            var diffBtn = difficultyPrefabGo.AddComponent<Button>();
            diffBtn.targetGraphic = diffBtnImage;
            var diffIconGo = CreateUiObject("Icon", difficultyPrefabGo.transform);
            StretchFull(diffIconGo.GetComponent<RectTransform>());
            var diffIconImage = diffIconGo.AddComponent<Image>();
            var glowGo = CreateUiObject("Glow", difficultyPrefabGo.transform);
            StretchFull(glowGo.GetComponent<RectTransform>());
            var glowImage = glowGo.AddComponent<Image>();
            glowImage.color = new Color(0.2f, 0.85f, 1f, 0.55f);
            glowImage.raycastTarget = false;
            glowGo.SetActive(false);
            var difficultyPrefab = difficultyPrefabGo.AddComponent<SupermarketOrganizeHomeDifficultyButton>();
            var diffPrefabSo = new SerializedObject(difficultyPrefab);
            diffPrefabSo.FindProperty("button").objectReferenceValue = diffBtn;
            diffPrefabSo.FindProperty("iconImage").objectReferenceValue = diffIconImage;
            diffPrefabSo.FindProperty("glowImage").objectReferenceValue = glowImage;
            diffPrefabSo.ApplyModifiedPropertiesWithoutUndo();

            var entryCharGo = CreateUiObject("EntryCharacter", root.transform);
            var entryCharRt = entryCharGo.GetComponent<RectTransform>();
            entryCharRt.sizeDelta = new Vector2(72f, 72f);
            var entryCharImage = entryCharGo.AddComponent<Image>();
            entryCharGo.SetActive(false);

            var home = root.AddComponent<SupermarketOrganizeHomePanel>();
            var homeSo = new SerializedObject(home);
            homeSo.FindProperty("root").objectReferenceValue = root;
            homeSo.FindProperty("rootCanvasGroup").objectReferenceValue = rootGroup;
            homeSo.FindProperty("staminaText").objectReferenceValue = staminaText;
            homeSo.FindProperty("difficultyButtonParent").objectReferenceValue = buttonParent.GetComponent<RectTransform>();
            homeSo.FindProperty("difficultyButtonPrefab").objectReferenceValue = difficultyPrefab;
            homeSo.FindProperty("detailPopup").objectReferenceValue = detailPopup;
            homeSo.FindProperty("detailMaskButton").objectReferenceValue = detailMaskButton;
            homeSo.FindProperty("detailMaskRoot").objectReferenceValue = detailMask;
            homeSo.FindProperty("entryCharacterRect").objectReferenceValue = entryCharRt;
            homeSo.FindProperty("entryCharacterImage").objectReferenceValue = entryCharImage;
            homeSo.ApplyModifiedPropertiesWithoutUndo();
            difficultyPrefabGo.SetActive(false);
            return root;
        }

        static GameObject EnsureHudSection(Transform canvasTransform)
        {
            var existing = UnityEngine.Object.FindObjectOfType<SupermarketOrganizeHudSection>(true);
            if (existing != null)
                return existing.gameObject;

            var gameHud = UnityEngine.Object.FindObjectOfType<GameHudUguiPanel>(true);
            var parent = gameHud != null ? gameHud.transform : canvasTransform;

            var root = CreateUiObject(HudSectionRootName, parent);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0f, 1f);
            rootRt.anchoredPosition = new Vector2(16f, -96f);
            rootRt.sizeDelta = new Vector2(320f, 96f);

            var handleGo = CreateUiObject("HandleCount", root.transform);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0f, 1f);
            handleRt.anchorMax = new Vector2(1f, 1f);
            handleRt.offsetMin = new Vector2(0f, -34f);
            handleRt.offsetMax = new Vector2(-120f, 0f);
            var handleText = handleGo.AddComponent<Text>();
            handleText.fontSize = 22;
            handleText.alignment = TextAnchor.MiddleLeft;
            handleText.color = Color.white;
            handleText.text = "手持 0/4";

            var remainingGo = CreateUiObject("Remaining", root.transform);
            var remainingRt = remainingGo.GetComponent<RectTransform>();
            remainingRt.anchorMin = new Vector2(0f, 0f);
            remainingRt.anchorMax = new Vector2(1f, 0f);
            remainingRt.offsetMin = new Vector2(0f, 0f);
            remainingRt.offsetMax = new Vector2(-120f, 34f);
            var remainingText = remainingGo.AddComponent<Text>();
            remainingText.fontSize = 18;
            remainingText.alignment = TextAnchor.MiddleLeft;
            remainingText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            remainingText.text = "剩余地面物品：0";

            var upgradeGo = CreateUiObject("UpgradeButton", root.transform);
            var upgradeRt = upgradeGo.GetComponent<RectTransform>();
            upgradeRt.anchorMin = new Vector2(1f, 0.5f);
            upgradeRt.anchorMax = new Vector2(1f, 0.5f);
            upgradeRt.pivot = new Vector2(1f, 0.5f);
            upgradeRt.anchoredPosition = new Vector2(0f, 0f);
            upgradeRt.sizeDelta = new Vector2(108f, 44f);
            var upgradeImage = upgradeGo.AddComponent<Image>();
            upgradeImage.color = new Color(0.95f, 0.72f, 0.18f, 0.95f);
            var upgradeButton = upgradeGo.AddComponent<Button>();
            upgradeButton.targetGraphic = upgradeImage;
            var upgradeLabelGo = CreateUiObject("Text", upgradeGo.transform);
            StretchFull(upgradeLabelGo.GetComponent<RectTransform>());
            var upgradeLabel = upgradeLabelGo.AddComponent<Text>();
            upgradeLabel.text = "升级";
            upgradeLabel.fontSize = 20;
            upgradeLabel.alignment = TextAnchor.MiddleCenter;
            upgradeLabel.color = Color.white;

            var sweepGo = CreateUiObject("SweepRobotButton", root.transform);
            var sweepRt = sweepGo.GetComponent<RectTransform>();
            sweepRt.anchorMin = new Vector2(1f, 0.5f);
            sweepRt.anchorMax = new Vector2(1f, 0.5f);
            sweepRt.pivot = new Vector2(1f, 0.5f);
            sweepRt.anchoredPosition = new Vector2(0f, -52f);
            sweepRt.sizeDelta = new Vector2(108f, 44f);
            var sweepImage = sweepGo.AddComponent<Image>();
            sweepImage.color = new Color(0.35f, 0.75f, 0.95f, 0.95f);
            var sweepButton = sweepGo.AddComponent<Button>();
            sweepButton.targetGraphic = sweepImage;
            var sweepLabelGo = CreateUiObject("Text", sweepGo.transform);
            StretchFull(sweepLabelGo.GetComponent<RectTransform>());
            var sweepLabel = sweepLabelGo.AddComponent<Text>();
            sweepLabel.text = "机器人";
            sweepLabel.fontSize = 18;
            sweepLabel.alignment = TextAnchor.MiddleCenter;
            sweepLabel.color = Color.white;

            var section = root.AddComponent<SupermarketOrganizeHudSection>();
            var sectionSo = new SerializedObject(section);
            sectionSo.FindProperty("sectionRoot").objectReferenceValue = root;
            sectionSo.FindProperty("handleCountText").objectReferenceValue = handleText;
            sectionSo.FindProperty("remainingText").objectReferenceValue = remainingText;
            sectionSo.FindProperty("upgradeButton").objectReferenceValue = upgradeButton;
            sectionSo.FindProperty("sweepRobotButton").objectReferenceValue = sweepButton;
            sectionSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        static Button CreateHeaderButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var go = CreateUiObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(44f, 44f);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.28f, 0.3f, 0.34f, 0.95f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var textGo = CreateUiObject("Text", go.transform);
            StretchFull(textGo.GetComponent<RectTransform>());
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return button;
        }

        static void ConfigureRadial360Fill(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0f;
            image.raycastTarget = false;
        }

        static Button CreateActionButton(Transform parent, string name, string label, Vector2 anchoredPos, Color color)
        {
            var go = CreateUiObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(180f, 72f);
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textGo = CreateUiObject("Text", go.transform);
            StretchFull(textGo.GetComponent<RectTransform>());
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 26;
            text.raycastTarget = false;
            return button;
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        [MenuItem(MenuRoot + "Auto Assign Container Zones To Director")]
        static void AutoAssignContainerZonesToDirector()
        {
            var director = ResolveTargetDirector();
            if (director == null)
            {
                EditorUtility.DisplayDialog("超市收纳", "场景中未找到 SupermarketOrganizeDirector。", "确定");
                return;
            }

            var count = AssignContainerZones(director);
            Selection.activeGameObject = director.gameObject;
            EditorUtility.DisplayDialog("超市收纳", $"已为 Director 分配 {count} 个 Container Zone。", "确定");
        }

        [MenuItem(MenuRoot + "Auto Assign Container Zones To Director", true)]
        static bool AutoAssignContainerZonesToDirectorValidate() =>
            ResolveTargetDirector() != null;

        public static int AssignContainerZones(SupermarketOrganizeDirector director)
        {
            if (director == null)
                return 0;

            Undo.RecordObject(director, "Assign Container Zones");

#if UNITY_2023_1_OR_NEWER
            var zones = UnityEngine.Object.FindObjectsByType<SupermarketContainerZone>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
#else
            var zones = UnityEngine.Object.FindObjectsOfType<SupermarketContainerZone>(true);
#endif
            Array.Sort(zones, (a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return string.Compare(a.name, b.name, StringComparison.Ordinal);
            });

            var so = new SerializedObject(director);
            var prop = so.FindProperty("containerZones");
            prop.arraySize = zones.Length;
            for (var i = 0; i < zones.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(director);
            MarkSceneDirty();
            return zones.Length;
        }

        static SupermarketOrganizeDirector ResolveTargetDirector()
        {
            if (Selection.activeGameObject != null)
            {
                var selected = Selection.activeGameObject.GetComponent<SupermarketOrganizeDirector>();
                if (selected != null)
                    return selected;
            }

            return UnityEngine.Object.FindObjectOfType<SupermarketOrganizeDirector>();
        }

        const int RobotAgentTypeId = -1372625422;

        [MenuItem(MenuRoot + "Ensure Sweep Robot In Scene")]
        static void EnsureSweepRobotInScene()
        {
            var existing = UnityEngine.Object.FindObjectOfType<SupermarketSweepRobotAgent>(true);
            if (existing != null)
            {
                SnapSweepRobotToNavMesh(existing);
                Selection.activeGameObject = existing.gameObject;
                EditorUtility.DisplayDialog("超市收纳", "场景中已存在扫地机器人，并已尝试吸附到 NavMesh。", "确定");
                return;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(go, "Create Sweep Robot");
            go.name = "SweepRobot";
            go.transform.localScale = new Vector3(0.45f, 0.12f, 0.45f);

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Undo.DestroyObjectImmediate(collider);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.height = 0.22f;
            agent.radius = 0.2f;
            agent.baseOffset = 0.12f;
            agent.speed = 2.2f;
            agent.angularSpeed = 420f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.35f;

            var agentSo = new SerializedObject(agent);
            agentSo.FindProperty("m_AgentTypeID").intValue = RobotAgentTypeId;
            agentSo.ApplyModifiedPropertiesWithoutUndo();

            SnapTransformToNavMesh(go.transform);

            var sweepAgent = go.AddComponent<SupermarketSweepRobotAgent>();
            var sweepSo = new SerializedObject(sweepAgent);
            sweepSo.FindProperty("visualRoot").objectReferenceValue = go;
            sweepSo.FindProperty("agent").objectReferenceValue = agent;
            sweepSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = go;
            MarkSceneDirty();
        }

        [MenuItem(MenuRoot + "Snap Sweep Robot To NavMesh")]
        static void SnapSelectedSweepRobotToNavMesh()
        {
            var existing = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<SupermarketSweepRobotAgent>()
                : null;
            existing ??= UnityEngine.Object.FindObjectOfType<SupermarketSweepRobotAgent>(true);
            if (existing == null)
            {
                EditorUtility.DisplayDialog("超市收纳", "未找到扫地机器人。", "确定");
                return;
            }

            SnapSweepRobotToNavMesh(existing);
            Selection.activeGameObject = existing.gameObject;
            MarkSceneDirty();
        }

        static void SnapSweepRobotToNavMesh(SupermarketSweepRobotAgent sweepRobot)
        {
            if (sweepRobot == null)
                return;

            SnapTransformToNavMesh(sweepRobot.transform);

            var navAgent = sweepRobot.GetComponent<NavMeshAgent>();
            if (navAgent != null && navAgent.enabled)
                navAgent.Warp(sweepRobot.transform.position);
        }

        static void SnapTransformToNavMesh(Transform target)
        {
            if (target == null)
                return;

            var pos = target.position;
            var x = pos.x;
            var z = pos.z;
            for (var y = pos.y + 8f; y >= pos.y - 12f; y -= 0.35f)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, y, z), out var hit, 12f, NavMesh.AllAreas))
                    continue;

                target.position = hit.position;
                return;
            }
        }

        [MenuItem(MenuRoot + "Ensure Director In Scene")]
        static void EnsureDirectorInScene()
        {
            var director = UnityEngine.Object.FindObjectOfType<SupermarketOrganizeDirector>();
            if (director == null)
            {
                var go = new GameObject(nameof(SupermarketOrganizeDirector));
                Undo.RegisterCreatedObjectUndo(go, "Create Director");
                director = go.AddComponent<SupermarketOrganizeDirector>();
            }

            if (director.GetComponent<SupermarketItemSpawner>() == null)
                director.gameObject.AddComponent<SupermarketItemSpawner>();

            if (director.GetComponent<SupermarketOrganizeIntroController>() == null)
                director.gameObject.AddComponent<SupermarketOrganizeIntroController>();

            AssignContainerZones(director);
            Selection.activeGameObject = director.gameObject;
            MarkSceneDirty();
        }

        static void EnsureTriggerCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null)
                collider = Undo.AddComponent<BoxCollider>(go);
            collider.isTrigger = true;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null)
                c = Undo.AddComponent<T>(go);
            return c;
        }

        static void MarkSceneDirty()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif

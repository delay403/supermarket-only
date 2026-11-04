using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Game.Hud;
using Game.UI;

namespace Game.EditorTools
{
    public static class UIPrefabGenerator
    {
        private const string PrefabDir = "Assets/Game/Prefabs/UI";

        [MenuItem("Game/Generate/Create UI Prefabs (UGUI)")]
        public static void CreateAll()
        {
            EnsureDir("Assets/Game/Prefabs");
            EnsureDir(PrefabDir);

            CreateHudPrefab();
            CreateGameHudUguiPrefab();
            CreateRodShopPrefab();
            CreateResultPrefab();
            CreatePetPrefab();
            CreatePetHatchPrefab();
            CreateWingShopPrefab();
            CreateTrailShopPrefab();
            CreateMapSelectPrefab();
            CreateSignInPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateHudPrefab()
        {
            var root = CreateCanvasRoot("HUD");
            var hud = root.AddComponent<HudController>();
            var ugui = root.AddComponent<GameHudUguiPanel>();
            WireGameHudUguiPanel(root, ugui, hud);
            SaveAsPrefab(root, $"{PrefabDir}/HUD.prefab");
        }

        private static void CreateGameHudUguiPrefab()
        {
            var root = CreateCanvasRoot("GameHudUGUI");
            var hud = root.AddComponent<HudController>();
            var panel = root.AddComponent<GameHudUguiPanel>();
            WireGameHudUguiPanel(root, panel, hud);
            SaveAsPrefab(root, $"{PrefabDir}/GameHudUGUI.prefab");
        }

        static void WireGameHudUguiPanel(GameObject root, GameHudUguiPanel panel, HudController hud)
        {
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("uguiHud").objectReferenceValue = panel;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var pso = new SerializedObject(panel);

            var topLeft = CreatePanel(root.transform, "TopLeft", Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(12f, -12f), new Vector2(420f, 120f), new Color(0, 0, 0, 0));
            pso.FindProperty("btnSetting").objectReferenceValue =
                CreateButton(topLeft.transform, "Setting", "设置").GetComponent<Button>();

            var topRight = CreatePanel(root.transform, "TopRight", Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-12f, -12f), new Vector2(520f, 80f), new Color(0, 0, 0, 0));
            pso.FindProperty("coinsText").objectReferenceValue =
                CreateText(topRight.transform, "Coins", "0", 20, TextAnchor.MiddleLeft);
            var gemsText = CreateText(topRight.transform, "Gems", "0", 20, TextAnchor.MiddleLeft);
            pso.FindProperty("gemsText").objectReferenceValue = gemsText;
            var gemsRt = (RectTransform)gemsText.transform;
            gemsRt.anchoredPosition = new Vector2(200f, -16f);

            var towerRoot = CreatePanel(root.transform, "TowerSection", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
            var towerRt = towerRoot.GetComponent<RectTransform>();
            towerRt.offsetMin = Vector2.zero;
            towerRt.offsetMax = Vector2.zero;
            pso.FindProperty("towerSectionRoot").objectReferenceValue = towerRoot;
            pso.FindProperty("heightText").objectReferenceValue =
                CreateText(towerRoot.transform, "Height", "当前: 0米", 22, TextAnchor.MiddleCenter);
            pso.FindProperty("staminaSlider").objectReferenceValue =
                CreateSlider(towerRoot.transform, "Stamina", new Vector2(0f, -80f), new Vector2(600f, 36f));
            pso.FindProperty("staminaLabel").objectReferenceValue =
                CreateText(towerRoot.transform, "StaminaLabel", "0/0", 18, TextAnchor.MiddleCenter);
            pso.FindProperty("btnAutoClimb").objectReferenceValue =
                CreateButton(towerRoot.transform, "AutoClimb", "自动攀爬").GetComponent<Button>();

            var fishRoot = CreatePanel(root.transform, "FishingSection", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
            var fishRt = fishRoot.GetComponent<RectTransform>();
            fishRt.offsetMin = Vector2.zero;
            fishRt.offsetMax = Vector2.zero;
            pso.FindProperty("fishingSectionRoot").objectReferenceValue = fishRoot;
            pso.FindProperty("fishingZoneLabel").objectReferenceValue =
                CreateText(fishRoot.transform, "Zone", "钓鱼区", 20, TextAnchor.UpperLeft);
            pso.FindProperty("strengthText").objectReferenceValue =
                CreateText(fishRoot.transform, "Strength", "1000", 22, TextAnchor.UpperLeft);
            var fishBtn = CreateButton(fishRoot.transform, "FishAction", "开始钓鱼").GetComponent<Button>();
            pso.FindProperty("btnFishingAction").objectReferenceValue = fishBtn;
            pso.FindProperty("fishingActionLabel").objectReferenceValue = fishBtn.GetComponentInChildren<Text>();
            pso.FindProperty("fishingProgressSlider").objectReferenceValue =
                CreateSlider(fishRoot.transform, "FishProgress", new Vector2(0f, -200f), new Vector2(700f, 40f));

            var rightBar = CreatePanel(root.transform, "RightBar", Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-24f, 0f), new Vector2(100f, 420f), new Color(0, 0, 0, 0));
            pso.FindProperty("routerWing").objectReferenceValue =
                CreateButton(rightBar.transform, "Wing", "翅膀").GetComponent<Button>();
            pso.FindProperty("routerRodShop").objectReferenceValue =
                CreateButton(rightBar.transform, "Rod", "钓竿").GetComponent<Button>();
            pso.FindProperty("routerFishCodex").objectReferenceValue =
                CreateButton(rightBar.transform, "Codex", "图鉴").GetComponent<Button>();
            pso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateRodShopPrefab()
        {
            var root = CreateCanvasRoot("RodShopPanel");
            var panel = root.AddComponent<RodShopPanel>();
            panel.root = CreateFullscreenDim(root.transform, "RootDim", 0.55f);
            panel.maskCloseButton = panel.root.GetComponent<Button>() ?? panel.root.AddComponent<Button>();
            panel.closeButton = CreateButton(panel.root.transform, "Close", "X").GetComponent<Button>();
            SaveAsPrefab(root, $"{PrefabDir}/RodShopPanel.prefab");
        }

        private static void CreateResultPrefab()
        {
            var root = CreateCanvasRoot("ResultPanel");
            root.AddComponent<ResultPanel>();
            SaveAsPrefab(root, $"{PrefabDir}/ResultPanel.prefab");
        }

        private static void CreatePetPrefab()
        {
            var root = CreateCanvasRoot("PetPanel");
            root.AddComponent<PetPanel>();
            SaveAsPrefab(root, $"{PrefabDir}/PetPanel.prefab");
        }

        private static void CreatePetHatchPrefab()
        {
            var root = CreateCanvasRoot("PetHatchPanel");
            root.AddComponent<PetHatchPanel>();
            SaveAsPrefab(root, $"{PrefabDir}/PetHatchPanel.prefab");
        }

        private static void CreateWingShopPrefab()
        {
            var root = CreateCanvasRoot("WingShopPanel");
            root.AddComponent<WingShopPanel>();
            // Serialized refs left default: WingShopPanel.Awake builds full shop UI at runtime.
            SaveAsPrefab(root, $"{PrefabDir}/WingShopPanel.prefab");
        }

        private static void CreateTrailShopPrefab()
        {
            var root = CreateCanvasRoot("TrailShopPanel");
            root.AddComponent<TrailShopPanel>();
            // Serialized refs left default; TrailShopPanel inherits WingShopPanel.Awake-built UI at runtime.
            SaveAsPrefab(root, $"{PrefabDir}/TrailShopPanel.prefab");
        }

        private static void CreateMapSelectPrefab()
        {
            EnsureDir(PrefabDir);
            var itemPrefab = EnsureMapItemViewPrefabAsset();

            var root = CreateCanvasRoot("MapSelectPanel");
            var panel = root.AddComponent<MapSelectPanel>();

            var dimGo = CreateFullscreenDim(root.transform, "RootDim", 0.55f);
            dimGo.transform.SetAsFirstSibling();
            var dimImg = dimGo.GetComponent<Image>();
            dimImg.raycastTarget = true;
            var dimBtn = dimGo.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;

            panel.root = dimGo;
            panel.maskCloseButton = dimBtn;

            var card = CreatePanel(dimGo.transform, "MainPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(980f, 640f), new Color(0.12f, 0.12f, 0.14f, 0.98f));
            panel.mainPanel = card.GetComponent<RectTransform>();
            card.transform.SetSiblingIndex(0);

            var title = CreateText(card.transform, "Title", "切换地图", 26, TextAnchor.MiddleCenter);
            StretchTopBand((RectTransform)title.transform, 52f, new Vector2(0f, -10f));

            panel.closeButton = CreateButton(card.transform, "Close", "关闭");
            var closeRt = panel.closeButton.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-14f, -14f);
            closeRt.sizeDelta = new Vector2(140f, 48f);

            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            previewGo.transform.SetParent(card.transform, false);
            var previewRt = previewGo.GetComponent<RectTransform>();
            previewRt.anchorMin = new Vector2(0.5f, 1f);
            previewRt.anchorMax = new Vector2(0.5f, 1f);
            previewRt.pivot = new Vector2(0.5f, 1f);
            previewRt.anchoredPosition = new Vector2(0f, -72f);
            previewRt.sizeDelta = new Vector2(560f, 200f);
            var previewImg = previewGo.GetComponent<Image>();
            previewImg.color = new Color(0.08f, 0.08f, 0.1f, 1f);
            panel.previewImage = previewImg;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(card.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.offsetMin = new Vector2(28f, 96f);
            scrollRt.offsetMax = new Vector2(-28f, -286f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
            var scroll = scrollGo.GetComponent<ScrollRect>();

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            var vpImg = viewportGo.GetComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            vpImg.raycastTarget = true;
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, -16f);

            var hlg = contentGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.padding = new RectOffset(8, 8, 8, 8);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            panel.scrollRect = scroll;
            panel.gridContent = contentRt;

            panel.itemPrefab = itemPrefab;
            if (panel.itemPrefab == null)
                Debug.LogWarning($"{nameof(UIPrefabGenerator)}: Failed to create/load MapItemView.prefab; assign {nameof(MapSelectPanel.itemPrefab)} manually.");

            panel.btnMain = CreateButton(card.transform, "BtnMain", "传送");
            PlaceBottomCenter(panel.btnMain.GetComponent<RectTransform>(), new Vector2(0f, 22f),
                new Vector2(340f, 56f));
            var btnLabelGo = panel.btnMain.transform.Find("Label");
            panel.btnMainLabel = btnLabelGo != null ? btnLabelGo.GetComponent<Text>() : null;

            CreateMapUnlockOverlay(panel, dimGo.transform);

            SaveAsPrefab(root, $"{PrefabDir}/MapSelectPanel.prefab");
        }

        private static void StretchTopBand(RectTransform rt, float height, Vector2 anchoredPos)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(-160f, height);
        }

        private static void PlaceBottomCenter(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void CreateMapUnlockOverlay(MapSelectPanel panel, Transform dimParent)
        {
            var overlay = CreateFullscreenDim(dimParent, "UnlockOverlay", 0.72f);
            overlay.SetActive(false);
            panel.unlockOverlayRoot = overlay;

            var dimImg = overlay.GetComponent<Image>();
            dimImg.raycastTarget = true;

            var popup = CreatePanel(overlay.transform, "UnlockPopup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560f, 420f), new Color(0.16f, 0.16f, 0.2f, 1f));
            panel.unlockPopupPanel = popup.GetComponent<RectTransform>();

            var title = CreateText(popup.transform, "UnlockTitle", "解锁新地图!", 26, TextAnchor.UpperCenter);
            var titleRt = (RectTransform)title.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -20f);
            titleRt.sizeDelta = new Vector2(-40f, 80f);
            panel.unlockTitleText = title;

            var iconGo = new GameObject("UnlockIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(popup.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0f, 14f);
            iconRt.sizeDelta = new Vector2(260f, 160f);
            panel.unlockMapIconImage = iconGo.GetComponent<Image>();
            panel.unlockMapIconImage.color = new Color(1f, 1f, 1f, 0.15f);

            panel.unlockBtnReturn = CreateButton(popup.transform, "BtnReturn", "返回");
            PlaceBottomLeft(panel.unlockBtnReturn.GetComponent<RectTransform>(), new Vector2(36f, 28f),
                new Vector2(200f, 52f));

            panel.unlockBtnTeleport = CreateButton(popup.transform, "BtnTeleportPopup", "传送");
            PlaceBottomRight(panel.unlockBtnTeleport.GetComponent<RectTransform>(), new Vector2(-36f, 28f),
                new Vector2(200f, 52f));

            overlay.transform.SetAsLastSibling();
        }

        private static MapItemView EnsureMapItemViewPrefabAsset()
        {
            var path = $"{PrefabDir}/MapItemView.prefab";
            var existingGo = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existingGo != null)
            {
                var mv = existingGo.GetComponent<MapItemView>();
                if (mv != null)
                    return mv;
            }

            var instance = BuildMapItemViewInstance();
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return saved != null ? saved.GetComponent<MapItemView>() : null;
        }

        private static GameObject BuildMapItemViewInstance()
        {
            const float w = 204f;
            const float h = 268f;

            var root = new GameObject("MapItemView", typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);

            var le = root.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.14f, 0.18f, 0.96f);
            bg.raycastTarget = true;

            var cellBtn = root.AddComponent<Button>();
            cellBtn.targetGraphic = bg;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(root.transform, false);
            var prt = portraitGo.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 1f);
            prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -10f);
            prt.sizeDelta = new Vector2(w - 14f, 146f);
            var portraitImg = portraitGo.GetComponent<Image>();
            portraitImg.color = new Color(1f, 1f, 1f, 0.14f);
            portraitImg.preserveAspect = true;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(root.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0f, 10f);
            nameRt.sizeDelta = new Vector2(-12f, 34f);
            var nameTxt = nameGo.GetComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameTxt.fontSize = 20;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;
            nameTxt.text = "地图";

            var lockRoot = new GameObject("LockRoot", typeof(RectTransform), typeof(Image));
            lockRoot.transform.SetParent(root.transform, false);
            var lockRt = lockRoot.GetComponent<RectTransform>();
            lockRt.anchorMin = new Vector2(0f, 1f);
            lockRt.anchorMax = new Vector2(0f, 1f);
            lockRt.pivot = new Vector2(0f, 1f);
            lockRt.anchoredPosition = new Vector2(8f, -8f);
            lockRt.sizeDelta = new Vector2(44f, 44f);
            lockRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var prizeRoot = new GameObject("PrizeRoot", typeof(RectTransform));
            prizeRoot.transform.SetParent(root.transform, false);
            var prizeRt = prizeRoot.GetComponent<RectTransform>();
            prizeRt.anchorMin = new Vector2(1f, 1f);
            prizeRt.anchorMax = new Vector2(1f, 1f);
            prizeRt.pivot = new Vector2(1f, 1f);
            prizeRt.anchoredPosition = new Vector2(-8f, -8f);
            prizeRt.sizeDelta = new Vector2(92f, 36f);

            var medalImgGo = new GameObject("MedalIcon", typeof(RectTransform), typeof(Image));
            medalImgGo.transform.SetParent(prizeRoot.transform, false);
            var medalRt = medalImgGo.GetComponent<RectTransform>();
            medalRt.anchorMin = new Vector2(0f, 0.5f);
            medalRt.anchorMax = new Vector2(0f, 0.5f);
            medalRt.pivot = new Vector2(0f, 0.5f);
            medalRt.anchoredPosition = Vector2.zero;
            medalRt.sizeDelta = new Vector2(32f, 32f);
            var medalImg = medalImgGo.GetComponent<Image>();
            medalImg.color = new Color(1f, 0.85f, 0.35f, 1f);

            var amtGo = new GameObject("Amount", typeof(RectTransform), typeof(Text));
            amtGo.transform.SetParent(prizeRoot.transform, false);
            var amtRt = amtGo.GetComponent<RectTransform>();
            amtRt.anchorMin = new Vector2(1f, 0.5f);
            amtRt.anchorMax = new Vector2(1f, 0.5f);
            amtRt.pivot = new Vector2(1f, 0.5f);
            amtRt.anchoredPosition = Vector2.zero;
            amtRt.sizeDelta = new Vector2(56f, 32f);
            var amtTxt = amtGo.GetComponent<Text>();
            amtTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            amtTxt.fontSize = 18;
            amtTxt.alignment = TextAnchor.MiddleRight;
            amtTxt.color = Color.white;
            amtTxt.text = "x1";

            var bannerRoot = new GameObject("CurrentBanner", typeof(RectTransform), typeof(Image));
            bannerRoot.transform.SetParent(root.transform, false);
            var bannerRt = bannerRoot.GetComponent<RectTransform>();
            bannerRt.anchorMin = new Vector2(0.5f, 1f);
            bannerRt.anchorMax = new Vector2(0.5f, 1f);
            bannerRt.pivot = new Vector2(0.5f, 1f);
            bannerRt.anchoredPosition = new Vector2(0f, -52f);
            bannerRt.sizeDelta = new Vector2(112f, 26f);
            bannerRoot.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.95f, 0.92f);

            var bannerTxtGo = new GameObject("BannerLabel", typeof(RectTransform), typeof(Text));
            bannerTxtGo.transform.SetParent(bannerRoot.transform, false);
            var bannerTxtRt = bannerTxtGo.GetComponent<RectTransform>();
            bannerTxtRt.anchorMin = Vector2.zero;
            bannerTxtRt.anchorMax = Vector2.one;
            bannerTxtRt.offsetMin = Vector2.zero;
            bannerTxtRt.offsetMax = Vector2.zero;
            var bannerTxt = bannerTxtGo.GetComponent<Text>();
            bannerTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            bannerTxt.fontSize = 15;
            bannerTxt.alignment = TextAnchor.MiddleCenter;
            bannerTxt.color = Color.white;
            bannerTxt.text = "当前地图";
            bannerRoot.SetActive(false);

            var frameGo = new GameObject("SelectionFrame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(root.transform, false);
            var frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.anchorMin = Vector2.zero;
            frameRt.anchorMax = Vector2.one;
            frameRt.offsetMin = new Vector2(-4f, -4f);
            frameRt.offsetMax = new Vector2(4f, 4f);
            var frameImg = frameGo.GetComponent<Image>();
            frameImg.color = new Color(1f, 0.55f, 0.15f, 0.95f);
            frameImg.type = Image.Type.Simple;
            frameImg.raycastTarget = false;
            frameGo.SetActive(false);

            var checkRoot = new GameObject("SelectedCheck", typeof(RectTransform), typeof(Image));
            checkRoot.transform.SetParent(root.transform, false);
            var checkRt = checkRoot.GetComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(1f, 1f);
            checkRt.anchorMax = new Vector2(1f, 1f);
            checkRt.pivot = new Vector2(1f, 1f);
            checkRt.anchoredPosition = new Vector2(-6f, -52f);
            checkRt.sizeDelta = new Vector2(36f, 36f);
            checkRoot.GetComponent<Image>().color = new Color(0.3f, 0.85f, 0.35f, 1f);
            checkRoot.SetActive(false);

            var mapItem = root.AddComponent<MapItemView>();
            SerializedObject so = new SerializedObject(mapItem);
            so.FindProperty("cellButton").objectReferenceValue = cellBtn;
            so.FindProperty("portraitImage").objectReferenceValue = portraitImg;
            so.FindProperty("nameText").objectReferenceValue = nameTxt;
            so.FindProperty("lockRoot").objectReferenceValue = lockRoot;
            so.FindProperty("prizeRoot").objectReferenceValue = prizeRoot;
            so.FindProperty("prizeCurrencyIcon").objectReferenceValue = medalImg;
            so.FindProperty("prizeAmountText").objectReferenceValue = amtTxt;
            so.FindProperty("currentMapBannerRoot").objectReferenceValue = bannerRoot;
            so.FindProperty("currentMapBannerText").objectReferenceValue = bannerTxt;
            so.FindProperty("selectionFrame").objectReferenceValue = frameRt;
            so.FindProperty("selectedCheckRoot").objectReferenceValue = checkRoot;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void CreateSignInPrefab()
        {
            var root = CreateCanvasRoot("SignInPanel");
            var panel = root.AddComponent<SignInPanel>();
            panel.root = CreateFullscreenDim(root.transform, "RootDim", 0.6f);

            var card = CreatePanel(panel.root.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(980f, 640f), new Color(0.1f, 0.1f, 0.1f, 0.95f));

            panel.infoText = CreateText(card.transform, "Info", "Daily Sign-In", 22, TextAnchor.UpperLeft);
            var infoRt = (RectTransform)panel.infoText.transform;
            infoRt.anchorMin = new Vector2(0f, 0f);
            infoRt.anchorMax = new Vector2(1f, 1f);
            infoRt.pivot = new Vector2(0f, 1f);
            infoRt.anchoredPosition = new Vector2(24f, -24f);
            infoRt.sizeDelta = new Vector2(-48f, -140f);

            panel.claimButton = CreateButton(card.transform, "Claim", "Claim Today");
            PlaceBottomLeft(panel.claimButton.GetComponent<RectTransform>(), new Vector2(24f, 24f), new Vector2(240f, 54f));

            panel.closeButton = CreateButton(card.transform, "Close", "Close");
            PlaceBottomRight(panel.closeButton.GetComponent<RectTransform>(), new Vector2(-24f, 24f), new Vector2(180f, 54f));

            SaveAsPrefab(root, $"{PrefabDir}/SignInPanel.prefab");
        }

        private static GameObject CreateCanvasRoot(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static GameObject CreateFullscreenDim(Transform parent, string name, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            return go;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -16f);
            rt.sizeDelta = new Vector2(380f, 48f);

            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = text;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var btn = go.GetComponent<Button>();

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var t = textGo.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 22;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;
            return btn;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var sliderGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);
            var rt = (RectTransform)sliderGo.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            // Fill Area
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var faRt = (RectTransform)fillAreaGo.transform;
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.offsetMin = new Vector2(4f, 4f);
            faRt.offsetMax = new Vector2(-4f, -4f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillGo.GetComponent<Image>().color = new Color(0.2f, 0.9f, 0.3f, 0.9f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.fillRect = fillRt;
            slider.targetGraphic = fillGo.GetComponent<Image>();
            slider.transition = Selectable.Transition.None;
            slider.handleRect = null;
            return slider;
        }

        private static void PlaceBottomLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void PlaceBottomRight(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void SaveAsPrefab(GameObject root, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                Object.DestroyImmediate(existing);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureDir(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}


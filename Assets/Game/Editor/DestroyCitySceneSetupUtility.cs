using System;
using System.Collections.Generic;
using Game.Core.Content;
using Game.Collectibles.Stats;
using Game.DestroyCity;
using Game.Interaction;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// 摧毁城市场景组件配置工具（选中物体后执行对应菜单）。
    /// </summary>
    public static class DestroyCitySceneSetupUtility
    {
        const string InteractableLayerName = "WorldInteractable";
        const int DefaultSpawnPointCount = 4;

        static readonly (string nameHint, string producerId)[] ProducerNameHints =
        {
            ("Manual", FuelProducerIds.Manual),
            ("手动", FuelProducerIds.Manual),
            ("Auto1", FuelProducerIds.Auto1),
            ("Auto_1", FuelProducerIds.Auto1),
            ("Auto2", FuelProducerIds.Auto2),
            ("Auto_2", FuelProducerIds.Auto2),
            ("Auto3", FuelProducerIds.Auto3),
            ("Auto_3", FuelProducerIds.Auto3),
        };

        [MenuItem("Game/Destroy City/Setup Selected Fuel Producer")]
        public static void SetupSelectedFuelProducer()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("Destroy City Setup", "请先选中燃料生产器根物体。", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Setup Fuel Producer");
            SetupFuelProducerRoot(root, ResolveProducerId(root.name));
            MarkSceneDirty();
            Debug.Log($"[DestroyCitySetup] 已配置燃料生产器: {root.name}", root);
        }

        [MenuItem("Game/Destroy City/Setup Selected Nuke Launcher")]
        public static void SetupSelectedNukeLauncher()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("Destroy City Setup", "请先选中核弹发射器根物体。", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Setup Nuke Launcher");
            SetupNukeLauncherRoot(root);
            MarkSceneDirty();
            Debug.Log($"[DestroyCitySetup] 已配置核弹发射器: {root.name}", root);
        }

        [MenuItem("Game/Destroy City/Fix Player For Destroy City")]
        public static void FixPlayerForDestroyCityMenu()
        {
            var player = Selection.activeGameObject;
            if (player == null)
                player = FindPlayerRoot();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Destroy City", "未找到 Player 物体。", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(player, "Fix Player For Destroy City");
            FixPlayerForDestroyCity(player);
            RemoveLegacySceneBootstrap();
            MarkSceneDirty();
            Selection.activeGameObject = player;
            Debug.Log("[DestroyCitySetup] Player 已切换为摧毁城市组件。", player);
        }

        [MenuItem("Game/Destroy City/Remove Legacy Scene Bootstrap")]
        public static void RemoveLegacySceneBootstrapMenu()
        {
            RemoveLegacySceneBootstrap();
            MarkSceneDirty();
        }

        [MenuItem("Game/Destroy City/Assign House Layer To Buildings")]
        public static void AssignHouseLayerToBuildingsMenu()
        {
            var layer = LayerMask.NameToLayer("House");
            if (layer < 0)
            {
                EditorUtility.DisplayDialog("Destroy City", "Project Settings 中未找到 House Layer。", "OK");
                return;
            }

            var count = 0;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                count += AssignHouseLayerRecursive(roots[i].transform, layer);

            MarkSceneDirty();
            Debug.Log($"[DestroyCitySetup] 已为 {count} 个建筑根节点设置 House Layer。");
        }

        [MenuItem("Game/Destroy City/Ensure View Mode Toggle In HUD")]
        public static void EnsureViewModeToggleInHudMenu()
        {
            var hud = UnityEngine.Object.FindObjectOfType<DestroyCityHudSection>(true);
            if (hud == null)
            {
                EditorUtility.DisplayDialog("Destroy City", "场景中未找到 DestroyCityHudSection。", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Ensure View Mode Toggle");
            var existing = hud.GetComponentInChildren<NukeLaunchViewModeToggle>(true);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[DestroyCitySetup] 已存在导弹视角切换按钮。", existing);
                return;
            }

            var buttonGo = new GameObject("BtnNukeViewMode", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(buttonGo, "Ensure View Mode Toggle");
            buttonGo.transform.SetParent(hud.transform, false);

            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 120f);
            rect.sizeDelta = new Vector2(160f, 48f);

            var image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.12f, 0.45f, 0.85f, 0.92f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.GetComponent<Text>();
            label.text = "导弹视角";
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 22;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var toggle = buttonGo.AddComponent<NukeLaunchViewModeToggle>();
            SetSerialized(toggle, "toggleButton", buttonGo.GetComponent<Button>());
            SetSerialized(toggle, "labelText", label);
            SetSerialized(hud, "viewModeToggleRoot", buttonGo);
            SetSerialized(hud, "viewModeToggleButton", buttonGo.GetComponent<Button>());
            SetSerialized(hud, "viewModeToggle", toggle);

            Selection.activeGameObject = buttonGo;
            MarkSceneDirty();
            Debug.Log("[DestroyCitySetup] 已创建导弹视角切换按钮。", buttonGo);
        }

        static int AssignHouseLayerRecursive(Transform node, int layer)
        {
            if (node == null)
                return 0;

            var count = 0;
            var name = node.name;
            if (name.Contains("House") || name.Contains("house") || name.Contains("fangzi"))
            {
                Undo.RecordObject(node.gameObject, "Assign House Layer");
                node.gameObject.layer = layer;
                count++;
                return count;
            }

            for (var i = 0; i < node.childCount; i++)
                count += AssignHouseLayerRecursive(node.GetChild(i), layer);

            return count;
        }

        [MenuItem("Game/Destroy City/Ensure Scene XP Floater Settings")]
        public static void EnsureSceneXpFloaterSettingsMenu()
        {
            var settings = EnsureSceneXpFloaterSettings();
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Destroy City", "当前场景无效。", "OK");
                return;
            }

            MarkSceneDirty();
            Selection.activeGameObject = settings.gameObject;
            Debug.Log("[DestroyCitySetup] 已确保场景存在 DestroyCityXpFloaterSettings。请将自制飘字预制体拖到 Prefab 字段。", settings);
        }

        [MenuItem("Game/Destroy City/Create Default XP Float Prefab")]
        public static void CreateDefaultXpFloatPrefab()
        {
            var folder = GameContentPaths.DestroyCityModuleRoot;
            var path = folder + "/DestroyCityXpFloat.prefab";

            EnsureContentFolder(GameContentPaths.ModeResourcesRoot(Game.Core.GameplayModule.DestroyCity));

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Destroy City",
                        "Resources/DestroyCity/DestroyCityXpFloat.prefab 已存在，是否覆盖？",
                        "覆盖",
                        "取消"))
                    return;
            }

            var root = new GameObject("DestroyCityXpFloat");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.transform.localScale = Vector3.one * 0.012f;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(root.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320f, 80f);
            rt.localPosition = Vector3.zero;

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 42;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.35f, 1f, 0.45f, 1f);
            text.raycastTarget = false;
            text.text = "+10 XP";

            root.AddComponent<DestroyCityXpFloatView>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);

            if (prefab != null)
            {
                var view = prefab.GetComponent<DestroyCityXpFloatView>();
                var so = new SerializedObject(view);
                so.FindProperty("label").objectReferenceValue = prefab.GetComponentInChildren<Text>(true);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = prefab;
            Debug.Log($"[DestroyCitySetup] 已创建默认飘字预制体: {path}");
        }

        [MenuItem("Game/Destroy City/Setup Fuel Xp Bar Texts")]
        public static void SetupFuelXpBarTexts()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("Destroy City", "请先选中燃料预制体或实例。", "OK");
                return;
            }

            var xpBar = root.GetComponentInChildren<FuelInstanceXpBar>(true);
            if (xpBar == null)
            {
                EditorUtility.DisplayDialog("Destroy City", "未找到 FuelInstanceXpBar 组件。", "OK");
                return;
            }

            var canvas = xpBar.GetComponent<RectTransform>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Destroy City", "FuelInstanceXpBar 需挂在 Canvas 上。", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Setup Fuel Xp Bar Texts");

            var levelText = EnsureXpTextChild(canvas, "LevelText", new Vector2(0f, 30f), 36, "Lv.1");
            var xpText = EnsureXpTextChild(canvas, "XpText", new Vector2(0f, -50f), 32, "0/100");

            var so = new SerializedObject(xpBar);
            so.FindProperty("levelText").objectReferenceValue = levelText;
            so.FindProperty("xpText").objectReferenceValue = xpText;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(xpBar);

            Selection.activeGameObject = root;
            MarkSceneDirty();
            Debug.Log("[DestroyCitySetup] 已创建 LevelText / XpText 并绑定到 FuelInstanceXpBar。", root);
        }

        static Text EnsureXpTextChild(RectTransform parent, string name, Vector2 anchoredPos, int fontSize, string defaultText)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                var t = existing.GetComponent<Text>();
                if (t != null)
                    return t;
            }

            var go = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                go.transform.SetParent(parent, false);
            }

            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
                rt = Undo.AddComponent<RectTransform>(go);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 80f);
            rt.anchoredPosition = anchoredPos;

            var text = go.GetComponent<Text>();
            if (text == null)
                text = Undo.AddComponent<Text>(go);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = defaultText;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (go.GetComponent<Outline>() == null)
            {
                var outline = Undo.AddComponent<UnityEngine.UI.Outline>(go);
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            return text;
        }

        static GameObject FindPlayerRoot()
        {
            var motor = UnityEngine.Object.FindObjectOfType<PlayerMotor>();
            return motor != null ? motor.gameObject : null;
        }

        static void FixPlayerForDestroyCity(GameObject player)
        {
            RemoveFishingComponents(player);

            EnsureComponent<PlayerDestroyCityController>(player);
            EnsureComponent<DestroyCityStatsAggregator>(player);
            EnsureComponent<PlayerFuelCarryController>(player);

            var interactor = EnsureComponent<WorldRaycastInteractor>(player);
            var cam = Camera.main;
            if (cam != null)
            {
                var so = new SerializedObject(interactor);
                so.FindProperty("raycastCamera").objectReferenceValue = cam;
                so.FindProperty("interactorRoot").objectReferenceValue = player.transform;
                var layer = LayerMask.GetMask(InteractableLayerName);
                if (layer != 0)
                    so.FindProperty("interactableMask").intValue = layer;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var rig = player.GetComponent<PlayerRig>();
            if (rig != null)
                rig.enabled = true;

            if (player.GetComponent<RectTransform>() != null)
            {
                Debug.LogWarning(
                    "[DestroyCitySetup] Player 使用 RectTransform（来自钓鱼场景拷贝）。若移动/碰撞异常，请将 Player 放到世界坐标并改用 Transform。",
                    player);
            }
        }

        static void RemoveFishingComponents(GameObject player)
        {
            RemoveComponentIfExists(player, "PlayerFishingController");
            RemoveComponentIfExists(player, "FishingStatsAggregator");
            RemoveComponentIfExists(player, "FishingCameraFeedback");
            RemoveComponentIfExists(player, "FishingCatchNotificationPresenter");
            RemoveComponentIfExists(player, "PlayerFishingLineDriver");
            RemoveComponentIfExists(player, "PlayerFishingAnimatorDriver");
            RemoveComponentIfExists(player, "PlayerFishingCatchPresenter");
            RemoveComponentIfExists(player, "FishingPresentationFx");
            RemoveComponentIfExists(player, "FishingHudInputBridge");
            RemoveComponentIfExists(player, "FishingPlaytestInput");
        }

        static void RemoveComponentIfExists(GameObject go, string typeName)
        {
            var components = go.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c == null)
                    continue;
                if (c.GetType().Name == typeName)
                    Undo.DestroyObjectImmediate(c);
            }
        }

        static void RemoveLegacySceneBootstrap()
        {
            var legacy = GameObject.Find("DestroyCitySceneBootstrap");
            if (legacy != null)
                Undo.DestroyObjectImmediate(legacy);
        }

        static void SetupFuelProducerRoot(GameObject root, string producerId)
        {
            EnsureInteractableLayerOnHierarchy(root);

            var produceButtonGo = FindDeepChild(root.transform, "ProduceButton", "生产按钮", "Button");
            var upgradeButtonGo = FindDeepChild(root.transform, "UpgradeButton", "升级按钮");
            var unlockGateGo = FindDeepChild(root.transform, "UnlockGate", "Unlock", "锁定");
            var fuelRoot = FindDeepChild(root.transform, "FuelInstanceRoot", "FuelRoot", "生产区");
            var dropletSpawn = FindDeepChild(root.transform, "DropletSpawnPoint", "FuelDropletSpawn", "液滴");
            var lockedVisual = FindDeepChild(root.transform, "LockedVisual", "Locked", "锁定显示");
            var activeVisual = FindDeepChild(root.transform, "ActiveVisual", "Active", "正常显示");
            var spawnPoints = CollectSpawnPoints(root.transform);

            if (produceButtonGo == null)
                produceButtonGo = CreateInteractableChild(root, "ProduceButton", Vector3.up * 0.5f, new Vector3(0.6f, 0.2f, 0.6f));
            if (fuelRoot == null)
                fuelRoot = CreateChild(root, "FuelInstanceRoot");
            if (dropletSpawn == null)
            {
                dropletSpawn = CreateChild(root, "DropletSpawnPoint");
                dropletSpawn.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            }
            if (spawnPoints.Count == 0)
            {
                var spawnRoot = FindDeepChild(root.transform, "FuelSpawnPoints", "SpawnPoints");
                if (spawnRoot == null)
                    spawnRoot = CreateChild(root, "FuelSpawnPoints");
                for (var i = 0; i < DefaultSpawnPointCount; i++)
                {
                    var sp = FindDeepChild(spawnRoot.transform, $"Spawn{i + 1}", $"Point{i + 1}");
                    if (sp == null)
                    {
                        sp = CreateChild(spawnRoot, $"Spawn{i + 1}");
                        sp.transform.localPosition = new Vector3((i - 1.5f) * 0.6f, 0.05f, 0f);
                    }

                    spawnPoints.Add(sp.transform);
                }
            }

            EnsureCollider(produceButtonGo);
            var produceBtn = EnsureComponent<FuelProducerProduceButton>(produceButtonGo);

            FuelProducerUpgradeUi upgradeUi = null;
            var upgradeCanvasGo = activeVisual != null
                ? FindCanvasUnder(activeVisual)
                : FindDeepChild(root.transform, "Canvas");
            if (upgradeCanvasGo != null)
            {
                upgradeUi = EnsureComponent<FuelProducerUpgradeUi>(upgradeCanvasGo);
                var uiButton = upgradeCanvasGo.GetComponentInChildren<Button>(true);
                if (uiButton != null)
                    SetSerialized(upgradeUi, "upgradeButton", uiButton);

                var canvas = upgradeCanvasGo.GetComponent<Canvas>();
                Game.UI.WorldSpace3DCanvas.EnsureCanvasInputReady(canvas);
            }

            if (upgradeButtonGo != null)
            {
                var legacyComponents = upgradeButtonGo.GetComponents<WorldInteractableBase>();
                for (var i = 0; i < legacyComponents.Length; i++)
                    Undo.DestroyObjectImmediate(legacyComponents[i]);
            }

            FuelProducerUnlockUi unlockUi = null;
            var unlockCanvasGo = lockedVisual != null ? FindCanvasUnder(lockedVisual) : null;
            if (unlockCanvasGo != null)
            {
                unlockUi = EnsureComponent<FuelProducerUnlockUi>(unlockCanvasGo);
                var unlockButton = unlockCanvasGo.GetComponentInChildren<Button>(true);
                if (unlockButton != null)
                {
                    SetSerialized(unlockUi, "unlockButton", unlockButton);
                    var label = unlockButton.GetComponentInChildren<Text>(true);
                    if (label != null)
                        SetSerialized(unlockUi, "buttonLabelText", label);
                }

                var conditionText = FindTextOnCanvas(unlockCanvasGo.transform, "Condition", "UnlockCondition", "条件");
                if (conditionText != null)
                    SetSerialized(unlockUi, "conditionText", conditionText);

                var unlockCanvas = unlockCanvasGo.GetComponent<Canvas>();
                Game.UI.WorldSpace3DCanvas.EnsureCanvasInputReady(unlockCanvas);
            }

            RetireUnlockGate(unlockGateGo);

            var controller = EnsureComponent<FuelProducerController>(root);
            SetSerialized(controller, "producerId", producerId);
            SetSerializedArray(controller, "spawnPoints", spawnPoints.ToArray());
            SetSerialized(controller, "produceButton", produceBtn);
            SetSerialized(controller, "upgradeUi", upgradeUi);
            SetSerialized(controller, "unlockUi", unlockUi);
            SetSerialized(controller, "lockedVisualRoot", lockedVisual);
            SetSerialized(controller, "activeVisualRoot", activeVisual);
            SetSerialized(controller, "fuelInstanceRoot", fuelRoot.transform);

            var dropletEmitter = EnsureComponent<FuelProducerDropletEmitter>(root);
            SetSerialized(dropletEmitter, "spawnPoint", dropletSpawn.transform);

            TryAssignDefaultFuelPrefab(controller);

            var produceSlot = EnsureInteractionSlotOnChild(
                produceButtonGo,
                "ProduceInteractionZone",
                WorldInteractionKind.ProduceFuel,
                produceBtn,
                null,
                Vector3.zero,
                new Vector3(1.4f, 0.8f, 1.4f),
                20);

            var interactionHub = EnsureComponent<FuelProducerInteractionHub>(root);
            SetSerialized(interactionHub, "produceSlot", produceSlot);
            SetSerialized(controller, "interactionHub", interactionHub);
        }

        static WorldInteractionSlot EnsureInteractionSlotOnChild(
            GameObject parent,
            string childName,
            WorldInteractionKind kind,
            WorldInteractableBase interactable,
            MonoBehaviour handler,
            Vector3 localCenter,
            Vector3 localSize,
            int selectionPriority)
        {
            var slotGo = parent != null
                ? FindDeepChild(parent.transform, childName, "InteractionZone", "InteractionTrigger")
                : null;
            if (slotGo == null && parent != null)
                slotGo = CreateChild(parent, childName);

            return ConfigureInteractionSlot(slotGo, kind, interactable, handler, localCenter, localSize, selectionPriority);
        }

        static WorldInteractionSlot ConfigureInteractionSlot(
            GameObject slotGo,
            WorldInteractionKind kind,
            WorldInteractableBase interactable,
            MonoBehaviour handler,
            Vector3 localCenter,
            Vector3 localSize,
            int selectionPriority)
        {
            if (slotGo == null)
                return null;

            var box = slotGo.GetComponent<BoxCollider>();
            if (box == null)
                box = Undo.AddComponent<BoxCollider>(slotGo);

            box.isTrigger = true;
            box.center = localCenter;
            box.size = localSize;

            var slot = EnsureComponent<WorldInteractionSlot>(slotGo);
            SetSerializedEnum(slot, "kind", kind);
            SetSerialized(slot, "selectionPriority", selectionPriority);
            SetSerialized(slot, "interactableTarget", interactable);
            SetSerialized(slot, "handlerTarget", handler);
            return slot;
        }

        static void RemoveLegacyCombinedInteractionZone(Transform root)
        {
            var legacyZone = FindDeepChild(root, "InteractionZone", "InteractZone", "交互区");
            if (legacyZone == null || legacyZone.transform.parent != root)
                return;

            Undo.DestroyObjectImmediate(legacyZone);
        }

        static void TryAssignDefaultFuelPrefab(FuelProducerController controller)
        {
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("fuelInstancePrefab");
            if (prop == null || prop.objectReferenceValue != null)
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<FuelInstanceView>("Assets/Prefabs/摧毁城市/燃料 1.prefab");
            if (prefab == null)
                return;

            prop.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EnsureFuelPrefabInteractionSlot(prefab);
        }

        static void EnsureFuelPrefabInteractionSlot(FuelInstanceView fuelPrefab)
        {
            if (fuelPrefab == null)
                return;

            var slot = EnsureInteractionSlotOnChild(
                fuelPrefab.gameObject,
                "InteractionTrigger",
                WorldInteractionKind.PickupFuel,
                fuelPrefab,
                null,
                Vector3.zero,
                new Vector3(1.6f, 1.6f, 1.6f),
                15);

            SetSerialized(fuelPrefab, "interactionSlot", slot);
            EditorUtility.SetDirty(fuelPrefab);
        }

        static void SetupNukeLauncherRoot(GameObject root)
        {
            EnsureInteractableLayerOnHierarchy(root);

            var launchButtonGo = FindDeepChild(root.transform, "LaunchButton", "发射按钮", "Launch");
            var fuelDepositGo = FindDeepChild(root.transform, "FuelDepositZone", "FuelDeposit", "燃料放置", "燃料区");
            var nukeVisual = FindDeepChild(root.transform, "NukeVisualRoot", "NukeVisual", "核弹");
            var cityTarget = FindCityTargetInScene()
                             ?? FindDeepChild(root.transform, "CityTarget")?.transform
                             ?? FindDeepChildInScene("CityTarget", "城市区", "City");

            if (launchButtonGo == null)
                launchButtonGo = CreateInteractableChild(root, "LaunchButton", new Vector3(0f, 0.5f, 1f), new Vector3(0.8f, 0.25f, 0.8f));
            if (fuelDepositGo == null)
                fuelDepositGo = CreateInteractableChild(root, "FuelDepositZone", new Vector3(1.2f, 0.2f, 0f), new Vector3(0.8f, 0.1f, 0.8f));
            if (nukeVisual == null)
            {
                nukeVisual = CreateChild(root, "NukeVisualRoot");
                nukeVisual.transform.localPosition = Vector3.up;
            }

            EnsureCollider(launchButtonGo);

            var launcher = EnsureComponent<NukeLauncherController>(root);
            var launchBtn = EnsureComponent<NukeLaunchButton>(launchButtonGo);
            var deposit = EnsureComponent<NukeFuelDepositZone>(fuelDepositGo);
            var nukeCanvasGo = FindLauncherHudCanvas(root.transform);
            NukeLauncherXpBar nukeXpBar = null;
            if (nukeCanvasGo != null)
            {
                nukeXpBar = EnsureComponent<NukeLauncherXpBar>(nukeCanvasGo);
                EnsureNukeXpBarUi(nukeCanvasGo.transform);
            }

            var lid = FindDeepChild(root.transform, "gaizi");
            var spawnPoint = FindDeepChild(root.transform, "MissileSpawnPoint", "SpawnPoint", "导弹生成点");
            if (spawnPoint == null && nukeVisual != null)
            {
                spawnPoint = CreateChild(nukeVisual, "MissileSpawnPoint");
                spawnPoint.transform.localPosition = Vector3.zero;
            }

            var alarm = EnsureComponent<NukeLauncherAlarmEffects>(root);
            var landingMarkerGo = FindDeepChild(root.transform, "LandingMarker", "落点标记");
            NukeLandingMarker landingMarker = null;
            if (landingMarkerGo != null)
                landingMarker = EnsureComponent<NukeLandingMarker>(landingMarkerGo);

            SetSerialized(launcher, "launchButton", launchBtn);
            SetSerialized(launcher, "fuelDepositZone", deposit);
            SetSerialized(launcher, "nukeXpBar", nukeXpBar);
            SetSerialized(launcher, "nukeVisualRoot", nukeVisual.transform);
            if (spawnPoint != null)
                SetSerialized(launcher, "missileSpawnPoint", spawnPoint.transform);
            if (lid != null)
                SetSerialized(launcher, "lidTransform", lid.transform);
            SetSerialized(launcher, "alarmEffects", alarm);
            SetSerialized(launcher, "landingMarker", landingMarker);
            if (fuelDepositGo != null)
                SetSerialized(launcher, "nukeUpgradeFxAnchor", fuelDepositGo.transform);
            if (cityTarget != null)
            {
                SetSerialized(launcher, "cityTarget", cityTarget);
                var steer = EnsureComponent<NukeCityTargetSteerInput>(cityTarget.gameObject);
                SetSerialized(steer, "launcher", launcher);
                SetSerialized(steer, "steerTransform", cityTarget);
            }

            var flightCam = UnityEngine.Object.FindObjectOfType<NukeMissileFlightCamera>()
                            ?? EnsureComponent<NukeMissileFlightCamera>(root);
            SetSerialized(launcher, "flightCamera", flightCam);

            if (UnityEngine.Object.FindObjectOfType<NukeLaunchViewModeController>() == null)
                EnsureComponent<NukeLaunchViewModeController>(root);

            RemoveLegacyCombinedInteractionZone(root.transform);

            var launchSlot = EnsureInteractionSlotOnChild(
                launchButtonGo,
                "LaunchInteractionZone",
                WorldInteractionKind.LaunchMissile,
                launchBtn,
                null,
                Vector3.zero,
                new Vector3(1.6f, 1.2f, 1.6f),
                30);

            var depositSlot = EnsureInteractionSlotOnChild(
                fuelDepositGo,
                "DepositInteractionZone",
                WorldInteractionKind.DepositFuel,
                null,
                deposit,
                Vector3.zero,
                new Vector3(2.4f, 1.2f, 2.4f),
                25);

            var interactionHub = EnsureComponent<NukeLauncherInteractionHub>(root);
            SetSerialized(launcher, "interactionHub", interactionHub);
            SetSerialized(interactionHub, "launchSlot", launchSlot);
            SetSerialized(interactionHub, "depositFuelSlot", depositSlot);
        }

        static List<Transform> CollectSpawnPoints(Transform root)
        {
            var list = new List<Transform>();
            var spawnRoot = FindDeepChild(root, "FuelSpawnPoints", "SpawnPoints", "生产点");
            if (spawnRoot == null)
                return list;

            for (var i = 0; i < spawnRoot.transform.childCount; i++)
                list.Add(spawnRoot.transform.GetChild(i));

            return list;
        }

        static Transform FindCityTargetInScene() => FindDeepChildInScene("CityTarget", "城市区", "City");

        static Transform FindDeepChildInScene(params string[] nameHints)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                foreach (var hint in nameHints)
                {
                    var hit = FindDeepChild(root.transform, hint);
                    if (hit != null)
                        return hit.transform;
                }
            }

            return null;
        }

        static GameObject FindDeepChild(Transform root, params string[] nameHints)
        {
            foreach (var hint in nameHints)
            {
                var hit = FindDeepChildByName(root, hint);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        static GameObject FindDeepChildByName(Transform root, string nameHint)
        {
            if (root.name.IndexOf(nameHint, StringComparison.OrdinalIgnoreCase) >= 0)
                return root.gameObject;

            for (var i = 0; i < root.childCount; i++)
            {
                var hit = FindDeepChildByName(root.GetChild(i), nameHint);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        static GameObject FindCanvasUnder(GameObject visualRoot)
        {
            if (visualRoot == null)
                return null;

            var canvas = visualRoot.GetComponentInChildren<Canvas>(true);
            return canvas != null ? canvas.gameObject : null;
        }

        static Text FindTextOnCanvas(Transform canvasRoot, params string[] nameHints)
        {
            if (canvasRoot == null)
                return null;

            foreach (var hint in nameHints)
            {
                var tr = canvasRoot.Find(hint);
                if (tr == null)
                    continue;
                var text = tr.GetComponent<Text>();
                if (text != null)
                    return text;
            }

            var texts = canvasRoot.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null)
                    continue;
                for (var n = 0; n < nameHints.Length; n++)
                {
                    if (texts[i].name == nameHints[n])
                        return texts[i];
                }
            }

            return null;
        }

        static void RetireUnlockGate(GameObject unlockGateGo)
        {
            if (unlockGateGo == null)
                return;

            var interactables = unlockGateGo.GetComponents<WorldInteractableBase>();
            for (var i = 0; i < interactables.Length; i++)
                Undo.DestroyObjectImmediate(interactables[i]);

            var col = unlockGateGo.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            unlockGateGo.SetActive(false);
        }

        static GameObject CreateInteractableChild(GameObject parent, string name, Vector3 localPos, Vector3 boxSize)
        {
            var go = CreateChild(parent, name);
            go.transform.localPosition = localPos;
            EnsureBoxCollider(go, boxSize);
            SetLayerRecursive(go, InteractableLayerName);
            return go;
        }

        static GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static GameObject FindLauncherHudCanvas(Transform root)
        {
            var canvases = root.GetComponentsInChildren<Canvas>(true);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null)
                    continue;
                if (canvas.transform.parent != null &&
                    canvas.transform.parent.name.IndexOf("FuelDeposit", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                return canvas.gameObject;
            }

            return null;
        }

        static void EnsureDepositTriggerCollider(GameObject go)
        {
            if (go == null)
                return;

            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                var sphere = Undo.AddComponent<SphereCollider>(go);
                sphere.radius = 2f;
                col = sphere;
            }

            col.isTrigger = true;
        }

        static void EnsureNukeXpBarUi(Transform canvasRoot)
        {
            if (canvasRoot == null)
                return;

            EnsureUiText(canvasRoot, "LevelText", new Vector2(0f, 40f), new Vector2(320f, 60f), 36,
                string.Format("导弹 Lv.{0}", 1));
            EnsureUiText(canvasRoot, "XpText", new Vector2(0f, -20f), new Vector2(320f, 50f), 28, "0/100");

            var sliderGo = canvasRoot.Find("XpSlider")?.gameObject;
            if (sliderGo == null)
            {
                sliderGo = new GameObject("XpSlider", typeof(RectTransform), typeof(Slider));
                Undo.RegisterCreatedObjectUndo(sliderGo, "Create Nuke Xp Slider");
                sliderGo.transform.SetParent(canvasRoot, false);
                var rt = sliderGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(280f, 24f);
                rt.anchoredPosition = new Vector2(0f, -55f);

                var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(sliderGo.transform, false);
                var bgRt = bg.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                var fillArea = new GameObject("Fill Area", typeof(RectTransform));
                fillArea.transform.SetParent(sliderGo.transform, false);
                var fillAreaRt = fillArea.GetComponent<RectTransform>();
                fillAreaRt.anchorMin = Vector2.zero;
                fillAreaRt.anchorMax = Vector2.one;
                fillAreaRt.offsetMin = new Vector2(5f, 5f);
                fillAreaRt.offsetMax = new Vector2(-5f, -5f);

                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(fillArea.transform, false);
                var fillRt = fill.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
                fill.GetComponent<Image>().color = new Color(0.2f, 0.85f, 0.35f, 1f);

                var slider = sliderGo.GetComponent<Slider>();
                slider.fillRect = fillRt;
                slider.targetGraphic = fill.GetComponent<Image>();
                slider.direction = Slider.Direction.LeftToRight;
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = 0f;
            }
        }

        static Text EnsureUiText(Transform parent, string name, Vector2 anchoredPos, Vector2 size, int fontSize, string text)
        {
            var tr = parent.Find(name);
            GameObject go;
            if (tr == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Text));
                Undo.RegisterCreatedObjectUndo(go, "Create Nuke UI Text");
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = tr.gameObject;
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        static void EnsureCollider(GameObject go)
        {
            if (go.GetComponent<Collider>() != null)
                return;
            EnsureBoxCollider(go, new Vector3(0.6f, 0.2f, 0.6f));
        }

        static void EnsureBoxCollider(GameObject go, Vector3 size)
        {
            var box = go.GetComponent<BoxCollider>();
            if (box == null)
                box = Undo.AddComponent<BoxCollider>(go);
            box.size = size;
            box.center = Vector3.zero;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null)
                return c;
            return Undo.AddComponent<T>(go);
        }

        static void SetSerialized(UnityEngine.Object target, string propName, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propName);
            if (prop == null)
                return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void SetSerialized(UnityEngine.Object target, string propName, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propName);
            if (prop == null)
                return;
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void SetSerialized(UnityEngine.Object target, string propName, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propName);
            if (prop == null)
                return;
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void SetSerialized(UnityEngine.Object target, string propName, int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propName);
            if (prop == null)
                return;
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void SetSerializedEnum<T>(UnityEngine.Object target, string propName, T value) where T : struct
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propName);
            if (prop == null)
                return;
            prop.intValue = Convert.ToInt32(value);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void SetSerializedArray(UnityEngine.Object target, string propName, Transform[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propName);
            if (prop == null)
                return;
            prop.arraySize = values?.Length ?? 0;
            for (var i = 0; i < prop.arraySize; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void SetSerializedObjectArray(UnityEngine.Object target, string propName, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propName);
            if (prop == null)
                return;
            prop.arraySize = values?.Length ?? 0;
            for (var i = 0; i < prop.arraySize; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values != null ? values[i] : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static string ResolveProducerId(string objectName)
        {
            if (objectName.IndexOf("Manual", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("手动", StringComparison.OrdinalIgnoreCase) >= 0)
                return FuelProducerIds.Manual;

            if (objectName.IndexOf("Auto", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("自动", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (objectName.Contains("(2)") || objectName.EndsWith("2", StringComparison.Ordinal))
                    return FuelProducerIds.Auto3;
                if (objectName.Contains("(1)") || objectName.EndsWith("3", StringComparison.Ordinal))
                    return FuelProducerIds.Auto2;
                return FuelProducerIds.Auto1;
            }

            foreach (var (hint, id) in ProducerNameHints)
            {
                if (objectName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return id;
            }

            return FuelProducerIds.Manual;
        }

        static void EnsureInteractableLayerOnHierarchy(GameObject root)
        {
            if (LayerMask.NameToLayer(InteractableLayerName) < 0)
            {
                Debug.LogWarning(
                    $"[DestroyCitySetup] 未找到 Layer '{InteractableLayerName}'。请在 Project Settings → Tags and Layers 中添加。");
                return;
            }

            SetLayerRecursive(root, InteractableLayerName);
        }

        static void SetLayerRecursive(GameObject go, string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
                return;

            go.layer = layer;
            for (var i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layerName);
        }

        static DestroyCityXpFloaterSettings EnsureSceneXpFloaterSettings()
        {
            var existing = UnityEngine.Object.FindObjectOfType<DestroyCityXpFloaterSettings>();
            if (existing != null)
                return existing;

            var go = new GameObject("DestroyCityXpFloaterSettings");
            Undo.RegisterCreatedObjectUndo(go, "Ensure XP Floater Settings");
            return go.AddComponent<DestroyCityXpFloaterSettings>();
        }

        static void MarkSceneDirty()
        {
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void EnsureContentFolder(string folderPath)
        {
            var parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureContentFolder(parent);
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}

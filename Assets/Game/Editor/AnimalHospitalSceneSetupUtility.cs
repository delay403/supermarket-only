#if UNITY_EDITOR
using Game.AnimalHospital;
using Game.AnimalHospital.Config;
using Game.Core.Content;
using Game.UI;
using Game.UI.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>创建/补齐动物医院玩法场景骨架与 UI 占位。</summary>
    public static class AnimalHospitalSceneSetupUtility
    {
        const string ScenePath = "Assets/Game/Modes/AnimalHospital/Scenes/AnimalHospitalScene.unity";

        [MenuItem("Game/Animal Hospital/Create Or Open Scene Skeleton")]
        public static void CreateOrOpenSceneSkeleton()
        {
            AnimalHospitalConfigGenerator.Generate();

            var dir = PathDir(ScenePath);
            if (!AssetDatabase.IsValidFolder(dir))
                EnsureFolder(dir);

            if (!System.IO.File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            SetupSceneContents();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[AnimalHospital] Scene ready: {ScenePath}");
        }

        [MenuItem("Game/Animal Hospital/Ensure HUD Crosshair")]
        public static void EnsureHudCrosshair()
        {
            var hud = Object.FindObjectOfType<AnimalHospitalHudSection>(true);
            if (hud == null)
            {
                Debug.LogWarning("[AnimalHospital] 未找到 AnimalHospitalHudSection。");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Ensure HUD Crosshair");
            var crosshair = hud.GetComponentInChildren<AnimalHospitalCrosshairUi>(true);
            if (crosshair == null)
            {
                crosshair = AnimalHospitalCrosshairUi.CreateCrosshair(hud.transform);
                var so = new SerializedObject(hud);
                so.FindProperty("crosshairUi").objectReferenceValue = crosshair;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                crosshair.EnsureDefaultCrosshair();
            }

            EditorUtility.SetDirty(hud);
            Debug.Log("[AnimalHospital] HUD 准星已就绪。");
        }

        [MenuItem("Game/Animal Hospital/Setup Current Scene Contents")]
        public static void SetupSceneContents()
        {
            var directorGo = GameObject.Find("AnimalHospitalDirector")
                             ?? new GameObject("AnimalHospitalDirector");
            var director = directorGo.GetComponent<AnimalHospitalDirector>()
                           ?? directorGo.AddComponent<AnimalHospitalDirector>();

            var waiting = EnsureZone<AnimalHospitalWaitingZone>("WaitingZone", new Vector3(-6f, 0f, 0f));
            var reception = EnsureZone<AnimalHospitalReceptionZone>("ReceptionZone", new Vector3(0f, 0f, 0f));
            var corridor = EnsureZone<AnimalHospitalCorridorZone>("CorridorZone", new Vector3(0f, 0f, -8f));
            var exit = EnsureZone<AnimalHospitalHospitalExit>("HospitalExit", new Vector3(-12f, 0f, 0f));
            var cabinet = EnsureZone<AnimalHospitalMedicineCabinet>("MedicineCabinet", new Vector3(6f, 0f, 2f));

            var wardsRoot = GameObject.Find("Wards") ?? new GameObject("Wards");
            var wards = new AnimalHospitalWard[3];
            for (var i = 0; i < wards.Length; i++)
            {
                var ward = EnsureZone<AnimalHospitalWard>($"Ward_{i + 1}", new Vector3(8f, 0f, -4f + i * 4f), wardsRoot.transform);
                wards[i] = ward;
                WireWard(ward, i + 1);
            }

            WireReception(reception);
            WireWaiting(waiting);
            WireCabinet(cabinet);
            WireCorridor(corridor);
            EnsurePropsAndCoffee();
            EnsureDaySettlementStation();

            var so = new SerializedObject(director);
            so.FindProperty("waitingZone").objectReferenceValue = waiting;
            so.FindProperty("receptionZone").objectReferenceValue = reception;
            so.FindProperty("corridorZone").objectReferenceValue = corridor;
            so.FindProperty("hospitalExit").objectReferenceValue = exit;
            so.FindProperty("medicineCabinet").objectReferenceValue = cabinet;
            var wardsProp = so.FindProperty("wards");
            wardsProp.arraySize = wards.Length;
            for (var i = 0; i < wards.Length; i++)
                wardsProp.GetArrayElementAtIndex(i).objectReferenceValue = wards[i];

            var config = AssetDatabase.LoadAssetAtPath<AnimalHospitalConfig>(
                $"{GameContentPaths.AnimalHospitalConfigRoot}/AnimalHospitalConfig.asset");
            if (config != null)
            {
                config.EnsureDefaultInteractableMask();
                EditorUtility.SetDirty(config);
            }

            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureBinder(config);
            EnsureHud();
            RemoveInGameHomePanel();
            EnsureResultPanel();
            EnsureDeathPanel();
            EnsureCoffeeOfferPanel();
        }

        [MenuItem("Game/Animal Hospital/Ensure Settlement UI Panels")]
        public static void EnsureSettlementUiPanels()
        {
            RemoveInGameHomePanel();
            DestroyLegacyPanelIfNeeded<AnimalHospitalResultPanel>();
            DestroyLegacyPanelIfNeeded<AnimalHospitalDeathPanel>();

            EnsureResultPanel();
            EnsureDeathPanel();
            EnsureCoffeeOfferPanel();
            EnsureDaySettlementStation();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[AnimalHospital] Sliding UI panels (result/death/coffee) ensured.");
        }

        static void DestroyLegacyPanelIfNeeded<T>() where T : AnimalHospitalSlidingPanelBase
        {
            var existing = Object.FindObjectOfType<T>(true);
            if (existing != null && existing.transform.Find("Root/MainPanel") == null)
                Object.DestroyImmediate(existing.gameObject);
        }

        static void EnsureBinder(AnimalHospitalConfig config)
        {
            var binderGo = GameObject.Find("GameModeConfigBinder")
                           ?? new GameObject("GameModeConfigBinder");
            var binder = binderGo.GetComponent<Game.Core.Content.GameModeConfigBinder>()
                         ?? binderGo.AddComponent<Game.Core.Content.GameModeConfigBinder>();
            var so = new SerializedObject(binder);
            so.FindProperty("animalHospital").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireWaiting(AnimalHospitalWaitingZone waiting)
        {
            var chairs = new Transform[4];
            for (var i = 0; i < chairs.Length; i++)
            {
                var chairGo = waiting.transform.Find($"Chair_{i}")?.gameObject
                              ?? CreateMarker($"Chair_{i}", waiting.transform, new Vector3(-1f + i * 0.8f, 0f, 0f), Color.cyan);
                chairs[i] = chairGo.transform;
            }

            var so = new SerializedObject(waiting);
            var prop = so.FindProperty("chairs");
            prop.arraySize = chairs.Length;
            for (var i = 0; i < chairs.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = chairs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireReception(AnimalHospitalReceptionZone reception)
        {
            var receptionPoint = EnsureChildMarker(reception.transform, "ReceptionPoint", Vector3.forward, Color.yellow);
            var reportSpawn = EnsureChildMarker(reception.transform, "ReportSpawn", new Vector3(-0.8f, 1f, 0.5f), Color.white);
            var admissionSpawn = EnsureChildMarker(reception.transform, "AdmissionSpawn", new Vector3(0.8f, 1f, 0.5f), Color.gray);

            var report = EnsureInteractable(reception.transform, "Report", AnimalHospitalInteractableKind.Report, new Vector3(-1f, 1f, 0f));
            var camera = EnsureInteractable(reception.transform, "Camera", AnimalHospitalInteractableKind.Camera, new Vector3(-0.3f, 1f, 0f));
            var computer = EnsureInteractable(reception.transform, "Computer", AnimalHospitalInteractableKind.Computer, new Vector3(0.3f, 1f, 0f));
            var printer = EnsureInteractable(reception.transform, "Printer", AnimalHospitalInteractableKind.Printer, new Vector3(1f, 1f, 0f));
            var curtain = EnsureInteractable(reception.transform, "Curtain", AnimalHospitalInteractableKind.Curtain, new Vector3(0f, 2f, -1f));
            var curtainMotion = curtain.GetComponent<AnimalHospitalCurtainController>();
            if (curtainMotion == null)
                curtainMotion = curtain.gameObject.AddComponent<AnimalHospitalCurtainController>();

            var so = new SerializedObject(reception);
            so.FindProperty("receptionPoint").objectReferenceValue = receptionPoint;
            so.FindProperty("reportSpawnPoint").objectReferenceValue = reportSpawn;
            so.FindProperty("admissionSpawnPoint").objectReferenceValue = admissionSpawn;
            so.FindProperty("reportInteractable").objectReferenceValue = report;
            so.FindProperty("cameraInteractable").objectReferenceValue = camera;
            so.FindProperty("computerInteractable").objectReferenceValue = computer;
            so.FindProperty("printerInteractable").objectReferenceValue = printer;
            so.FindProperty("curtainInteractable").objectReferenceValue = curtain;
            so.FindProperty("curtainMotion").objectReferenceValue = curtainMotion;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireWard(AnimalHospitalWard ward, int index)
        {
            var so = new SerializedObject(ward);
            so.FindProperty("wardId").stringValue = $"ward_{index:00}";
            so.FindProperty("bedPoint").objectReferenceValue =
                EnsureChildMarker(ward.transform, "Bed", new Vector3(0f, 0.5f, 1f), Color.blue);
            so.FindProperty("patientEnterPoint").objectReferenceValue =
                EnsureChildMarker(ward.transform, "Enter", new Vector3(0f, 0f, -1f), Color.blue);
            so.FindProperty("ceilingEventAnchor").objectReferenceValue =
                EnsureChildMarker(ward.transform, "CeilingAnchor", new Vector3(0f, 3f, 0f), Color.red);
            so.FindProperty("shadowSpawnPoint").objectReferenceValue =
                EnsureChildMarker(ward.transform, "ShadowSpawn", new Vector3(1f, 0f, 0f), Color.black);
            so.FindProperty("analyzer").objectReferenceValue =
                EnsureInteractable(ward.transform, "Analyzer", AnimalHospitalInteractableKind.Analyzer, new Vector3(-1f, 1f, 0f));
            so.FindProperty("wardComputer").objectReferenceValue =
                EnsureInteractable(ward.transform, "WardComputer", AnimalHospitalInteractableKind.WardComputer, new Vector3(0f, 1f, 0f));
            so.FindProperty("wardDisplay").objectReferenceValue =
                EnsureInteractable(ward.transform, "WardDisplay", AnimalHospitalInteractableKind.WardDisplay, new Vector3(1f, 1.5f, 0f));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireCabinet(AnimalHospitalMedicineCabinet cabinet)
        {
            var meds = new AnimalHospitalMedicineInteractable[3];
            for (var i = 0; i < meds.Length; i++)
            {
                var go = CreateMarker($"Medicine_{i + 1}", cabinet.transform, new Vector3(-0.6f + i * 0.6f, 1f, 0f), Color.magenta);
                var med = go.GetComponent<AnimalHospitalMedicineInteractable>()
                          ?? go.AddComponent<AnimalHospitalMedicineInteractable>();
                med.SetMedicineId($"medicine_0{i + 1}");
                AnimalHospitalInteractableLayers.ApplyWorldInteractableLayer(go);
                meds[i] = med;
            }

            var so = new SerializedObject(cabinet);
            var prop = so.FindProperty("medicines");
            prop.arraySize = meds.Length;
            for (var i = 0; i < meds.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = meds[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireCorridor(AnimalHospitalCorridorZone corridor)
        {
            if (corridor == null)
                return;

            var existingVolumes = corridor.GetComponentsInChildren<AnimalHospitalCorridorVolume>(true);
            if (existingVolumes.Length == 0)
            {
                CreateCorridorVolume(corridor.transform, "Volume_A", new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 4f));
                CreateCorridorVolume(corridor.transform, "Volume_B", new Vector3(2f, 0f, 0f), new Vector3(2f, 0f, 4f));
                existingVolumes = corridor.GetComponentsInChildren<AnimalHospitalCorridorVolume>(true);
            }

            var so = new SerializedObject(corridor);
            var volumesProp = so.FindProperty("volumes");
            volumesProp.arraySize = existingVolumes.Length;
            for (var i = 0; i < existingVolumes.Length; i++)
                volumesProp.GetArrayElementAtIndex(i).objectReferenceValue = existingVolumes[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateCorridorVolume(Transform parent, string name, Vector3 localPosition, Vector3 halfExtents)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var volume = go.AddComponent<AnimalHospitalCorridorVolume>();
            var so = new SerializedObject(volume);
            so.FindProperty("localHalfExtents").vector3Value = halfExtents;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsurePropsAndCoffee()
        {
            var propsRoot = GameObject.Find("PropStations") ?? new GameObject("PropStations");
            propsRoot.transform.position = new Vector3(-2f, 0f, 4f);

            CreateProp(propsRoot.transform, "StunBaton", AnimalHospitalPropId.StunBaton, new Vector3(0f, 1f, 0f));
            CreateProp(propsRoot.transform, "Taser", AnimalHospitalPropId.Taser, new Vector3(0.7f, 1f, 0f));
            CreateProp(propsRoot.transform, "Pistol", AnimalHospitalPropId.Pistol, new Vector3(1.4f, 1f, 0f));
            CreateProp(propsRoot.transform, "FireExtinguisher", AnimalHospitalPropId.FireExtinguisher, new Vector3(2.1f, 1f, 0f));
            CreateProp(propsRoot.transform, "FreezeGun", AnimalHospitalPropId.FreezeGun, new Vector3(2.8f, 1f, 0f));

            var coffeeGo = CreateMarker("CoffeeMachine", null, new Vector3(-4f, 1f, 4f), new Color(0.55f, 0.35f, 0.2f));
            if (coffeeGo.GetComponent<AnimalHospitalCoffeeMachine>() == null)
                coffeeGo.AddComponent<AnimalHospitalCoffeeMachine>();
            if (coffeeGo.GetComponent<Collider>() == null)
                coffeeGo.AddComponent<BoxCollider>();
            AnimalHospitalInteractableLayers.ApplyWorldInteractableLayer(coffeeGo);
        }

        static void EnsureDaySettlementStation()
        {
            if (Object.FindObjectOfType<AnimalHospitalDaySettlementStation>(true) != null)
                return;

            var reception = Object.FindObjectOfType<AnimalHospitalReceptionZone>();
            var parent = reception != null ? reception.transform : null;
            var go = CreateMarker("DaySettlementStation", parent, new Vector3(2.2f, 1f, -0.8f), new Color(0.92f, 0.72f, 0.18f));
            if (go.GetComponent<AnimalHospitalDaySettlementStation>() == null)
                go.AddComponent<AnimalHospitalDaySettlementStation>();
            if (go.GetComponent<Collider>() == null)
                go.AddComponent<BoxCollider>();
            AnimalHospitalInteractableLayers.ApplyWorldInteractableLayer(go);
        }

        static void CreateProp(Transform parent, string name, AnimalHospitalPropId propId, Vector3 localPos)
        {
            var go = parent.Find(name)?.gameObject ?? CreateMarker(name, parent, localPos, Color.white);
            var prop = go.GetComponent<AnimalHospitalPropInteractable>()
                       ?? go.AddComponent<AnimalHospitalPropInteractable>();
            var so = new SerializedObject(prop);
            so.FindProperty("propId").enumValueIndex = (int)propId;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (go.GetComponent<Collider>() == null)
                go.AddComponent<BoxCollider>();
            AnimalHospitalInteractableLayers.ApplyItemLayer(go);
        }

        static AnimalHospitalInteractable EnsureInteractable(
            Transform parent,
            string name,
            AnimalHospitalInteractableKind kind,
            Vector3 localPos)
        {
            var go = parent.Find(name)?.gameObject ?? CreateMarker(name, parent, localPos, Color.green);
            var interactable = go.GetComponent<AnimalHospitalInteractable>()
                               ?? go.AddComponent<AnimalHospitalInteractable>();
            interactable.Configure(kind, name.ToLowerInvariant(), name);
            if (go.GetComponent<Collider>() == null)
                go.AddComponent<BoxCollider>();
            AnimalHospitalInteractableLayers.ApplyWorldInteractableLayer(go);
            return interactable;
        }

        static Transform EnsureChildMarker(Transform parent, string name, Vector3 localPos, Color color)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing;
            return CreateMarker(name, parent, localPos, color).transform;
        }

        static T EnsureZone<T>(string name, Vector3 position, Transform parent = null) where T : Component
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                if (parent != null)
                    go.transform.SetParent(parent, false);
                go.transform.position = position;
            }

            return go.GetComponent<T>() ?? go.AddComponent<T>();
        }

        static GameObject CreateMarker(string name, Transform parent, Vector3 localPos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.35f;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
            return go;
        }

        [MenuItem("Game/Animal Hospital/Ensure Crosshair In HUD")]
        public static void EnsureCrosshairInHud()
        {
            var hud = Object.FindObjectOfType<AnimalHospitalHudSection>(true);
            if (hud == null)
            {
                Debug.LogWarning("[AnimalHospital] 未找到 AnimalHospitalHudSection。");
                return;
            }

            hud.gameObject.SetActive(true);
            var so = new SerializedObject(hud);
            var crosshairProp = so.FindProperty("crosshairUi");
            var existing = crosshairProp.objectReferenceValue as AnimalHospitalCrosshairUi;
            if (existing == null)
                existing = hud.GetComponentInChildren<AnimalHospitalCrosshairUi>(true);

            if (existing == null)
            {
                var crosshairGo = new GameObject("Crosshair", typeof(RectTransform));
                crosshairGo.transform.SetParent(hud.transform, false);
                var crosshairRt = crosshairGo.GetComponent<RectTransform>();
                crosshairRt.anchorMin = crosshairRt.anchorMax = new Vector2(0.5f, 0.5f);
                crosshairRt.anchoredPosition = Vector2.zero;
                crosshairRt.sizeDelta = new Vector2(14f, 14f);
                var crosshairImage = crosshairGo.AddComponent<Image>();
                crosshairImage.color = new Color(1f, 1f, 1f, 0.75f);
                crosshairImage.raycastTarget = false;
                existing = crosshairGo.AddComponent<AnimalHospitalCrosshairUi>();
            }

            crosshairProp.objectReferenceValue = existing;
            EnsureHudSurvivalDayLabel(hud);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("[AnimalHospital] Crosshair ensured on HUD.");
        }

        static void EnsureHudSurvivalDayLabel(AnimalHospitalHudSection hud)
        {
            if (hud == null)
                return;

            var so = new SerializedObject(hud);
            var existing = so.FindProperty("survivalDayText").objectReferenceValue as Text;
            if (existing != null)
                return;

            var dayTf = hud.transform.Find("SurvivalDay") ?? hud.transform.Find("DayText");
            Text dayText;
            if (dayTf != null)
            {
                dayText = dayTf.GetComponent<Text>();
            }
            else
            {
                dayText = CreateText(hud.transform, "SurvivalDay", new Vector2(0.85f, 0.9f), "第1天");
            }

            so.FindProperty("survivalDayText").objectReferenceValue = dayText;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
        }

        static void EnsureHud()
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("AnimalHospitalCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var existing = Object.FindObjectOfType<AnimalHospitalHudSection>(true);
            if (existing != null)
                return;

            var root = new GameObject("AnimalHospitalHudSection", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rt = root.GetComponent<RectTransform>();
            StretchFull(rt);

            var toast = CreateText(root.transform, "Toast", new Vector2(0.5f, 0.85f), " ");

            var crosshairUi = AnimalHospitalCrosshairUi.CreateCrosshair(root.transform);

            var aim = CreateText(root.transform, "AimHint", new Vector2(0.5f, 0.46f), "");
            var survivalDay = CreateText(root.transform, "SurvivalDay", new Vector2(0.85f, 0.9f), "第1天");
            var sanity = CreateText(root.transform, "Sanity", new Vector2(0.15f, 0.9f), "理智");
            var medicine = CreateText(root.transform, "Medicine", new Vector2(0.15f, 0.84f), "药物：无");
            var prop = CreateText(root.transform, "Prop", new Vector2(0.15f, 0.78f), "道具：无");

            var holdRoot = new GameObject("HoldProgress", typeof(RectTransform));
            holdRoot.transform.SetParent(root.transform, false);
            var holdRt = holdRoot.GetComponent<RectTransform>();
            holdRt.anchorMin = new Vector2(0.35f, 0.12f);
            holdRt.anchorMax = new Vector2(0.65f, 0.16f);
            holdRt.offsetMin = Vector2.zero;
            holdRt.offsetMax = Vector2.zero;
            var bg = holdRoot.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(holdRoot.transform, false);
            StretchFull(fillGo.GetComponent<RectTransform>());
            var fill = fillGo.AddComponent<Image>();
            fill.color = new Color(0.3f, 0.9f, 0.4f, 0.9f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;

            var btnGo = new GameObject("InteractHoldButton", typeof(RectTransform));
            btnGo.transform.SetParent(root.transform, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.78f, 0.08f);
            btnRt.anchorMax = new Vector2(0.95f, 0.18f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.55f, 0.9f, 0.9f);
            var btn = btnGo.AddComponent<Button>();
            CreateText(btnGo.transform, "Label", new Vector2(0.5f, 0.5f), "按住交互");

            var section = root.AddComponent<AnimalHospitalHudSection>();
            var so = new SerializedObject(section);
            so.FindProperty("sanityText").objectReferenceValue = sanity;
            so.FindProperty("survivalDayText").objectReferenceValue = survivalDay;
            so.FindProperty("heldMedicineText").objectReferenceValue = medicine;
            so.FindProperty("heldPropText").objectReferenceValue = prop;
            so.FindProperty("toastText").objectReferenceValue = toast;
            so.FindProperty("aimHintText").objectReferenceValue = aim;
            so.FindProperty("crosshairUi").objectReferenceValue = crosshairUi;
            so.FindProperty("holdProgressFill").objectReferenceValue = fill;
            so.FindProperty("holdProgressRoot").objectReferenceValue = holdRoot;
            so.FindProperty("interactHoldButton").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RemoveInGameHomePanel()
        {
            var canvas = Object.FindObjectOfType<Canvas>(true);
            if (canvas == null)
                return;

            var home = canvas.transform.Find("AnimalHospitalHomePanel");
            if (home != null)
                Object.DestroyImmediate(home.gameObject);
        }

        static void EnsureResultPanel() =>
            EnsureSlidingPanel<AnimalHospitalResultPanel>(
                "AnimalHospitalResultPanel",
                new Vector2(620f, 560f),
                BuildResultPanelContent);

        static void EnsureDeathPanel() =>
            EnsureSlidingPanel<AnimalHospitalDeathPanel>(
                "AnimalHospitalDeathPanel",
                new Vector2(560f, 420f),
                BuildDeathPanelContent);

        delegate void PanelContentBuilder(Transform mainPanel, Component panelComponent);

        static void EnsureSlidingPanel<T>(
            string shellName,
            Vector2 mainPanelSize,
            PanelContentBuilder buildContent) where T : AnimalHospitalSlidingPanelBase
        {
            var existing = Object.FindObjectOfType<T>(true);
            if (existing != null && existing.transform.Find("Root/MainPanel") != null)
            {
                WireSlidingPanelShell(existing, existing.transform.Find("Root")?.gameObject,
                    existing.transform.Find("Root/MainPanel") as RectTransform,
                    existing.transform.Find("Root/Mask")?.GetComponent<Image>(),
                    existing.transform.Find("Root/Mask")?.GetComponent<Button>());
                return;
            }

            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var canvas = ResolveUiCanvas();
            var shell = CreateSlidingPanelShell(shellName, canvas, mainPanelSize,
                out var root, out var mainPanel, out var maskImage, out var maskButton);
            var panel = shell.AddComponent<T>();
            buildContent(mainPanel, panel);
            WireSlidingPanelShell(panel, root, mainPanel, maskImage, maskButton);
        }

        static void WireSlidingPanelShell(
            AnimalHospitalSlidingPanelBase panel,
            GameObject root,
            RectTransform mainPanel,
            Image maskImage,
            Button maskButton)
        {
            if (panel == null)
                return;

            var so = new SerializedObject(panel);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("mainPanel").objectReferenceValue = mainPanel;
            so.FindProperty("maskImage").objectReferenceValue = maskImage;
            so.FindProperty("maskCloseButton").objectReferenceValue = maskButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreateSlidingPanelShell(
            string shellName,
            Transform parent,
            Vector2 mainPanelSize,
            out GameObject root,
            out RectTransform mainPanel,
            out Image maskImage,
            out Button maskButton)
        {
            var shell = new GameObject(shellName, typeof(RectTransform));
            shell.transform.SetParent(parent, false);
            StretchFull(shell.GetComponent<RectTransform>());
            shell.SetActive(false);

            root = CreateUiObject("Root", shell.transform);
            StretchFull(root.GetComponent<RectTransform>());

            var maskGo = CreateUiObject("Mask", root.transform);
            StretchFull(maskGo.GetComponent<RectTransform>());
            maskImage = maskGo.AddComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.55f);
            maskButton = maskGo.AddComponent<Button>();
            maskButton.transition = Selectable.Transition.None;

            var mainGo = CreateUiObject("MainPanel", root.transform);
            mainPanel = mainGo.GetComponent<RectTransform>();
            mainPanel.anchorMin = mainPanel.anchorMax = new Vector2(0.5f, 0.5f);
            mainPanel.pivot = new Vector2(0.5f, 0.5f);
            mainPanel.sizeDelta = mainPanelSize;
            mainGo.AddComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f, 0.98f);

            return shell;
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void BuildResultPanelContent(Transform mainPanel, Component panelComponent)
        {
            var title = CreateAnchoredText(mainPanel, "Title", new Vector2(0f, -20f), "换班报告", 28, new Vector2(520f, 44f));
            var survivalDay = CreateAnchoredText(mainPanel, "SurvivalDayText", new Vector2(-150f, -80f), string.Empty, 18, new Vector2(280f, 32f));
            var planned = CreateAnchoredText(mainPanel, "PlannedPatientsText", new Vector2(150f, -80f), string.Empty, 18, new Vector2(280f, 32f));
            var treated = CreateAnchoredText(mainPanel, "TreatedPatientsText", new Vector2(-150f, -120f), string.Empty, 18, new Vector2(280f, 32f));
            var photographed = CreateAnchoredText(mainPanel, "AnomalousPhotographedText", new Vector2(150f, -120f), string.Empty, 18, new Vector2(280f, 32f));
            var handled = CreateAnchoredText(mainPanel, "AnomalousHandledText", new Vector2(-150f, -160f), string.Empty, 18, new Vector2(280f, 32f));
            var performance = CreateAnchoredText(mainPanel, "PerformanceText", new Vector2(150f, -160f), string.Empty, 18, new Vector2(280f, 32f));
            performance.color = new Color(1f, 0.55f, 0.15f);
            var coins = CreateAnchoredText(mainPanel, "CoinsEarnedText", new Vector2(0f, -200f), string.Empty, 18, new Vector2(520f, 32f));
            var hint = CreateAnchoredText(mainPanel, "HintText", new Vector2(0f, -240f), string.Empty, 16, new Vector2(520f, 28f));
            hint.color = new Color(1f, 0.75f, 0.35f);

            var settle = CreateAnchoredButton(mainPanel, "SettleButton", new Vector2(-130f, -320f), "直接换班", new Vector2(220f, 56f));
            var doubleSettle = CreateAnchoredButton(mainPanel, "DoubleSettleButton", new Vector2(130f, -320f), "双倍获得", new Vector2(220f, 56f));
            var home = CreateAnchoredButton(mainPanel, "HomeButton", new Vector2(0f, -400f), "返回主页", new Vector2(220f, 56f));

            var so = new SerializedObject(panelComponent);
            so.FindProperty("survivalDayText").objectReferenceValue = survivalDay;
            so.FindProperty("plannedPatientsText").objectReferenceValue = planned;
            so.FindProperty("treatedPatientsText").objectReferenceValue = treated;
            so.FindProperty("anomalousPhotographedText").objectReferenceValue = photographed;
            so.FindProperty("anomalousHandledText").objectReferenceValue = handled;
            so.FindProperty("performanceText").objectReferenceValue = performance;
            so.FindProperty("coinsEarnedText").objectReferenceValue = coins;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.FindProperty("settleButton").objectReferenceValue = settle;
            so.FindProperty("doubleSettleButton").objectReferenceValue = doubleSettle;
            so.FindProperty("homeButton").objectReferenceValue = home;
            so.FindProperty("allowMaskClose").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildDeathPanelContent(Transform mainPanel, Component panelComponent)
        {
            var title = CreateAnchoredText(mainPanel, "Title", new Vector2(0f, -24f), "彻底失智", 28, new Vector2(520f, 44f));
            var description = CreateAnchoredText(mainPanel, "Description", new Vector2(0f, -120f),
                "理智值为0，彻底失智！是否立即恢复理智", 20, new Vector2(520f, 80f));
            var restart = CreateAnchoredButton(mainPanel, "RestartButton", new Vector2(-130f, -280f), "重新开始", new Vector2(220f, 56f));
            var recover = CreateAnchoredButton(mainPanel, "RecoverButton", new Vector2(130f, -280f), "回复", new Vector2(220f, 56f));

            var so = new SerializedObject(panelComponent);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("recoverButton").objectReferenceValue = recover;
            so.FindProperty("allowMaskClose").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Text CreateAnchoredText(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            string value,
            int fontSize,
            Vector2 size)
        {
            var go = CreateUiObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = fontSize;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.raycastTarget = false;
            return text;
        }

        static Button CreateAnchoredButton(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            string label,
            Vector2 size)
        {
            var go = CreateUiObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.55f, 0.9f, 0.95f);
            var btn = go.AddComponent<Button>();

            var labelGo = CreateUiObject("Label", go.transform);
            StretchFull(labelGo.GetComponent<RectTransform>());
            var text = labelGo.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 20;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.raycastTarget = false;
            return btn;
        }

        const string FreeCurrencyOfferPrefabPath = "Assets/Game/Core/Art/UI/Prefabs/FreeCurrencyOfferPanel -.prefab";

        static void EnsureCoffeeOfferPanel()
        {
            foreach (var existing in Object.FindObjectsOfType<FreeCurrencyOfferPanel>(true))
            {
                if (existing.PanelId == UiPanelId.AnimalHospitalCoffeeOffer)
                    return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FreeCurrencyOfferPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[AnimalHospital] 未找到 {FreeCurrencyOfferPrefabPath}，无法创建咖啡领取面板。");
                return;
            }

            var canvas = ResolveUiCanvas();
            var instance = PrefabUtility.InstantiatePrefab(prefab, canvas) as GameObject;
            if (instance == null)
                return;

            instance.name = "AnimalHospitalCoffeeOfferPanel";
            instance.SetActive(false);

            var offer = instance.GetComponent<FreeCurrencyOfferPanel>();
            if (offer == null)
                offer = instance.AddComponent<FreeCurrencyOfferPanel>();

            var so = new SerializedObject(offer);
            so.FindProperty("panelId").enumValueIndex = (int)UiPanelId.AnimalHospitalCoffeeOffer;
            so.FindProperty("kind").enumValueIndex = (int)FreeCurrencyOfferPanel.OfferKind.Coins;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform ResolveUiCanvas()
        {
            var hud = Object.FindObjectOfType<AnimalHospitalHudSection>(true);
            if (hud != null)
                return hud.transform.parent != null ? hud.transform.parent : hud.transform;

            var canvas = Object.FindObjectOfType<Canvas>();
            return canvas != null ? canvas.transform : null;
        }

        static Text CreateText(Transform parent, string name, Vector2 anchor, string value, int fontSize = 22)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(420f, 40f);
            rt.anchoredPosition = Vector2.zero;
            var text = go.AddComponent<Text>();
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = fontSize;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return text;
        }

        static Button CreateButton(Transform parent, string name, Vector2 anchor, string label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(240f, 56f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.55f, 0.9f, 0.95f);
            var btn = go.AddComponent<Button>();
            CreateText(go.transform, "Label", new Vector2(0.5f, 0.5f), label);
            return btn;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static string PathDir(string assetPath)
        {
            var idx = assetPath.LastIndexOf('/');
            return idx > 0 ? assetPath.Substring(0, idx) : assetPath;
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.EscapeSnake;
using Game.Hud;
using Game.AI;
using Game.Player;
using Game.UI;
using Game.WormDodge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// 一键生成躲避虫子 UI Prefab，并配置 EscapeWormScene 场景物体与组件。
    /// </summary>
    public static class WormDodgeContentBuilder
    {
        const string PrefabFolder = "Assets/UI/Prefabs/Panel";
        const string ScenePath = "Assets/Game/Modes/WormDodge/Scenes/EscapeWormScene.unity";
        const string SafeZonePrefabPath = "Assets/jiechongzi/Prefab/anquanqv.prefab";

        static Font UiFont =>
            AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Font/AlibabaPuHuiTi-3-85-Bold.ttf")
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Game/Worm Dodge/Build All UI Prefabs")]
        public static void BuildAllUiPrefabs()
        {
            EnsureFolder(PrefabFolder);
            BuildHomePanelPrefab();
            BuildResultPanelPrefab();
            BuildFloorRewardPanelPrefab();
            BuildLeaderboardHudPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WormDodge] UI Prefab 已全部生成到 Assets/UI/Prefabs/Panel/");
        }

        [MenuItem("Game/Worm Dodge/Setup EscapeWormScene (Full)")]
        public static void SetupEscapeWormSceneFull()
        {
            BuildAllUiPrefabs();
            SetupEscapeWormScene();
        }

        [MenuItem("Game/Worm Dodge/Setup EscapeWormScene Only")]
        public static void SetupEscapeWormSceneOnly() => SetupEscapeWormScene();

        static void SetupEscapeWormScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[WormDodge] 无法打开场景：{ScenePath}");
                return;
            }

            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[WormDodge] 场景中未找到 Canvas。");
                return;
            }

            WireUiPrefabs(canvas.transform);
            SetupGameplayZones();
            SetupPlayer();
            SetupDirector();
            EnsureAiPath();
            SpawnAiBots(8);
            EnsureWormDodgeStaminaBox();
            EnsureWormDodgeStaminaButton();
            EnsureLeaderboardHudOnGameHud();
            RemoveEmptyPlaceholderPanels(canvas.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[WormDodge] EscapeWormScene 配置完成。");
        }

        static void WireUiPrefabs(Transform canvas)
        {
            PlacePanelPrefab<WormDodgeHomePanel>(canvas, "WormDodgeHomePanel.prefab", removeExisting: true);
            PlacePanelPrefab<WormDodgeResultPanel>(canvas, "WormDodgeResultPanel.prefab", removeExisting: true);
            PlacePanelPrefab<WormDodgeFloorRewardPanel>(canvas, "WormDodgeFloorRewardPanel.prefab", removeExisting: true);
            PlaceLeaderboardHud(canvas);
            EnsureHudSection(canvas);
        }

        static void PlaceLeaderboardHud(Transform canvas)
        {
            var path = $"{PrefabFolder}/WormDodgeLeaderboardHud.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[WormDodge] 缺少 Prefab：{path}，请先执行 Build All UI Prefabs。");
                return;
            }

            var oldPanel = canvas.Find("WormDodgeLeaderboardPanel");
            if (oldPanel != null)
                Object.DestroyImmediate(oldPanel.gameObject);

            var oldHud = canvas.Find("WormDodgeLeaderboardHud");
            if (oldHud != null)
                Object.DestroyImmediate(oldHud.gameObject);

            var instance = PrefabUtility.InstantiatePrefab(prefab, canvas) as GameObject;
            if (instance == null)
                return;

            instance.name = "WormDodgeLeaderboardHud";
            var rt = instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }
        }

        static void PlacePanelPrefab<T>(Transform canvas, string prefabName, bool removeExisting) where T : Component
        {
            var path = $"{PrefabFolder}/{prefabName}";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[WormDodge] 缺少 Prefab：{path}，请先执行 Build All UI Prefabs。");
                return;
            }

            var panelName = Path.GetFileNameWithoutExtension(prefabName);
            if (removeExisting)
            {
                var old = canvas.Find(panelName);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, canvas) as GameObject;
            if (instance == null)
                return;

            instance.name = panelName;
            var rt = instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }
        }

        static void EnsureHudSection(Transform canvas)
        {
            var hud = Object.FindObjectOfType<GameHudUguiPanel>(true);
            Transform parent = hud != null ? hud.transform : canvas;
            var sectionGo = parent.Find("WormDodgeHudSection");
            if (sectionGo == null)
            {
                sectionGo = new GameObject("WormDodgeHudSection", typeof(RectTransform)).transform;
                sectionGo.SetParent(parent, false);
                var rt = sectionGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -40f);
                rt.sizeDelta = new Vector2(520f, 80f);
            }

            var section = sectionGo.GetComponent<WormDodgeHudSection>();
            if (section == null)
                section = sectionGo.gameObject.AddComponent<WormDodgeHudSection>();

            var so = new SerializedObject(section);
            so.FindProperty("floorText").objectReferenceValue = FindOrCreateText(sectionGo, "FloorText", "当前层数：0", new Vector2(0f, 0f));
            so.FindProperty("runCoinsText").objectReferenceValue = FindOrCreateText(sectionGo, "RunCoinsText", "本局 +0", new Vector2(160f, 0f));
            so.FindProperty("chaseHintText").objectReferenceValue = FindOrCreateText(sectionGo, "ChaseHintText", string.Empty, new Vector2(0f, -36f));
            so.ApplyModifiedPropertiesWithoutUndo();

            var legacyStamina = sectionGo.Find("StaminaText");
            if (legacyStamina != null)
                Object.DestroyImmediate(legacyStamina.gameObject);
        }

        static void EnsureWormDodgeStaminaBox()
        {
            var hud = Object.FindObjectOfType<GameHudUguiPanel>(true);
            if (hud == null)
            {
                Debug.LogWarning("[WormDodge] 未找到 GameHudUguiPanel，无法创建体力框。");
                return;
            }

            var hudSo = new SerializedObject(hud);
            if (hudSo.FindProperty("wormDodgeStaminaRoot").objectReferenceValue != null)
                return;

            var gem = FindDeepChild(hud.transform, "Gem");
            if (gem == null)
            {
                Debug.LogWarning("[WormDodge] GameHudUGUI 中未找到 Gem 示例框，请手动绑定体力框。");
                return;
            }

            var staminaRoot = Object.Instantiate(gem.gameObject, gem.parent);
            staminaRoot.name = "WormDodgeStamina";
            staminaRoot.transform.SetSiblingIndex(gem.GetSiblingIndex() + 1);

            var plus = FindDeepChild(staminaRoot.transform, "Plus")
                       ?? FindDeepChild(staminaRoot.transform, "Add");
            if (plus != null)
            {
                plus.gameObject.SetActive(true);
            }
            else
            {
                foreach (var btn in staminaRoot.GetComponentsInChildren<Button>(true))
                {
                    if (btn.gameObject == staminaRoot)
                        continue;

                    btn.gameObject.SetActive(true);
                    break;
                }
            }

            var amountText = FindDeepChild(staminaRoot.transform, "Gems")?.GetComponent<Text>()
                             ?? staminaRoot.GetComponentInChildren<Text>(true);
            if (amountText != null)
            {
                amountText.name = "StaminaAmountText";
                amountText.text = "5/5";
            }

            var regenGo = CreateUiObject("StaminaRegenText", staminaRoot.transform);
            var regenRt = regenGo.GetComponent<RectTransform>();
            regenRt.anchorMin = new Vector2(0f, 0f);
            regenRt.anchorMax = new Vector2(1f, 0f);
            regenRt.pivot = new Vector2(0.5f, 1f);
            regenRt.anchoredPosition = new Vector2(0f, -4f);
            regenRt.sizeDelta = new Vector2(0f, 22f);
            var regenText = regenGo.AddComponent<Text>();
            regenText.font = UiFont;
            regenText.fontSize = 18;
            regenText.alignment = TextAnchor.MiddleCenter;
            regenText.color = new Color(1f, 1f, 1f, 0.9f);
            regenText.text = "--:--";
            regenText.raycastTarget = false;

            hudSo.FindProperty("wormDodgeStaminaRoot").objectReferenceValue = staminaRoot;
            hudSo.FindProperty("wormDodgeStaminaAmountText").objectReferenceValue = amountText;
            hudSo.FindProperty("wormDodgeStaminaRegenText").objectReferenceValue = regenText;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            staminaRoot.SetActive(true);
            EditorUtility.SetDirty(hud);

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hud.gameObject);
            if (!string.IsNullOrEmpty(prefabPath))
                PrefabUtility.SaveAsPrefabAssetAndConnect(hud.gameObject, prefabPath, InteractionMode.AutomatedAction);
        }

        static void EnsureWormDodgeStaminaButton()
        {
            var hud = Object.FindObjectOfType<GameHudUguiPanel>(true);
            if (hud == null)
                return;

            var hudSo = new SerializedObject(hud);
            if (hudSo.FindProperty("btnWormDodgeStamina").objectReferenceValue != null)
                return;

            var rootProp = hudSo.FindProperty("wormDodgeStaminaRoot");
            var rootGo = rootProp.objectReferenceValue as GameObject;
            if (rootGo == null)
                return;

            var add = FindDeepChild(rootGo.transform, "Add") ?? FindDeepChild(rootGo.transform, "Plus");
            Button button = null;
            if (add != null)
                button = add.GetComponent<Button>();

            if (button == null)
            {
                foreach (var btn in rootGo.GetComponentsInChildren<Button>(true))
                {
                    if (btn.gameObject == rootGo)
                        continue;

                    button = btn;
                    break;
                }
            }

            if (button == null)
                return;

            button.gameObject.SetActive(true);
            hudSo.FindProperty("btnWormDodgeStamina").objectReferenceValue = button;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hud.gameObject);
            if (!string.IsNullOrEmpty(prefabPath))
                PrefabUtility.SaveAsPrefabAssetAndConnect(hud.gameObject, prefabPath, InteractionMode.AutomatedAction);
        }

        static void EnsureLeaderboardHudOnGameHud()
        {
            var hud = Object.FindObjectOfType<GameHudUguiPanel>(true);
            if (hud == null)
                return;

            var panelRoot = hud.transform.Find("WormDodgeLeaderboardPanel")
                            ?? hud.transform.Find("WormDodgeLeaderboardHud");
            if (panelRoot == null)
                return;

            var leaderboardHud = panelRoot.GetComponent<WormDodgeLeaderboardHud>();
            if (leaderboardHud == null)
                leaderboardHud = panelRoot.gameObject.AddComponent<WormDodgeLeaderboardHud>();

            var survivor = FindDeepChild(panelRoot, "SurvivorCountText")?.GetComponent<Text>();
            var rank1 = FindDeepChild(panelRoot, "Rank1Text")?.GetComponent<Text>();
            var rank2 = FindDeepChild(panelRoot, "Rank2Text")?.GetComponent<Text>();
            var rank3 = FindDeepChild(panelRoot, "Rank3Text")?.GetComponent<Text>();
            var myRank = FindDeepChild(panelRoot, "PlayerRankText")?.GetComponent<Text>();

            var so = new SerializedObject(leaderboardHud);
            so.FindProperty("survivorCountText").objectReferenceValue = survivor;
            so.FindProperty("rank1Text").objectReferenceValue = rank1;
            so.FindProperty("rank2Text").objectReferenceValue = rank2;
            so.FindProperty("rank3Text").objectReferenceValue = rank3;
            so.FindProperty("playerRankText").objectReferenceValue = myRank;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(leaderboardHud);
            EditorUtility.SetDirty(hud);
        }

        static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
                return null;

            if (root.name == name)
                return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeepChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        static Text FindOrCreateText(Transform parent, string name, string content, Vector2 anchoredPos)
        {
            var t = parent.Find(name);
            Text text;
            if (t == null)
            {
                var go = CreateUiObject(name, parent);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = new Vector2(150f, 36f);
                text = go.AddComponent<Text>();
            }
            else
            {
                text = t.GetComponent<Text>();
            }

            text.font = UiFont;
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        static void SetupGameplayZones()
        {
            UpgradeSafeZonePrefab();
            foreach (var safe in Object.FindObjectsOfType<WormSafeZone>(true))
            {
                EnsureTriggerCollider(safe.gameObject);
                safe.FitBoxColliderToPlatform();
            }

            foreach (var root in Object.FindObjectsOfType<Transform>(true)
                         .Where(t => t.name.StartsWith("anquanqv")))
            {
                if (root.GetComponent<WormSafeZone>() == null)
                    root.gameObject.AddComponent<WormSafeZone>();
                EnsureTriggerCollider(root.gameObject);
                EnsureSpawnPoint(root);
                root.GetComponent<WormSafeZone>()?.FitBoxColliderToPlatform();
            }

            foreach (var finish in Object.FindObjectsOfType<Transform>(true)
                         .Where(t => t.name.StartsWith("zhongdian")))
            {
                if (finish.GetComponent<WormFinishZone>() == null)
                    finish.gameObject.AddComponent<WormFinishZone>();
                EnsureTriggerCollider(finish.gameObject, boxSize: new Vector3(8f, 3f, 8f));
            }

            CreateFloorMarkersAlongRoute();
        }

        static void UpgradeSafeZonePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SafeZonePrefabPath);
            if (prefab == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(SafeZonePrefabPath);
            if (root.GetComponent<WormSafeZone>() == null)
                root.AddComponent<WormSafeZone>();
            EnsureTriggerCollider(root, boxSize: new Vector3(6f, 3f, 6f));
            EnsureSpawnPoint(root.transform);
            var zone = root.GetComponent<WormSafeZone>();
            zone?.FitBoxColliderToPlatform();
            PrefabUtility.SaveAsPrefabAsset(root, SafeZonePrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void EnsureSpawnPoint(Transform root)
        {
            var spawn = root.Find("SpawnPoint");
            if (spawn != null)
                return;

            var go = new GameObject("SpawnPoint");
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.up * 0.5f;
        }

        static void EnsureTriggerCollider(GameObject go, Vector3? boxSize = null)
        {
            var box = go.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = go.AddComponent<BoxCollider>();
                box.size = boxSize ?? new Vector3(6f, 3f, 6f);
                box.center = Vector3.up * 1f;
            }

            box.isTrigger = true;

            if (box.size.y > 6f || box.size.x > 20f || box.size.z > 20f)
            {
                var zone = go.GetComponent<WormSafeZone>();
                if (zone != null)
                    zone.FitBoxColliderToPlatform();
            }

            var meshCol = go.GetComponent<MeshCollider>();
            if (meshCol != null)
                meshCol.isTrigger = true;
        }

        static void CreateFloorMarkersAlongRoute()
        {
            var path = Object.FindObjectOfType<WormPath>();
            if (path == null || path.WaypointCount < 2)
                return;

            var parent = GameObject.Find("WormDodgeZones");
            if (parent == null)
                parent = new GameObject("WormDodgeZones");

            var existing = parent.transform.Find("FloorMarkers");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var markersRoot = new GameObject("FloorMarkers");
            markersRoot.transform.SetParent(parent.transform, false);

            var count = path.WaypointCount;
            for (var i = 1; i < count; i += 3)
            {
                var pos = path.GetWaypointWorld(i);
                var floorIndex = (i / 3) + 1;

                var floorGo = new GameObject($"FloorZone_{floorIndex}");
                floorGo.transform.SetParent(markersRoot.transform, false);
                floorGo.transform.position = pos + Vector3.up * 1f;

                var box = floorGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(8f, 2f, 8f);

                var zone = floorGo.AddComponent<WormFloorSettlementZone>();
                var so = new SerializedObject(zone);
                so.FindProperty("coinReward").longValue = 50L + floorIndex * 20L;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (floorIndex % 5 == 0)
                {
                    var rewardGo = new GameObject($"FloorRewardPopup_{floorIndex}");
                    rewardGo.transform.SetParent(markersRoot.transform, false);
                    rewardGo.transform.position = pos + Vector3.up * 1.2f;
                    var rewardBox = rewardGo.AddComponent<BoxCollider>();
                    rewardBox.isTrigger = true;
                    rewardBox.size = new Vector3(8f, 2f, 8f);
                    var rewardZone = rewardGo.AddComponent<WormDodgeFloorRewardZone>();
                    var rewardSo = new SerializedObject(rewardZone);
                    rewardSo.FindProperty("coinReward").longValue = 200L + floorIndex * 50L;
                    rewardSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static void SetupPlayer()
        {
            var player = GameObject.Find("Player") ?? GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[WormDodge] 未找到 Player。");
                return;
            }

            if (player.GetComponent<PlayerWormDodgeController>() == null)
                player.AddComponent<PlayerWormDodgeController>();

            var participant = player.GetComponent<WormDodgeParticipant>();
            if (participant == null)
                participant = player.AddComponent<WormDodgeParticipant>();

            var target = player.GetComponent<WormChaseTarget>();
            if (target == null)
                target = player.AddComponent<WormChaseTarget>();

            var spawn = GameObject.Find("SpawnPoint") ?? GameObject.Find("RunStartSpawn");
            participant.ConfigureForPlayer(true, spawn != null ? spawn.transform : player.transform);
            target.SetHideOnDeath(false);

            var indicatorChild = player.transform.Find("SafeZoneIndicator");
            if (indicatorChild != null)
                WormDodgePresenceSetup.EnsureSafeZoneIndicator(player);
        }

        static void SetupDirector()
        {
            var director = Object.FindObjectOfType<WormDodgeDirector>();
            if (director == null)
            {
                var go = new GameObject("WormDodgeDirector");
                director = go.AddComponent<WormDodgeDirector>();
            }

            var motor = Object.FindObjectOfType<WormChainMotor>();
            var spawn = GameObject.Find("SpawnPoint")?.transform
                        ?? GameObject.Find("RunStartSpawn")?.transform;

            var so = new SerializedObject(director);
            so.FindProperty("wormMotor").objectReferenceValue = motor;
            so.FindProperty("runStartSpawn").objectReferenceValue = spawn;
            so.FindProperty("autoStartRunOnPlay").boolValue = false;
            so.FindProperty("openHomeOnSceneStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (motor != null)
            {
                var motorSo = new SerializedObject(motor);
                motorSo.FindProperty("postChasePatrolCooldownSeconds").floatValue = 15f;
                motorSo.ApplyModifiedPropertiesWithoutUndo();

                if (motor.GetComponent<WormChainMotorAudio>() == null)
                    motor.gameObject.AddComponent<WormChainMotorAudio>();

                SetupWormMotorAudio(motor);
            }
        }

        static void SetupWormMotorAudio(WormChainMotor motor)
        {
            var audio = motor.GetComponent<WormChainMotorAudio>();
            if (audio == null)
                return;

            var moveClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/EscapeWorm/5-虫子爬行.mp3");
            var wallClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/EscapeWorm/14-虫子叫声.mp3");
            var audioSo = new SerializedObject(audio);
            audioSo.FindProperty("moveLoopClipFallback").objectReferenceValue = moveClip;
            audioSo.FindProperty("insideWallLoopClipFallback").objectReferenceValue = wallClip;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            EnsureBootstrapWormSfx(moveClip, wallClip);
        }

        static void EnsureBootstrapWormSfx(AudioClip moveClip, AudioClip wallClip)
        {
            var bootstrap = Object.FindObjectOfType<Game.Core.GameBootstrapper>();
            if (bootstrap == null || moveClip == null)
                return;

            var so = new SerializedObject(bootstrap);
            var prop = so.FindProperty("bootstrapSfxLibrary");
            if (prop == null || !prop.isArray)
                return;

            UpsertBootstrapSfxEntry(prop, Game.Audio.SfxId.WormMoveLoop, moveClip);
            if (wallClip != null)
                UpsertBootstrapSfxEntry(prop, Game.Audio.SfxId.WormInsideWallLoop, wallClip);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void UpsertBootstrapSfxEntry(SerializedProperty library, Game.Audio.SfxId id, AudioClip clip)
        {
            for (var i = 0; i < library.arraySize; i++)
            {
                var element = library.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("id").enumValueIndex != (int)id)
                    continue;

                element.FindPropertyRelative("clip").objectReferenceValue = clip;
                element.FindPropertyRelative("volumeScale").floatValue = 1f;
                return;
            }

            var idx = library.arraySize;
            library.InsertArrayElementAtIndex(idx);
            var added = library.GetArrayElementAtIndex(idx);
            added.FindPropertyRelative("id").enumValueIndex = (int)id;
            added.FindPropertyRelative("clip").objectReferenceValue = clip;
            added.FindPropertyRelative("volumeScale").floatValue = 1f;
        }

        static WormDodgeAiPath EnsureAiPath()
        {
            var root = GameObject.Find("WormDodgeAiPath");
            if (root == null)
                root = new GameObject("WormDodgeAiPath");

            var aiPath = root.GetComponent<WormDodgeAiPath>();
            if (aiPath == null)
                aiPath = root.AddComponent<WormDodgeAiPath>();

            if (aiPath.WaypointCount > 0)
                return aiPath;

            for (var i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            var wormPath = Object.FindObjectOfType<WormPath>();
            if (wormPath == null || wormPath.WaypointCount == 0)
                return aiPath;

            var waypoints = new List<Transform>();
            var step = Mathf.Max(1, wormPath.WaypointCount / 14);
            for (var i = 0; i < wormPath.WaypointCount; i += step)
            {
                var wpGo = new GameObject($"AiWp_{i}");
                wpGo.transform.SetParent(root.transform, false);
                wpGo.transform.position = wormPath.GetWaypointWorld(i);
                waypoints.Add(wpGo.transform);
            }

            var so = new SerializedObject(aiPath);
            var prop = so.FindProperty("waypoints");
            prop.arraySize = waypoints.Count;
            for (var i = 0; i < waypoints.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return aiPath;
        }

        static void SpawnAiBots(int count)
        {
            var parent = GameObject.Find("WormDodgeAiRoot");
            if (parent == null)
                parent = new GameObject("WormDodgeAiRoot");

            for (var i = parent.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.transform.GetChild(i).gameObject);

            var aiPath = EnsureAiPath();
            var visualTemplate = FindAiVisualTemplate();

            for (var i = 0; i < count; i++)
            {
                var root = new GameObject($"AI ({i + 1})");
                root.transform.SetParent(parent.transform, false);

                var characterRoot = visualTemplate != null
                    ? Object.Instantiate(visualTemplate, root.transform, false)
                    : new GameObject("CharacterRoot");
                characterRoot.name = "CharacterRoot";
                characterRoot.transform.localPosition = new Vector3(0f, -1f, 0f);

                var displayName = $"AI跑者{i + 1}";
                WormDodgeAiActor.ConfigureRoot(root, characterRoot, aiPath, displayName);

                var behavior = root.GetComponent<AIFakeWormDodgeBehavior>();
                if (behavior != null)
                {
                    var behaviorSo = new SerializedObject(behavior);
                    behaviorSo.FindProperty("aiPath").objectReferenceValue = aiPath;
                    behaviorSo.FindProperty("displayName").stringValue = displayName;
                    behaviorSo.FindProperty("startWaypointIndex").intValue = 0;
                    behaviorSo.ApplyModifiedPropertiesWithoutUndo();
                }

                var spawn = GameObject.Find("SpawnPoint")?.transform
                            ?? GameObject.Find("RunStartSpawn")?.transform;
                var startPos = spawn != null
                    ? spawn.position
                    : (aiPath.WaypointCount > 0 ? aiPath.GetWaypointWorld(0) : root.transform.position);
                var startRot = spawn != null ? spawn.rotation : Quaternion.identity;

                // 轻微分散出生点，便于各 AI 落到各自最近路点（同点则同路点）。
                var spread = new Vector3((i - count * 0.5f) * 0.35f, 0f, (i % 3 - 1) * 0.25f);
                var birthPos = startPos + spread;

                var agent = characterRoot.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && UnityEngine.AI.NavMesh.SamplePosition(birthPos, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                    agent.Warp(hit.position);
                else
                    characterRoot.transform.SetPositionAndRotation(birthPos, startRot);
            }
        }

        static GameObject FindAiVisualTemplate()
        {
            var player = Object.FindObjectOfType<PlayerMotor>();
            if (player == null)
                return null;

            var rig = player.GetComponent<PlayerRig>();
            if (rig != null && rig.characterSocket != null)
            {
                var anim = rig.characterSocket.GetComponentInChildren<Animator>(true);
                if (anim != null)
                    return anim.gameObject;
            }

            var fallbackAnim = player.GetComponentInChildren<Animator>(true);
            return fallbackAnim != null ? fallbackAnim.gameObject : null;
        }

        static void RemoveEmptyPlaceholderPanels(Transform canvas)
        {
            var home = canvas.Find("WormDodgeHomePanel");
            if (home != null && home.childCount == 0 && PrefabUtility.GetCorrespondingObjectFromSource(home.gameObject) == null)
                Object.DestroyImmediate(home.gameObject);
        }

        #region Prefab builders

        static void BuildHomePanelPrefab() =>
            SavePanelPrefab(BuildHomePanel(), "WormDodgeHomePanel.prefab");

        static void BuildResultPanelPrefab() =>
            SavePanelPrefab(BuildResultPanel(), "WormDodgeResultPanel.prefab");

        static void BuildFloorRewardPanelPrefab() =>
            SavePanelPrefab(BuildFloorRewardPanel(), "WormDodgeFloorRewardPanel.prefab");

        static void BuildLeaderboardHudPrefab() =>
            SavePanelPrefab(BuildLeaderboardHud(), "WormDodgeLeaderboardHud.prefab");

        static GameObject BuildHomePanel()
        {
            var shell = CreatePanelShell("WormDodgeHomePanel", out var root, out var mainPanel);
            var panel = shell.AddComponent<WormDodgeHomePanel>();
            StretchFull(mainPanel);
            var mainBg = mainPanel.GetComponent<Image>();
            if (mainBg != null)
                mainBg.color = new Color(0f, 0f, 0f, 0.35f);

            var preview = CreateUiObject("CharacterPreview", mainPanel);
            var previewRt = preview.GetComponent<RectTransform>();
            previewRt.anchorMin = previewRt.anchorMax = new Vector2(0.5f, 0.5f);
            previewRt.pivot = new Vector2(0.5f, 0.5f);
            previewRt.anchoredPosition = new Vector2(0f, 40f);
            previewRt.sizeDelta = new Vector2(360f, 420f);
            var previewImage = preview.AddComponent<RawImage>();
            previewImage.color = Color.white;
            preview.AddComponent<RawImageCamera>();

            var locked = CreateUiObject("Locked", mainPanel);
            var lockedRt = locked.GetComponent<RectTransform>();
            lockedRt.anchorMin = lockedRt.anchorMax = new Vector2(0.5f, 0.5f);
            lockedRt.pivot = new Vector2(0.5f, 0.5f);
            lockedRt.anchoredPosition = new Vector2(0f, 40f);
            lockedRt.sizeDelta = new Vector2(96f, 96f);
            var lockedImage = locked.AddComponent<Image>();
            lockedImage.color = new Color(1f, 0.82f, 0.1f, 0.95f);
            lockedImage.raycastTarget = false;
            locked.SetActive(false);

            var prevBtn = CreateButton(mainPanel, "PrevCharacterButton", "<", new Vector2(-220f, 40f), new Vector2(72f, 72f));
            var nextBtn = CreateButton(mainPanel, "NextCharacterButton", ">", new Vector2(220f, 40f), new Vector2(72f, 72f));

            var badge = CreateUiObject("EquippedBadge", mainPanel);
            var badgeRt = badge.GetComponent<RectTransform>();
            badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRt.pivot = new Vector2(0.5f, 0f);
            badgeRt.anchoredPosition = new Vector2(0f, 250f);
            badgeRt.sizeDelta = new Vector2(160f, 42f);
            var badgeBg = badge.AddComponent<Image>();
            badgeBg.color = new Color(1f, 0.82f, 0.1f, 0.95f);
            var badgeText = CreateLabel(badge.transform, "Text", "使用中", Vector2.zero, 22);
            badgeText.color = Color.black;
            var badgeTextRt = badgeText.GetComponent<RectTransform>();
            badgeTextRt.anchorMin = Vector2.zero;
            badgeTextRt.anchorMax = Vector2.one;
            badgeTextRt.offsetMin = Vector2.zero;
            badgeTextRt.offsetMax = Vector2.zero;
            badgeTextRt.anchoredPosition = Vector2.zero;

            var charName = CreateLabel(mainPanel, "CharacterNameText", "角色名", new Vector2(0f, -190f), 28);
            var gateHint = CreateLabel(mainPanel, "UnlockGateHint", "", new Vector2(0f, -230f), 22);
            gateHint.gameObject.SetActive(false);

            var unlockBtn = CreateButton(mainPanel, "UnlockButton", "解锁", new Vector2(-120f, -290f), new Vector2(180f, 68f));
            var useBtn = CreateButton(mainPanel, "UseButton", "使用", new Vector2(120f, -290f), new Vector2(180f, 68f));
            var startBtn = CreateButton(mainPanel, "StartButton", "开始游戏", new Vector2(0f, -380f), new Vector2(280f, 80f));
            var startBtnImage = startBtn.GetComponent<Image>();
            if (startBtnImage != null)
                startBtnImage.color = new Color(1f, 0.82f, 0.1f, 1f);

            var overlay = BuildUnlockOverlay(root.transform);
            var overlayComp = overlay.AddComponent<WormDodgeCharacterUnlockOverlay>();

            BindPanel(panel,
                ("root", root),
                ("mainPanel", mainPanel),
                ("characterPreviewImage", previewImage),
                ("prevCharacterButton", prevBtn),
                ("nextCharacterButton", nextBtn),
                ("unlockButton", unlockBtn),
                ("useButton", useBtn),
                ("startButton", startBtn),
                ("equippedBadgeRoot", badge),
                ("lockedIconRoot", locked),
                ("characterNameText", charName),
                ("unlockGateHint", gateHint),
                ("unlockOverlay", overlayComp));

            BindPanel(overlayComp,
                ("root", overlay),
                ("titleText", overlay.transform.Find("Popup/TitleText")?.GetComponent<Text>()),
                ("messageText", overlay.transform.Find("Popup/MessageText")?.GetComponent<Text>()),
                ("confirmButton", overlay.transform.Find("Popup/ConfirmButton")?.GetComponent<Button>()),
                ("confirmLabel", overlay.transform.Find("Popup/ConfirmButton/Text")?.GetComponent<Text>()),
                ("closeButton", overlay.transform.Find("Popup/CloseButton")?.GetComponent<Button>()),
                ("maskButton", overlay.transform.Find("MaskButton")?.GetComponent<Button>()));

            return shell;
        }

        static GameObject BuildUnlockOverlay(Transform parent)
        {
            var overlay = CreateUiObject("UnlockOverlay", parent);
            StretchFull(overlay.GetComponent<RectTransform>());
            var maskBtn = CreateInvisibleButton(overlay, "MaskButton");

            var popup = CreateUiObject("Popup", overlay.transform);
            var popupRt = popup.GetComponent<RectTransform>();
            popupRt.anchorMin = popupRt.anchorMax = new Vector2(0.5f, 0.5f);
            popupRt.sizeDelta = new Vector2(520f, 300f);
            var popupBg = popup.AddComponent<Image>();
            popupBg.color = new Color(0.12f, 0.35f, 0.78f, 0.98f);

            var title = CreateLabel(popup.transform, "TitleText", "解锁角色", new Vector2(0f, -20f), 30);
            title.fontStyle = FontStyle.Bold;
            var message = CreateLabel(popup.transform, "MessageText", "是否消耗金币 x999 解锁该角色？", new Vector2(0f, -90f), 24);
            var closeBtn = CreateButton(popup.transform, "CloseButton", "X", new Vector2(220f, -20f), new Vector2(48f, 48f));
            var confirmBtn = CreateButton(popup.transform, "ConfirmButton", "解锁", new Vector2(0f, -210f), new Vector2(220f, 68f));
            var confirmBtnImage = confirmBtn.GetComponent<Image>();
            if (confirmBtnImage != null)
                confirmBtnImage.color = new Color(1f, 0.82f, 0.1f, 1f);

            overlay.SetActive(false);
            return overlay;
        }

        static GameObject BuildResultPanel()
        {
            var shell = CreatePanelShell("WormDodgeResultPanel", out var root, out var mainPanel);
            var panel = shell.AddComponent<WormDodgeResultPanel>();

            CreateTitle(mainPanel, "游戏结束");
            var title = mainPanel.Find("TitleText")?.GetComponent<Text>();
            var status = CreateLabel(mainPanel, "StatusText", "被虫子吃掉了！", new Vector2(0f, -70f));
            var floor = CreateLabel(mainPanel, "FloorText", "当前到达层数：0 层", new Vector2(0f, -120f));
            var bestFloor = CreateLabel(mainPanel, "BestFloorText", "历史最高层数：0 层", new Vector2(0f, -160f));
            var clearTime = CreateLabel(mainPanel, "ClearTimeText", "本局通关时间：00:00", new Vector2(0f, -200f));
            var bestClear = CreateLabel(mainPanel, "BestClearTimeText", "历史最快通关：暂无", new Vector2(0f, -240f));
            var coins = CreateLabel(mainPanel, "CoinsText", "x0", new Vector2(0f, -290f), 40);

            var homeBtn = CreateButton(mainPanel, "HomeButton", "返回主页", new Vector2(-220f, -380f), new Vector2(180f, 64f));
            var againBtn = CreateButton(mainPanel, "PlayAgainButton", "再来一局", new Vector2(0f, -380f), new Vector2(180f, 64f));
            var doubleBtn = CreateButton(mainPanel, "DoubleRewardButton", "双倍奖励", new Vector2(220f, -380f), new Vector2(180f, 64f));

            var reviveRoot = CreateUiObject("ReviveRoot", mainPanel);
            var reviveRt = reviveRoot.GetComponent<RectTransform>();
            reviveRt.anchoredPosition = new Vector2(0f, -460f);
            reviveRt.sizeDelta = new Vector2(220f, 64f);
            var reviveBtn = CreateButton(reviveRoot.transform, "ReviveButton", "立即复活", Vector2.zero, new Vector2(220f, 64f));

            var maskBtn = CreateInvisibleButton(root, "MaskCloseButton");

            BindPanel(panel,
                ("root", root),
                ("mainPanel", mainPanel),
                ("maskCloseButton", maskBtn),
                ("titleText", title),
                ("statusText", status),
                ("floorText", floor),
                ("bestFloorText", bestFloor),
                ("clearTimeText", clearTime),
                ("bestClearTimeText", bestClear),
                ("coinsText", coins),
                ("homeButton", homeBtn),
                ("playAgainButton", againBtn),
                ("doubleRewardButton", doubleBtn),
                ("reviveButton", reviveBtn),
                ("reviveButtonRoot", reviveRoot));

            return shell;
        }

        static GameObject BuildFloorRewardPanel()
        {
            var shell = CreatePanelShell("WormDodgeFloorRewardPanel", out var root, out var mainPanel);
            var panel = shell.AddComponent<WormDodgeFloorRewardPanel>();

            var title = CreateTitle(mainPanel.transform, "楼层奖励");
            var body = CreateLabel(mainPanel.transform, "BodyText", "您已到达 N 层，请领取专属奖励！", new Vector2(0f, -80f));
            var reward = CreateLabel(mainPanel, "RewardText", "x500", new Vector2(0f, -150f), 44);
            var claim = CreateButton(mainPanel, "ClaimButton", "前往领取", new Vector2(-130f, -260f), new Vector2(200f, 68f));
            var doubleClaim = CreateButton(mainPanel, "DoubleClaimButton", "双倍领取", new Vector2(130f, -260f), new Vector2(200f, 68f));

            BindPanel(panel,
                ("root", root),
                ("mainPanel", mainPanel),
                ("titleText", title),
                ("bodyText", body),
                ("rewardText", reward),
                ("claimButton", claim),
                ("doubleClaimButton", doubleClaim));
            return shell;
        }

        static GameObject BuildLeaderboardHud()
        {
            var shell = new GameObject("WormDodgeLeaderboardHud", typeof(RectTransform));
            StretchFull(shell.GetComponent<RectTransform>());

            var hudRoot = CreateUiObject("HudRoot", shell.transform);
            var hudRt = hudRoot.GetComponent<RectTransform>();
            hudRt.anchorMin = new Vector2(0f, 0.3f);
            hudRt.anchorMax = new Vector2(0.35f, 0.95f);
            hudRt.offsetMin = new Vector2(12f, 0f);
            hudRt.offsetMax = new Vector2(-12f, -12f);
            var hudBg = hudRoot.AddComponent<Image>();
            hudBg.color = new Color(0f, 0f, 0f, 0.45f);
            hudBg.raycastTarget = false;

            var survivor = CreateLabel(hudRoot.transform, "SurvivorCountText", "当前存活人数：0", new Vector2(0f, 0f));
            var rank1 = CreateLabel(hudRoot.transform, "Rank1Text", "#1 ---", new Vector2(0f, -40f));
            var rank2 = CreateLabel(hudRoot.transform, "Rank2Text", "#2 ---", new Vector2(0f, -80f));
            var rank3 = CreateLabel(hudRoot.transform, "Rank3Text", "#3 ---", new Vector2(0f, -120f));
            var myRank = CreateLabel(hudRoot.transform, "PlayerRankText", "我的排名：-", new Vector2(0f, -180f));

            var hud = shell.AddComponent<WormDodgeLeaderboardHud>();
            BindPanel(hud,
                ("survivorCountText", survivor),
                ("rank1Text", rank1),
                ("rank2Text", rank2),
                ("rank3Text", rank3),
                ("playerRankText", myRank));

            return shell;
        }

        #endregion

        #region UI helpers

        static GameObject CreatePanelShell(string name, out GameObject root, out RectTransform mainPanel)
        {
            var shell = new GameObject(name, typeof(RectTransform));
            StretchFull(shell.GetComponent<RectTransform>());

            root = CreateUiObject("Root", shell.transform);
            StretchFull(root.GetComponent<RectTransform>());
            var rootImage = root.AddComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0.55f);

            var mainGo = CreateUiObject("MainPanel", root.transform);
            mainPanel = mainGo.GetComponent<RectTransform>();
            mainPanel.anchorMin = mainPanel.anchorMax = new Vector2(0.5f, 0.5f);
            mainPanel.pivot = new Vector2(0.5f, 0.5f);
            mainPanel.sizeDelta = new Vector2(680f, 520f);
            var panelBg = mainGo.AddComponent<Image>();
            panelBg.color = new Color(0.15f, 0.18f, 0.24f, 0.98f);

            return shell;
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static Text CreateTitle(Transform parent, string title)
        {
            var text = CreateLabel(parent, "TitleText", title, new Vector2(0f, -24f), 34);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            var rt = text.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600f, 48f);
            return text;
        }

        static Text CreateLabel(Transform parent, string name, string content, Vector2 anchoredPos, int fontSize = 26)
        {
            var go = CreateUiObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(620f, 40f);
            var text = go.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
        {
            var go = CreateUiObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.22f, 0.55f, 0.95f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textGo = CreateUiObject("Text", go.transform);
            StretchFull(textGo.GetComponent<RectTransform>());
            var text = textGo.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            return button;
        }

        static Button CreateInvisibleButton(GameObject root, string name)
        {
            var go = CreateUiObject(name, root.transform);
            StretchFull(go.GetComponent<RectTransform>());
            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.01f);
            return go.AddComponent<Button>();
        }

        static void BindPanel(Component panel, params (string prop, Object value)[] bindings)
        {
            var so = new SerializedObject(panel);
            foreach (var (prop, value) in bindings)
            {
                var p = so.FindProperty(prop);
                if (p != null)
                    p.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SavePanelPrefab(GameObject panel, string fileName)
        {
            var path = $"{PrefabFolder}/{fileName}";
            PrefabUtility.SaveAsPrefabAsset(panel, path);
            Object.DestroyImmediate(panel);
        }

        static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                var name = Path.GetFileName(folder);
                if (!string.IsNullOrEmpty(parent))
                    AssetDatabase.CreateFolder(parent, name);
            }
        }

        #endregion
    }
}
#endif

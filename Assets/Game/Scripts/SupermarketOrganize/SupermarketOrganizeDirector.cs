using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.SupermarketOrganize.Config;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>场景级导演：生成物品、初始化服务、胜利与开场占位。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeDirector : MonoBehaviour
    {
        public static SupermarketOrganizeDirector Instance { get; private set; }

        [SerializeField] private SupermarketOrganizeConfig config;
        [SerializeField] private SupermarketItemSpawner spawner;
        [SerializeField] private SupermarketOrganizeIntroController introController;
        [SerializeField] private SupermarketContainerZone[] containerZones;
        [SerializeField] private SupermarketOrganizeDifficultyManager difficultyManager;
        [SerializeField] private Transform playerHandleAnchor;
        [SerializeField] private bool useHomePageBeforeGameplay = true;

        SupermarketOrganizeService _service;
        SupermarketHandleZone _handleZone;
        SupermarketItemHighlightController _highlights;
        SupermarketCrosshairInteractor _crosshair;
        SupermarketFirstPersonCamera _firstPersonCamera;
        SupermarketOrganizeSkillController _skillController;
        SupermarketOrganizeAttributeApplicator _attributeApplicator;
        SupermarketSweepRobotController _sweepRobot;
        SupermarketGroundItemOcclusionCuller _occlusionCuller;

        bool _gameplayInitialized;
        bool _challengeStarted;
        Coroutine _deferredGameplayRoutine;

        static readonly HashSet<string> PreservedContainerChildNames = new HashSet<string>
        {
            "GridOrigin",
            "TipObject",
            "PlacePreview",
        };

        public SupermarketOrganizeService Service => _service;
        public SupermarketOrganizeConfig Config => config;
        public SupermarketOrganizeSkillController SkillController => _skillController;
        public SupermarketSweepRobotController SweepRobot => _sweepRobot;
        public bool GameplayInitialized => _gameplayInitialized;
        public bool HasChallengeStarted => _challengeStarted;
        public SupermarketItemSpawner Spawner => spawner;

        void Awake()
        {
            Instance = this;
            config ??= SupermarketOrganizeConfig.ResolveRuntime();
        }

        void Start()
        {
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.SupermarketOrganize))
                return;

            BuildRuntimeGraph();

            var skipInGameHome = SupermarketOrganizeLaunchContext.ConsumeSkipInGameHomePage();
            if (!skipInGameHome && useHomePageBeforeGameplay && TryShowHomePanel())
                return;

            if (skipInGameHome)
                HideInGameHomePanel();

            BeginDeferredGameplay(skipInGameHome ? ResolveDefaultDifficultyEntry() : null);
        }

        public void ShowHome()
        {
            if (_deferredGameplayRoutine != null)
            {
                StopCoroutine(_deferredGameplayRoutine);
                _deferredGameplayRoutine = null;
            }

            // 中途退出时开场可能仍在播放：取消它，避免下次开局重复触发 IntroFinished。
            if (introController != null)
            {
                introController.IntroFinished -= OnIntroFinished;
                introController.CancelIntro();
            }

            _gameplayInitialized = false;
            _challengeStarted = false;
            _service?.ResetForLobby();
            _occlusionCuller?.InvalidateAll();
            _crosshair?.InvalidateZoneCache();
            ResolveDifficultyManager()?.HideAll();
            SetGameplayHudVisible(false);
            UiCoordinator.Close(UiPanelId.SupermarketOrganizeResult);
            UiRouter.OpenSupermarketOrganizeHome();
        }

        void OnDestroy()
        {
            if (_deferredGameplayRoutine != null)
                StopCoroutine(_deferredGameplayRoutine);

            if (_service != null)
                _service.GameWon -= OnGameWon;

            if (Instance == this)
                Instance = null;
        }

        bool TryShowHomePanel()
        {
            var home = FindInGameHomePanel();
            if (home == null)
                return false;

            SetGameplayHudVisible(false);
            home.ShowOnSceneStart();
            return true;
        }

        static void HideInGameHomePanel()
        {
            var home = FindInGameHomePanel();
            if (home != null)
                home.Close();

            UiCoordinator.Close(UiPanelId.SupermarketOrganizeHome);
        }

        static SupermarketOrganizeHomePanel FindInGameHomePanel()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<SupermarketOrganizeHomePanel>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<SupermarketOrganizeHomePanel>(true);
#endif
        }

        public void BeginDeferredGameplay(SupermarketOrganizeDifficultyEntryDef entry)
        {
            if (_gameplayInitialized)
                return;

            _challengeStarted = true;

            if (_deferredGameplayRoutine != null)
                StopCoroutine(_deferredGameplayRoutine);

            _deferredGameplayRoutine = StartCoroutine(BeginDeferredGameplayRoutine(entry));
        }

        IEnumerator BeginDeferredGameplayRoutine(SupermarketOrganizeDifficultyEntryDef entry)
        {
            var minDuration = entry != null ? entry.entryAnimationSeconds : 0f;
            var startTime = Time.realtimeSinceStartup;

            ApplySelectedDifficulty(entry);
            ClearPlacedItemsFromZones(ResolveActiveContainerZones());
            ScatterItems();
            yield return null;
            RegisterZonesAndItems();
            yield return null;

            while (Time.realtimeSinceStartup - startTime < minDuration)
                yield return null;

            _gameplayInitialized = true;
            SetGameplayHudVisible(true);
            BeginIntroFlow();
            _deferredGameplayRoutine = null;
        }

        void SetGameplayHudVisible(bool visible)
        {
#if UNITY_2023_1_OR_NEWER
            var interactionHud = FindFirstObjectByType<SupermarketOrganizeInteractionHud>(FindObjectsInactive.Include);
            var skillHud = FindFirstObjectByType<SupermarketOrganizeSkillHud>(FindObjectsInactive.Include);
            var hudSection = FindFirstObjectByType<SupermarketOrganizeHudSection>(FindObjectsInactive.Include);
#else
            var interactionHud = FindObjectOfType<SupermarketOrganizeInteractionHud>(true);
            var skillHud = FindObjectOfType<SupermarketOrganizeSkillHud>(true);
            var hudSection = FindObjectOfType<SupermarketOrganizeHudSection>(true);
#endif
            if (interactionHud != null)
                interactionHud.gameObject.SetActive(visible);
            if (skillHud != null)
                skillHud.gameObject.SetActive(visible);
            if (hudSection != null)
            {
                hudSection.gameObject.SetActive(visible);
                if (visible)
                    hudSection.RefreshAll();
            }
        }

        public void BuildRuntimeGraph()
        {
            config ??= SupermarketOrganizeConfig.ResolveRuntime();
            var motor = FindObjectOfType<PlayerMotor>();
            if (motor == null)
                return;

            var player = motor.gameObject;
            SupermarketOrganizePlayerSetup.ConfigurePlayer(player, Camera.main);

            _handleZone = SupermarketHandleZoneAnchor.EnsureOnCamera(
                player.transform,
                Camera.main,
                config);
            if (_handleZone == null)
            {
                _handleZone = player.GetComponentInChildren<SupermarketHandleZone>(true);
                if (_handleZone == null)
                {
                    var handleGo = new GameObject("HandleZone");
                    handleGo.transform.SetParent(Camera.main != null ? Camera.main.transform : player.transform, false);
                    if (config != null)
                        handleGo.transform.localPosition = config.handleZoneLocalOffset;
                    _handleZone = handleGo.AddComponent<SupermarketHandleZone>();
                }
            }

            _handleZone.ApplyConfig(config);

            _highlights = player.GetComponent<SupermarketItemHighlightController>();
            if (_highlights == null)
                _highlights = player.AddComponent<SupermarketItemHighlightController>();

            if (config != null)
                _highlights.ApplyColors(
                    config.aimHighlightColor,
                    config.sameIdHighlightColor,
                    config.sameIdOutlineColor,
                    config.sameIdOutlineWidth,
                    config.heldItemOutlineColor,
                    config.heldItemOutlineWidth);

            if (_service == null)
                _service = new SupermarketOrganizeService(config, _handleZone, _highlights);

            _service.GameWon -= OnGameWon;
            _service.GameWon += OnGameWon;

            _crosshair = player.GetComponent<SupermarketCrosshairInteractor>();
            if (_crosshair == null)
                _crosshair = player.AddComponent<SupermarketCrosshairInteractor>();

            var feedback = FindObjectOfType<SupermarketShelfFeedbackUi>(true);
            _crosshair.Configure(Camera.main, _service, _highlights, feedback, config);

            _skillController = player.GetComponent<SupermarketOrganizeSkillController>();
            if (_skillController == null)
                _skillController = player.AddComponent<SupermarketOrganizeSkillController>();
            _skillController.Configure(_service, _crosshair, player.transform);

            _attributeApplicator = player.GetComponent<SupermarketOrganizeAttributeApplicator>();
            if (_attributeApplicator == null)
                _attributeApplicator = player.AddComponent<SupermarketOrganizeAttributeApplicator>();
            _attributeApplicator.Configure(_service, _crosshair);

            if (player.GetComponent<SupermarketOrganizePlayerMotorBoost>() == null)
                player.AddComponent<SupermarketOrganizePlayerMotorBoost>();

            _sweepRobot = GetComponent<SupermarketSweepRobotController>();
            if (_sweepRobot == null)
                _sweepRobot = gameObject.AddComponent<SupermarketSweepRobotController>();
            _sweepRobot.Configure(_service, player.transform);

            var robotAgent = _sweepRobot.Agent;
            if (robotAgent == null)
            {
#if UNITY_2023_1_OR_NEWER
                robotAgent = FindFirstObjectByType<SupermarketSweepRobotAgent>(FindObjectsInactive.Include);
#else
                robotAgent = FindObjectOfType<SupermarketSweepRobotAgent>(true);
#endif
            }

            if (robotAgent != null)
                _sweepRobot.BindAgent(robotAgent);

            _occlusionCuller = GetComponent<SupermarketGroundItemOcclusionCuller>();
            if (_occlusionCuller == null)
                _occlusionCuller = gameObject.AddComponent<SupermarketGroundItemOcclusionCuller>();
            _occlusionCuller.Configure(Camera.main, config, _service, _crosshair, _highlights);

            var interactionHud = FindObjectOfType<SupermarketOrganizeInteractionHud>(true);
            interactionHud?.BindInteractor(_crosshair);

            var skillHud = FindObjectOfType<SupermarketOrganizeSkillHud>(true);
            skillHud?.Bind(_skillController);

            var hudSection = FindObjectOfType<SupermarketOrganizeHudSection>(true);
            hudSection?.RefreshAll();

            _firstPersonCamera = Camera.main != null ? Camera.main.GetComponent<SupermarketFirstPersonCamera>() : null;
            if (_firstPersonCamera == null && Camera.main != null)
                _firstPersonCamera = Camera.main.gameObject.AddComponent<SupermarketFirstPersonCamera>();

            if (_firstPersonCamera != null)
            {
                SupermarketOrganizePlayerSetup.ConfigureFirstPersonCamera(
                    player,
                    Camera.main);
            }

            if (introController == null)
                introController = GetComponent<SupermarketOrganizeIntroController>();
            if (introController == null)
                introController = gameObject.AddComponent<SupermarketOrganizeIntroController>();
        }

        void ScatterItems()
        {
            if (spawner == null)
                spawner = GetComponent<SupermarketItemSpawner>();
            if (spawner == null)
                spawner = gameObject.AddComponent<SupermarketItemSpawner>();

            spawner.ClearSpawnedItems();
        }

        static SupermarketOrganizeDifficultyEntryDef ResolveDefaultDifficultyEntry()
        {
            var home = SupermarketOrganizeHomeConfig.ResolveRuntime();
            var list = home?.GetSortedDifficulties();
            if (list == null || list.Count == 0)
                return null;

            return list[0];
        }

        static void ClearPlacedItemsFromZones(IReadOnlyList<SupermarketContainerZone> zones)
        {
            if (zones == null)
                return;

            for (var z = 0; z < zones.Count; z++)
            {
                var zone = zones[z];
                if (zone == null)
                    continue;

                var zoneTransform = zone.transform;
                for (var i = zoneTransform.childCount - 1; i >= 0; i--)
                {
                    var child = zoneTransform.GetChild(i);
                    if (child == null || PreservedContainerChildNames.Contains(child.name))
                        continue;

                    Destroy(child.gameObject);
                }
            }
        }

        void ApplySelectedDifficulty(SupermarketOrganizeDifficultyEntryDef entry)
        {
            var manager = ResolveDifficultyManager();
            if (entry != null && !string.IsNullOrWhiteSpace(entry.id))
            {
                if (manager != null)
                    manager.ApplyDifficulty(entry.id);
                return;
            }

            manager?.HideAll();
        }

        SupermarketOrganizeDifficultyManager ResolveDifficultyManager()
        {
            if (difficultyManager != null)
                return difficultyManager;

            difficultyManager = SupermarketOrganizeDifficultyManager.ResolveRuntime();
            return difficultyManager;
        }

        IReadOnlyList<SupermarketContainerZone> ResolveActiveContainerZones()
        {
            var manager = ResolveDifficultyManager();
            if (manager != null)
                return manager.ActiveSetup != null
                    ? manager.GetActiveShelves()
                    : System.Array.Empty<SupermarketContainerZone>();

            var zoneList = new List<SupermarketContainerZone>();
            if (containerZones != null)
            {
                for (var i = 0; i < containerZones.Length; i++)
                {
                    if (containerZones[i] != null)
                        zoneList.Add(containerZones[i]);
                }
            }

            if (zoneList.Count > 0)
                return zoneList;

#if UNITY_2023_1_OR_NEWER
            var zones = FindObjectsByType<SupermarketContainerZone>(FindObjectsSortMode.None);
#else
            var zones = FindObjectsOfType<SupermarketContainerZone>();
#endif
            zoneList.AddRange(zones);
            return zoneList;
        }

        static HashSet<SupermarketContainerZone> BuildAllowedZoneSet(IReadOnlyList<SupermarketContainerZone> zones)
        {
            var allowed = new HashSet<SupermarketContainerZone>();
            if (zones == null)
                return allowed;

            for (var i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null)
                    allowed.Add(zones[i]);
            }

            return allowed;
        }

        void RegisterZonesAndItems()
        {
            if (_service == null)
                return;

            _service.ClearRegisteredWorldContent();

            var zoneList = ResolveActiveContainerZones();
            var allowedZones = BuildAllowedZoneSet(zoneList);
            for (var i = 0; i < zoneList.Count; i++)
            {
                var zone = zoneList[i];
                if (zone == null || !allowedZones.Contains(zone) || !zone.gameObject.activeInHierarchy)
                    continue;

                _service.RegisterZone(zone);
            }

            var spawned = spawner != null ? spawner.SpawnAllItems(zoneList) : null;
            if (spawned != null)
            {
                for (var i = 0; i < spawned.Count; i++)
                    _service.RegisterItem(spawned[i]);
            }

            _service.NotifyProgressChanged();
            _crosshair?.InvalidateZoneCache();
        }

        void BeginIntroFlow()
        {
            if (_service == null || introController == null)
            {
                _service?.BeginGameAfterIntro();
                return;
            }

            _service.SetIntroPending();
            introController.IntroFinished += OnIntroFinished;
            introController.BeginIntroIfNeeded();
        }

        void OnIntroFinished()
        {
            if (introController != null)
                introController.IntroFinished -= OnIntroFinished;

            _service?.BeginGameAfterIntro();
        }

        public bool TryShuffleGroundItems()
        {
            if (_service == null || !_service.IsGameRunning)
                return false;

            if (spawner == null)
                spawner = GetComponent<SupermarketItemSpawner>();

            if (spawner == null)
                return false;

            var count = _service.TryShuffleUnplacedGroundItems(spawner);
            if (count > 0)
                _occlusionCuller?.InvalidateAll();

            return count > 0;
        }

        void OnGameWon()
        {
            if (_service == null)
                return;

            UiRouter.OpenSupermarketOrganizeResult(new SupermarketOrganizeResultViewModel
            {
                CompletedShelfCount = _service.CompletedShelfCount,
                TotalShelfCount = _service.TotalShelfCount,
                PlacedItemCount = _service.PlacedItemCount,
                TotalItemSlotCount = _service.TotalItemSlotCount,
            });
        }
    }
}

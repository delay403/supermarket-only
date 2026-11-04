using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Collectibles.Stats;
using Game.Save;
using Game.Core.Config;
using Game.Core.Content;
using Game.Audio;
using Game.VFX;
using Game.UI.Framework;
using Game.Maps;
using Game.Fishing;
using Game.Tower;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 游戏启动入口：<b>请在首场景（或常驻场景）里放一个本组件</b>，在 Inspector 绑定可选 Config 覆盖项、VFX 池、音效表与 BGM。
    /// 若在场景里没有任何 <see cref="GameBootstrapper"/>，运行后会在首张场景加载末尾自动创建一个空白实例（仅有 Resources 回退与空 VFX/音频表）并打警告。
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        private static bool s_booted;

        [Header("Optional overrides (otherwise loaded from Resources)")]
        [SerializeField] private GameBalanceConfig balanceOverride;
        [SerializeField] private Maps.MapDatabase mapDbOverride;
        [SerializeField] private Collectibles.Pets.PetDatabase petDbOverride;
        [SerializeField] private Collectibles.Wings.WingDatabase wingDbOverride;
        [SerializeField] private Collectibles.Trails.TrailDatabase trailDbOverride;
        [SerializeField] private Collectibles.Characters.CharacterDatabase characterDbOverride;
        [SerializeField] private Collectibles.Rods.RodDatabase rodDbOverride;
        [SerializeField] private Fishing.Config.FishDatabase fishDbOverride;
        [SerializeField] private Fishing.Config.FishingZoneDatabase fishingZoneDbOverride;
        [SerializeField] private DailySignIn.SignInDatabase signInDbOverride;
        [SerializeField] private Features.LuckyWheel.WheelDatabase wheelDbOverride;
        [SerializeField] private Features.OnlineReward.OnlineRewardDatabase onlineRewardDbOverride;
        [SerializeField] private Collectibles.QualityVisualLibrary qualityVisualsOverride;
        [SerializeField] private Features.StrengthTraining.StrengthTrainingGroundDatabase strengthTrainingGroundsOverride;
        [SerializeField] private DestroyCity.Config.NukeDatabase destroyCityNukesOverride;
        [SerializeField] private DestroyCity.Config.DestroyCityMapDatabase destroyCityMapsOverride;
        [SerializeField] private DestroyCity.Config.FuelDatabase destroyCityFuelsOverride;
        [SerializeField] private DestroyCity.Config.FuelProducerDatabase destroyCityFuelProducersOverride;
        [SerializeField] private DestroyCity.Config.DestroyCityBuildingDatabase destroyCityBuildingsOverride;

        [Header("Supermarket Organize")]
        [SerializeField] private SupermarketOrganize.Config.SupermarketOrganizeConfig supermarketOrganizeOverride;
        [SerializeField] private SupermarketOrganize.Config.SupermarketItemDatabase supermarketItemsOverride;
        [SerializeField] private SupermarketOrganize.Config.SupermarketMapDatabase supermarketMapsOverride;

        [Header("Animal Hospital")]
        [SerializeField] private AnimalHospital.Config.AnimalHospitalConfig animalHospitalOverride;

        [Header("Worm Dodge")]
        [SerializeField] private Game.WormDodge.Pickups.WormDodgePickupConfig wormDodgePickupOverride;

        [Tooltip("可选：场景跳转 / 过场策略；不填则尝试 Core/Resources/SceneNavigationSettings。")]
        [SerializeField] private SceneNavigationSettings sceneNavigationSettingsOverride;

        [Tooltip("可选：产品形态（仅爬塔/仅钓鱼/融合/仅摧毁城市）；不填则尝试 Resources/GameProductSettings。")]
        [SerializeField] private GameProductSettings gameProductSettingsOverride;

        [Header("Runtime services（可选：首场景在 Bootstrapper 上配表，或之后改 DDOL 对象）")]
        [SerializeField] private List<GameplayVfxManager.VfxPrefabBinding> bootstrapVfxBindings;
        [SerializeField] private List<AudioManager.SfxClipEntry> bootstrapSfxLibrary;
        [SerializeField] private AudioClip bootstrapBgmClip;
        [SerializeField] private bool playBootstrapBgmIfClipSet = true;

        /// <summary>
        /// 在场景里未放置 Bootstrapper 时的兜底：此时所有 SerializeField 为空，配置依赖 <see cref="ConfigRegistry"/> 的 Resources 回退。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateFallbackIfSceneHasNone()
        {
            if (s_booted)
                return;

            var go = new GameObject(nameof(GameBootstrapper));
            DontDestroyOnLoad(go);
            go.AddComponent<GameBootstrapper>();
            Debug.LogWarning(
                "[GameBootstrapper] 场景中未找到 Bootstrapper，已自动创建空实例。请在首场景层级中放置并配置 GameBootstrapper（含 VFX/SFX），否则只能使用 Resources 与各 Manager 默认值。",
                go);
        }

        private void Awake()
        {
            if (s_booted)
            {
                Destroy(gameObject);
                return;
            }

            s_booted = true;
            DontDestroyOnLoad(gameObject);

            var binder = FindObjectOfType<GameModeConfigBinder>(true);
            if (binder != null)
                GameContentService.RegisterBinderAssets(binder);

            SceneNavigationSettings.RuntimeOverride = sceneNavigationSettingsOverride;
            GameProductSettings.RuntimeOverride = gameProductSettingsOverride;

            // Configs
            var configInput = new ConfigRegistry.Input
            {
                balance = balanceOverride,
                maps = mapDbOverride,
                pets = petDbOverride,
                wings = wingDbOverride,
                trails = trailDbOverride,
                characters = characterDbOverride,
                rods = rodDbOverride,
                fish = fishDbOverride,
                fishingZones = fishingZoneDbOverride,
                signIn = signInDbOverride,
                wheel = wheelDbOverride,
                onlineReward = onlineRewardDbOverride,
                qualityVisuals = qualityVisualsOverride,
                strengthTrainingGrounds = strengthTrainingGroundsOverride,
                destroyCityNukes = destroyCityNukesOverride,
                destroyCityMaps = destroyCityMapsOverride,
                destroyCityFuels = destroyCityFuelsOverride,
                destroyCityFuelProducers = destroyCityFuelProducersOverride,
                destroyCityBuildings = destroyCityBuildingsOverride,
                supermarketOrganize = supermarketOrganizeOverride,
                supermarketItems = supermarketItemsOverride,
                supermarketMaps = supermarketMapsOverride,
                animalHospital = animalHospitalOverride,
                wormDodgePickup = wormDodgePickupOverride,
            };

            if (binder != null)
                configInput = binder.BuildConfigRegistryInput(configInput);

            ConfigRegistry.Initialize(configInput);

            // Save
            SaveService.Initialize();
            SaveService.LoadOrCreate();

            if (GetComponent<Game.Features.OnlineReward.OnlineRewardTicker>() == null)
                gameObject.AddComponent<Game.Features.OnlineReward.OnlineRewardTicker>();

            if (GameProductSettings.IsFishingContentEnabled)
                FishingStatBonusDebug.LogPowerBreakdown("GameBootstrapper");

            SceneManager.sceneLoaded -= OnSceneLoadedRefreshMode;
            SceneManager.sceneLoaded += OnSceneLoadedRefreshMode;
            RefreshActiveGameplayScene(SceneManager.GetActiveScene());

            GameplayVfxManager.EnsureCreated();
            if (bootstrapVfxBindings != null && bootstrapVfxBindings.Count > 0)
                GameplayVfxManager.ReplaceRuntimeBindings(bootstrapVfxBindings);

            AudioManager.EnsureCreated();
            var am = AudioManager.Instance;
            if (bootstrapSfxLibrary != null && bootstrapSfxLibrary.Count > 0)
                am.ReplaceSfxLibrary(bootstrapSfxLibrary);
            if (bootstrapBgmClip != null && playBootstrapBgmIfClipSet)
                am.PlayBgm(bootstrapBgmClip, loop: true, fadeSeconds: 0f);
            am.ApplyUserVolumeFromSave();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveService.Save();
        }

        void OnApplicationQuit()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedRefreshMode;
            SaveService.Save();
        }

        static void OnSceneLoadedRefreshMode(Scene scene, LoadSceneMode mode)
        {
            if (!s_booted)
                return;

            RefreshActiveGameplayScene(scene);
        }

        static void RefreshActiveGameplayScene(Scene scene)
        {
            if (!scene.IsValid())
                return;

            GameplayModuleContext.ApplyLoadedScene(scene);
            ApplyModeConfigBinders(scene);
            ApplyModeRuntimeBinders(scene);

            // Binder 可能刚补齐角色/钓竿等 SO，再补一次免费解锁并装备。
            if (SaveService.Data != null)
                FreeCatalogGrant.EnsureFreeItemsOwnedAndEquipped(SaveService.Data);

            if (GameplayModuleContext.IsModuleActive(GameplayModule.Fishing))
                FishingPlayerSetup.RefreshSceneServices();

            if (GameplayModuleContext.IsModuleActive(GameplayModule.DestroyCity))
                DestroyCity.DestroyCityPlayerSetup.RefreshSceneServices();

            if (GameplayModuleContext.IsModuleActive(GameplayModule.WormDodge))
            {
                WormDodge.WormDodgePlayerSetup.RefreshSceneServices();
                WormDodge.WormDodgeAudioBootstrap.EnsureRegistered();
            }

            if (GameplayModuleContext.IsModuleActive(GameplayModule.SupermarketOrganize))
                SupermarketOrganize.SupermarketOrganizePlayerSetup.RefreshSceneServices();

            if (GameplayModuleContext.IsModuleActive(GameplayModule.AnimalHospital))
                AnimalHospital.AnimalHospitalPlayerSetup.RefreshSceneServices();

            if (GameplayModuleContext.IsModuleActive(GameplayModule.Tower))
                RefreshTowerManagersFromCurrentMap();

            UiCoordinator.RefreshEquipmentVisuals();
        }

        static void ApplyModeConfigBinders(Scene scene)
        {
#if UNITY_2023_1_OR_NEWER
            var binders = Object.FindObjectsByType<GameModeConfigBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var binders = Object.FindObjectsOfType<GameModeConfigBinder>(true);
#endif
            for (var i = 0; i < binders.Length; i++)
            {
                var binder = binders[i];
                if (binder == null || binder.gameObject.scene != scene)
                    continue;

                ConfigRegistry.ApplySceneBinder(binder);
            }
        }

        static void ApplyModeRuntimeBinders(Scene scene)
        {
#if UNITY_2023_1_OR_NEWER
            var binders = Object.FindObjectsByType<GameModeRuntimeBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var binders = Object.FindObjectsOfType<GameModeRuntimeBinder>(true);
#endif
            for (var i = 0; i < binders.Length; i++)
            {
                var binder = binders[i];
                if (binder == null || binder.gameObject.scene != scene)
                    continue;

                binder.ApplyToRuntimeServices();
            }
        }

        static void RefreshTowerManagersFromCurrentMap()
        {
#if UNITY_2023_1_OR_NEWER
            var list = Object.FindObjectsByType<TowerManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var list = Object.FindObjectsOfType<TowerManager>();
#endif
            for (var i = 0; i < list.Length; i++)
            {
                var tm = list[i];
                if (tm != null)
                    tm.RefreshFromCurrentMapConfig();
            }
        }
    }
}


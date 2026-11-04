using System;
using System.Collections.Generic;
using Game.Maps;
using Game.UI.MainHome;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 统一产品配置（Resources/<see cref="ResourcesAssetName"/>.asset）：
    /// 主页模式列表、启用模块、Build Settings 场景均由此驱动。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Core/Game Product Settings", fileName = "GameProductSettings")]
    public sealed class GameProductSettings : ScriptableObject
    {
        public const string ResourcesAssetName = "GameProductSettings";

        [Header("Home Mode List")]
        [Tooltip("列表顺序 = 主页 UI 顺序。勾选 enabled 的模式会显示在主页，并决定运行时启用的玩法模块。")]
        public List<MainHomeGameModeEntry> modes = new List<MainHomeGameModeEntry>();

        [Header("Legacy Fallback")]
        [Tooltip("未配置 modes 列表时，用单模式包体形态决定启用模块。")]
        public GameProductMode productMode = GameProductMode.TowerOnly;

        [Tooltip("未配置 modes 列表时，勾选后以 enabledModules 决定启用哪些玩法。")]
        public bool useGameplayModuleFlags;

        [Tooltip("未配置 modes 列表时，融合包体启用的玩法模块（可多选）。")]
        public GameplayModule enabledModules = GameplayModule.Tower | GameplayModule.Fishing;

        [Header("Editor")]
        [Tooltip("在 Inspector 修改模式或场景时，自动同步 Build Settings。")]
        public bool autoSyncBuildScenes = true;

        [Header("Content Layout")]
        [Tooltip("为 true 时保留宠物/翅膀/拖尾/角色等通用养成 Resources 与 Bootstrapper 引用。")]
        public bool includeSharedCosmeticsResources;

        [Tooltip("为 true 时保留签到/转盘等 Activity Resources（爬塔模块会自动包含 Activity）。")]
        public bool includeSharedActivityResources = true;

        [Tooltip("为 true 时保留折扣/钻石兑换等通用经济 Resources。")]
        public bool includeSharedEconomyResources = true;

        public static GameProductSettings RuntimeOverride { get; set; }

        public bool UsesModeListConfiguration => modes != null && modes.Count > 0;

        public static GameProductSettings ResolveActive()
        {
            return RuntimeOverride != null ? RuntimeOverride : TryLoadFromResources();
        }

        public static GameplayModule ResolveEnabledModules() =>
            ResolveEnabledModules(ResolveActive());

        public static GameplayModule ResolveEnabledModules(GameProductSettings settings)
        {
            if (settings == null)
                return GameplayModule.Tower;

            if (settings.UsesModeListConfiguration)
            {
                var fromModes = settings.ComputeModulesFromModes();
                if (fromModes != GameplayModule.None)
                    return fromModes;
            }

            if (settings.useGameplayModuleFlags && settings.enabledModules != GameplayModule.None)
                return settings.enabledModules;

            return ProductModeToModules(settings.productMode);
        }

        public static GameProductMode ActiveMode => ResolveSaveMode();

        public static bool IsTowerContentEnabled => HasModule(GameplayModule.Tower);

        public static bool IsFishingContentEnabled => HasModule(GameplayModule.Fishing);

        public static bool IsDestroyCityContentEnabled => HasModule(GameplayModule.DestroyCity);

        public static bool IsWormDodgeContentEnabled => HasModule(GameplayModule.WormDodge);

        public static bool IsSupermarketOrganizeContentEnabled => HasModule(GameplayModule.SupermarketOrganize);

        public static bool IsAnimalHospitalContentEnabled => HasModule(GameplayModule.AnimalHospital);

        public static bool HasModule(GameplayModule module) =>
            module != GameplayModule.None && (ResolveEnabledModules() & module) == module;

        /// <summary>当前产品是否需要加载宠物/翅膀/拖尾/角色等共享养成配置。</summary>
        public bool NeedsSharedCosmeticsResources() =>
            includeSharedCosmeticsResources || AnyEnabledModeUsesSharedCosmetics();

        /// <summary>当前产品是否需要加载签到/转盘等活动配置。</summary>
        public bool NeedsSharedActivityResources() =>
            includeSharedActivityResources || AnyEnabledModeUsesSharedActivity();

        /// <summary>当前产品是否需要加载折扣/钻石兑换等经济配置。</summary>
        public bool NeedsSharedEconomyResources() =>
            includeSharedEconomyResources
            || AnyEnabledModeUsesSharedCosmetics()
            || AnyEnabledModeUsesSharedActivity();

        public bool AnyEnabledModeUsesSharedCosmetics()
        {
            if (modes != null && modes.Count > 0)
            {
                for (var i = 0; i < modes.Count; i++)
                {
                    var entry = modes[i];
                    if (entry == null || !entry.enabled)
                        continue;

                    var hud = entry.hudVisibility;
                    var equip = entry.equipmentBonuses;
                    if (hud.petHatch || hud.petBackpack || hud.character || hud.trail || hud.wing
                        || equip.pet || equip.wing || equip.trail || equip.character)
                        return true;
                }

                return false;
            }

            var modules = useGameplayModuleFlags && enabledModules != GameplayModule.None
                ? enabledModules
                : ProductModeToModules(productMode);
            return modules != GameplayModule.SupermarketOrganize
                   && modules != GameplayModule.AnimalHospital;
        }

        public bool AnyEnabledModeUsesSharedActivity()
        {
            if (modes != null && modes.Count > 0)
            {
                for (var i = 0; i < modes.Count; i++)
                {
                    var entry = modes[i];
                    if (entry == null || !entry.enabled)
                        continue;

                    if (entry.hudVisibility.signIn || entry.hudVisibility.luckyWheel || entry.hudVisibility.onlineReward)
                        return true;
                }

                return false;
            }

            var modules = useGameplayModuleFlags && enabledModules != GameplayModule.None
                ? enabledModules
                : ProductModeToModules(productMode);
            return modules != GameplayModule.SupermarketOrganize
                   && modules != GameplayModule.AnimalHospital;
        }

        public static int CountEnabledModules()
        {
            var modules = ResolveEnabledModules();
            int count = 0;
            for (var bit = 1; bit <= (int)GameplayModule.AnimalHospital; bit <<= 1)
            {
                if (((int)modules & bit) != 0)
                    count++;
            }

            return count;
        }

        public static GameProductSettings TryLoadFromResources()
        {
            return Resources.Load<GameProductSettings>(ResourcesAssetName);
        }

        public List<MainHomeGameModeEntry> BuildAvailableModes()
        {
            var result = new List<MainHomeGameModeEntry>();
            if (modes == null)
                return result;

            for (var i = 0; i < modes.Count; i++)
            {
                var entry = modes[i];
                if (entry != null && entry.IsAvailableForCurrentProduct())
                    result.Add(entry);
            }

            return result;
        }

        /// <summary>清除未启用模式的预览/过场资源引用，避免单包体打包时拖入其他玩法 UI。</summary>
        public int ClearDisabledModeMediaReferences()
        {
            if (modes == null || modes.Count == 0)
                return 0;

            var cleared = 0;
            for (var i = 0; i < modes.Count; i++)
            {
                var entry = modes[i];
                if (entry == null || entry.IsAvailableForCurrentProduct())
                    continue;

                if (entry.previewSprite != null)
                {
                    entry.previewSprite = null;
                    cleared++;
                }

                if (entry.previewSprite != null)
                {
                    entry.previewSprite = null;
                    cleared++;
                }

                if (entry.mainModeSprite != null)
                {
                    entry.mainModeSprite = null;
                    cleared++;
                }

                if (entry.previewPrefab != null)
                {
                    entry.previewPrefab = null;
                    cleared++;
                }

                if (entry.transitionLoadingSprite != null)
                {
                    entry.transitionLoadingSprite = null;
                    cleared++;
                }
            }

            return cleared;
        }

        public List<string> CollectRequiredGameplaySceneNames(SceneNavigationSettings nav = null, bool includeRemoteGameplayScenes = true)
        {
            nav ??= SceneNavigationSettings.ResolveActive();
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void AddScene(string sceneName)
            {
                if (string.IsNullOrWhiteSpace(sceneName))
                    return;

                sceneName = sceneName.Trim();
                if (!seen.Add(sceneName))
                    return;

                result.Add(sceneName);
            }

            if (nav != null)
            {
                if (nav.useMainHomeScene)
                    AddScene(nav.mainHomeSceneName);
                if (nav.useTransitionScene)
                    AddScene(nav.transitionSceneName);
            }

            if (includeRemoteGameplayScenes)
            {
                var available = BuildAvailableModes();
                for (var i = 0; i < available.Count; i++)
                    AddScene(available[i].ResolveGameplaySceneName(nav));
            }

            return result;
        }

        public GameplayModule ComputeModulesFromModes()
        {
            var modules = GameplayModule.None;
            if (modes == null)
                return modules;

            for (var i = 0; i < modes.Count; i++)
            {
                var entry = modes[i];
                if (entry == null || !entry.IsAvailableForCurrentProduct())
                    continue;

                var module = entry.RequiredModule;
                if (module != GameplayModule.None)
                    modules |= module;
            }

            return modules;
        }

        static GameplayModule ProductModeToModules(GameProductMode mode) => mode switch
        {
            GameProductMode.FishingOnly => GameplayModule.Fishing,
            GameProductMode.Hybrid => GameplayModule.Tower | GameplayModule.Fishing,
            GameProductMode.DestroyCityOnly => GameplayModule.DestroyCity,
            GameProductMode.WormDodgeOnly => GameplayModule.WormDodge,
            GameProductMode.SupermarketOrganizeOnly => GameplayModule.SupermarketOrganize,
            GameProductMode.AnimalHospitalOnly => GameplayModule.AnimalHospital,
            _ => GameplayModule.Tower,
        };

        static GameProductMode ResolveSaveMode()
        {
            var modules = ResolveEnabledModules();
            if (modules == GameplayModule.WormDodge)
                return GameProductMode.WormDodgeOnly;
            if (modules == GameplayModule.SupermarketOrganize)
                return GameProductMode.SupermarketOrganizeOnly;
            if (modules == GameplayModule.AnimalHospital)
                return GameProductMode.AnimalHospitalOnly;
            if (modules == GameplayModule.DestroyCity)
                return GameProductMode.DestroyCityOnly;
            if (modules == GameplayModule.Fishing)
                return GameProductMode.FishingOnly;
            if (modules == GameplayModule.Tower)
                return GameProductMode.TowerOnly;
            if (modules == (GameplayModule.Tower | GameplayModule.Fishing))
                return GameProductMode.Hybrid;

            return GameProductMode.Hybrid;
        }
    }
}

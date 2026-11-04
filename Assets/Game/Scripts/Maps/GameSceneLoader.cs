using Game.Core;
using Game.Core.Config;
using Game.Save;
using Game.SupermarketOrganize;
using Game.UI.MainHome;
using UnityEngine;

namespace Game.Maps
{
    /// <summary>
    /// 地图跳转入口：统一走 <see cref="SceneTransitionCoroutineHost.BeginNavigation"/>（UI Mask 缩放遮罩 + 中转场景 + 异步加载）。
    /// </summary>
    public static partial class GameSceneLoader
    {
        public static void LoadMap(MapDef def)
        {
            if (def == null) return;
            var name = def.gameplaySceneName;
            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: Map '{def.mapId}' has empty gameplaySceneName; skip load.");
                return;
            }

            NavigateToGameplayScene(name.Trim());
        }

        /// <summary>任意 gameplay 场景名跳转。</summary>
        public static void NavigateToGameplayScene(string gameplaySceneName)
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
                return;

            var trimmed = gameplaySceneName.Trim();

            var cfg = SceneNavigationSettings.ResolveActive();
            SceneTransitionCoroutineHost.BeginNavigation(trimmed, cfg);
        }

        /// <summary>
        /// 从玩法场景返回主页（设置「返回主页」、结算页回主页等）。
        /// 打开 MainHome 时直接进入选关页。
        /// </summary>
        public static void NavigateToMainHome()
        {
            MainHomeNavigationContext.RequestOpenToModeSelect();

            var cfg = SceneNavigationSettings.ResolveActive();
            var mainHomeName = ResolveMainHomeSceneName(cfg);

            if (cfg == null || !cfg.useMainHomeScene)
            {
                NavigateToMainGameplayScene(cfg);
                return;
            }

            if (!CanLoadScene(mainHomeName))
            {
                Debug.LogWarning(
                    $"{nameof(GameSceneLoader)}: 主页场景「{mainHomeName}」未加入 Build Settings，无法返回主页。");
                return;
            }

            NavigateToGameplayScene(mainHomeName);
        }

        static string ResolveMainHomeSceneName(SceneNavigationSettings cfg) =>
            cfg != null && !string.IsNullOrWhiteSpace(cfg.mainHomeSceneName)
                ? cfg.mainHomeSceneName.Trim()
                : "MainHome";

        public static bool CanLoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            sceneName = sceneName.Trim();
            if (Application.CanStreamedLevelBeLoaded(sceneName))
                return true;

            return ShouldUseAddressablesForScene(sceneName);
        }

        /// <summary>主页「开始游戏」：按产品形态进入对应 gameplay 场景（仍走过场）。</summary>
        public static void NavigateToMainGameplayFromHome()
        {
            NavigateToMainGameplayScene(SceneNavigationSettings.ResolveActive());
        }

        static void NavigateToMainGameplayScene(SceneNavigationSettings cfg)
        {
            if (IsWormDodgeOnlyProduct())
                NavigateToWormDodgeMainScene(cfg);
            else if (IsAnimalHospitalOnlyProduct())
                NavigateToAnimalHospitalMainScene(cfg);
            else if (IsSupermarketOrganizeOnlyProduct())
                NavigateToSupermarketOrganizeMainScene(cfg);
            else if (IsDestroyCityOnlyProduct())
                NavigateToDestroyCityMainScene(cfg);
            else if (IsFishingOnlyProduct())
                NavigateToFishingMainScene(cfg);
            else
                NavigateToCurrentSaveMap();
        }

        static bool IsAnimalHospitalOnlyProduct() =>
            GameProductSettings.IsAnimalHospitalContentEnabled
            && !GameProductSettings.IsTowerContentEnabled
            && !GameProductSettings.IsFishingContentEnabled
            && !GameProductSettings.IsDestroyCityContentEnabled
            && !GameProductSettings.IsWormDodgeContentEnabled
            && !GameProductSettings.IsSupermarketOrganizeContentEnabled;

        static bool IsSupermarketOrganizeOnlyProduct() =>
            GameProductSettings.IsSupermarketOrganizeContentEnabled
            && !GameProductSettings.IsTowerContentEnabled
            && !GameProductSettings.IsFishingContentEnabled
            && !GameProductSettings.IsDestroyCityContentEnabled
            && !GameProductSettings.IsWormDodgeContentEnabled
            && !GameProductSettings.IsAnimalHospitalContentEnabled;

        static bool IsWormDodgeOnlyProduct() =>
            GameProductSettings.IsWormDodgeContentEnabled
            && !GameProductSettings.IsTowerContentEnabled
            && !GameProductSettings.IsFishingContentEnabled
            && !GameProductSettings.IsDestroyCityContentEnabled
            && !GameProductSettings.IsSupermarketOrganizeContentEnabled
            && !GameProductSettings.IsAnimalHospitalContentEnabled;

        static bool IsDestroyCityOnlyProduct() =>
            GameProductSettings.IsDestroyCityContentEnabled
            && !GameProductSettings.IsTowerContentEnabled
            && !GameProductSettings.IsFishingContentEnabled;

        static bool IsFishingOnlyProduct() =>
            GameProductSettings.IsFishingContentEnabled && !GameProductSettings.IsTowerContentEnabled;

        /// <summary>进入钓鱼主场景（主包直连钓鱼）。</summary>
        public static void NavigateToFishingMainScene(SceneNavigationSettings cfg = null)
        {
            if (!GameProductSettings.IsFishingContentEnabled)
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 当前产品形态未启用钓鱼内容。");
                return;
            }

            cfg ??= SceneNavigationSettings.ResolveActive();
            var scene = ResolveFishingGameplaySceneName(
                SaveService.Data?.fishing?.currentFishingSceneId,
                cfg);
            if (string.IsNullOrWhiteSpace(scene))
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 无法解析钓鱼主场景名。");
                return;
            }

            NavigateToGameplayScene(scene);
        }

        /// <summary>进入存档中当前地图对应的 gameplay 场景。</summary>
        public static void NavigateToCurrentSaveMap()
        {
            var scene = ResolveGameplaySceneFromSave();
            if (string.IsNullOrWhiteSpace(scene))
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 无法从存档解析当前地图场景名（currentMapId / MapDef / gameplaySceneName）。");
                return;
            }

            NavigateToGameplayScene(scene);
        }

        /// <summary>
        /// 当 Pending 为空时尝试根据存档解析 gameplay 场景名（按当前产品形态选择塔/钓鱼/摧毁城市存档）。
        /// </summary>
        public static string ResolveGameplaySceneFromSave()
        {
            if (GameProductSettings.IsWormDodgeContentEnabled)
                return ResolveWormDodgeSceneFromSave();

            if (GameProductSettings.IsSupermarketOrganizeContentEnabled)
                return ResolveSupermarketOrganizeSceneFromSave();

            if (GameProductSettings.IsDestroyCityContentEnabled)
                return ResolveDestroyCitySceneFromSave();

            if (GameProductSettings.IsFishingContentEnabled && !GameProductSettings.IsTowerContentEnabled)
                return ResolveFishingSceneFromSave();

            var maps = ConfigRegistry.Maps;
            var save = SaveService.Data;
            if (maps == null || save == null)
                return null;

            if (!maps.TryGet(save.currentMapId, out var def) || def == null)
                return null;

            var sceneName = def.gameplaySceneName;
            return string.IsNullOrWhiteSpace(sceneName) ? null : sceneName.Trim();
        }

        /// <summary>进入存档中当前钓鱼场景（fishing.currentFishingSceneId）。</summary>
        public static void NavigateToCurrentFishingScene()
        {
            if (!GameProductSettings.IsFishingContentEnabled)
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 当前产品形态未启用钓鱼内容。");
                return;
            }

            var cfg = SceneNavigationSettings.ResolveActive();
            var scene = ResolveFishingGameplaySceneName(
                SaveService.Data?.fishing?.currentFishingSceneId,
                cfg);
            if (string.IsNullOrWhiteSpace(scene))
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 无法从存档解析钓鱼场景名。");
                return;
            }

            NavigateToGameplayScene(scene);
        }

        public static string ResolveFishingSceneFromSave()
        {
            var cfg = SceneNavigationSettings.ResolveActive();
            return ResolveFishingGameplaySceneName(
                SaveService.Data?.fishing?.currentFishingSceneId,
                cfg);
        }

        /// <summary>
        /// 将存档中的钓鱼场景 id 解析为 Build Settings 中的 Unity 场景名。
        /// </summary>
        public static string ResolveFishingGameplaySceneName(
            string fishingSceneId,
            SceneNavigationSettings cfg = null)
        {
            cfg ??= SceneNavigationSettings.ResolveActive();
            var id = string.IsNullOrWhiteSpace(fishingSceneId) ? "fishing_map_1" : fishingSceneId.Trim();

            if (id == "FishScene")
                return id;

            if (id.StartsWith("fishing_map", System.StringComparison.OrdinalIgnoreCase))
            {
                return cfg != null && !string.IsNullOrWhiteSpace(cfg.fishingMainSceneName)
                    ? cfg.fishingMainSceneName.Trim()
                    : "FishScene";
            }

            return id;
        }

        /// <summary>进入摧毁城市主场景。</summary>
        public static void NavigateToDestroyCityMainScene(SceneNavigationSettings cfg = null)
        {
            if (!GameProductSettings.IsDestroyCityContentEnabled)
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 当前产品形态未启用摧毁城市内容。");
                return;
            }

            cfg ??= SceneNavigationSettings.ResolveActive();
            var scene = ResolveDestroyCityGameplaySceneName(
                SaveService.Data?.destroyCity?.currentDestroyCitySceneId,
                cfg);
            if (string.IsNullOrWhiteSpace(scene))
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 无法解析摧毁城市主场景名。");
                return;
            }

            NavigateToGameplayScene(scene);
        }

        public static string ResolveDestroyCitySceneFromSave()
        {
            var cfg = SceneNavigationSettings.ResolveActive();
            return ResolveDestroyCityGameplaySceneName(
                SaveService.Data?.destroyCity?.currentDestroyCitySceneId,
                cfg);
        }

        public static string ResolveDestroyCityGameplaySceneName(
            string destroyCitySceneId,
            SceneNavigationSettings cfg = null)
        {
            cfg ??= SceneNavigationSettings.ResolveActive();
            var id = string.IsNullOrWhiteSpace(destroyCitySceneId) ? "destroy_city_map_1" : destroyCitySceneId.Trim();

            var maps = ConfigRegistry.DestroyCityMaps;
            if (maps != null && maps.TryGet(id, out var def) && def != null &&
                !string.IsNullOrWhiteSpace(def.gameplaySceneName))
                return def.gameplaySceneName.Trim();

            if (id == "DestroyCityScene")
                return id;

            return cfg != null && !string.IsNullOrWhiteSpace(cfg.destroyCityMainSceneName)
                ? cfg.destroyCityMainSceneName.Trim()
                : "DestroyCityScene";
        }

        /// <summary>进入躲避虫子主场景。</summary>
        public static void NavigateToWormDodgeMainScene(SceneNavigationSettings cfg = null)
        {
            if (!GameProductSettings.IsWormDodgeContentEnabled)
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 当前产品形态未启用躲避虫子内容。");
                return;
            }

            cfg ??= SceneNavigationSettings.ResolveActive();
            var scene = ResolveWormDodgeGameplaySceneName(
                SaveService.Data?.wormDodge?.currentWormDodgeSceneId,
                cfg);
            if (string.IsNullOrWhiteSpace(scene))
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 无法解析躲避虫子主场景名。");
                return;
            }

            NavigateToGameplayScene(scene);
        }

        public static string ResolveWormDodgeSceneFromSave()
        {
            var cfg = SceneNavigationSettings.ResolveActive();
            return ResolveWormDodgeGameplaySceneName(
                SaveService.Data?.wormDodge?.currentWormDodgeSceneId,
                cfg);
        }

        public static string ResolveWormDodgeGameplaySceneName(
            string wormDodgeSceneId,
            SceneNavigationSettings cfg = null)
        {
            cfg ??= SceneNavigationSettings.ResolveActive();
            var id = string.IsNullOrWhiteSpace(wormDodgeSceneId) ? "worm_dodge_map_1" : wormDodgeSceneId.Trim();

            if (id == "EscapeWormScene" || id == "WormDodgeScene")
                return id;

            if (id == "jiechongzi")
            {
                return cfg != null && !string.IsNullOrWhiteSpace(cfg.wormDodgeMainSceneName)
                    ? cfg.wormDodgeMainSceneName.Trim()
                    : "EscapeWormScene";
            }

            return cfg != null && !string.IsNullOrWhiteSpace(cfg.wormDodgeMainSceneName)
                ? cfg.wormDodgeMainSceneName.Trim()
                : "EscapeWormScene";
        }

        /// <summary>进入超市收纳主场景。</summary>
        /// <param name="skipInGameHomePage">
        /// 为 true 时跳过场景内难度选关页并直接以默认难度开局（仅 MainHome 快捷进入时使用）。
        /// TT 直连进玩法时应保持 false，让玩家在场景内选择杂货店/超级市场。
        /// </param>
        public static void NavigateToSupermarketOrganizeMainScene(
            SceneNavigationSettings cfg = null,
            bool skipInGameHomePage = false)
        {
            if (!GameProductSettings.IsSupermarketOrganizeContentEnabled)
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 当前产品形态未启用超市收纳内容。");
                return;
            }

            cfg ??= SceneNavigationSettings.ResolveActive();
            var scene = ResolveSupermarketOrganizeGameplaySceneName(
                SaveService.Data?.supermarketOrganize?.currentSceneId,
                cfg);
            if (string.IsNullOrWhiteSpace(scene))
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 无法解析超市收纳主场景名。");
                return;
            }

            if (skipInGameHomePage)
                SupermarketOrganizeLaunchContext.RequestSkipInGameHomePage();

            NavigateToGameplayScene(scene);
        }

        public static string ResolveSupermarketOrganizeSceneFromSave()
        {
            var cfg = SceneNavigationSettings.ResolveActive();
            return ResolveSupermarketOrganizeGameplaySceneName(
                SaveService.Data?.supermarketOrganize?.currentSceneId,
                cfg);
        }

        public static string ResolveSupermarketOrganizeGameplaySceneName(
            string sceneId,
            SceneNavigationSettings cfg = null)
        {
            cfg ??= SceneNavigationSettings.ResolveActive();
            var id = string.IsNullOrWhiteSpace(sceneId) ? "supermarket_map_1" : sceneId.Trim();

            var maps = ConfigRegistry.SupermarketMaps;
            if (maps != null && maps.TryGet(id, out var def) && def != null &&
                !string.IsNullOrWhiteSpace(def.gameplaySceneName))
                return def.gameplaySceneName.Trim();

            if (id == "SupermarketOrganizeScene")
                return id;

            return cfg != null && !string.IsNullOrWhiteSpace(cfg.supermarketOrganizeMainSceneName)
                ? cfg.supermarketOrganizeMainSceneName.Trim()
                : "SupermarketOrganizeScene";
        }

        /// <summary>进入动物医院主场景（进入后自动开始当日流程）。</summary>
        public static void NavigateToAnimalHospitalMainScene(SceneNavigationSettings cfg = null)
        {
            if (!GameProductSettings.IsAnimalHospitalContentEnabled)
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 当前产品形态未启用动物医院内容。");
                return;
            }

            cfg ??= SceneNavigationSettings.ResolveActive();
            var scene = ResolveAnimalHospitalGameplaySceneName(
                SaveService.Data?.animalHospital?.currentSceneId,
                cfg);
            if (string.IsNullOrWhiteSpace(scene))
            {
                Debug.LogWarning($"{nameof(GameSceneLoader)}: 无法解析动物医院主场景名。");
                return;
            }

            NavigateToGameplayScene(scene);
        }

        public static string ResolveAnimalHospitalGameplaySceneName(
            string sceneId,
            SceneNavigationSettings cfg = null)
        {
            cfg ??= SceneNavigationSettings.ResolveActive();
            if (!string.IsNullOrWhiteSpace(sceneId))
            {
                var id = sceneId.Trim();
                if (id == "AnimalHospitalScene" || Application.CanStreamedLevelBeLoaded(id))
                    return id;
            }

            return cfg != null && !string.IsNullOrWhiteSpace(cfg.animalHospitalMainSceneName)
                ? cfg.animalHospitalMainSceneName.Trim()
                : "AnimalHospitalScene";
        }
    }
}

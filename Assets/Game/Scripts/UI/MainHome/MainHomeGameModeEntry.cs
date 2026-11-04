using System;
using Game.Core;
using Game.Maps;
using UnityEngine;

namespace Game.UI.MainHome
{
    public enum MainHomeGameplayKind
    {
        AutoFromProductSettings = 0,
        WormDodge = 1,
        DestroyCity = 2,
        Fishing = 3,
        Tower = 4,
        CustomScene = 5,
        SupermarketOrganize = 6,
        AnimalHospital = 7,
    }

    [Serializable]
    public sealed class MainHomeGameModeEntry
    {
        [Tooltip("勾选后该模式会出现在主页列表，并参与运行时模块启用判定。")]
        public bool enabled = true;

        public string modeId = "worm_dodge";
        public string displayName = "躲避虫子";

        [Tooltip("主页左侧主模式大卡图（列表第一项使用；为空则回退 previewSprite）。")]
        public Sprite mainModeSprite;

        [Tooltip("右侧副模式列表缩略图；为空则回退 previewSprite。")]
        public Sprite previewSprite;

        [Tooltip("主页左侧预览预制体（可选；主模式优先 mainModeSprite）。")]
        public GameObject previewPrefab;

        [Tooltip("进入该模式时过场加载页背景图（可选）；不填则用 SceneNavigationSettings 默认图。")]
        public Sprite transitionLoadingSprite;

        public MainHomeGameplayKind gameplayKind = MainHomeGameplayKind.WormDodge;

        [Header("HUD 按钮显隐")]
        public GameModeHudVisibility hudVisibility = GameModeHudVisibility.SharedExceptFishingOnly;

        [Header("装备属性加成")]
        public GameModeEquipmentBonuses equipmentBonuses = GameModeEquipmentBonuses.SharedExceptRod;

        [Tooltip("玩法场景名。")]
        public string customSceneName;

        public GameplayModule RequiredModule => gameplayKind switch
        {
            MainHomeGameplayKind.WormDodge => GameplayModule.WormDodge,
            MainHomeGameplayKind.DestroyCity => GameplayModule.DestroyCity,
            MainHomeGameplayKind.Fishing => GameplayModule.Fishing,
            MainHomeGameplayKind.Tower => GameplayModule.Tower,
            MainHomeGameplayKind.SupermarketOrganize => GameplayModule.SupermarketOrganize,
            MainHomeGameplayKind.AnimalHospital => GameplayModule.AnimalHospital,
            _ => GameplayModule.None,
        };

        public bool IsAvailableForCurrentProduct()
        {
            if (!enabled)
                return false;

            return gameplayKind == MainHomeGameplayKind.CustomScene
                ? !string.IsNullOrWhiteSpace(customSceneName)
                : true;
        }

        public bool CanLaunchFromHome() => enabled && IsSceneLoadable();

        public Sprite ResolveMainModeSprite() =>
            mainModeSprite != null ? mainModeSprite : previewSprite;

        public Sprite ResolveSecondarySprite() => previewSprite;

        public static string ResolveDisplayPlayerCount(MainHomeGameModeEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.modeId))
                return "0";

            unchecked
            {
                var hash = entry.modeId.GetHashCode();
                var count = 100_000L + System.Math.Abs((long)hash % 9_000_000L);
                return count.ToString();
            }
        }

        public void ApplyPresetForGameplayKind()
        {
            hudVisibility = GameModeHudVisibility.ForGameplayKind(gameplayKind);
            equipmentBonuses = GameModeEquipmentBonuses.ForGameplayKind(gameplayKind);
        }

        public bool IsSceneLoadable()
        {
            var scene = ResolveGameplaySceneName();
            return !string.IsNullOrWhiteSpace(scene) && GameSceneLoader.CanLoadScene(scene);
        }

        public string ResolveGameplaySceneName(SceneNavigationSettings nav = null)
        {
            nav ??= SceneNavigationSettings.ResolveActive();

            if (gameplayKind == MainHomeGameplayKind.CustomScene)
                return string.IsNullOrWhiteSpace(customSceneName) ? string.Empty : customSceneName.Trim();

            if (!string.IsNullOrWhiteSpace(customSceneName))
                return customSceneName.Trim();

            return gameplayKind switch
            {
                MainHomeGameplayKind.WormDodge => nav != null && !string.IsNullOrWhiteSpace(nav.wormDodgeMainSceneName)
                    ? nav.wormDodgeMainSceneName.Trim()
                    : "EscapeWormScene",
                MainHomeGameplayKind.DestroyCity => nav != null && !string.IsNullOrWhiteSpace(nav.destroyCityMainSceneName)
                    ? nav.destroyCityMainSceneName.Trim()
                    : "DestroyCityScene",
                MainHomeGameplayKind.Fishing => nav != null && !string.IsNullOrWhiteSpace(nav.fishingMainSceneName)
                    ? nav.fishingMainSceneName.Trim()
                    : "FishScene",
                MainHomeGameplayKind.Tower => GameSceneLoader.ResolveGameplaySceneFromSave(),
                MainHomeGameplayKind.SupermarketOrganize => nav != null
                    && !string.IsNullOrWhiteSpace(nav.supermarketOrganizeMainSceneName)
                    ? nav.supermarketOrganizeMainSceneName.Trim()
                    : "SupermarketOrganizeScene",
                MainHomeGameplayKind.AnimalHospital => nav != null
                    && !string.IsNullOrWhiteSpace(nav.animalHospitalMainSceneName)
                    ? nav.animalHospitalMainSceneName.Trim()
                    : "AnimalHospitalScene",
                _ => string.Empty,
            };
        }

        public void NavigateToGameplay(bool fromMainHomeLobby = false)
        {
            GameplayModuleContext.SetFromEntry(this);
            SceneTransitionPresentation.SetPendingBackground(transitionLoadingSprite);

            switch (gameplayKind)
            {
                case MainHomeGameplayKind.WormDodge:
                    GameSceneLoader.NavigateToWormDodgeMainScene();
                    return;
                case MainHomeGameplayKind.DestroyCity:
                    GameSceneLoader.NavigateToDestroyCityMainScene();
                    return;
                case MainHomeGameplayKind.Fishing:
                    GameSceneLoader.NavigateToCurrentFishingScene();
                    return;
                case MainHomeGameplayKind.Tower:
                    GameSceneLoader.NavigateToCurrentSaveMap();
                    return;
                case MainHomeGameplayKind.SupermarketOrganize:
                    GameSceneLoader.NavigateToSupermarketOrganizeMainScene(skipInGameHomePage: fromMainHomeLobby);
                    return;
                case MainHomeGameplayKind.AnimalHospital:
                    GameSceneLoader.NavigateToAnimalHospitalMainScene();
                    return;
                case MainHomeGameplayKind.CustomScene:
                    var scene = ResolveGameplaySceneName();
                    if (!string.IsNullOrWhiteSpace(scene))
                        GameSceneLoader.NavigateToGameplayScene(scene);
                    return;
                default:
                    GameSceneLoader.NavigateToMainGameplayFromHome();
                    return;
            }
        }
    }
}

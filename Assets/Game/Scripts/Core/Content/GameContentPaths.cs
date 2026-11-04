using System.Collections.Generic;

namespace Game.Core.Content
{
    /// <summary>
    /// 工程内容根路径约定。仅 <c>*/Resources/</c> 目录会被 Unity 无条件打入包体；
    /// <c>*/Content/</c> 仅供编辑器存放或未启用模块的离线资源，不参与 Resources 打包规则。
    /// </summary>
    public static class GameContentPaths
    {
        public const string LegacyResourcesRoot = "Assets/Resources";

        public const string CoreResourcesRoot = "Assets/Game/Core/Resources";
        public const string ModesRoot = "Assets/Game/Modes";
        public const string SharedRoot = "Assets/Game/Shared";

        public const string FguiResourcesRoot = "Assets/Game/UI/FGUI/Resources";
        public const string FguiContentRoot = "Assets/Game/UI/FGUI/Content";
        public const string LegacyFguiResourcesRoot = "Assets/FGUIforUnity/Resources";

        public const string ModeSlotResources = "Resources";
        public const string ModeSlotContent = "Content";
        public const string ModeSlotArt = "Art";
        public const string ModeSlotAudio = "Audio";
        public const string ModeSlotScenes = "Scenes";

        public const string ModeSlotConfigs = "Configs";

        public static readonly string[] ModeStandardSlots =
        {
            ModeSlotResources,
            ModeSlotContent,
            ModeSlotArt,
            ModeSlotAudio,
            ModeSlotScenes,
        };

        public const string ProductSettingsAssetPath = CoreResourcesRoot + "/GameProductSettings.asset";
        public const string SceneNavigationSettingsAssetPath = CoreResourcesRoot + "/SceneNavigationSettings.asset";

        public const string CoreScenesRoot = "Assets/Game/Core/Scenes";
        public const string SceneTransitionScenePath = CoreScenesRoot + "/SceneTransition.unity";
        public const string MainHomeScenePath = CoreScenesRoot + "/MainHome.unity";

        public static string ModeContentRoot(GameplayModule module) =>
            $"{ModesRoot}/{ModuleFolderName(module)}/{ModeSlotContent}";

        public static string ModeConfigRoot(GameplayModule module) =>
            $"{ModeContentRoot(module)}/{ModeSlotConfigs}";

        public static string SharedFeatureConfigRoot(SharedContentFeature feature) =>
            $"{SharedFeatureContentRoot(feature)}/{ModeSlotConfigs}";

        public static string SupermarketConfigRoot =>
            $"{ModeConfigRoot(GameplayModule.SupermarketOrganize)}/SupermarketOrganize";

        public static string SupermarketItemDatabaseAssetPath =>
            $"{SupermarketConfigRoot}/SupermarketItemDatabase.asset";

        public static string SupermarketItemDefFolder =>
            $"{SupermarketConfigRoot}/Item";

        public static string DestroyCityConfigRoot =>
            $"{ModeConfigRoot(GameplayModule.DestroyCity)}/DestroyCity";

        public static string FishingConfigRoot =>
            $"{ModeConfigRoot(GameplayModule.Fishing)}";

        public static string TowerConfigRoot =>
            $"{ModeConfigRoot(GameplayModule.Tower)}";

        public static string SupermarketArtRoot =>
            ModeArtRoot(GameplayModule.SupermarketOrganize);

        public static string ModeArtRoot(GameplayModule module) =>
            ModeSlotRoot(module, ModeSlotArt);

        public static string ModeAudioRoot(GameplayModule module) =>
            ModeSlotRoot(module, ModeSlotAudio);

        public static string ModeScenesRoot(GameplayModule module) =>
            $"{ModesRoot}/{ModuleFolderName(module)}/{ModeSlotScenes}";

        public static string ModeResourcesRoot(GameplayModule module) =>
            $"{ModesRoot}/{ModuleFolderName(module)}/{ModeSlotResources}";

        public static string FishingOutputDir =>
            $"{FishingConfigRoot}/Fish";

        public static string DestroyCityModuleRoot =>
            DestroyCityConfigRoot;

        public static string ModeSlotRoot(GameplayModule module, string slot) =>
            $"{ModesRoot}/{ModuleFolderName(module)}/{slot}";

        public static string AnimalHospitalConfigRoot =>
            $"{ModeConfigRoot(GameplayModule.AnimalHospital)}/AnimalHospital";

        public static string WormDodgeConfigRoot =>
            ModeConfigRoot(GameplayModule.WormDodge);

        public static string WormDodgePickupConfigAssetPath =>
            $"{WormDodgeConfigRoot}/WormDodgePickupConfig.asset";

        public static IEnumerable<GameplayModule> EnumerateGameplayModules()
        {
            for (var bit = 1; bit <= (int)GameplayModule.AnimalHospital; bit <<= 1)
                yield return (GameplayModule)bit;
        }

        public static string SharedFeatureResourcesRoot(SharedContentFeature feature) =>
            $"{SharedRoot}/{FeatureFolderName(feature)}/Resources";

        public static string SharedFeatureContentRoot(SharedContentFeature feature) =>
            $"{SharedRoot}/{FeatureFolderName(feature)}/Content";

        public static string ModuleFolderName(GameplayModule module) => module switch
        {
            GameplayModule.SupermarketOrganize => "SupermarketOrganize",
            GameplayModule.AnimalHospital => "AnimalHospital",
            GameplayModule.Tower => "Tower",
            GameplayModule.Fishing => "Fishing",
            GameplayModule.DestroyCity => "DestroyCity",
            GameplayModule.WormDodge => "WormDodge",
            _ => module.ToString(),
        };

        public static string FeatureFolderName(SharedContentFeature feature) => feature switch
        {
            SharedContentFeature.Cosmetics => "Cosmetics",
            SharedContentFeature.Economy => "Economy",
            _ => feature.ToString(),
        };
    }

    public enum SharedContentFeature
    {
        Cosmetics,
        Economy,
    }
}

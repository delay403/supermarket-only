#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Content;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 定义各玩法模块与共享功能包的资源槽位映射。
    /// 模式配置固定放在 <see cref="GameContentPaths.ModeConfigRoot"/>，不再按启用状态搬运。
    /// </summary>
    static class GameModeContentLayout
    {
        public readonly struct ContentEntry
        {
            public readonly string legacyResourcesRelativePath;
            public readonly string resourcesLoadPrefix;

            public ContentEntry(string legacyResourcesRelativePath, string resourcesLoadPrefix = null)
            {
                this.legacyResourcesRelativePath = legacyResourcesRelativePath;
                this.resourcesLoadPrefix = resourcesLoadPrefix ?? legacyResourcesRelativePath;
            }
        }

        public readonly struct ModuleSpec
        {
            public readonly GameplayModule module;
            public readonly IReadOnlyList<ContentEntry> entries;

            public ModuleSpec(GameplayModule module, params ContentEntry[] entries)
            {
                this.module = module;
                this.entries = entries;
            }
        }

        public readonly struct SharedFeatureSpec
        {
            public readonly SharedContentFeature feature;
            public readonly Func<GameProductSettings, bool> isEnabled;
            public readonly IReadOnlyList<ContentEntry> entries;

            public SharedFeatureSpec(
                SharedContentFeature feature,
                Func<GameProductSettings, bool> isEnabled,
                params ContentEntry[] entries)
            {
                this.feature = feature;
                this.isEnabled = isEnabled;
                this.entries = entries;
            }
        }

        public static readonly ContentEntry[] CoreEntries =
        {
            new("GameProductSettings.asset"),
            new("SceneNavigationSettings.asset"),
            new("GameBalanceConfig.asset"),
            new("喜脉体.ttf"),
            new("PlayerJumpConfig.asset"),
            new("URP"),
        };

        static readonly ModuleSpec[] s_modules =
        {
            new(
                GameplayModule.AnimalHospital,
                new ContentEntry("AnimalHospital")),
            new(
                GameplayModule.SupermarketOrganize,
                new ContentEntry("SupermarketOrganize")),
            new(
                GameplayModule.Tower,
                new ContentEntry("Activity"),
                new ContentEntry("LeaderBoard")),
            new(
                GameplayModule.Fishing,
                new ContentEntry("Fish"),
                new ContentEntry("FishZone"),
                new ContentEntry("FishDatabase.asset"),
                new ContentEntry("FishingZoneDatabase.asset"),
                new ContentEntry("FishingCatchCelebrateLibrary.asset"),
                new ContentEntry("Rod"),
                new ContentEntry("RodDatabase.asset"),
                new ContentEntry("StrengthTrainingGroundDatabase.asset")),
            new(
                GameplayModule.DestroyCity,
                new ContentEntry("DestroyCity")),
            new(
                GameplayModule.WormDodge,
                new ContentEntry("WormDodgePickupConfig.asset")),
        };

        static readonly SharedFeatureSpec[] s_sharedFeatures =
        {
            new(
                SharedContentFeature.Cosmetics,
                settings => settings == null || settings.includeSharedCosmeticsResources,
                new ContentEntry("PetDatabase.asset"),
                new ContentEntry("Pets"),
                new ContentEntry("WingDatabase.asset"),
                new ContentEntry("WingDatabase 1.asset"),
                new ContentEntry("Wings"),
                new ContentEntry("TrailDatabase.asset"),
                new ContentEntry("Trails"),
                new ContentEntry("CharacterDatabase.asset"),
                new ContentEntry("Character"),
                new ContentEntry("QualityVisualLibrary.asset"),
                new ContentEntry("Hatch")),
            new(
                SharedContentFeature.Economy,
                settings => settings == null || settings.includeSharedEconomyResources,
                new ContentEntry("DiscountOfferDatabase.asset"),
                new ContentEntry("DiamondCoinsExchangeDatabase.asset")),
        };

        public static IReadOnlyList<ModuleSpec> Modules => s_modules;

        public static IReadOnlyList<SharedFeatureSpec> SharedFeatures => s_sharedFeatures;

        public static string ModeConfigRoot(GameplayModule module) =>
            GameContentPaths.ModeConfigRoot(module);

        public static string SharedConfigRoot(SharedContentFeature feature) =>
            GameContentPaths.SharedFeatureConfigRoot(feature);

        public static bool ShouldIncludeTowerActivityResources(GameProductSettings settings) =>
            GameProductSettings.HasModule(GameplayModule.Tower)
            || (settings != null && settings.includeSharedActivityResources);

        public static IReadOnlyList<string> CollectLegacySourcePaths(GameProductSettings settings)
        {
            var result = new List<string>();
            CollectFromRoot(GameContentPaths.LegacyResourcesRoot, result);

            if (DirectoryExists(GameContentPaths.LegacyFguiResourcesRoot))
                CollectFromRoot(GameContentPaths.LegacyFguiResourcesRoot, result, recursive: true);

            return result;
        }

        static void CollectFromRoot(string root, List<string> result, bool recursive = false)
        {
            if (!DirectoryExists(root))
                return;

            if (!recursive)
            {
                foreach (var entry in System.IO.Directory.GetFileSystemEntries(root))
                {
                    var name = System.IO.Path.GetFileName(entry);
                    if (string.IsNullOrEmpty(name) || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;

                    result.Add($"{root}/{name}".Replace('\\', '/'));
                }

                return;
            }

            foreach (var file in System.IO.Directory.GetFiles(root, "*", System.IO.SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(file.Replace('\\', '/'));
            }
        }

        static bool DirectoryExists(string path) =>
            !string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path);
    }
}
#endif

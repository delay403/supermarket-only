using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Core.Content;
using Game.Core.Config;
using Game.Maps;
using Game.Collectibles.Pets;
using Game.Collectibles.Wings;
using Game.Collectibles.Trails;
using Game.DailySignIn;

namespace Game.EditorTools
{
    public static class GameAssetGenerator
    {
        private const string CoreResourcesDir = GameContentPaths.CoreResourcesRoot;
        private static readonly string TowerResourcesDir = GameContentPaths.ModeResourcesRoot(Game.Core.GameplayModule.Tower);
        private static readonly string SharedCosmeticsDir = GameContentPaths.SharedFeatureResourcesRoot(Game.Core.Content.SharedContentFeature.Cosmetics);

        [MenuItem("Game/Generate/Create Resources Configs (SO)")]
        public static void CreateResourceConfigs()
        {
            EnsureDir(CoreResourcesDir);
            EnsureDir($"{TowerResourcesDir}/Activity");
            EnsureDir(SharedCosmeticsDir);

            CreateOrSelect<GameBalanceConfig>($"{CoreResourcesDir}/GameBalanceConfig.asset");
            CreateOrSelect<MapDatabase>($"{TowerResourcesDir}/Activity/MapDatabase.asset");
            CreateOrSelect<PetDatabase>($"{SharedCosmeticsDir}/PetDatabase.asset");
            CreateOrSelect<WingDatabase>($"{SharedCosmeticsDir}/WingDatabase.asset");
            CreateOrSelect<TrailDatabase>($"{SharedCosmeticsDir}/TrailDatabase.asset");
            CreateOrSelect<SignInDatabase>($"{TowerResourcesDir}/Activity/SignInDatabase.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Game/Generate/Create Sample Maps + SignIn Days")]
        public static void CreateSampleContent()
        {
            EnsureDir($"{TowerResourcesDir}/Activity");

            var mapDb = CreateOrSelect<MapDatabase>($"{TowerResourcesDir}/Activity/MapDatabase.asset");
            if (mapDb.maps == null) mapDb.maps = new List<MapDef>();

            EnsureDir("Assets/Game/Configs/Maps");
            for (int i = 1; i <= 3; i++)
            {
                var mapPath = $"Assets/Game/Configs/Maps/Map_{i}.asset";
                var def = AssetDatabase.LoadAssetAtPath<MapDef>(mapPath);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<MapDef>();
                    def.mapId = $"map_{i}";
                    def.displayName = $"Map {i}";
                    def.climbSpeedMultiplier = 1f + (i - 1) * 0.1f;
                    def.maxTowerHeight = i == 1 ? 200f : 400f * i;
                    def.medalsRequiredToUnlock = i == 1 ? 0 : 1;
                    AssetDatabase.CreateAsset(def, mapPath);
                }
                if (!mapDb.maps.Contains(def)) mapDb.maps.Add(def);
            }
            EditorUtility.SetDirty(mapDb);

            var signDb = CreateOrSelect<SignInDatabase>($"{TowerResourcesDir}/Activity/SignInDatabase.asset");
            if (signDb.days == null) signDb.days = new List<SignInDayDef>();

            EnsureDir("Assets/Game/Configs/SignIn");
            for (int day = 1; day <= 7; day++)
            {
                var dayPath = $"Assets/Game/Configs/SignIn/Day_{day}.asset";
                var dayDef = AssetDatabase.LoadAssetAtPath<SignInDayDef>(dayPath);
                if (dayDef == null)
                {
                    dayDef = ScriptableObject.CreateInstance<SignInDayDef>();
                    dayDef.dayNumber = day;
                    dayDef.reward = new RewardDef
                    {
                        type = day % 2 == 0 ? RewardType.Gems : RewardType.Coins,
                        amount = day % 2 == 0 ? 10 : 100 * day,
                        itemId = string.Empty
                    };
                    AssetDatabase.CreateAsset(dayDef, dayPath);
                }
                if (!signDb.days.Contains(dayDef)) signDb.days.Add(dayDef);
            }
            EditorUtility.SetDirty(signDb);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureDir(string path)
        {
            EnsureDirPublic(path);
        }

        public static void EnsureDirPublic(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static T CreateOrSelect<T>(string assetPath) where T : ScriptableObject =>
            CreateOrSelectPublic<T>(assetPath);

        public static T CreateOrSelectPublic<T>(string assetPath) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }
    }
}


#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Core.Content;
using Game.SupermarketOrganize.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// 为 Build Settings 场景中的 <see cref="GameBootstrapper"/> / <see cref="GameModeConfigBinder"/> 自动绑定 Content/Configs 资产。
    /// </summary>
    public static class GameModeBootstrapWiringUtility
    {
        const string SupermarketScenePath = "Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity";
        const string AnimalHospitalScenePath = "Assets/Game/Modes/AnimalHospital/Scenes/AnimalHospitalScene.unity";
        const string FishingScenePath = "Assets/Game/Modes/Fishing/Scenes/FishScene.unity";
        const string DestroyCityScenePath = "Assets/Game/Modes/DestroyCity/Scenes/DestroyCityScene.unity";
        const string WormDodgeScenePath = "Assets/Game/Modes/WormDodge/Scenes/EscapeWormScene.unity";
        const string TowerMap1ScenePath = "Assets/Game/Modes/Tower/Scenes/Map1.unity";
        const string TowerMap2ScenePath = "Assets/Game/Modes/Tower/Scenes/Map2.unity";
        const string TowerMap3ScenePath = "Assets/Game/Modes/Tower/Scenes/Map3.unity";

        const string SharedCosmeticsRoot = "Assets/Game/Shared/Cosmetics/Content";
        const string SharedEconomyRoot = "Assets/Game/Shared/Economy/Content";
        const string TowerActivityConfigRoot = "Assets/Game/Modes/Tower/Content/Configs/Activity";

        [MenuItem("Game/Content/Auto-Wire Mode Config References In Build Scenes")]
        public static void AutoWireMenu()
        {
            var settings = GameProductFusionBuildTools.LoadProductSettingsAsset();
            var report = AutoWireEnabledModeConfigs(settings, log: true, saveScenes: true);
            EditorUtility.DisplayDialog("模式配置引用", report, "确定");
        }

        public static string AutoWireEnabledModeConfigs(GameProductSettings settings, bool log, bool saveScenes = false)
        {
            EnsureWormDodgePickupConfigAsset();

            var changed = 0;
            var sb = new StringBuilder();
            sb.AppendLine("=== Auto-Wire Mode Config References ===");
            sb.AppendLine($"启用模块：{GameProductSettings.ResolveEnabledModules(settings)}");
            sb.AppendLine();

            var originalScene = SceneManager.GetActiveScene().path;
            var scenePaths = CollectEnabledBuildScenePaths();

            for (var i = 0; i < scenePaths.Count; i++)
            {
                var scenePath = scenePaths[i];
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var sceneChanged = false;

                if (GameProductSettings.HasModule(GameplayModule.SupermarketOrganize)
                    && scenePath.Equals(SupermarketScenePath, System.StringComparison.OrdinalIgnoreCase))
                    sceneChanged |= WireSupermarketScene();

                if (GameProductSettings.HasModule(GameplayModule.AnimalHospital)
                    && scenePath.Equals(AnimalHospitalScenePath, System.StringComparison.OrdinalIgnoreCase))
                    sceneChanged |= WireAnimalHospitalScene();

                if (GameProductSettings.HasModule(GameplayModule.Fishing)
                    && scenePath.Equals(FishingScenePath, System.StringComparison.OrdinalIgnoreCase))
                    sceneChanged |= WireFishingScene(settings);

                if (GameProductSettings.HasModule(GameplayModule.DestroyCity)
                    && scenePath.Equals(DestroyCityScenePath, System.StringComparison.OrdinalIgnoreCase))
                    sceneChanged |= WireDestroyCityScene(settings);

                if (GameProductSettings.HasModule(GameplayModule.WormDodge)
                    && scenePath.Equals(WormDodgeScenePath, System.StringComparison.OrdinalIgnoreCase))
                    sceneChanged |= WireWormDodgeScene(settings);

                if (GameProductSettings.HasModule(GameplayModule.Tower)
                    && IsTowerMapScene(scenePath))
                    sceneChanged |= WireTowerScene(settings);

                if (sceneChanged)
                {
                    if (saveScenes)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }

                    changed++;
                    sb.AppendLine($"已更新：{scenePath}");
                }
            }

            if (!string.IsNullOrEmpty(originalScene) && System.IO.File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            sb.AppendLine();
            sb.AppendLine($"共更新 {changed} 个场景。");
            if (log)
                Debug.Log(sb.ToString());

            return sb.ToString();
        }

        public static void AutoWireEnabledModeConfigs(bool log) =>
            AutoWireEnabledModeConfigs(GameProductFusionBuildTools.LoadProductSettingsAsset(), log, saveScenes: false);

        static bool WireSupermarketScene()
        {
            var binder = EnsureConfigBinder();
            var so = new SerializedObject(binder);
            so.Update();
            var changed = false;

            changed |= AssignIfFound(so, "supermarketOrganize", $"{GameContentPaths.SupermarketConfigRoot}/SupermarketOrganizeConfig.asset");
            changed |= AssignIfFound(so, "supermarketItems", GameContentPaths.SupermarketItemDatabaseAssetPath);
            changed |= AssignIfFound(so, "supermarketMaps", $"{GameContentPaths.SupermarketConfigRoot}/SupermarketMapDatabase.asset");
            changed |= AssignIfFound(so, "supermarketProgression", $"{GameContentPaths.SupermarketConfigRoot}/SupermarketOrganizeProgressionConfig.asset");
            changed |= AssignIfFound(so, "supermarketUpgradeUi", $"{GameContentPaths.SupermarketConfigRoot}/SupermarketOrganizeUpgradeUiConfig.asset");
            changed |= AssignIfFound(so, "supermarketHome", $"{GameContentPaths.SupermarketConfigRoot}/SupermarketOrganizeHomeConfig.asset");
            changed |= WireSharedCosmetics(so);
            changed |= WireSharedActivity(so);
            changed |= WireSharedEconomy(so);

            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();

            return changed;
        }

        static bool WireAnimalHospitalScene()
        {
            var binder = EnsureConfigBinder();
            var so = new SerializedObject(binder);
            so.Update();
            var changed = false;

            changed |= AssignIfFound(so, "animalHospital",
                $"{GameContentPaths.AnimalHospitalConfigRoot}/AnimalHospitalConfig.asset");
            changed |= WireSharedEconomy(so);

            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();

            return changed;
        }

        static bool WireFishingScene(GameProductSettings settings)
        {
            var binder = EnsureConfigBinder();
            var so = new SerializedObject(binder);
            so.Update();
            var changed = false;

            changed |= AssignIfFound(so, "fishDatabase", $"{GameContentPaths.FishingConfigRoot}/FishDatabase.asset");
            changed |= AssignIfFound(so, "fishingZoneDatabase", $"{GameContentPaths.FishingConfigRoot}/FishingZoneDatabase.asset");
            changed |= AssignIfFound(so, "rodDatabase", $"{GameContentPaths.FishingConfigRoot}/RodDatabase.asset");
            changed |= AssignIfFound(so, "strengthTrainingGrounds", $"{GameContentPaths.FishingConfigRoot}/StrengthTrainingGroundDatabase.asset");
            changed |= WireSharedCosmetics(so);
            changed |= WireSharedActivity(so);
            changed |= WireSharedEconomy(so);

            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();

            return changed;
        }

        static bool WireWormDodgeScene(GameProductSettings settings)
        {
            var binder = EnsureConfigBinder();
            var so = new SerializedObject(binder);
            so.Update();
            var changed = false;

            changed |= AssignIfFound(so, "wormDodgePickup", GameContentPaths.WormDodgePickupConfigAssetPath);
            changed |= WireSharedCosmetics(so);
            changed |= WireSharedActivity(so);
            changed |= WireSharedEconomy(so);

            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();

            return changed;
        }

        static bool WireDestroyCityScene(GameProductSettings settings)
        {
            var binder = EnsureConfigBinder();
            var so = new SerializedObject(binder);
            so.Update();
            var changed = false;

            changed |= AssignIfFound(so, "destroyCityNukes", $"{GameContentPaths.DestroyCityConfigRoot}/NukeDatabase.asset");
            changed |= AssignIfFound(so, "destroyCityMaps", $"{GameContentPaths.DestroyCityConfigRoot}/DestroyCityMapDatabase.asset");
            changed |= AssignIfFound(so, "destroyCityFuels", $"{GameContentPaths.DestroyCityConfigRoot}/FuelDatabase.asset");
            changed |= AssignIfFound(so, "destroyCityFuelProducers", $"{GameContentPaths.DestroyCityConfigRoot}/FuelProducerDatabase.asset");
            changed |= AssignIfFound(so, "destroyCityBuildings", $"{GameContentPaths.DestroyCityConfigRoot}/DestroyCityBuildingDatabase.asset");
            changed |= WireSharedCosmetics(so);
            changed |= WireSharedActivity(so);
            changed |= WireSharedEconomy(so);

            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();

            return changed;
        }

        static bool WireTowerScene(GameProductSettings settings)
        {
            var binder = EnsureConfigBinder();
            var so = new SerializedObject(binder);
            so.Update();
            var changed = false;

            changed |= AssignIfFound(so, "mapDatabase", $"{TowerActivityConfigRoot}/MapDatabase.asset");
            changed |= AssignIfFound(so, "signInDatabase", $"{TowerActivityConfigRoot}/SignInDatabase.asset");
            changed |= AssignIfFound(so, "wheelDatabase", $"{TowerActivityConfigRoot}/WheelDatabase.asset");
            changed |= AssignIfFound(so, "onlineRewardDatabase", $"{TowerActivityConfigRoot}/OnlineRewardDatabase.asset");
            changed |= WireSharedCosmetics(so);
            changed |= WireSharedActivity(so);
            changed |= WireSharedEconomy(so);

            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();

            return changed;
        }

        static bool WireSharedCosmetics(SerializedObject binderSo)
        {
            var changed = false;
            changed |= AssignIfFound(binderSo, "petDatabase", $"{SharedCosmeticsRoot}/PetDatabase.asset");
            changed |= AssignIfFound(binderSo, "wingDatabase", $"{SharedCosmeticsRoot}/WingDatabase.asset");
            changed |= AssignIfFound(binderSo, "trailDatabase", $"{SharedCosmeticsRoot}/TrailDatabase.asset");
            changed |= AssignIfFound(binderSo, "characterDatabase", $"{SharedCosmeticsRoot}/CharacterDatabase.asset");
            changed |= AssignIfFound(binderSo, "qualityVisualLibrary", $"{SharedCosmeticsRoot}/QualityVisualLibrary.asset");
            return changed;
        }

        static bool WireSharedActivity(SerializedObject binderSo)
        {
            var changed = false;
            changed |= AssignIfFound(binderSo, "signInDatabase", $"{TowerActivityConfigRoot}/SignInDatabase.asset");
            changed |= AssignIfFound(binderSo, "wheelDatabase", $"{TowerActivityConfigRoot}/WheelDatabase.asset");
            changed |= AssignIfFound(binderSo, "onlineRewardDatabase", $"{TowerActivityConfigRoot}/OnlineRewardDatabase.asset");
            changed |= AssignIfFound(binderSo, "signInDatabase", $"{SharedCosmeticsRoot}/Configs/SignInDatabase.asset");
            changed |= AssignIfFound(binderSo, "wheelDatabase", $"{SharedCosmeticsRoot}/Configs/WheelDatabase.asset");
            changed |= AssignIfFound(binderSo, "onlineRewardDatabase", $"{SharedCosmeticsRoot}/Configs/OnlineRewardDatabase.asset");
            return changed;
        }

        static bool WireSharedEconomy(SerializedObject binderSo)
        {
            var changed = false;
            changed |= AssignIfFound(binderSo, "discountOffers", $"{SharedEconomyRoot}/DiscountOfferDatabase.asset");
            changed |= AssignIfFound(binderSo, "diamondCoinsExchange", $"{SharedEconomyRoot}/DiamondCoinsExchangeDatabase.asset");
            return changed;
        }

        static GameModeConfigBinder EnsureConfigBinder()
        {
            var binder = Object.FindObjectOfType<GameModeConfigBinder>(true);
            if (binder != null)
                return binder;

            var host = new GameObject(nameof(GameModeConfigBinder));
            return host.AddComponent<GameModeConfigBinder>();
        }

        static bool IsTowerMapScene(string scenePath) =>
            scenePath.Equals(TowerMap1ScenePath, System.StringComparison.OrdinalIgnoreCase)
            || scenePath.Equals(TowerMap2ScenePath, System.StringComparison.OrdinalIgnoreCase)
            || scenePath.Equals(TowerMap3ScenePath, System.StringComparison.OrdinalIgnoreCase);

        static void EnsureWormDodgePickupConfigAsset()
        {
            var path = GameContentPaths.WormDodgePickupConfigAssetPath;
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                return;

            EnsureAssetFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));

            var config = ScriptableObject.CreateInstance<Game.WormDodge.Pickups.WormDodgePickupConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
        }

        static void EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent))
                EnsureAssetFolder(parent);

            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }

        static bool AssignIfFound(SerializedObject so, string propertyName, string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null)
                return false;

            var prop = so.FindProperty(propertyName);
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
                return false;

            if (prop.objectReferenceValue == asset)
                return false;

            prop.objectReferenceValue = asset;
            return true;
        }

        static List<string> CollectEnabledBuildScenePaths()
        {
            var result = new List<string>();
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled && !string.IsNullOrEmpty(scenes[i].path))
                    result.Add(scenes[i].path);
            }

            return result;
        }
    }
}
#endif

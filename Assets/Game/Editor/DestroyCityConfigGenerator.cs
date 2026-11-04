using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Core.Content;
using Game.DestroyCity;
using Game.DestroyCity.Config;

namespace Game.EditorTools
{
    public static class DestroyCityConfigGenerator
    {
        static readonly string ModuleRoot = GameContentPaths.DestroyCityModuleRoot;

        static readonly (int level, string name, float upgradeXpFromPrev, int radius)[] NukeRows =
        {
            (1, "纸壳核弹", 0f, 7),
            (2, "普通核弹", 200f, 21),
            (3, "虚空核弹", 1000f, 35),
            (4, "熔岩核弹", 5000f, 106),
            (5, "超级核弹", 250000f, 141),
            (6, "虚空超级核弹", 550000f, 177),
            (7, "熔岩超级核弹", 1500000f, 212),
            (8, "火箭核弹", 9000000f, 247),
            (9, "虚空火箭核弹", 12500000f, 283),
            (10, "熔岩火箭核弹", 25000000f, 354),
        };

        static readonly (int level, float xpPerTick, long upgradeCost)[] ManualProducerRows =
        {
            (1, 10f, 200L),
            (2, 40f, 700L),
            (3, 200f, 1600L),
            (4, 600f, 3000L),
            (5, 1500f, 5300L),
            (6, 2800f, 9000L),
            (7, 4200f, 14700L),
            (8, 12000f, 23600L),
            (9, 35000f, 37100L),
            (10, 88000f, 57800L),
            (11, 186000f, 89000L),
            (12, 325000f, 136000L),
            (13, 482000f, 206300L),
            (14, 600000f, 311100L),
            (15, 750000f, 466700L),
            (16, 900000f, 696900L),
            (17, 1075000f, 1036700L),
            (18, 1250000f, 1536700L),
            (19, 1875000f, 2270900L),
            (20, 2500000f, 0L),
        };

        [MenuItem("Game/Generate/Create Destroy City Configs")]
        public static void CreateDestroyCityConfigs()
        {
            EnsureModuleFolders();

            var nukeDb = CreateOrSelectDb<NukeDatabase>($"{ModuleRoot}/NukeDatabase.asset");
            var fuelDb = CreateOrSelectDb<FuelDatabase>($"{ModuleRoot}/FuelDatabase.asset");
            var producerDb = CreateOrSelectDb<FuelProducerDatabase>($"{ModuleRoot}/FuelProducerDatabase.asset");
            var mapDb = CreateOrSelectDb<DestroyCityMapDatabase>($"{ModuleRoot}/DestroyCityMapDatabase.asset");
            var buildingDb = CreateOrSelectDb<DestroyCityBuildingDatabase>($"{ModuleRoot}/DestroyCityBuildingDatabase.asset");

            nukeDb.nukes = BuildNukes();
            fuelDb.fuels = BuildFuels();
            producerDb.producers = BuildProducers();
            mapDb.maps = BuildMaps();
            buildingDb.buildings = BuildBuildings();

            EditorUtility.SetDirty(nukeDb);
            EditorUtility.SetDirty(fuelDb);
            EditorUtility.SetDirty(producerDb);
            EditorUtility.SetDirty(mapDb);
            EditorUtility.SetDirty(buildingDb);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[DestroyCity] 配置已生成到 {ModuleRoot}/（含 Nukes、Fuels、Producers、Maps、Buildings 子目录）。" +
                "若 Resources 根目录仍有旧 Database，可手动删除。");
        }

        static void EnsureModuleFolders()
        {
            GameAssetGenerator.EnsureDirPublic(ModuleRoot);
            GameAssetGenerator.EnsureDirPublic($"{ModuleRoot}/Nukes");
            GameAssetGenerator.EnsureDirPublic($"{ModuleRoot}/Fuels");
            GameAssetGenerator.EnsureDirPublic($"{ModuleRoot}/Producers");
            GameAssetGenerator.EnsureDirPublic($"{ModuleRoot}/Maps");
            GameAssetGenerator.EnsureDirPublic($"{ModuleRoot}/Buildings");
        }

        static T CreateOrSelectDb<T>(string path) where T : ScriptableObject =>
            GameAssetGenerator.CreateOrSelectPublic<T>(path);

        static List<NukeDef> BuildNukes()
        {
            var list = new List<NukeDef>();
            for (var i = 0; i < NukeRows.Length; i++)
            {
                var row = NukeRows[i];
                var path = $"{ModuleRoot}/Nukes/Nuke_Lv{row.level}.asset";
                var nuke = LoadOrCreate<NukeDef>(path);
                nuke.nukeId = $"nuke_{row.level}";
                nuke.displayName = row.name;
                nuke.level = row.level;
                nuke.upgradeXpRequired = row.upgradeXpFromPrev;
                nuke.explosionRadius = row.radius;
                nuke.coinRewardMultiplier = 1f;
                EditorUtility.SetDirty(nuke);
                list.Add(nuke);
            }

            return list;
        }

        static List<FuelDef> BuildFuels()
        {
            var list = new List<FuelDef>();
            for (var lv = 1; lv <= ManualProducerRows.Length; lv++)
            {
                var path = $"{ModuleRoot}/Fuels/Fuel_Lv{lv}.asset";
                var fuel = LoadOrCreate<FuelDef>(path);
                fuel.fuelId = $"fuel_{lv}";
                fuel.displayName = $"燃料 Lv{lv}";
                fuel.level = lv;
                fuel.xpToNextLevel = lv < ManualProducerRows.Length
                    ? ManualProducerRows[lv].xpPerTick * 0.5f
                    : 0f;
                EditorUtility.SetDirty(fuel);
                list.Add(fuel);
            }

            return list;
        }

        static List<FuelProducerDef> BuildProducers()
        {
            var list = new List<FuelProducerDef>();

            var manual = LoadOrCreateProducer(FuelProducerIds.Manual, "手动燃料生产器", true,
                FuelProducerUnlockPolicy.AlwaysOpen, 0);
            manual.levelTable = BuildProducerLevelTable(ManualProducerRows);
            manual.productionCapLevel = ManualProducerRows.Length;
            manual.baseXpPerTick = ManualProducerRows[0].xpPerTick;
            EditorUtility.SetDirty(manual);
            list.Add(manual);

            list.Add(LoadOrCreateProducer(FuelProducerIds.Auto1, "自动燃料生产器 I", false,
                FuelProducerUnlockPolicy.CoinOnce, 5000, autoXpScale: 0.35f));
            list.Add(LoadOrCreateProducer(FuelProducerIds.Auto2, "自动燃料生产器 II", false,
                FuelProducerUnlockPolicy.FreeUnlockOnce, 0, autoXpScale: 0.35f));
            list.Add(LoadOrCreateProducer(FuelProducerIds.Auto3, "自动燃料生产器 III", false,
                FuelProducerUnlockPolicy.GemOnce, 0, gemCost: 100, autoXpScale: 0.35f));

            return list;
        }

        static List<FuelProducerLevelRow> BuildProducerLevelTable(
            (int level, float xpPerTick, long upgradeCost)[] rows)
        {
            var table = new List<FuelProducerLevelRow>(rows.Length);
            for (var i = 0; i < rows.Length; i++)
            {
                table.Add(new FuelProducerLevelRow
                {
                    level = rows[i].level,
                    xpPerTick = rows[i].xpPerTick,
                    upgradeCostToNext = rows[i].upgradeCost,
                });
            }

            return table;
        }

        static FuelProducerDef LoadOrCreateProducer(
            string id,
            string displayName,
            bool manual,
            FuelProducerUnlockPolicy policy,
            long coinCost,
            long gemCost = 0,
            float autoXpScale = 1f)
        {
            var path = $"{ModuleRoot}/Producers/Producer_{id}.asset";
            var def = LoadOrCreate<FuelProducerDef>(path);
            def.producerId = id;
            def.displayName = displayName;
            def.isManual = manual;
            def.autoIntervalSeconds = manual ? 1f : 2f;
            def.unlockPolicy = policy;
            def.unlockCoinCost = coinCost;
            def.unlockGemCost = gemCost;
            def.productionCapLevel = ManualProducerRows.Length;
            def.maxFuelSlots = 4;

            if (manual)
            {
                def.levelTable = BuildProducerLevelTable(ManualProducerRows);
                def.baseXpPerTick = ManualProducerRows[0].xpPerTick;
            }
            else
            {
                def.levelTable = BuildScaledProducerLevelTable(autoXpScale);
                def.baseXpPerTick = ManualProducerRows[0].xpPerTick * autoXpScale;
                def.upgradeCoinCostPerLevel = ManualProducerRows[0].upgradeCost;
            }

            EditorUtility.SetDirty(def);
            return def;
        }

        static List<FuelProducerLevelRow> BuildScaledProducerLevelTable(float scale)
        {
            var table = new List<FuelProducerLevelRow>(ManualProducerRows.Length);
            for (var i = 0; i < ManualProducerRows.Length; i++)
            {
                var row = ManualProducerRows[i];
                table.Add(new FuelProducerLevelRow
                {
                    level = row.level,
                    xpPerTick = row.xpPerTick * scale,
                    upgradeCostToNext = row.upgradeCost,
                });
            }

            return table;
        }

        static List<DestroyCityMapDef> BuildMaps()
        {
            var path = $"{ModuleRoot}/Maps/DestroyCity_Map1.asset";
            var map = LoadOrCreate<DestroyCityMapDef>(path);
            map.mapId = "destroy_city_map_1";
            map.displayName = "城市 Alpha";
            map.mapLevel = 1;
            map.gameplaySceneName = "DestroyCityScene";
            map.cityScaleMeters = 200f;
            map.baseCoinReward = 1000;
            EditorUtility.SetDirty(map);
            return new List<DestroyCityMapDef> { map };
        }

        static List<DestroyCityBuildingDef> BuildBuildings()
        {
            var residential = LoadOrCreateBuilding("Residential", DestroyCityBuildingCategory.Residential, "民居", 200L);
            var office = LoadOrCreateBuilding("Office", DestroyCityBuildingCategory.Office, "写字楼", 4000L);
            var other = LoadOrCreateBuilding("Other", DestroyCityBuildingCategory.Other, "其他建筑", 1000L);
            return new List<DestroyCityBuildingDef> { residential, office, other };
        }

        static DestroyCityBuildingDef LoadOrCreateBuilding(
            string fileName,
            DestroyCityBuildingCategory category,
            string displayName,
            long coinValue)
        {
            var path = $"{ModuleRoot}/Buildings/Building_{fileName}.asset";
            var def = LoadOrCreate<DestroyCityBuildingDef>(path);
            def.category = category;
            def.displayName = displayName;
            def.coinValue = coinValue;
            EditorUtility.SetDirty(def);
            return def;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}

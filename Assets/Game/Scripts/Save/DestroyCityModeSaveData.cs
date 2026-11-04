using System;
using Game.Core.Config;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// 摧毁城市玩法专属存档（仅在 <see cref="Game.Core.GameProductSettings.IsDestroyCityContentEnabled"/> 时读写）。
    /// </summary>
    [Serializable]
    public sealed partial class DestroyCityModeSaveData
    {
        public string currentDestroyCitySceneId = "destroy_city_map_1";

        /// <summary>基础燃料产出率（养成加成基数）；&lt;0 表示未初始化。</summary>
        public float baseFuelRate = -1f;

        /// <summary>（旧版）全局燃料台等级；v8 迁移至各生产器。</summary>
        public int fuelStationLevel = 1;

        /// <summary>（旧版）全局燃料经验池；v8 迁移至 nukeXp。</summary>
        public float fuelXp;

        /// <summary>核弹等级（从 1 起）。</summary>
        public int nukeLevel = 1;

        /// <summary>核弹当前等级内的经验进度（灌注燃料时累加）。</summary>
        public float nukeXp;

        /// <summary>玩家当前拿起的燃料实例 id；空表示未携带。</summary>
        public string carriedFuelInstanceId = "";

        /// <summary>携带中燃料的快照（拿起时从实例列表移除，数据存于此）。</summary>
        public int carriedFuelLevel = 1;

        public float carriedFuelXp;

        public int totalLaunches;
        public int totalCitiesDestroyed;

        /// <summary>融合包体下摧毁城市模式独立经济与养成。</summary>
        public ModeScopedProfile profile = new ModeScopedProfile();

        /// <summary>各燃料生产器独立等级与解锁状态。</summary>
        public System.Collections.Generic.List<FuelProducerSaveEntry> fuelProducers =
            new System.Collections.Generic.List<FuelProducerSaveEntry>();

        /// <summary>生产区内的燃料实例（代表燃料经验）。</summary>
        public System.Collections.Generic.List<FuelInstanceSaveEntry> fuelInstances =
            new System.Collections.Generic.List<FuelInstanceSaveEntry>();

        [Serializable]
        public struct FuelProducerSaveEntry
        {
            public string producerId;
            public int level;
            public bool unlocked;

            /// <summary>该生产器累计燃料经验池；&lt;0 表示尚未从旧实例数据迁移。</summary>
            public float storedFuelXp;

            /// <summary>是否已初始化 storedFuelXp（旧存档首次加载时会从实例求和迁移）。</summary>
            public bool hasStoredFuelXp;
        }

        [Serializable]
        public struct FuelInstanceSaveEntry
        {
            public string instanceId;
            public string producerId;
            public int slotIndex;
            public int level;
            public float xp;
        }

        public void MigrateDefaultsIfNeeded()
        {
            var defaultBase = ConfigRegistry.ResolveDestroyCityDefaultBaseFuelRate();
            if (baseFuelRate < 0f)
                baseFuelRate = defaultBase;
            else if (defaultBase > 0f && baseFuelRate < defaultBase * 0.5f)
                baseFuelRate = defaultBase;

            baseFuelRate = Mathf.Max(0f, baseFuelRate);
            fuelStationLevel = Mathf.Max(1, fuelStationLevel);
            nukeLevel = Mathf.Max(1, nukeLevel);
            fuelXp = Mathf.Max(0f, fuelXp);
            nukeXp = Mathf.Max(0f, nukeXp);
            totalLaunches = Mathf.Max(0, totalLaunches);
            totalCitiesDestroyed = Mathf.Max(0, totalCitiesDestroyed);

            if (string.IsNullOrEmpty(currentDestroyCitySceneId))
                currentDestroyCitySceneId = "destroy_city_map_1";
            if (carriedFuelInstanceId == null)
                carriedFuelInstanceId = "";
            carriedFuelLevel = Mathf.Max(1, carriedFuelLevel);
            carriedFuelXp = Mathf.Max(0f, carriedFuelXp);

            if (fuelProducers == null)
                fuelProducers = new System.Collections.Generic.List<FuelProducerSaveEntry>();
            if (fuelInstances == null)
                fuelInstances = new System.Collections.Generic.List<FuelInstanceSaveEntry>();

            EnsureDiscountOfferSaveInitialized();
        }
    }
}

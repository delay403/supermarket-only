using Game.DestroyCity.Config;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>解析燃料世界预制体（优先 Inspector 覆盖，其次 FuelDatabase）。</summary>
    public static class FuelPrefabResolver
    {
        public static GameObject ResolveWorldPrefab(FuelInstanceView overridePrefab, int fuelLevel)
        {
            if (overridePrefab != null)
                return overridePrefab.gameObject;

            var db = FuelDatabase.ResolveRuntime();
            if (db == null)
                return null;

            return db.ResolveWorldPrefabForLevel(fuelLevel);
        }
    }
}

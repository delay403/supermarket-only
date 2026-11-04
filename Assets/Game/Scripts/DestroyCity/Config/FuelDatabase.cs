using System;
using System.Collections.Generic;
using Game.Core.Config;
using UnityEngine;

namespace Game.DestroyCity.Config
{
    [CreateAssetMenu(menuName = "Game/DestroyCity/FuelDatabase", fileName = "FuelDatabase")]
    public sealed class FuelDatabase : ScriptableObject
    {
        public const int DefaultMaxLevel = 14;

        public List<FuelDef> fuels = new List<FuelDef>();

        public bool TryGet(string id, out FuelDef def)
        {
            def = null;
            if (fuels == null || string.IsNullOrWhiteSpace(id))
                return false;

            for (var i = 0; i < fuels.Count; i++)
            {
                var f = fuels[i];
                if (f != null && f.fuelId == id)
                {
                    def = f;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetByLevel(int level, out FuelDef def)
        {
            def = null;
            if (fuels == null)
                return false;

            for (var i = 0; i < fuels.Count; i++)
            {
                var f = fuels[i];
                if (f != null && f.level == level)
                {
                    def = f;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 按等级取 worldPrefab；无精确匹配时回退到不高于该等级的最近条目，再回退到任意已配置预制体。
        /// </summary>
        public GameObject ResolveWorldPrefabForLevel(int level)
        {
            level = Mathf.Max(1, level);
            if (TryGetByLevel(level, out var exact) && exact != null && exact.worldPrefab != null)
                return exact.worldPrefab;

            GameObject best = null;
            var bestLevel = int.MinValue;
            if (fuels != null)
            {
                for (var i = 0; i < fuels.Count; i++)
                {
                    var f = fuels[i];
                    if (f == null || f.worldPrefab == null)
                        continue;
                    if (f.level <= level && f.level > bestLevel)
                    {
                        bestLevel = f.level;
                        best = f.worldPrefab;
                    }
                }
            }

            if (best != null)
                return best;

            if (fuels == null)
                return null;

            for (var i = fuels.Count - 1; i >= 0; i--)
            {
                var f = fuels[i];
                if (f != null && f.worldPrefab != null)
                    return f.worldPrefab;
            }

            return null;
        }

        public int MaxLevel
        {
            get
            {
                var max = 0;
                if (fuels != null)
                {
                    for (var i = 0; i < fuels.Count; i++)
                    {
                        var f = fuels[i];
                        if (f != null)
                            max = Mathf.Max(max, f.level);
                    }
                }

                return max > 0 ? max : DefaultMaxLevel;
            }
        }

        public static int CountFuelDefs(FuelDatabase db)
        {
            if (db?.fuels == null)
                return 0;

            var count = 0;
            for (var i = 0; i < db.fuels.Count; i++)
            {
                if (db.fuels[i] != null)
                    count++;
            }

            return count;
        }

        /// <summary>运行时解析燃料库（ConfigRegistry → Resources 回退，取条目更多的一份）。</summary>
        public static FuelDatabase ResolveRuntime()
        {
            var registry = ConfigRegistry.DestroyCityFuels;
            var resources = Resources.Load<FuelDatabase>(DestroyCityResourcePaths.FuelDatabase);
            return PickBest(registry, resources);
        }

        static FuelDatabase PickBest(FuelDatabase a, FuelDatabase b)
        {
            var countA = CountFuelDefs(a);
            var countB = CountFuelDefs(b);
            if (countA >= countB && countA > 0)
                return a;
            if (countB > 0)
                return b;
            return a != null ? a : b;
        }

        public float GetXpToNextLevel(int level)
        {
            if (TryGetByLevel(level, out var def) && def != null)
                return Mathf.Max(0f, def.xpToNextLevel);

            var bal = ConfigRegistry.Balance;
            return bal != null ? bal.destroyCityFallbackFuelXpToNext * level : 100f * level;
        }
    }
}

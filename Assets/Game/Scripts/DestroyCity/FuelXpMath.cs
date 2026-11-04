using Game.DestroyCity.Config;
using Game.Save;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>燃料等级 ↔ 经验换算。</summary>
    public static class FuelXpMath
    {
        public static float GetXpToNextLevel(int level)
        {
            var db = FuelDatabase.ResolveRuntime();
            return db != null ? db.GetXpToNextLevel(level) : 100f * level;
        }

        public static int GetMaxLevel()
        {
            var db = FuelDatabase.ResolveRuntime();
            return db != null ? db.MaxLevel : FuelDatabase.DefaultMaxLevel;
        }

        /// <summary>从 1 级升到 level 级所需累计经验（level=1 时为 0）。</summary>
        public static float GetTotalXpForLevel(int level)
        {
            level = Mathf.Max(1, level);
            var sum = 0f;
            for (var l = 1; l < level; l++)
                sum += GetXpToNextLevel(l);
            return sum;
        }

        /// <summary>生产一个 level 级整燃料所需的总经验。</summary>
        public static float GetSpawnCostForLevel(int level)
        {
            level = Mathf.Max(1, level);
            var total = GetTotalXpForLevel(level);
            return total > 0.01f ? total : GetXpToNextLevel(1);
        }

        /// <summary>实例总经验 = 到达当前等级累计 + 本级进度。</summary>
        public static float GetInstanceTotalXp(int level, float xp)
        {
            return GetTotalXpForLevel(level) + Mathf.Max(0f, xp);
        }

        /// <summary>给定总经验，返回可达最高等级及剩余进度 xp。</summary>
        public static int GetMaxLevelForTotalXp(float totalXp, out float remainingXp)
        {
            var maxDb = GetMaxLevel();
            var level = 1;
            var remaining = Mathf.Max(0f, totalXp);

            while (level < maxDb)
            {
                var need = GetXpToNextLevel(level);
                if (remaining + 1e-4f < need)
                    break;
                remaining -= need;
                level++;
            }

            remainingXp = remaining;
            return level;
        }

        /// <summary>合并两份燃料经验为单一实例数据（保留 targetInstanceId）。</summary>
        public static DestroyCityModeSaveData.FuelInstanceSaveEntry MergeInstances(
            DestroyCityModeSaveData.FuelInstanceSaveEntry keep,
            DestroyCityModeSaveData.FuelInstanceSaveEntry absorb)
        {
            var total = GetInstanceTotalXp(keep.level, keep.xp)
                + GetInstanceTotalXp(absorb.level, absorb.xp);
            var level = GetMaxLevelForTotalXp(total, out var xp);
            NormalizeInstance(ref level, ref xp);
            keep.level = level;
            keep.xp = xp;
            return keep;
        }

        public static void NormalizeInstance(ref int level, ref float xp)
        {
            level = Mathf.Max(1, level);
            xp = Mathf.Max(0f, xp);
            var max = GetMaxLevel();
            while (level < max)
            {
                var need = GetXpToNextLevel(level);
                if (xp + 1e-4f < need)
                    break;
                xp -= need;
                level++;
            }

            if (level >= max)
                level = max;
        }
    }
}

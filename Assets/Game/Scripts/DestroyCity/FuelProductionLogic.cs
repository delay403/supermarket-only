using System.Collections.Generic;
using Game.Save;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 根据生产器「燃料总经验」生成单个显示用燃料实例（总经验守恒）。
    /// </summary>
    public static class FuelProductionLogic
    {
        const float Epsilon = 1e-4f;

        public static string BuildStableInstanceId(string producerId) =>
            string.IsNullOrWhiteSpace(producerId) ? "fuel_unknown" : $"fuel_{producerId.Trim()}";

        /// <summary>从总经验生成 0 或 1 个显示条目（不含存档写入）。</summary>
        public static bool TryBuildDisplayEntry(
            float totalXp,
            string producerId,
            int productionCapLevel,
            string existingInstanceId,
            out DestroyCityModeSaveData.FuelInstanceSaveEntry entry)
        {
            entry = default;
            if (totalXp <= Epsilon || string.IsNullOrWhiteSpace(producerId))
                return false;

            productionCapLevel = Mathf.Clamp(productionCapLevel, 1, FuelXpMath.GetMaxLevel());
            totalXp = Mathf.Max(0f, totalXp);

            var level = FuelXpMath.GetMaxLevelForTotalXp(totalXp, out var xp);
            ApplyProductionCap(ref level, ref xp, productionCapLevel);
            FuelXpMath.NormalizeInstance(ref level, ref xp);

            entry = new DestroyCityModeSaveData.FuelInstanceSaveEntry
            {
                instanceId = string.IsNullOrWhiteSpace(existingInstanceId)
                    ? BuildStableInstanceId(producerId)
                    : existingInstanceId,
                producerId = producerId,
                slotIndex = 0,
                level = level,
                xp = xp,
            };
            return true;
        }

        /// <summary>兼容旧调用：始终最多返回 1 个显示实例。</summary>
        public static List<DestroyCityModeSaveData.FuelInstanceSaveEntry> SplitTotalXp(
            float totalXp,
            string producerId,
            int maxSlots,
            int productionCapLevel)
        {
            var result = new List<DestroyCityModeSaveData.FuelInstanceSaveEntry>(1);
            if (TryBuildDisplayEntry(totalXp, producerId, productionCapLevel, null, out var entry))
                result.Add(entry);
            return result;
        }

        static void ApplyProductionCap(ref int level, ref float xp, int productionCapLevel)
        {
            productionCapLevel = Mathf.Clamp(productionCapLevel, 1, FuelXpMath.GetMaxLevel());
            if (level <= productionCapLevel)
                return;

            var total = FuelXpMath.GetInstanceTotalXp(level, xp);
            level = productionCapLevel;
            xp = Mathf.Max(0f, total - FuelXpMath.GetTotalXpForLevel(productionCapLevel));
            FuelXpMath.NormalizeInstance(ref level, ref xp);
        }
    }
}

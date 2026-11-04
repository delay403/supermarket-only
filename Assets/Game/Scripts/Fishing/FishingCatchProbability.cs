using Game.Collectibles.Stats;
using Game.Fishing.Config;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 九格鱼池概率：默认 20/20/15/15/9/9/9/2/1（‰ 200…10）；
    /// 有特殊钓鱼概率加成时使用 15/15/15/15/11/11/11/4/3。
    /// </summary>
    public static class FishingCatchProbability
    {
        static readonly int[] DefaultPermille = { 200, 200, 150, 150, 90, 90, 90, 20, 10 };
        static readonly int[] SpecialPermille = { 150, 150, 150, 150, 110, 110, 110, 40, 30 };

        public static bool UsesSpecialTable(FishingStatsAggregator.FinalFishingStats stats) =>
            StatModifiersUiFormat.HasSpecialCatchBonus(stats.totalSpecialCatchBonus);

        /// <summary>概率公示用：按九格表或配置权重计算单条有效权重（不含玩家养成）。</summary>
        public static int GetDisclosureWeight(
            FishingZoneDef zone,
            int poolIndex,
            bool useSpecialTable,
            int configuredWeight)
        {
            if (zone?.fishPool == null || poolIndex < 0 || poolIndex >= zone.fishPool.Length)
                return 0;

            var table = useSpecialTable ? SpecialPermille : DefaultPermille;
            if (poolIndex >= table.Length)
                return configuredWeight > 0 ? configuredWeight : 0;

            return table[poolIndex];
        }

        public readonly struct DisclosureRow
        {
            public readonly FishDef Fish;
            public readonly float Probability01;
            public readonly int PoolIndex;

            public DisclosureRow(FishDef fish, float probability01, int poolIndex)
            {
                Fish = fish;
                Probability01 = probability01;
                PoolIndex = poolIndex;
            }
        }

        /// <summary>按鱼池条目顺序构建概率公示行（默认九格概率表）。</summary>
        public static System.Collections.Generic.List<DisclosureRow> BuildDisclosureRows(
            FishingZoneDef zone,
            bool useSpecialTable = false)
        {
            var list = new System.Collections.Generic.List<DisclosureRow>();
            if (zone?.fishPool == null || zone.fishPool.Length == 0)
                return list;

            var totalWeight = 0;
            var scratch = new System.Collections.Generic.List<(FishDef fish, int weight, int poolIndex)>();

            for (var i = 0; i < zone.fishPool.Length; i++)
            {
                var entry = zone.fishPool[i];
                if (!FishingZoneFishEntryUtility.IsValidPoolEntry(entry))
                    continue;

                var fish = FishingZoneFishEntryUtility.ResolveFishDef(entry);
                var baseWeight = FishingZoneFishEntryUtility.GetConfiguredWeight(entry);
                var weight = GetDisclosureWeight(zone, i, useSpecialTable, baseWeight);
                if (weight <= 0)
                    continue;

                scratch.Add((fish, weight, i));
                totalWeight += weight;
            }

            if (totalWeight <= 0)
                return list;

            for (var i = 0; i < scratch.Count; i++)
            {
                var row = scratch[i];
                var probability = row.weight / (float)totalWeight;
                list.Add(new DisclosureRow(row.fish, probability, row.poolIndex));
            }

            return list;
        }

        /// <summary>按鱼池条目顺序（最多 9 条）返回有效权重；不足 9 条时仅覆盖前 N 条。</summary>
        public static int GetEffectiveWeight(
            FishingZoneDef zone,
            int poolIndex,
            FishingStatsAggregator.FinalFishingStats stats,
            int configuredWeight)
        {
            if (zone?.fishPool == null || poolIndex < 0 || poolIndex >= zone.fishPool.Length)
                return 0;

            var table = UsesSpecialTable(stats) ? SpecialPermille : DefaultPermille;
            if (poolIndex >= table.Length)
                return configuredWeight > 0 ? configuredWeight : 0;

            return table[poolIndex];
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Game.DestroyCity.Config
{
    [CreateAssetMenu(menuName = "Game/DestroyCity/FuelProducerDef", fileName = "FuelProducerDef")]
    public sealed class FuelProducerDef : ScriptableObject
    {
        public string producerId = "fuel_producer_manual";
        public string displayName = "Manual Fuel Producer";
        public bool isManual = true;

        [Tooltip("自动生产器间隔（秒）；手动生产器忽略。")]
        [Min(0.1f)]
        public float autoIntervalSeconds = 1f;

        public FuelProducerUnlockPolicy unlockPolicy = FuelProducerUnlockPolicy.AlwaysOpen;

        [Tooltip("CoinOnce / WorldAreaSaveKey 等策略使用。")]
        public long unlockCoinCost = 5000;

        [Min(0)]
        public long unlockGemCost = 50;

        [Tooltip("WorldAreaSaveKey 策略：对应 PlayerSaveData.unlockedWorldAreaIds。")]
        public string unlockSaveKey = "";

        [Header("等级曲线（优先于下方公式）")]
        [Tooltip("每级单次产出经验与升级金币；有数据时覆盖 baseXpPerTick / upgradeCoinCostPerLevel 公式。")]
        public List<FuelProducerLevelRow> levelTable = new List<FuelProducerLevelRow>();

        [Tooltip("无 levelTable 时：单次按钮/自动 tick 基础燃料经验。")]
        [Min(0f)]
        public float baseXpPerTick = 10f;

        [Tooltip("生产目标等级上限（燃料可升到的最高等级，受数据库限制）。")]
        [Min(1)]
        public int productionCapLevel = 10;

        [Tooltip("该生产器最多同时显示的燃料实例数（达上限且最高级满经验时可分裂低两级新燃料）。")]
        [Min(1)]
        public int maxFuelSlots = 4;

        [Tooltip("无 levelTable 时：升到下一级生产器等级所需金币基数。")]
        [Min(0)]
        public long upgradeCoinCostPerLevel = 1000;

        [Tooltip("无 levelTable 时：每级产量倍率增量。")]
        [Min(0f)]
        public float xpPerLevelMultiplier = 0.25f;

        public Sprite icon;

        public bool HasLevelTable => levelTable != null && levelTable.Count > 0;

        public int GetMaxProducerLevel()
        {
            if (!HasLevelTable)
                return productionCapLevel;

            var max = 1;
            for (var i = 0; i < levelTable.Count; i++)
            {
                if (levelTable[i].level > max)
                    max = levelTable[i].level;
            }

            return max;
        }

        public bool TryGetLevelRow(int level, out FuelProducerLevelRow row)
        {
            row = default;
            if (!HasLevelTable)
                return false;

            for (var i = 0; i < levelTable.Count; i++)
            {
                if (levelTable[i].level != level)
                    continue;
                row = levelTable[i];
                return true;
            }

            return false;
        }

        public float GetXpPerTickForLevel(int level)
        {
            if (TryGetLevelRow(level, out var row))
                return row.xpPerTick;

            var mul = 1f + (level - 1) * xpPerLevelMultiplier;
            return baseXpPerTick * mul;
        }

        public long GetUpgradeCostForLevel(int level)
        {
            if (TryGetLevelRow(level, out var row))
                return row.upgradeCostToNext;

            return upgradeCoinCostPerLevel * level;
        }
    }
}

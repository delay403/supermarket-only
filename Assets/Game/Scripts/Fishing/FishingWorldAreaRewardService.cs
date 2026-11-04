using Game.Core;
using Game.Core.Config;
using Game.Maps;
using Game.Save;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 按 <see cref="Game.WorldTriggers.CoinUnlockAreaTrigger"/> 分区解锁进度解析金币/力量奖励。
    /// </summary>
    public static class FishingWorldAreaRewardService
    {
        public static int GetUnlockedWorldAreaProgressCount()
        {
            var tiers = ResolveTiers();
            if (tiers == null || tiers.Length == 0 || SaveService.Data == null)
                return 0;

            var count = 0;
            for (var i = 0; i < tiers.Length; i++)
            {
                var key = tiers[i].areaSaveKey;
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (SaveService.Data.HasUnlockedWorldArea(key))
                    count++;
            }

            return count;
        }

        public static long ResolveFreeCoins()
        {
            if (TryResolveTier(out var tier) && tier.freeCoins > 0)
                return tier.freeCoins;

            var balance = ConfigRegistry.Balance;
            var unlockedMaps = MapService.GetUnlockedMapCountInCatalog();
            return balance != null ? balance.ResolveRewardFreeCoins(unlockedMaps) : 0L;
        }

        public static float ResolveStrengthBonus()
        {
            if (TryResolveTier(out var tier) && tier.strengthBonus > 0f)
                return tier.strengthBonus;

            return ConfigRegistry.ResolveFishingFreeStrengthBonus();
        }

        static bool TryResolveTier(out FishingWorldAreaRewardTier tier)
        {
            tier = default;
            var tiers = ResolveTiers();
            if (tiers == null || tiers.Length == 0)
                return false;

            var progress = GetUnlockedWorldAreaProgressCount();
            var index = Mathf.Clamp(progress, 0, tiers.Length - 1);
            tier = tiers[index];
            return true;
        }

        static FishingWorldAreaRewardTier[] ResolveTiers() =>
            ConfigRegistry.Balance != null ? ConfigRegistry.Balance.fishingWorldAreaRewardTiers : null;
    }
}

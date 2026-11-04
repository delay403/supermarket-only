using System;
using Game.Core.Config;
using Game.Collectibles.Stats;
using Game.Economy;
using Game.Fishing.Config;
using Game.Save;
using UnityEngine;

namespace Game.Fishing
{
    public static class FishingService
    {
        public struct ReelResult
        {
            public float progressDelta;
            public float progressCurrent;
            public float progressMax;
            public bool completed;
        }

        public struct CatchResult
        {
            public bool success;
            public FishDef fish;
            public long coinsAwarded;
            /// <summary>本次钓获前该鱼是否未解锁（图鉴未发现）。</summary>
            public bool wasFirstDiscovery;
        }

        public static float GetProgressMax(FishingZoneDef zone)
        {
            if (zone == null) return 100f;
            return Mathf.Max(1f, zone.progressMax);
        }

        public static float ComputeReelProgressDelta(FishingStatsAggregator.FinalFishingStats stats)
        {
            var bal = ConfigRegistry.Balance;
            var scale = bal != null && bal.fishingReelProgressScale > 0f
                ? bal.fishingReelProgressScale
                : 1f;

            var power = stats.totalPower;
            var add = stats.totalProgressAdd;
            return Mathf.Max(0f, (power + add) * scale);
        }

        public static ReelResult ApplyReel(FishingZoneDef zone, float currentProgress, FishingStatsAggregator.FinalFishingStats stats)
        {
            var max = GetProgressMax(zone);
            var delta = ComputeReelProgressDelta(stats);
            var next = Mathf.Min(max, currentProgress + delta);
            return new ReelResult
            {
                progressDelta = delta,
                progressCurrent = next,
                progressMax = max,
                completed = next >= max - 0.0001f,
            };
        }

        public static CatchResult RollCatch(
            FishingZoneDef zone,
            FishingStatsAggregator.FinalFishingStats stats,
            int catchCoinMultiplier = 1)
        {
            var fail = new CatchResult { success = false };
            if (zone == null || zone.fishPool == null || zone.fishPool.Length == 0)
                return fail;

            var totalWeight = 0;
            var validCount = 0;
            for (var i = 0; i < zone.fishPool.Length; i++)
            {
                var e = zone.fishPool[i];
                if (!FishingZoneFishEntryUtility.IsValidPoolEntry(e))
                    continue;

                var baseW = FishingZoneFishEntryUtility.GetConfiguredWeight(e);
                var w = FishingCatchProbability.GetEffectiveWeight(zone, i, stats, baseW);
                if (w <= 0) continue;
                totalWeight += w;
                validCount++;
            }

            if (totalWeight <= 0 || validCount == 0)
                return fail;

            var roll = UnityEngine.Random.Range(0, totalWeight);
            var acc = 0;
            FishDef picked = null;
            for (var i = 0; i < zone.fishPool.Length; i++)
            {
                var e = zone.fishPool[i];
                var fish = FishingZoneFishEntryUtility.ResolveFishDef(e);
                if (fish == null)
                    continue;

                var baseW = FishingZoneFishEntryUtility.GetConfiguredWeight(e);
                var w = FishingCatchProbability.GetEffectiveWeight(zone, i, stats, baseW);
                if (w <= 0) continue;
                acc += w;
                if (roll < acc)
                {
                    picked = fish;
                    break;
                }
            }

            if (picked == null)
                return fail;

            var coinMul = Mathf.Max(0f, 1f + stats.totalCoinMul);
            var sessionMul = Mathf.Max(1, catchCoinMultiplier);
            var coins = (long)Mathf.Floor(picked.rewardCoins * coinMul * sessionMul);
            if (coins < 0) coins = 0;

            var wasFirstDiscovery = !FishCodexService.IsDiscovered(picked.fishId);

            if (SaveService.Data != null)
            {
                SaveService.Data.coins += coins;
                FishCodexService.RecordCatch(picked);
                SaveService.Save();
                EconomyDisplayEvents.Raise();
            }
            else
                FishCodexService.RecordCatch(picked);

            return new CatchResult
            {
                success = true,
                fish = picked,
                coinsAwarded = coins,
                wasFirstDiscovery = wasFirstDiscovery,
            };
        }
    }
}

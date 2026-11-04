using Game.Core;
using Game.Core.Config;
using Game.Fishing;
using Game.Maps;
using Game.Save;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>
    /// 按当前玩法模块与对应进度解析免费金币数量。
    /// </summary>
    public static class ModeRewardCoinResolver
    {
        public static long ResolveForActiveModule()
        {
            return ResolveForModule(ResolveTargetModule());
        }

        public static long ResolveForModule(GameplayModule module) => module switch
        {
            GameplayModule.Fishing => FishingWorldAreaRewardService.ResolveFreeCoins(),
            GameplayModule.DestroyCity => ResolveDestroyCityFreeCoins(),
            GameplayModule.WormDodge => ResolveWormDodgeFreeCoins(),
            GameplayModule.Tower => ResolveTowerFreeCoins(),
            _ => ResolveTowerFreeCoins(),
        };

        public static GameplayModule ResolveTargetModule()
        {
            if (GameplayModuleContext.IsFusionProduct())
            {
                var active = GameplayModuleContext.ActiveModule;
                if (active != GameplayModule.None)
                    return active;
            }

            if (GameProductSettings.IsDestroyCityContentEnabled)
                return GameplayModule.DestroyCity;
            if (GameProductSettings.IsFishingContentEnabled)
                return GameplayModule.Fishing;
            if (GameProductSettings.IsWormDodgeContentEnabled)
                return GameplayModule.WormDodge;

            return GameplayModule.Tower;
        }

        static long ResolveDestroyCityFreeCoins()
        {
            var balance = ConfigRegistry.Balance;
            return ResolveFromProgressTiers(
                GetDestroyCityProgress(),
                balance != null ? balance.destroyCityRewardCoinTiers : null,
                ResolveTowerFreeCoins());
        }

        static long ResolveWormDodgeFreeCoins()
        {
            var balance = ConfigRegistry.Balance;
            return ResolveFromProgressTiers(
                GetWormDodgeProgress(),
                balance != null ? balance.wormDodgeRewardCoinTiers : null,
                ResolveTowerFreeCoins());
        }

        static long ResolveTowerFreeCoins()
        {
            var balance = ConfigRegistry.Balance;
            var unlocked = MapService.GetUnlockedMapCountInCatalog();
            return balance != null ? balance.ResolveRewardFreeCoins(unlocked) : 5000L;
        }

        static long ResolveFromProgressTiers(int progress, ProgressRewardCoinTier[] tiers, long fallback)
        {
            if (tiers == null || tiers.Length == 0)
                return fallback;

            var coins = tiers[0].freeCoins > 0 ? tiers[0].freeCoins : fallback;
            for (var i = 0; i < tiers.Length; i++)
            {
                var tier = tiers[i];
                if (progress >= tier.minProgress && tier.freeCoins > 0)
                    coins = tier.freeCoins;
            }

            return coins > 0 ? coins : fallback;
        }

        static int GetDestroyCityProgress()
        {
            var destroyCity = SaveService.Data?.destroyCity;
            if (destroyCity == null)
                return 0;

            var unlockedProducers = 0;
            if (destroyCity.fuelProducers != null)
            {
                for (var i = 0; i < destroyCity.fuelProducers.Count; i++)
                {
                    if (destroyCity.fuelProducers[i].unlocked)
                        unlockedProducers++;
                }
            }

            var nukeProgress = Mathf.Max(0, destroyCity.nukeLevel - 1);
            return Mathf.Max(unlockedProducers, nukeProgress, destroyCity.totalCitiesDestroyed);
        }

        static int GetWormDodgeProgress() =>
            SaveService.Data?.wormDodge?.bestFloorsReached ?? 0;
    }
}

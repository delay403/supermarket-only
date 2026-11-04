using Game.Core.Config;
using Game.Economy.Config;
using Game.Save;
using UnityEngine;

namespace Game.Economy
{
    public static class DiamondCoinsExchangeService
    {
        public static DiamondCoinsExchangeDatabase Database =>
            ConfigRegistry.DiamondCoinsExchange ??
            Resources.Load<DiamondCoinsExchangeDatabase>("DiamondCoinsExchangeDatabase");

        public static long ResolveBaseCoins() => FreeCoinsRewardService.ResolveFreeCoins();

        public static long ResolveCoinsForTier(DiamondCoinsExchangeTierDef tier)
        {
            var baseCoins = ResolveBaseCoins();
            var mul = tier.freeCoinMultiplier > 0f ? tier.freeCoinMultiplier : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(baseCoins * mul));
        }

        public static bool TryExchangeTier(int tierId, out long coinsGranted, out bool insufficientGems)
        {
            coinsGranted = 0;
            insufficientGems = false;
            var db = Database;
            if (db == null || !db.TryGet(tierId, out var tier))
                return false;

            var data = SaveService.Data;
            if (data == null)
                return false;

            if (data.gems < tier.gemCost)
            {
                insufficientGems = true;
                return false;
            }

            coinsGranted = ResolveCoinsForTier(tier);
            if (coinsGranted <= 0)
                return false;

            data.gems -= tier.gemCost;
            data.coins += coinsGranted;
            SaveService.Save();
            return true;
        }
    }
}

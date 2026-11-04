using Game.Core;
using Game.Core.Config;
using Game.Economy;
using Game.Save;
using UnityEngine;

namespace Game.Collectibles.Pets
{
    public static class PetSellService
    {
        public static bool CanSellInCurrentProduct() => GameProductSettings.IsFishingContentEnabled;

        public static bool IsInstanceSellable(string petInstanceId)
        {
            if (!CanSellInCurrentProduct() || SaveService.Data == null)
                return false;

            if (string.IsNullOrWhiteSpace(petInstanceId))
                return false;

            if (PetService.IsInstanceEquipped(petInstanceId))
                return false;

            return PetService.TryResolveInstanceToPetId(petInstanceId, out _);
        }

        public static bool TryGetSellPrice(string petInstanceId, out long priceCoins)
        {
            priceCoins = 0;
            if (!IsInstanceSellable(petInstanceId))
                return false;

            if (!PetService.TryResolveInstanceToPetId(petInstanceId, out var petId))
                return false;

            var db = ConfigRegistry.Pets;
            if (db == null || !db.TryGet(petId, out var def) || def == null)
                return false;

            priceCoins = ResolveSellPrice(def);
            return priceCoins > 0;
        }

        public static bool TrySell(string petInstanceId)
        {
            if (!TryGetSellPrice(petInstanceId, out var price))
                return false;

            if (!PetService.TryRemoveInstance(petInstanceId))
                return false;

            SaveService.Data.coins += price;
            SaveService.Save();
            EconomyDisplayEvents.Raise();
            return true;
        }

        static long ResolveSellPrice(PetDef def)
        {
            if (def.fishingSellPriceCoins > 0)
                return def.fishingSellPriceCoins;

            if (def.modifiers.coinsAdd > 0.001f)
                return Mathf.Max(1, Mathf.RoundToInt(def.modifiers.coinsAdd));

            return Mathf.Max(1, def.hatchCostCoins);
        }
    }
}

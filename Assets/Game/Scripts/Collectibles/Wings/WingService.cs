using System;
using System.Collections.Generic;
using Game.Economy;
using Game.Save;
using Game.Core.Config;
using Game.Audio;

namespace Game.Collectibles.Wings
{
    public static class WingService
    {
        public static bool IsOwned(string wingId)
        {
            if (SaveService.Data == null) return false;
            return SaveService.Data.HasWing(wingId ?? string.Empty);
        }

        public static void Equip(string wingId)
        {
            if (SaveService.Data == null) return;
            if (!string.IsNullOrWhiteSpace(wingId) && !IsOwned(wingId)) return;
            SaveService.Data.equippedWingId = wingId;
        }

        public static bool TryBuy(string wingId)
        {
            if (SaveService.Data == null) return false;
            var db = ConfigRegistry.Wings;
            if (db == null || !db.TryGet(wingId, out var def) || def == null) return false;
            if (IsOwned(wingId)) return true;

            if (!IsUnlockedForPurchase(def)) return false;
            if (SaveService.Data.coins < def.priceCoins) return false;

            SaveService.Data.coins -= def.priceCoins;
            SaveService.Data.AddWing(wingId);
            AudioGameplayFeedback.PlayResourceDeduct();
            DiscountOfferService.NotifyFirstWingPurchased();
            return true;
        }

        public static bool IsUnlockedForPurchase(WingDef def)
        {
            if (def == null) return false;
            if (def.requiredUnlockedLevel <= 0) return true;

            var db = ConfigRegistry.Wings;
            if (db == null || SaveService.Data?.ownedWingIds == null) return false;

            foreach (var ownedId in SaveService.Data.ownedWingIds)
            {
                if (!db.TryGet(ownedId, out var owned) || owned == null)
                    continue;
                if (owned.level == def.requiredUnlockedLevel)
                    return true;
            }

            return false;
        }

        public static bool CanUse(string wingId)
        {
            return IsOwned(wingId);
        }

        public static List<WingDef> GetAllSorted()
        {
            var list = new List<WingDef>();
            var db = ConfigRegistry.Wings;
            if (db == null) return list;
            list.AddRange(db.wings);
            list.Sort((a, b) =>
            {
                var la = a != null ? a.level : -1;
                var lb = b != null ? b.level : -1;
                var cmp = la.CompareTo(lb);
                if (cmp != 0) return cmp;
                return string.Compare(a != null ? a.wingId : "", b != null ? b.wingId : "", StringComparison.Ordinal);
            });
            return list;
        }
    }
}


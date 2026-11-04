using System;
using System.Collections.Generic;
using Game.Economy;
using Game.Save;
using Game.Core.Config;
using Game.Audio;

namespace Game.Collectibles.Rods
{
    public static class RodService
    {
        public static bool IsOwned(string rodId)
        {
            if (SaveService.Data == null) return false;
            return SaveService.Data.HasRod(rodId ?? string.Empty);
        }

        public static void Equip(string rodId)
        {
            if (SaveService.Data == null) return;
            if (!string.IsNullOrWhiteSpace(rodId) && !IsOwned(rodId)) return;
            SaveService.Data.equippedRodId = rodId;
            SaveService.Save();
        }

        public static bool TryBuy(string rodId)
        {
            if (SaveService.Data == null) return false;
            var db = ConfigRegistry.Rods;
            if (db == null || !db.TryGet(rodId, out var def) || def == null) return false;
            if (IsOwned(rodId)) return true;

            if (!IsUnlockedForPurchase(def)) return false;
            if (SaveService.Data.coins < def.priceCoins) return false;

            SaveService.Data.coins -= def.priceCoins;
            SaveService.Data.AddRod(rodId);
            AudioGameplayFeedback.PlayResourceDeduct();
            SaveService.Save();
            Game.Economy.EconomyDisplayEvents.Raise();
            // 首次商店购买（非 RodStarterGrant 赠送）；RecordMilestoneOnce 保证只触发一次。
            DiscountOfferService.NotifyFirstRodPurchased();
            return true;
        }

        public static bool IsUnlockedForPurchase(RodDef def)
        {
            if (def == null) return false;
            if (def.requiredUnlockedLevel <= 0) return true;

            var db = ConfigRegistry.Rods;
            if (db == null || SaveService.Data?.ownedRodIds == null) return false;

            foreach (var ownedId in SaveService.Data.ownedRodIds)
            {
                if (!db.TryGet(ownedId, out var owned) || owned == null)
                    continue;
                if (owned.level == def.requiredUnlockedLevel)
                    return true;
            }

            return false;
        }

        public static bool CanUse(string rodId)
        {
            return IsOwned(rodId);
        }

        public static List<RodDef> GetAllSorted()
        {
            var list = new List<RodDef>();
            var db = ConfigRegistry.Rods;
            if (db == null) return list;
            list.AddRange(db.rods);
            list.Sort((a, b) =>
            {
                var la = a != null ? a.level : -1;
                var lb = b != null ? b.level : -1;
                var cmp = la.CompareTo(lb);
                if (cmp != 0) return cmp;
                return string.Compare(a != null ? a.rodId : "", b != null ? b.rodId : "", StringComparison.Ordinal);
            });
            return list;
        }
    }
}

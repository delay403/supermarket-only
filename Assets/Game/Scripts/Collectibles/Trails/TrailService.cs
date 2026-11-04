using System;
using System.Collections.Generic;
using Game.Economy;
using Game.Save;
using Game.Core.Config;
using Game.Audio;

namespace Game.Collectibles.Trails
{
    public static class TrailService
    {
        public static bool IsOwned(string trailId)
        {
            if (SaveService.Data == null) return false;
            return SaveService.Data.HasTrail(trailId ?? string.Empty);
        }

        public static void Equip(string trailId)
        {
            if (SaveService.Data == null) return;
            if (!string.IsNullOrWhiteSpace(trailId) && !IsOwned(trailId)) return;
            SaveService.Data.equippedTrailId = trailId;
        }

        public static bool TryBuy(string trailId)
        {
            if (SaveService.Data == null) return false;
            var db = ConfigRegistry.Trails;
            if (db == null || !db.TryGet(trailId, out var def) || def == null) return false;
            if (IsOwned(trailId)) return true;

            if (!IsUnlockedForPurchase(def)) return false;
            if (SaveService.Data.coins < def.priceCoins) return false;

            SaveService.Data.coins -= def.priceCoins;
            SaveService.Data.AddTrail(trailId);
            AudioGameplayFeedback.PlayResourceDeduct();
            DiscountOfferService.NotifyFirstTrailPurchased();
            return true;
        }

        public static bool IsUnlockedForPurchase(TrailDef def)
        {
            if (def == null) return false;
            if (def.requiredUnlockedLevel <= 0) return true;

            var db = ConfigRegistry.Trails;
            if (db == null || SaveService.Data?.ownedTrailIds == null) return false;

            foreach (var ownedId in SaveService.Data.ownedTrailIds)
            {
                if (!db.TryGet(ownedId, out var owned) || owned == null)
                    continue;
                if (owned.level == def.requiredUnlockedLevel)
                    return true;
            }

            return false;
        }

        public static bool CanUse(string trailId)
        {
            return IsOwned(trailId);
        }

        public static List<TrailDef> GetAllSorted()
        {
            var list = new List<TrailDef>();
            var db = ConfigRegistry.Trails;
            if (db == null) return list;
            list.AddRange(db.trails);
            list.Sort((a, b) =>
            {
                var la = a != null ? a.level : -1;
                var lb = b != null ? b.level : -1;
                var cmp = la.CompareTo(lb);
                if (cmp != 0) return cmp;
                return string.Compare(a != null ? a.trailId : "", b != null ? b.trailId : "", StringComparison.Ordinal);
            });
            return list;
        }
    }
}


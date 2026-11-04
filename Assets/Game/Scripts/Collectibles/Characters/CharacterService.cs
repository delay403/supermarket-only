using System;
using System.Collections.Generic;
using Game.Core.Config;
using Game.Save;
using UnityEngine;
using Game.Audio;

namespace Game.Collectibles.Characters
{
    public static class CharacterService
    {
        public static bool IsOwned(string characterId)
        {
            return SaveService.Data != null && SaveService.Data.HasCharacter(characterId ?? string.Empty);
        }

        /// <summary>养成星级（1～3）；未拥有时返回 1。</summary>
        public static int GetGrowthStars(string characterId)
        {
            if (SaveService.Data == null ||
                !SaveService.Data.TryGetOwnedCharacter(characterId ?? string.Empty, out var e))
                return 1;
            var cap = GrowthCapFor(characterId);
            var g = e.growthStars < 1 ? 1 : e.growthStars;
            return MathfClampStars(g, cap);
        }

        /// <summary>卡片与详情展示用：已拥有则用存档星级，否则用配置 catalogStars。</summary>
        public static int GetDisplayStars(string characterId, CharacterDef def)
        {
            if (def == null) return 1;
            if (SaveService.Data != null &&
                SaveService.Data.TryGetOwnedCharacter(characterId ?? string.Empty, out var e))
                return MathfClampStars(e.growthStars, Mathf.Min(def.growthStarsCap, 3));
            return MathfClampStars(def.catalogStars, 3);
        }

        /// <summary>升一星（不超配置上限）；用于后续商店/素材消耗接线。</summary>
        public static bool TryPromoteGrowthStar(string characterId)
        {
            if (SaveService.Data == null) return false;
            var id = characterId ?? string.Empty;
            if (!SaveService.Data.TryGetOwnedCharacter(id, out var e)) return false;
            var db = ConfigRegistry.Characters;
            if (db == null || !db.TryGet(id, out var def) || def == null) return false;
            var cap = Mathf.Min(def.growthStarsCap, 3);
            if (e.growthStars >= cap) return false;
            e.growthStars = MathfClampStars(e.growthStars + 1, cap);
            SaveService.Data.UpsertOwnedCharacter(id, e.growthStars);
            return true;
        }

        static int GrowthCapFor(string characterId)
        {
            var db = ConfigRegistry.Characters;
            if (db == null || !db.TryGet(characterId ?? string.Empty, out var def) || def == null)
                return 3;
            return Mathf.Min(def.growthStarsCap, 3);
        }

        static int MathfClampStars(int v, int cap)
        {
            if (cap < 1) cap = 1;
            if (cap > 3) cap = 3;
            if (v < 1) return 1;
            if (v > cap) return cap;
            return v;
        }

        public static void Equip(string characterId)
        {
            if (SaveService.Data == null) return;
            if (!string.IsNullOrWhiteSpace(characterId) && !IsOwned(characterId)) return;
            SaveService.Data.equippedCharacterId = characterId ?? string.Empty;
        }

        public static bool TryBuy(string characterId)
        {
            if (SaveService.Data == null) return false;
            var db = ConfigRegistry.Characters;
            if (db == null || !db.TryGet(characterId, out var def) || def == null) return false;
            if (IsOwned(characterId)) return true;

            if (!IsUnlockedForPurchase(def)) return false;
            switch (def.priceCurrency)
            {
                case CharacterCurrencyType.Coins:
                    if (SaveService.Data.coins < def.priceAmount) return false;
                    SaveService.Data.coins -= def.priceAmount;
                    break;
                case CharacterCurrencyType.Gems:
                    if (SaveService.Data.gems < def.priceAmount) return false;
                    SaveService.Data.gems -= def.priceAmount;
                    break;
                default:
                    return false;
            }

            AudioGameplayFeedback.PlayResourceDeduct();

            var initialStars = MathfClampStars(def.catalogStars, Mathf.Min(def.growthStarsCap, 3));
            SaveService.Data.UpsertOwnedCharacter(characterId, initialStars);
            return true;
        }

        public static bool IsUnlockedForPurchase(CharacterDef def)
        {
            if (def == null) return false;
            if (def.requiredUnlockedLevel <= 0) return true;

            var db = ConfigRegistry.Characters;
            if (db == null || SaveService.Data == null) return false;

            foreach (var ownedId in SaveService.Data.EnumerateOwnedCharacterIds())
            {
                if (db.TryGet(ownedId, out var owned) && owned != null && owned.level >= def.requiredUnlockedLevel)
                    return true;
            }

            return false;
        }

        public static List<CharacterDef> GetAllSorted()
        {
            var list = new List<CharacterDef>();
            var db = ConfigRegistry.Characters;
            if (db == null) return list;
            list.AddRange(db.characters);
            list.Sort((a, b) =>
            {
                var la = a != null ? a.level : -1;
                var lb = b != null ? b.level : -1;
                var cmp = la.CompareTo(lb);
                if (cmp != 0) return cmp;
                return string.Compare(a != null ? a.characterId : "", b != null ? b.characterId : "",
                    StringComparison.Ordinal);
            });
            return list;
        }
    }
}

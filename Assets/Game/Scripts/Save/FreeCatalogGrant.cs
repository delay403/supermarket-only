using Game.Collectibles.Characters;
using Game.Collectibles.Rods;
using Game.Collectibles.Trails;
using Game.Collectibles.Wings;
using Game.Core.Config;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// 将配置表中价格为 0 的角色 / 翅膀 / 拖尾 / 钓竿默认解锁；
    /// 仅当该类存在已拥有（0 价）物品时才自动装备。无 0 价物品则不赠送、不强制装备。
    /// 在 <see cref="SaveService.LoadOrCreate"/> 与融合包体切模式后调用。
    /// </summary>
    public static class FreeCatalogGrant
    {
        public static void EnsureFreeItemsOwnedAndEquipped(PlayerSaveData data)
        {
            if (data == null)
                return;

            GrantFreeCharacters(data);
            GrantFreeWings(data);
            GrantFreeTrails(data);
            GrantFreeRods(data);

            EnsureCharacterEquipped(data);
            EnsureWingEquipped(data);
            EnsureTrailEquipped(data);
            EnsureRodEquipped(data);
        }

        static void GrantFreeCharacters(PlayerSaveData data)
        {
            var db = ConfigRegistry.Characters;
            if (db?.characters == null)
                return;

            for (var i = 0; i < db.characters.Count; i++)
            {
                var def = db.characters[i];
                if (def == null || string.IsNullOrWhiteSpace(def.characterId))
                    continue;
                if (def.priceAmount > 0)
                    continue;

                var id = def.characterId.Trim();
                if (data.HasCharacter(id))
                    continue;

                data.UpsertOwnedCharacter(id, InitialGrowthStars(def));
            }
        }

        static void GrantFreeWings(PlayerSaveData data)
        {
            var db = ConfigRegistry.Wings;
            if (db?.wings == null)
                return;

            for (var i = 0; i < db.wings.Count; i++)
            {
                var def = db.wings[i];
                if (def == null || string.IsNullOrWhiteSpace(def.wingId))
                    continue;
                if (def.priceCoins > 0)
                    continue;

                data.AddWing(def.wingId.Trim());
            }
        }

        static void GrantFreeTrails(PlayerSaveData data)
        {
            var db = ConfigRegistry.Trails;
            if (db?.trails == null)
                return;

            for (var i = 0; i < db.trails.Count; i++)
            {
                var def = db.trails[i];
                if (def == null || string.IsNullOrWhiteSpace(def.trailId))
                    continue;
                if (def.priceCoins > 0)
                    continue;

                data.AddTrail(def.trailId.Trim());
            }
        }

        static void GrantFreeRods(PlayerSaveData data)
        {
            var db = ConfigRegistry.Rods;
            if (db?.rods == null)
                return;

            for (var i = 0; i < db.rods.Count; i++)
            {
                var def = db.rods[i];
                if (def == null || string.IsNullOrWhiteSpace(def.rodId))
                    continue;
                if (def.priceCoins > 0)
                    continue;

                data.AddRod(def.rodId.Trim());
            }
        }

        static void EnsureCharacterEquipped(PlayerSaveData data)
        {
            // 仅当已拥有免费角色时才自动装备；该类无 0 价物品则不强行装备。
            if (!string.IsNullOrWhiteSpace(data.equippedCharacterId) && data.HasCharacter(data.equippedCharacterId))
                return;

            if (data.ownedCharacters == null || data.ownedCharacters.Count == 0)
            {
                data.equippedCharacterId = string.Empty;
                return;
            }

            var sorted = CharacterService.GetAllSorted();
            data.equippedCharacterId = PickFirstOwnedId(sorted, d => d?.characterId, data.HasCharacter)
                                       ?? FirstOwnedCharacterFallback(data);
        }

        static void EnsureWingEquipped(PlayerSaveData data)
        {
            // 仅当已拥有（通常来自 0 价赠送）且当前装备无效时才装备；无拥有则保持空。
            if (!string.IsNullOrWhiteSpace(data.equippedWingId) && data.HasWing(data.equippedWingId))
                return;

            if (data.ownedWingIds == null || data.ownedWingIds.Count == 0)
            {
                data.equippedWingId = string.Empty;
                return;
            }

            var sorted = WingService.GetAllSorted();
            var id = PickFirstOwnedId(sorted, d => d?.wingId, data.HasWing);
            data.equippedWingId = id ?? string.Empty;
        }

        static void EnsureTrailEquipped(PlayerSaveData data)
        {
            if (!string.IsNullOrWhiteSpace(data.equippedTrailId) && data.HasTrail(data.equippedTrailId))
                return;

            if (data.ownedTrailIds == null || data.ownedTrailIds.Count == 0)
            {
                data.equippedTrailId = string.Empty;
                return;
            }

            var sorted = TrailService.GetAllSorted();
            var id = PickFirstOwnedId(sorted, d => d?.trailId, data.HasTrail);
            data.equippedTrailId = id ?? string.Empty;
        }

        static void EnsureRodEquipped(PlayerSaveData data)
        {
            if (!string.IsNullOrWhiteSpace(data.equippedRodId) && data.HasRod(data.equippedRodId))
                return;

            if (data.ownedRodIds == null || data.ownedRodIds.Count == 0)
            {
                data.equippedRodId = string.Empty;
                return;
            }

            var sorted = RodService.GetAllSorted();
            var id = PickFirstOwnedId(sorted, d => d?.rodId, data.HasRod);
            data.equippedRodId = id ?? string.Empty;
        }

        static string PickFirstOwnedId<T>(
            System.Collections.Generic.List<T> sorted,
            System.Func<T, string> getId,
            System.Func<string, bool> isOwned)
        {
            if (sorted == null)
                return null;

            for (var i = 0; i < sorted.Count; i++)
            {
                var id = getId(sorted[i]);
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                id = id.Trim();
                if (isOwned(id))
                    return id;
            }

            return null;
        }

        static string FirstOwnedCharacterFallback(PlayerSaveData data)
        {
            foreach (var id in data.EnumerateOwnedCharacterIds())
            {
                if (!string.IsNullOrWhiteSpace(id))
                    return id;
            }

            return string.Empty;
        }

        static int InitialGrowthStars(CharacterDef def)
        {
            if (def == null)
                return 1;
            var cap = Mathf.Min(def.growthStarsCap, 3);
            if (cap < 1)
                cap = 1;
            var cat = Mathf.Clamp(def.catalogStars, 1, 3);
            return Mathf.Min(cat, cap);
        }
    }
}

using System;
using Game.Core.Config;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// 新存档无任何宠物实例时，赠送并装备一只库中最低阶宠物。
    /// </summary>
    public static class PetStarterGrant
    {
        public static void TryGrantStarterPetIfInventoryEmpty(PlayerSaveData data)
        {
            if (data == null) return;
            data.MigrateInventoryToPetInstancesIfNeeded();

            if (data.petInstances != null && data.petInstances.Count > 0)
                return;

            var db = ConfigRegistry.Pets;
            var petId = db != null ? db.GetLowestTierPetIdOrDefault("pet_1") : "pet_1";
            if (string.IsNullOrWhiteSpace(petId))
                return;

            if (data.petInstances == null)
                data.petInstances = new System.Collections.Generic.List<PlayerSaveData.PetInstanceEntry>();

            var instanceId = Guid.NewGuid().ToString("D");
            data.petInstances.Add(new PlayerSaveData.PetInstanceEntry
            {
                instanceId = instanceId,
                petId = petId.Trim(),
            });

            data.EnsurePetLoadoutMigrated();
            if (data.equippedPetIds != null && data.equippedPetIds.Count > 0 &&
                data.petUnlockedEquipSlots >= 1 &&
                string.IsNullOrWhiteSpace(data.equippedPetIds[0]))
                data.equippedPetIds[0] = instanceId;

            data.SyncLegacyEquippedPetId();
        }
    }
}

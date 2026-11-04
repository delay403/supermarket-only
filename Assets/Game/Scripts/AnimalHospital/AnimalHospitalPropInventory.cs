using System;
using Game.AnimalHospital.Config;
using Game.Save;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>道具持有与攻击/灭火。</summary>
    public sealed class AnimalHospitalPropInventory
    {
        public event Action<AnimalHospitalPropId> HeldPropChanged;

        public AnimalHospitalPropId HeldProp { get; private set; } = AnimalHospitalPropId.None;

        public bool HasExtinguisherOrFreeze =>
            HeldProp == AnimalHospitalPropId.FireExtinguisher || HeldProp == AnimalHospitalPropId.FreezeGun;

        public bool HasFreezeGun => HeldProp == AnimalHospitalPropId.FreezeGun;

        public bool CanUnlock(AnimalHospitalPropId propId, AnimalHospitalConfig config, AnimalHospitalModeSaveData save)
        {
            if (save != null && save.IsPropUnlocked(propId))
                return true;
            if (propId == AnimalHospitalPropId.StunBaton)
                return true;
            var cost = config != null ? config.ResolvePropUnlockCost(propId) : 0;
            return ModeSaveAccess.Coins >= cost;
        }

        public bool TryUnlock(AnimalHospitalPropId propId, AnimalHospitalConfig config, AnimalHospitalModeSaveData save)
        {
            if (save == null)
                return false;
            if (save.IsPropUnlocked(propId))
                return true;

            var cost = config != null ? config.ResolvePropUnlockCost(propId) : 0;
            if (ModeSaveAccess.Coins < cost)
                return false;

            ModeSaveAccess.AddCoins(-cost);
            save.SetPropUnlocked(propId, true);
            SaveService.Save();
            return true;
        }

        public bool TryEquip(AnimalHospitalPropId propId, AnimalHospitalModeSaveData save)
        {
            if (propId == AnimalHospitalPropId.None)
                return Holster();

            if (save != null && !save.IsPropUnlocked(propId) && propId != AnimalHospitalPropId.StunBaton)
                return false;

            HeldProp = propId;
            HeldPropChanged?.Invoke(HeldProp);
            return true;
        }

        public bool Holster()
        {
            if (HeldProp == AnimalHospitalPropId.None)
                return false;

            HeldProp = AnimalHospitalPropId.None;
            HeldPropChanged?.Invoke(HeldProp);
            return true;
        }

        public int AttackDamage(AnimalHospitalConfig config) =>
            HeldProp == AnimalHospitalPropId.None
                ? 0
                : (config != null ? config.propAttackDamage : 5);
    }
}

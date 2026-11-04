using System;
using UnityEngine;

namespace Game.Save
{
    [Serializable]
    public sealed class AnimalHospitalModeSaveData
    {
        public string currentSceneId = "animal_hospital_map_1";
        public int currentDay = 1;
        public int patientsTreatedTotal;
        public int patientsLostTotal;
        public float maxSanity = 100f;
        public float sanity = 70f;

        public bool unlockedStunBaton = true;
        public bool unlockedTaser;
        public bool unlockedPistol;
        public bool unlockedFireExtinguisher;
        public bool unlockedFreezeGun;

        public ModeScopedProfile profile = new ModeScopedProfile();

        public void MigrateDefaultsIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(currentSceneId))
                currentSceneId = "animal_hospital_map_1";

            currentDay = Mathf.Max(1, currentDay);
            patientsTreatedTotal = Mathf.Max(0, patientsTreatedTotal);
            patientsLostTotal = Mathf.Max(0, patientsLostTotal);
            maxSanity = Mathf.Max(1f, maxSanity);
            sanity = Mathf.Clamp(sanity, 0f, maxSanity);
            unlockedStunBaton = true;
        }

        public bool IsPropUnlocked(AnimalHospital.AnimalHospitalPropId propId) => propId switch
        {
            AnimalHospital.AnimalHospitalPropId.StunBaton => unlockedStunBaton,
            AnimalHospital.AnimalHospitalPropId.Taser => unlockedTaser,
            AnimalHospital.AnimalHospitalPropId.Pistol => unlockedPistol,
            AnimalHospital.AnimalHospitalPropId.FireExtinguisher => unlockedFireExtinguisher,
            AnimalHospital.AnimalHospitalPropId.FreezeGun => unlockedFreezeGun,
            _ => false,
        };

        public void SetPropUnlocked(AnimalHospital.AnimalHospitalPropId propId, bool unlocked)
        {
            switch (propId)
            {
                case AnimalHospital.AnimalHospitalPropId.StunBaton:
                    unlockedStunBaton = true;
                    break;
                case AnimalHospital.AnimalHospitalPropId.Taser:
                    unlockedTaser = unlocked;
                    break;
                case AnimalHospital.AnimalHospitalPropId.Pistol:
                    unlockedPistol = unlocked;
                    break;
                case AnimalHospital.AnimalHospitalPropId.FireExtinguisher:
                    unlockedFireExtinguisher = unlocked;
                    break;
                case AnimalHospital.AnimalHospitalPropId.FreezeGun:
                    unlockedFreezeGun = unlocked;
                    break;
            }
        }
    }
}

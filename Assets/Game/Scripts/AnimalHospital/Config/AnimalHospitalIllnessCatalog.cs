using System;
using UnityEngine;

namespace Game.AnimalHospital.Config
{
    [CreateAssetMenu(
        menuName = "Game/Animal Hospital/Illness Catalog",
        fileName = "AnimalHospitalIllnessCatalog")]
    public sealed class AnimalHospitalIllnessCatalog : ScriptableObject
    {
        public AnimalHospitalIllnessCatalogEntry[] illnesses = Array.Empty<AnimalHospitalIllnessCatalogEntry>();

        public AnimalHospitalIllnessCatalogEntry PickRandom()
        {
            if (illnesses == null || illnesses.Length == 0)
                return null;

            AnimalHospitalIllnessCatalogEntry pick = null;
            var guard = 0;
            while (pick == null && guard < 8)
            {
                pick = illnesses[UnityEngine.Random.Range(0, illnesses.Length)];
                guard++;
            }

            return pick;
        }
    }

    /// <summary>病症只引用药物 Id；具体药物信息在 Medicine Catalog。</summary>
    [Serializable]
    public sealed class AnimalHospitalIllnessCatalogEntry
    {
        public string illnessId = "illness_01";
        public string illnessDescription = "头疼";
        [Tooltip("治疗所需药物 Id（对应 Medicine Catalog 中的 medicineId）。")]
        public string[] requiredMedicineIds = Array.Empty<string>();
    }
}

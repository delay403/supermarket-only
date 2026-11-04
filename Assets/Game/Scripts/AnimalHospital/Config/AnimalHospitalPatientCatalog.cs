using System;
using UnityEngine;

namespace Game.AnimalHospital.Config
{
    [CreateAssetMenu(
        menuName = "Game/Animal Hospital/Patient Catalog",
        fileName = "AnimalHospitalPatientCatalog")]
    public sealed class AnimalHospitalPatientCatalog : ScriptableObject
    {
        public AnimalHospitalPatientCatalogEntry[] patients = Array.Empty<AnimalHospitalPatientCatalogEntry>();

        public AnimalHospitalPatientCatalogEntry PickRandom()
        {
            if (patients == null || patients.Length == 0)
                return null;

            AnimalHospitalPatientCatalogEntry pick = null;
            var guard = 0;
            while (pick == null && guard < 8)
            {
                pick = patients[UnityEngine.Random.Range(0, patients.Length)];
                guard++;
            }

            return pick;
        }
    }

    [Serializable]
    public sealed class AnimalHospitalPatientCatalogEntry
    {
        public string patientId = "patient_01";
        public string displayName = "病人";
        public GameObject prefab;
        public Sprite normalPhoto;
        public Sprite anomalousPhoto;
        [Tooltip("发狂后替换的身体材质（SkinnedMeshRenderer 全部材质槽）。")]
        public Material anomalousMaterial;
        public string preferredWardId;
    }
}

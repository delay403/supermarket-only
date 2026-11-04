using System;
using UnityEngine;

namespace Game.AnimalHospital.Config
{
    [CreateAssetMenu(
        menuName = "Game/Animal Hospital/Medicine Catalog",
        fileName = "AnimalHospitalMedicineCatalog")]
    public sealed class AnimalHospitalMedicineCatalog : ScriptableObject
    {
        public AnimalHospitalMedicineDef[] medicines = Array.Empty<AnimalHospitalMedicineDef>();

        public AnimalHospitalMedicineDef FindById(string medicineId)
        {
            if (string.IsNullOrEmpty(medicineId) || medicines == null)
                return null;

            for (var i = 0; i < medicines.Length; i++)
            {
                var med = medicines[i];
                if (med != null && string.Equals(med.medicineId, medicineId, StringComparison.Ordinal))
                    return med;
            }

            return null;
        }
    }

    [Serializable]
    public sealed class AnimalHospitalMedicineDef
    {
        public string medicineId = "medicine_01";
        public string displayName = "药物";
        [Tooltip("手持用药物预制体。")]
        public GameObject medicinePrefab;
        public Sprite medicineIcon;
        [Header("Hand Pose")]
        public Vector3 handLocalPosition = new Vector3(0.05f, -0.05f, 0f);
        public Vector3 handLocalEulerAngles;
        public Vector3 handLocalScale = Vector3.one;
    }
}

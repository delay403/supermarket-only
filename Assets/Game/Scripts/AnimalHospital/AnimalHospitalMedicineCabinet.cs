using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>药柜：挂若干药物交互物。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalMedicineCabinet : MonoBehaviour
    {
        [SerializeField] private AnimalHospitalMedicineInteractable[] medicines =
            System.Array.Empty<AnimalHospitalMedicineInteractable>();

        public AnimalHospitalMedicineInteractable[] Medicines => medicines;
    }
}

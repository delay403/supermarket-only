using Game.AnimalHospital.Config;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 场景药物交互点：只需配置药物 Id 与可选按住时间；显示名从药物库读取。
    /// </summary>
    public sealed class AnimalHospitalMedicineInteractable : AnimalHospitalInteractable
    {
        [SerializeField] private string medicineId = "medicine_01";

        public string MedicineId =>
            string.IsNullOrWhiteSpace(medicineId) ? "medicine_01" : medicineId.Trim();

        public override string DisplayName
        {
            get
            {
                var def = ResolveMedicineDef();
                if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                    return def.displayName;
                return MedicineId;
            }
        }

        void Awake() => ApplyIdentity();

        void OnValidate() => ApplyIdentity();

        public void SetMedicineId(string id)
        {
            medicineId = id;
            ApplyIdentity();
        }

        void ApplyIdentity()
        {
            // 名称不写入序列化字段，运行时始终从药物库解析。
            Configure(AnimalHospitalInteractableKind.Medicine, MedicineId, null);
        }

        AnimalHospitalMedicineDef ResolveMedicineDef() =>
            AnimalHospitalConfig.ResolveRuntime()?.FindMedicine(MedicineId);
    }
}

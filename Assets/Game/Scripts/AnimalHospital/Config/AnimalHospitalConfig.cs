using Game.Core.Config;
using Game.Core.Content;
using UnityEngine;

namespace Game.AnimalHospital.Config
{
    /// <summary>
    /// 医院玩法数值与场景预制体。病人/病症/药物目录拆到独立 Catalog SO。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Animal Hospital/Hospital Config", fileName = "AnimalHospitalConfig")]
    public sealed class AnimalHospitalConfig : ScriptableObject
    {
        [Header("Catalogs（独立 SO）")]
        public AnimalHospitalPatientCatalog patientCatalog;
        public AnimalHospitalIllnessCatalog illnessCatalog;
        public AnimalHospitalMedicineCatalog medicineCatalog;

        [Header("Interaction")]
        public float raycastMaxDistance = 6f;
        [Tooltip("射线可交互 Layer：AI + Item + WorldInteractable。")]
        public LayerMask interactableRaycastMask = default;
        public float tapMoveThresholdPixels = 22f;
        [Min(0.1f)] public float defaultHoldSeconds = 1.2f;
        [Min(0.1f)] public float reportHoldSeconds = 1f;
        [Min(0.1f)] public float cameraHoldSeconds = 1.2f;
        [Min(0.1f)] public float computerHoldSeconds = 1.2f;
        [Min(0.1f)] public float printerHoldSeconds = 1.2f;
        [Min(0.1f)] public float admissionHoldSeconds = 1f;
        [Min(0.1f)] public float patientHoldSeconds = 1.5f;
        [Min(0.1f)] public float analyzerHoldSeconds = 1.2f;
        [Min(0.1f)] public float medicineHoldSeconds = 0.8f;
        [Min(0.1f)] public float propHoldSeconds = 0.8f;
        [Min(0.1f)] public float coffeeHoldSeconds = 1.5f;
        [Min(0.1f)] public float daySettlementHoldSeconds = 1.2f;
        [Min(0.1f)] public float curtainHoldSeconds = 0.8f;
        [Min(0.1f)] public float extinguishHoldSeconds = 1.5f;

        [Header("First Person Camera")]
        public float eyeHeight = 1.55f;
        public float yawSpeed = 150f;
        public float pitchSpeed = 120f;
        public float pitchMin = -70f;
        public float pitchMax = 70f;
        public bool hidePlayerMeshInFirstPerson = true;

        [Header("Highlight")]
        public Color aimHighlightColor = new Color(1f, 1f, 0.35f, 0.35f);
        public Color aimOutlineColor = Color.white;
        [Range(0f, 10f)] public float aimOutlineWidth = 2.5f;
        public Color guideHighlightColor = new Color(0.25f, 0.95f, 0.45f, 0.45f);
        public Color guideOutlineColor = new Color(0.25f, 0.95f, 0.45f, 1f);
        [Range(0f, 10f)] public float guideOutlineWidth = 3.5f;

        void OnEnable() => EnsureDefaultInteractableMask();

        public void EnsureDefaultInteractableMask()
        {
            if (interactableRaycastMask.value != 0)
                return;

            interactableRaycastMask = AnimalHospitalInteractableLayers.DefaultRaycastMask;
        }

        [Header("Sanity")]
        public float maxSanity = 100f;
        public float startingSanity = 100f;
        public float coffeeSanityRestore = 10f;
        public float coffeeCooldownSeconds = 300f;
        [Tooltip("被动掉落：每隔多少秒掉 1 点理智。")]
        [Min(0.1f)] public float sanityPassiveDrainIntervalSeconds = 10f;
        [Min(0f)] public float sanityPassiveDrainAmount = 1f;
        [Tooltip("理智低于该比例时播放残血心跳循环。")]
        [Range(0.05f, 1f)] public float lowSanityHeartbeatNormalized = 0.5f;

        [Header("Treatment")]
        [Tooltip("治疗成功后继续躺床的秒数，之后再瞬移到病房出口离开。")]
        [Min(0f)] public float treatmentBedRestSeconds = 3f;

        [Header("Day Schedule")]
        [Tooltip("每天随机病人数量下限（含）。")]
        [Min(1)] public int patientsPerDayMin = 4;
        [Tooltip("每天随机病人数量上限（含）。")]
        [Min(1)] public int patientsPerDayMax = 12;
        [Tooltip("相邻病人出现间隔下限（秒）。")]
        [Min(1f)] public float patientArrivalIntervalMinSeconds = 30f;
        [Tooltip("相邻病人出现间隔上限（秒）。")]
        [Min(1f)] public float patientArrivalIntervalMaxSeconds = 60f;
        [Tooltip("首位病人在开局后多少秒内到达（随机 0～该值）。")]
        [Min(0.1f)] public float firstPatientArrivalMaxSeconds = 10f;
        public float curtainLeaveWaitSeconds = 15f;
        [Tooltip("完成接待（安排病房）后，异常病人发狂前的等待秒数。")]
        public float anomalousAttackDelayAfterReceptionSeconds = 0f;
        [Tooltip("异常病人发狂后的攻击间隔（秒）。")]
        [Min(0.5f)] public float anomalousAttackIntervalSeconds = 5f;

        [Header("Patient Emoji")]
        [Min(0.1f)] public float patientEmojiAttackedDuration = 2f;
        [Min(0.1f)] public float patientEmojiQuestionDuration = 2.5f;
        [Min(1f)] public float patientEmojiRandomIntervalSeconds = 15f;
        [Range(0f, 1f)] public float patientEmojiRandomChance = 0.25f;

        [Header("Prop Hand")]
        [Tooltip("手持道具模型挂载点（相对第一人称摄像机）。")]
        public Vector3 propHandleZoneLocalOffset = new Vector3(0.3f, -0.3f, 1.1f);

        [Header("Prop Unlock Costs (coins)")]
        public int taserUnlockCost = 500;
        public int pistolUnlockCost = 500;
        public int fireExtinguisherUnlockCost = 1000;
        public int freezeGunUnlockCost = 2000;
        public int propAttackDamage = 5;

        [Header("Combat")]
        public float shadowMaxHealth = 10f;
        public float anomalousPatientMaxHealth = 10f;
        public float shadowAttackSanityLoss = 5f;
        public float anomalousAttackSanityLoss = 5f;

        [Header("Events")]
        [Tooltip("异常事件每日最多触发次数；触发概率按存活天数在 AnimalHospitalService 中计算。")]
        [Min(0)] public int maxAnomalyEventsPerDay = 3;
        public float ceilingLookAngleDegrees = 60f;
        public float ceilingSanityLossPerSecond = 10f;

        public float wardFireCheckIntervalSeconds = 60f;
        public float wardFireSanityLossPerSecond = 5f;
        public float wardFireDurationSeconds = 300f;
        public float patientOnFireDurationSeconds = 10f;
        [Tooltip("在着火病房范围内按住灭火器/冷冻枪攻击键的持续秒数。")]
        [Min(0.5f)] public float wardExtinguishHoldSeconds = 2f;

        public float corridorSlimeCheckIntervalSeconds = 60f;
        public float corridorSlimeSanityLossPerSecond = 5f;
        public float corridorSlimeDurationSeconds = 180f;
        public float corridorSlimeGrowSpeed = 0.05f;

        public float corridorShadowCheckIntervalSeconds = 60f;

        [Header("Economy")]
        public int coinsPerTreatedPatient = 10;
        [Tooltip("每处理一个异常病人（离开医院）获得的金币。")]
        public int coinsPerHandledAnomaly = 10;
        [Tooltip("理智归零后恢复的理智值。")]
        [Min(1f)] public float deathSanityRestore = 50f;

        [Header("Prefabs / VFX")]
        public GameObject admissionFormPrefab;
        public GameObject reportPrefab;
        public GameObject shadowPrefab;
        public GameObject slimePrefab;
        public GameObject ceilingObjectPrefab;
        [Tooltip("病房着火时在 fireVisualRoots 点位生成的火焰特效（如 FX_Fire_Big_02）。")]
        public GameObject wardFireEffectPrefab;
        [Tooltip("治疗成功后挂在病人身上的特效（如 StarFogFlowGreen）。")]
        public GameObject treatmentEffectPrefab;
        [Min(0.5f)] public float treatmentEffectDestroySeconds = 3.5f;
        [Tooltip("病房手术交互完成后挂在病人身上的睡眠特效（如 Sleep）。")]
        public GameObject surgerySleepEffectPrefab;
        [Min(0.5f)] public float surgerySleepEffectDestroySeconds = 8f;

        [Header("Taser VFX")]
        [Tooltip("电击器电弧特效（FX_Electricity_01）；运行时由 ParticleBeamConnector 拉伸至目标。")]
        public GameObject taserElectricityPrefab;
        [Tooltip("灭火器喷射特效（FX_FlameThrower_01）。")]
        public GameObject fireExtinguisherBeamPrefab;
        [Tooltip("冷冻枪射线特效（FX_PowerBeam_01）。")]
        public GameObject freezeGunBeamPrefab;
        [Tooltip("电弧终点在目标 Actor 根节点上的高度偏移（米）。")]
        public float taserBeamEndHeightOffset = 1f;

        [Header("Prop Combat")]
        [Min(0.05f)] public float stunBatonAttackIntervalSeconds = 0.45f;
        [Min(0.05f)] public float pistolFireIntervalSeconds = 0.35f;
        [Tooltip("持续按住类武器：至少按住这么久才造成第一次伤害。")]
        [Min(0.1f)] public float propHoldDamageDelaySeconds = 2f;
        [Tooltip("手枪按住后首次开枪延迟（秒）；0 表示按下即射。")]
        [Min(0f)] public float pistolHoldDamageDelaySeconds = 0f;
        [Min(0.1f)] public float propBeamDamageIntervalSeconds = 2f;
        [Tooltip("电击器/冷冻枪命中后的移速倍率（0.3 = 降低 70%）。")]
        [Range(0.05f, 1f)] public float propSlowSpeedMultiplier = 0.3f;
        [Min(0.1f)] public float propSlowDurationSeconds = 2f;

        public AnimalHospitalMedicineDef FindMedicine(string medicineId) =>
            medicineCatalog != null ? medicineCatalog.FindById(medicineId) : null;

        public AnimalHospitalIllnessCatalogEntry PickRandomIllness() =>
            illnessCatalog != null ? illnessCatalog.PickRandom() : null;

        public AnimalHospitalPatientCatalogEntry PickRandomPatient() =>
            patientCatalog != null ? patientCatalog.PickRandom() : null;

        public float ResolveHoldSeconds(AnimalHospitalInteractableKind kind) => kind switch
        {
            AnimalHospitalInteractableKind.Report => reportHoldSeconds,
            AnimalHospitalInteractableKind.Camera => cameraHoldSeconds,
            AnimalHospitalInteractableKind.Computer => computerHoldSeconds,
            AnimalHospitalInteractableKind.Printer => printerHoldSeconds,
            AnimalHospitalInteractableKind.AdmissionForm => admissionHoldSeconds,
            AnimalHospitalInteractableKind.Patient => patientHoldSeconds,
            AnimalHospitalInteractableKind.Analyzer => analyzerHoldSeconds,
            AnimalHospitalInteractableKind.Medicine => medicineHoldSeconds,
            AnimalHospitalInteractableKind.Prop => propHoldSeconds,
            AnimalHospitalInteractableKind.CoffeeMachine => coffeeHoldSeconds,
            AnimalHospitalInteractableKind.DaySettlement => daySettlementHoldSeconds,
            AnimalHospitalInteractableKind.Curtain => curtainHoldSeconds,
            AnimalHospitalInteractableKind.WardComputer => computerHoldSeconds,
            AnimalHospitalInteractableKind.WardDisplay => computerHoldSeconds,
            AnimalHospitalInteractableKind.FireTarget => extinguishHoldSeconds,
            AnimalHospitalInteractableKind.Slime => extinguishHoldSeconds,
            _ => defaultHoldSeconds,
        };

        public int ResolvePropUnlockCost(AnimalHospitalPropId propId) => propId switch
        {
            AnimalHospitalPropId.Taser => taserUnlockCost,
            AnimalHospitalPropId.Pistol => pistolUnlockCost,
            AnimalHospitalPropId.FireExtinguisher => fireExtinguisherUnlockCost,
            AnimalHospitalPropId.FreezeGun => freezeGunUnlockCost,
            _ => 0,
        };

        public static AnimalHospitalConfig ResolveRuntime()
        {
            var fromRegistry = ConfigRegistry.AnimalHospitalConfig;
            if (fromRegistry != null)
                return fromRegistry;

            return Resources.Load<AnimalHospitalConfig>(AnimalHospitalResourcePaths.ConfigAssetName);
        }
    }
}

using Game.Player;
using UnityEngine;

namespace Game.Features.StrengthTraining
{
    /// <summary>
    /// 力量训练池区域：玩家进入后可挂机提升基础力量（类似跑步机）。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class StrengthTrainingZone : MonoBehaviour
    {
        [SerializeField] private string groundId;
        [SerializeField] private StrengthTrainingTier legacyTier = StrengthTrainingTier.Basic;
        [SerializeField] private LayerMask playerMask = ~0;
        [SerializeField] private Transform trainingPoint;
        [SerializeField] private bool useZoneFacing;
        [SerializeField] private float facingYawDegrees;
        [SerializeField] private bool allowFreeUnlock = true;
        [SerializeField] private bool allowGemUnlock = true;
        [SerializeField] private Sprite panelIcon;
        [SerializeField] private GameObject runningEffectPrefab;

        GameObject _spawnedEffect;
        Collider _zoneCollider;

        public string GroundId => ResolveGroundId();
        public StrengthTrainingTier LegacyTier => legacyTier;
        public Sprite PanelIcon => panelIcon;

        public bool AllowFreeUnlock
        {
            get
            {
                var def = StrengthTrainingService.ResolveGroundDef(GroundId);
                if (def == null) return legacyTier != StrengthTrainingTier.Basic && allowFreeUnlock;
                return def.unlockPolicy is StrengthTrainingUnlockPolicy.FreeOncePerDay
                    or StrengthTrainingUnlockPolicy.ProgressPerDay;
            }
        }

        public bool AllowGemUnlock
        {
            get
            {
                var def = StrengthTrainingService.ResolveGroundDef(GroundId);
                if (def == null)
                    return legacyTier == StrengthTrainingTier.PremiumDaily && allowGemUnlock;
                return def.unlockPolicy == StrengthTrainingUnlockPolicy.GemOncePerDay && allowGemUnlock;
            }
        }

        void Awake()
        {
            EnsureTriggerRigidbody();
            _zoneCollider = GetComponent<Collider>();
        }

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null)
                c.isTrigger = true;
            EnsureTriggerRigidbody();
        }

        void EnsureTriggerRigidbody()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void OnDestroy()
        {
            if (_spawnedEffect != null)
                Destroy(_spawnedEffect);
        }

        public string ResolveGroundId()
        {
            if (!string.IsNullOrWhiteSpace(groundId))
                return groundId.Trim();
            return StrengthTrainingService.LegacyTierToGroundId(legacyTier);
        }

        public bool ContainsPoint(Vector3 worldPosition)
        {
            if (_zoneCollider == null)
                _zoneCollider = GetComponent<Collider>();
            if (_zoneCollider == null)
                return false;

            var closest = _zoneCollider.ClosestPoint(worldPosition);
            return (closest - worldPosition).sqrMagnitude < 0.04f;
        }

        public Vector3 GetTrainingWorldPosition()
        {
            var t = trainingPoint != null ? trainingPoint : transform;
            return t.position;
        }

        public Quaternion GetTrainingWorldRotation()
        {
            if (useZoneFacing)
                return Quaternion.Euler(0f, facingYawDegrees, 0f);

            var t = trainingPoint != null ? trainingPoint : transform;
            return Quaternion.Euler(0f, t.eulerAngles.y, 0f);
        }

        public void StartTrainingEffect()
        {
            if (runningEffectPrefab == null)
                return;

            if (_spawnedEffect == null)
            {
                var parent = trainingPoint != null ? trainingPoint : transform;
                _spawnedEffect = Instantiate(runningEffectPrefab, parent.position, parent.rotation, parent);
            }

            _spawnedEffect.SetActive(true);
        }

        public void StopTrainingEffect()
        {
            if (_spawnedEffect != null)
                _spawnedEffect.SetActive(false);
        }

        void OnTriggerEnter(Collider other) => TryNotifyPlayer(other);

        void TryNotifyPlayer(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerMask) == 0)
                return;

            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor == null)
                return;

            var ctrl = motor.GetComponent<PlayerStrengthTrainingController>();
            if (ctrl == null)
                ctrl = motor.gameObject.AddComponent<PlayerStrengthTrainingController>();

            ctrl.NotifyEnterZone(this);
        }

        void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerMask) == 0)
                return;

            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor == null)
                return;

            motor.GetComponent<PlayerStrengthTrainingController>()?.NotifyExitZone(this);
        }
    }
}

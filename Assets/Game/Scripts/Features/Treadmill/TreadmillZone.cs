using Game.Player;
using UnityEngine;

namespace Game.Features.Treadmill
{
    /// <summary>
    /// 跑步机区域触发器：玩家进入后弹出解锁/确认面板，确认后开始锻炼（或先走向跑步点）。
    /// 请在场景中放置 Collider（isTrigger）并指定档位。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TreadmillZone : MonoBehaviour
    {
        [SerializeField] private TreadmillTier tier = TreadmillTier.Basic;
        [SerializeField] private LayerMask playerMask = ~0;
        [SerializeField] private bool useZoneFacing;
        [SerializeField] private float facingYawDegrees;

        [Header("跑步点")]
        [Tooltip("玩家开始锻炼时移动到此点；留空则使用区域 Transform。")]
        [SerializeField] private Transform runningPoint;

        [Tooltip("跑步时在跑步点循环播放的特效预制体。")]
        [SerializeField] private GameObject runningEffectPrefab;

        [Header("解锁方式（仅对对应档位生效）")]
        [SerializeField] private bool allowFreeUnlock = true;
        [SerializeField] private bool allowGemUnlock = true;

        [Header("面板展示")]
        [Tooltip("解锁/确认面板中显示的跑步机图标；留空则使用面板档位默认图。")]
        [SerializeField] private Sprite panelIcon;

        GameObject _spawnedRunningEffect;

        public TreadmillTier Tier => tier;
        public bool UseZoneFacing => useZoneFacing;
        public float FacingYawDegrees => facingYawDegrees;
        public bool AllowFreeUnlock => ResolveAllowFreeUnlock();
        public bool AllowGemUnlock => ResolveAllowGemUnlock();
        public Sprite PanelIcon => panelIcon;

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null)
                c.isTrigger = true;
        }

        void OnDestroy()
        {
            if (_spawnedRunningEffect != null)
                Destroy(_spawnedRunningEffect);
        }

        public Vector3 GetRunningWorldPosition()
        {
            var t = runningPoint != null ? runningPoint : transform;
            return t.position;
        }

        public Quaternion GetRunningWorldRotation()
        {
            if (useZoneFacing)
                return Quaternion.Euler(0f, facingYawDegrees, 0f);

            var t = runningPoint != null ? runningPoint : transform;
            return Quaternion.Euler(0f, t.eulerAngles.y, 0f);
        }

        public void StartRunningEffect()
        {
            if (runningEffectPrefab == null)
                return;

            if (_spawnedRunningEffect == null)
            {
                var parent = runningPoint != null ? runningPoint : transform;
                _spawnedRunningEffect = Instantiate(
                    runningEffectPrefab,
                    parent.position,
                    parent.rotation,
                    parent);
            }

            _spawnedRunningEffect.SetActive(true);
        }

        public void StopRunningEffect()
        {
            if (_spawnedRunningEffect != null)
                _spawnedRunningEffect.SetActive(false);
        }

        bool ResolveAllowFreeUnlock()
        {
            if (tier == TreadmillTier.Basic)
                return false;
            return allowFreeUnlock;
        }

        bool ResolveAllowGemUnlock()
        {
            if (tier != TreadmillTier.PremiumDaily)
                return false;
            return allowGemUnlock;
        }

        void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerMask) == 0)
                return;

            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor == null)
                return;

            var ctrl = motor.GetComponent<PlayerTreadmillController>();
            if (ctrl == null)
                ctrl = motor.gameObject.AddComponent<PlayerTreadmillController>();

            ctrl.NotifyEnterZone(this);
        }

        void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerMask) == 0)
                return;

            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor == null)
                return;

            var ctrl = motor.GetComponent<PlayerTreadmillController>();
            ctrl?.NotifyExitZone(this);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = tier switch
            {
                TreadmillTier.PremiumDaily => new Color(1f, 0.75f, 0.2f, 0.35f),
                TreadmillTier.FreeDaily => new Color(0.3f, 0.7f, 1f, 0.35f),
                _ => new Color(0.4f, 1f, 0.5f, 0.35f),
            };

            var col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                if (col is BoxCollider box)
                    Gizmos.DrawCube(box.center, box.size);
                else if (col is SphereCollider sphere)
                    Gizmos.DrawSphere(sphere.center, sphere.radius);
            }

            Gizmos.matrix = Matrix4x4.identity;
            var runPos = runningPoint != null ? runningPoint.position : transform.position + transform.forward * 0.5f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(runPos, 0.25f);

            if (useZoneFacing)
            {
                var fwd = Quaternion.Euler(0f, facingYawDegrees, 0f) * Vector3.forward;
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(runPos + Vector3.up * 0.2f, fwd * 1.5f);
            }
        }
#endif
    }
}

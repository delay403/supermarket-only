using System;
using Game.Fishing.Config;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 钓鱼区域触发器：玩家进入后可发起钓鱼；离开区域会取消进行中的钓鱼。
    /// 请在场景中放置 Collider（isTrigger）并绑定 <see cref="FishingZoneDef"/>。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FishingZone : MonoBehaviour
    {
        [SerializeField] private FishingZoneDef zoneDef;
        [SerializeField] private LayerMask playerMask = ~0;

        [Header("调试")]
        [SerializeField] private bool debugLog;

        [Header("可选：钓鱼时玩家站位")]
        [SerializeField] private Transform fishingPoint;
        [SerializeField] private bool useZoneFacing;
        [SerializeField] private float facingYawDegrees;

        [Header("放线落点")]
        [Tooltip("鱼线末端目标点；留空则使用区域中心向前延伸并下沉。")]
        [SerializeField] private Transform castTargetPoint;
        [SerializeField] private float castTargetForwardOffset = 6f;
        [SerializeField] private float castTargetDepth = 0.5f;

        [Header("概率公示")]
        [Tooltip("留空则在子物体中查找 FishingZoneProbabilityBoard。")]
        [SerializeField] private FishingZoneProbabilityBoard probabilityBoard;

        public FishingZoneDef ZoneDef => zoneDef;
        public string ZoneId => zoneDef != null ? zoneDef.zoneId : string.Empty;

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null)
                c.isTrigger = true;
        }

        public Vector3 GetFishingWorldPosition()
        {
            var t = fishingPoint != null ? fishingPoint : transform;
            return t.position;
        }

        public Quaternion GetFishingWorldRotation()
        {
            if (useZoneFacing)
                return Quaternion.Euler(0f, facingYawDegrees, 0f);

            var t = fishingPoint != null ? fishingPoint : transform;
            return Quaternion.Euler(0f, t.eulerAngles.y, 0f);
        }

        /// <summary>放线时鱼线末端应落入的世界坐标（钓鱼区内/水面下）。</summary>
        public Vector3 GetCastTargetWorldPosition()
        {
            if (castTargetPoint != null)
                return castTargetPoint.position;

            var origin = GetFishingWorldPosition();
            var rot = GetFishingWorldRotation();
            var forward = rot * Vector3.forward;
            var target = origin + forward * castTargetForwardOffset;
            target.y -= castTargetDepth;
            return target;
        }

        /// <summary>水面世界 Y（用于鱼线落水高度约束）。</summary>
        public float GetWaterSurfaceWorldY() => GetCastTargetWorldPosition().y + castTargetDepth;

        void Start()
        {
            NotifyOverlappingPlayers();
            RefreshProbabilityBoard();
        }

        void RefreshProbabilityBoard()
        {
            if (probabilityBoard == null)
                probabilityBoard = GetComponentInChildren<FishingZoneProbabilityBoard>(true);

            probabilityBoard?.Refresh(this);
        }

        void NotifyOverlappingPlayers()
        {
            var col = GetComponent<Collider>();
            if (col == null || !col.isTrigger)
                return;

            var bounds = col.bounds;
            var hits = Physics.OverlapBox(
                bounds.center,
                bounds.extents,
                transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < hits.Length; i++)
                TryHandleTriggerEnter(hits[i]);
        }

        void OnTriggerEnter(Collider other)
        {
            TryHandleTriggerEnter(other);
        }

        void OnTriggerExit(Collider other)
        {
            if (!TryResolvePlayerMotor(other, out var motor))
                return;

            motor.GetComponent<PlayerFishingController>()?.NotifyExitZone(this);
        }

        void TryHandleTriggerEnter(Collider other)
        {
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.Fishing))
                return;

            if (!TryResolvePlayerMotor(other, out var motor))
                return;

            var ctrl = motor.GetComponent<PlayerFishingController>();
            if (ctrl == null)
                ctrl = motor.gameObject.AddComponent<PlayerFishingController>();

            ctrl.NotifyEnterZone(this);

            if (debugLog)
                Debug.Log($"[FishingZone] 玩家进入区域 '{name}' (zoneId={ZoneId})", this);
        }

        /// <summary>由玩家 Start 扫描时调用。</summary>
        public void NotifyPlayerEnter(PlayerMotor motor)
        {
            if (!IsPlayerMotorAllowed(motor))
                return;

            var ctrl = motor.GetComponent<PlayerFishingController>();
            if (ctrl == null)
                ctrl = motor.gameObject.AddComponent<PlayerFishingController>();

            ctrl.NotifyEnterZone(this);
        }

        bool TryResolvePlayerMotor(Collider other, out PlayerMotor motor)
        {
            motor = null;
            if (other == null)
                return false;

            motor = other.GetComponentInParent<PlayerMotor>();
            return IsPlayerMotorAllowed(motor);
        }

        bool IsPlayerMotorAllowed(PlayerMotor motor)
        {
            if (motor == null)
                return false;

            var playerLayer = motor.gameObject.layer;
            if (playerMask.value != 0 && ((1 << playerLayer) & playerMask.value) == 0)
            {
                if (debugLog)
                    Debug.LogWarning(
                        $"[FishingZone] 玩家 Layer={playerLayer} 不在 playerMask 内，忽略触发。请把 Player 设为 Player 层，或将 FishingZone.playerMask 设为 Everything。",
                        this);
                return false;
            }

            return true;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
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
            var pos = fishingPoint != null ? fishingPoint.position : transform.position + transform.forward * 0.5f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pos, 0.25f);

            var castPos = GetCastTargetWorldPosition();
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(castPos, 0.2f);
            Gizmos.DrawLine(pos, castPos);
        }
#endif
    }
}

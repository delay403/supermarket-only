using System.Collections.Generic;
using Game.WormDodge;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EscapeSnake
{
    /// <summary>
    /// 安全区：仅依赖本物体 Collider Trigger 的 OnTriggerEnter/Exit 判定占用。
    /// 子物体上的死亡区等其他 Trigger 不会计入安全区。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WormSafeZone : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;

        readonly HashSet<WormDodgeParticipant> _occupants = new HashSet<WormDodgeParticipant>();
        Collider _trigger;

        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;
        public Collider TriggerCollider => _trigger;

        public bool TryGetNavMeshLanding(out Vector3 landing, float sampleRadius = 2.5f)
        {
            landing = SpawnPoint.position;
            if (!NavMesh.SamplePosition(
                    SpawnPoint.position,
                    out var hit,
                    Mathf.Max(0.5f, sampleRadius),
                    NavMesh.AllAreas))
                return false;

            landing = hit.position;
            return true;
        }

        /// <summary>AI 选点：平面距离、三维距离与爬升高度均需在限制内（不要求落点在 NavMesh 上）。</summary>
        public bool IsEligibleForAiEvasion(
            Vector3 fromPos,
            float planarRadius,
            float maxWorldDistance,
            float maxVerticalRise)
        {
            var landing = GetAiEvasionLandingPosition();
            var offset = landing - fromPos;
            var planar = offset;
            planar.y = 0f;
            if (planar.sqrMagnitude > planarRadius * planarRadius)
                return false;

            if (offset.sqrMagnitude > maxWorldDistance * maxWorldDistance)
                return false;

            if (offset.y > maxVerticalRise)
                return false;

            return true;
        }

        /// <summary>AI 跳上安全区时的目标点（平台 SpawnPoint，非 NavMesh 采样）。</summary>
        public Vector3 GetAiEvasionLandingPosition() => SpawnPoint.position;

        void Awake()
        {
            _trigger = GetComponent<Collider>();
            EnsureTriggerConfigured();
        }

        void EnsureTriggerConfigured()
        {
            if (_trigger == null)
                _trigger = GetComponent<Collider>();

            if (_trigger != null)
                _trigger.isTrigger = true;
        }

        public bool ContainsParticipant(WormDodgeParticipant participant) =>
            participant != null && _occupants.Contains(participant);

        public void RegisterParticipant(WormDodgeParticipant participant)
        {
            if (participant != null)
                _occupants.Add(participant);
        }

        public void UnregisterParticipant(WormDodgeParticipant participant) =>
            _occupants.Remove(participant);

        void OnTriggerEnter(Collider other)
        {
            if (!IsParticipantCollider(other, out var participant))
                return;

            if (!_occupants.Add(participant))
                return;

            WormSafeZoneUtility.InvalidateCache();
            WormDodgeDirector.Instance?.NotifyEnteredSafeZone(participant, this);
        }

        void OnTriggerExit(Collider other)
        {
            if (!IsParticipantCollider(other, out var participant))
                return;

            RemoveParticipantIfPresent(participant);
        }

        void RemoveParticipantIfPresent(WormDodgeParticipant participant)
        {
            if (!_occupants.Remove(participant))
                return;

            WormSafeZoneUtility.InvalidateCache();
            WormDodgeDirector.Instance?.NotifyExitedSafeZone(participant);
        }

        static bool IsParticipantCollider(Collider other, out WormDodgeParticipant participant)
        {
            participant = other.GetComponent<WormDodgeParticipant>();
            if (participant != null)
                return true;

            participant = other.GetComponentInParent<WormDodgeParticipant>();
            if (participant != null)
                return true;

            participant = other.GetComponentInChildren<WormDodgeParticipant>();
            return participant != null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _trigger = GetComponent<Collider>();
            EnsureTriggerConfigured();
        }

        public void FitBoxColliderToPlatform(float padding = 0.15f)
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
                return;

            var root = box.transform;
            var hasRenderer = false;
            var worldBounds = new Bounds(root.position, Vector3.zero);

            var renderers = root.GetComponentsInChildren<Renderer>();
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasRenderer)
                {
                    worldBounds = renderer.bounds;
                    hasRenderer = true;
                }
                else
                {
                    worldBounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasRenderer && spawnPoint != null)
            {
                worldBounds = new Bounds(spawnPoint.position, Vector3.one);
                hasRenderer = true;
            }

            if (!hasRenderer)
                return;

            worldBounds.Expand(padding);

            var lossy = root.lossyScale;
            box.center = root.InverseTransformPoint(worldBounds.center);
            box.size = new Vector3(
                Mathf.Max(0.5f, worldBounds.size.x / Mathf.Max(0.001f, Mathf.Abs(lossy.x))),
                Mathf.Max(0.5f, worldBounds.size.y / Mathf.Max(0.001f, Mathf.Abs(lossy.y))),
                Mathf.Max(0.5f, worldBounds.size.z / Mathf.Max(0.001f, Mathf.Abs(lossy.z))));
            box.isTrigger = true;
        }

        void OnDrawGizmosSelected()
        {
            var col = _trigger != null ? _trigger : GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 0.35f, 0.9f);
                var bounds = col.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }

            var spawn = SpawnPoint;
            if (spawn == null)
                return;

            Gizmos.color = new Color(0.95f, 0.85f, 0.15f, 0.95f);
            Gizmos.DrawSphere(spawn.position, 0.2f);
        }
#endif
    }
}

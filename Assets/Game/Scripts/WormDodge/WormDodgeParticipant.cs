using System;
using Game.EscapeSnake;
using UnityEngine;
using UnityEngine.AI;

namespace Game.WormDodge
{
    /// <summary>
    /// 可被多节虫选为追击目标的参与者（玩家或 AI）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeParticipant : MonoBehaviour
    {
        [SerializeField] private bool isPlayer;
        [SerializeField] private WormChaseTarget chaseTarget;
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private float respawnDelaySeconds = 2.5f;
        [SerializeField] private bool hideWhenDead = true;
        [SerializeField] private string leaderboardDisplayName;

        float _respawnAt = -1f;
        int _highestFloorReached;
        Collider _bodyCollider;

        public bool IsPlayer => isPlayer;
        public bool IsInSafeZone { get; internal set; }
        internal Collider BodyCollider => _bodyCollider;

        /// <summary>索敌采样点：优先角色碰撞体中心，避免仅用脚底坐标导致跨层误判。</summary>
        public Vector3 GetChaseDetectPosition()
        {
            if (_bodyCollider != null)
                return _bodyCollider.bounds.center;

            if (chaseTarget != null)
                return chaseTarget.transform.position;

            return transform.position + Vector3.up * 0.9f;
        }

        [Header("Debug (Runtime)")]
        [SerializeField] private bool debugIsInSafeZoneFlag;
        [SerializeField] private bool debugRegisteredInZone;
        [SerializeField] private bool debugInsideZoneGeometry;
        [SerializeField] private bool debugShouldShowIndicator;
        [SerializeField] private string debugSafeZoneName;

        public bool DebugIsInSafeZoneFlag => debugIsInSafeZoneFlag;
        public bool DebugRegisteredInZone => debugRegisteredInZone;
        public bool DebugInsideZoneGeometry => debugInsideZoneGeometry;
        public bool DebugShouldShowIndicator => debugShouldShowIndicator;

        public WormChaseTarget ChaseTarget => chaseTarget;
        public int HighestFloorReached => _highestFloorReached;
        public bool IsAliveForLeaderboard => chaseTarget != null && chaseTarget.IsAlive;

        public bool IsEligibleForChase =>
            chaseTarget != null && chaseTarget.IsAlive && !chaseTarget.IsInvincible && !IsInSafeZone;

        public event Action<WormDodgeParticipant, WormChaseDeathReason> Eliminated;

        void Awake()
        {
            if (chaseTarget == null)
                chaseTarget = GetComponent<WormChaseTarget>();
            if (chaseTarget == null)
                chaseTarget = gameObject.AddComponent<WormChaseTarget>();

            _bodyCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>(true);
            chaseTarget.Died += OnChaseTargetDied;
        }

        void OnDestroy()
        {
            if (chaseTarget != null)
                chaseTarget.Died -= OnChaseTargetDied;
        }

        void LateUpdate()
        {
            if (!WormDodgeRuntimeDebug.InspectorFieldsEnabled)
                return;

            RefreshSafeZoneDebugFields();
        }

        internal void RefreshSafeZoneDebugFields()
        {
            if (!WormDodgeRuntimeDebug.InspectorFieldsEnabled)
                return;

            debugIsInSafeZoneFlag = IsInSafeZone;
            WormSafeZoneUtility.RefreshParticipantDebugFields(
                this,
                out debugRegisteredInZone,
                out debugInsideZoneGeometry,
                out debugShouldShowIndicator,
                out var zone);
            debugSafeZoneName = zone != null ? zone.name : string.Empty;
        }

        void Update()
        {
            if (_respawnAt < 0f || Time.time < _respawnAt)
                return;

            _respawnAt = -1f;
            ReviveAtSpawn();
        }

        void OnChaseTargetDied(WormChaseTarget _, WormChaseDeathReason reason)
        {
            Eliminated?.Invoke(this, reason);
            if (isPlayer || respawnDelaySeconds < 0f)
                return;

            _respawnAt = Time.time + respawnDelaySeconds;
        }

        public void ReviveAtSpawn()
        {
            if (chaseTarget == null)
                return;

            EnsureVisible();
            var point = respawnPoint != null ? respawnPoint : transform;
            TeleportTo(point);
            chaseTarget.Revive();
        }

        public void TeleportTo(Transform point)
        {
            if (point == null)
                return;

            ApplyTransformWithCharacterController(point.position, point.rotation);
            respawnPoint = point;
        }

        /// <summary>新一局开始时重置进度、复活，并回到指定出生点（同时重置存档点）。</summary>
        public void ResetToRunStart(Transform spawnPoint)
        {
            ResetRunProgress();
            _respawnAt = -1f;
            EnsureVisible();

            if (spawnPoint != null)
                respawnPoint = spawnPoint;

            chaseTarget?.ClearInvincibility();
            chaseTarget?.Revive();

            if (respawnPoint != null)
                TeleportTo(respawnPoint);
        }

        /// <summary>激活自身及父节点，避免死亡隐藏后无法被重新收集/重置。</summary>
        public void EnsureVisible()
        {
            var current = transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    current.gameObject.SetActive(true);
                current = current.parent;
            }
        }

        public Transform RespawnPoint => respawnPoint;

        void ApplyTransformWithCharacterController(Vector3 position, Quaternion rotation)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            var agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                if (!agent.enabled)
                    agent.enabled = true;

                if (NavMesh.SamplePosition(position, out var hit, 2.5f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
                else
                    agent.Warp(position);

                if (agent.isOnNavMesh)
                    agent.ResetPath();
            }

            if (cc != null)
                cc.enabled = true;
        }

        public void SetCheckpoint(Transform point)
        {
            if (point != null)
                respawnPoint = point;
        }

        public void ConfigureForPlayer(bool player, Transform spawn = null)
        {
            isPlayer = player;
            respawnPoint = spawn != null ? spawn : respawnPoint;
            hideWhenDead = !player;
        }

        public void SetLeaderboardDisplayName(string name)
        {
            leaderboardDisplayName = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        }

        public string GetLeaderboardDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(leaderboardDisplayName))
                return leaderboardDisplayName.Trim();

            return gameObject.name;
        }

        public void ResetRunProgress()
        {
            _highestFloorReached = 0;
            IsInSafeZone = false;
        }

        public void RecordFloorReached(int floorIndex)
        {
            if (floorIndex > _highestFloorReached)
                _highestFloorReached = floorIndex;
        }
    }
}

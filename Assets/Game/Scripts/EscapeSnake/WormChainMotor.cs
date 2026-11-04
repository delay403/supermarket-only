using System;
using System.Collections.Generic;
using Game.WormDodge;
using UnityEngine;

namespace Game.EscapeSnake
{
    /// <summary>
    /// 扁平层级多节虫：tou、shenti1…shentiN 均为 chongzi 的子物体。
    /// 开局先汇入最近路点，再沿路径巡逻；索敌追击直至目标出圈或进安全区；结束后冷却 15 秒仅巡逻。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormChainMotor : MonoBehaviour
    {
        public delegate WormChaseTarget PatrolTargetPickerDelegate();

        enum MoveMode
        {
            JoinPath,
            Patrol,
            TurningBack,
            Chase,
        }

        [Header("Segments")]
        [Tooltip("头 → 尾，如 tou、shenti1、shenti2…。留空则自动从子物体收集。")]
        [SerializeField] private Transform[] segmentsHeadToTail;

        [Header("Path")]
        [SerializeField] private WormPath path;
        [SerializeField] private bool playOnStart = true;

        [Header("Motion")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float segmentSpacing = 1.5f;
        [SerializeField] private bool autoDetectSegmentSpacing = true;

        [Header("Rotation")]
        [SerializeField] private float maxJointAngle = 90f;
        [SerializeField] private float rotationLerpSpeed = 720f;

        [Header("Join path")]
        [SerializeField] private float joinPathArrivalTolerance = 0.35f;
        [SerializeField] private float turnAroundSeconds = 0.28f;

        [Header("Chase")]
        [Tooltip("巡逻/追击索敌半径（相对虫头水平距离）。")]
        [SerializeField] private float chaseDetectRadius = 14f;
        [Tooltip("索敌允许的最大高度差（米）；跨层目标不计入范围。")]
        [SerializeField] private float chaseMaxVerticalDelta = 4f;
        [Tooltip("巡逻时扫描索敌目标的间隔（秒）。")]
        [SerializeField] private float patrolTargetScanInterval = 0.25f;
        [Tooltip("追击时刷新目标状态的间隔（秒）。")]
        [SerializeField] private float chaseTargetRefreshInterval = 0.2f;
        [Tooltip("结束追击后仅巡逻、不索敌的冷却（秒）。")]
        [SerializeField] private float postChasePatrolCooldownSeconds = 15f;
        [SerializeField] private float chaseCloseSlowDistance = 1.35f;
        [SerializeField] private float chaseCloseMinSpeedMultiplier = 0.55f;

        [Header("Debug")]
        [Tooltip("锁定/丢失目标时输出 Console 日志（也可由 WormDodgeDirector 开启运行时调试）。")]
        [SerializeField] private bool logChaseTargeting;

#if UNITY_EDITOR
        [Header("Editor Gizmo")]
        [SerializeField] private bool drawRangeGizmos = true;
        [SerializeField] private bool drawChaseTargetLink = true;
        [SerializeField] private Color chasePlanarRangeColor = new Color(1f, 0.45f, 0.15f, 0.95f);
        [SerializeField] private Color chaseVerticalBandColor = new Color(1f, 0.75f, 0.2f, 0.45f);
        [SerializeField] private Color chaseTargetLinkColor = new Color(1f, 0.25f, 0.2f, 0.95f);
        [SerializeField] private float chaseTargetLinkNodeRadius = 0.35f;
#endif

        Quaternion[] _alignOffsets;
        readonly WormTrailSampler _trail = new WormTrailSampler();

        float _headPathDistance;
        int _pathMoveSign = 1;
        bool _isPlaying;
        MoveMode _moveMode = MoveMode.Patrol;
        WormChaseTarget _activeChaseTarget;
        Vector3 _chaseAimPosition;
        float _nextChaseTargetRefreshAt;
        float _nextPatrolScanAt;
        float _patrolTargetScanBlockedUntil;
        PatrolTargetPickerDelegate _patrolTargetPicker;

        float _joinTargetDistance;
        float _turnAroundElapsed;
        int _pendingPathMoveSign = 1;
        MoveMode _resumeMoveMode = MoveMode.Patrol;
        Quaternion _turnStartHeadRotation;
        Vector3 _turnTargetForward;

        public bool IsPlaying => _isPlaying;
        public bool IsChasing => _moveMode == MoveMode.Chase;
        public bool IsReturningToPath => false;
        public bool IsPatrolling => _moveMode == MoveMode.Patrol || _moveMode == MoveMode.JoinPath;
        public WormChaseTarget ActiveChaseTarget => _activeChaseTarget;
        public Vector3 ChaseAimPosition => _chaseAimPosition;
        public float ChaseTimeRemaining => 0f;
        public int SegmentCount => segmentsHeadToTail != null ? segmentsHeadToTail.Length : 0;
        public Transform GetSegmentTransform(int index)
        {
            if (segmentsHeadToTail == null || index < 0 || index >= segmentsHeadToTail.Length)
                return null;

            return segmentsHeadToTail[index];
        }

        public float SegmentSpacing => segmentSpacing;
        public float ChaseDetectRadius => Mathf.Max(0.5f, chaseDetectRadius);
        public float ChaseMaxVerticalDelta => Mathf.Max(0.5f, chaseMaxVerticalDelta);

        public void BlockPatrolTargetScan(float seconds)
        {
            if (seconds <= 0f)
                return;

            _patrolTargetScanBlockedUntil = Mathf.Max(_patrolTargetScanBlockedUntil, Time.time + seconds);
        }
        public float HeadPathDistance => _headPathDistance;
        public Transform HeadTransform => GetHead();

        /// <summary>目标是否在虫头索敌范围内（水平距离 + 高度差）。</summary>
        public bool IsTargetWithinChaseRange(Vector3 worldPos) =>
            IsTargetWithinChaseRange(worldPos, out _);

        public bool IsTargetWithinChaseRange(WormChaseTarget target) =>
            target != null && IsTargetWithinChaseRange(GetChaseTargetSamplePosition(target));

        public bool IsTargetWithinChaseRange(Vector3 worldPos, out float planarDistance)
        {
            planarDistance = GetChasePlanarDistance(worldPos);
            if (planarDistance > ChaseDetectRadius)
                return false;

            return GetChaseVerticalDelta(worldPos) <= ChaseMaxVerticalDelta;
        }

        public float GetChaseVerticalDelta(Vector3 worldPos)
        {
            var head = GetHead();
            var originY = head != null ? head.position.y : transform.position.y;
            return Mathf.Abs(worldPos.y - originY);
        }

        public static Vector3 GetChaseTargetSamplePosition(WormChaseTarget target)
        {
            if (target == null)
                return Vector3.zero;

            var participant = target.GetComponent<WormDodgeParticipant>() ??
                              target.GetComponentInParent<WormDodgeParticipant>();
            if (participant != null)
                return participant.GetChaseDetectPosition();

            return target.transform.position;
        }

        /// <summary>虫头到目标的水平距离（与索敌判定一致，忽略高度差）。</summary>
        public float GetChasePlanarDistance(Vector3 worldPos)
        {
            var head = GetHead();
            var origin = head != null ? head.position : transform.position;
            return Mathf.Sqrt(PlanarDistanceSqr(origin, worldPos));
        }

        /// <summary>虫头到目标的三维直线距离（仅用于调试对比）。</summary>
        public float GetChaseWorldDistance(Vector3 worldPos)
        {
            var head = GetHead();
            var origin = head != null ? head.position : transform.position;
            return Vector3.Distance(origin, worldPos);
        }

        static float PlanarDistanceSqr(Vector3 a, Vector3 b)
        {
            var planar = a - b;
            planar.y = 0f;
            return planar.sqrMagnitude;
        }

        bool ShouldLogChaseTargeting() =>
            logChaseTargeting || WormDodgeRuntimeDebug.InspectorFieldsEnabled;

        void LogChaseAcquired(WormChaseTarget target, string source, bool skipRangeCheck)
        {
            if (!ShouldLogChaseTargeting() || target == null)
                return;

            var pos = GetChaseTargetSamplePosition(target);
            var planar = GetChasePlanarDistance(pos);
            var world = GetChaseWorldDistance(pos);
            var inRange = IsTargetWithinChaseRange(pos);
            Debug.Log(
                $"[WormChainMotor] 锁定目标: {FormatChaseTarget(target)} | 来源={source} | " +
                $"平面距离={planar:F2}m | 高度差={GetChaseVerticalDelta(pos):F2}m | 三维距离={world:F2}m | " +
                $"索敌半径={ChaseDetectRadius:F2}m | 最大高度差={ChaseMaxVerticalDelta:F2}m | " +
                $"skipRangeCheck={skipRangeCheck} | 范围内={inRange}",
                this);
        }

        void LogChaseRejected(WormChaseTarget target, string source, bool skipRangeCheck)
        {
            if (!ShouldLogChaseTargeting() || target == null)
                return;

            var pos = target.transform.position;
            Debug.Log(
                $"[WormChainMotor] 拒绝锁定: {FormatChaseTarget(target)} | 来源={source} | " +
                $"平面距离={GetChasePlanarDistance(pos):F2}m | 三维距离={GetChaseWorldDistance(pos):F2}m | " +
                $"索敌半径={ChaseDetectRadius:F2}m | skipRangeCheck={skipRangeCheck}",
                this);
        }

        void LogChaseReleased(WormChaseTarget target, string reason)
        {
            if (!ShouldLogChaseTargeting() || target == null)
                return;

            var pos = target.transform.position;
            Debug.Log(
                $"[WormChainMotor] 丢失目标: {FormatChaseTarget(target)} | 原因={reason} | " +
                $"平面距离={GetChasePlanarDistance(pos):F2}m | 三维距离={GetChaseWorldDistance(pos):F2}m | " +
                $"索敌半径={ChaseDetectRadius:F2}m",
                this);
        }

        static string FormatChaseTarget(WormChaseTarget target)
        {
            if (target == null)
                return "(null)";

            var participant = target.GetComponent<WormDodgeParticipant>() ??
                              target.GetComponentInParent<WormDodgeParticipant>();
            if (participant != null)
                return participant.IsPlayer ? "玩家" : participant.GetLeaderboardDisplayName();

            return target.name;
        }

        public event Action<WormChaseTarget> ChaseStarted;
        public event Action ChaseEnded;
        public event Action<WormChaseTarget> ChaseTargetOutOfRange;

        void Awake()
        {
            ResolveSegments();
            if (autoDetectSegmentSpacing)
                TryAutoDetectSegmentSpacing();
            CaptureSegmentAlignment();
        }

        void Start()
        {
            ResetToPathStart();
            if (playOnStart)
                BeginRunPatrol();
        }

        void OnDisable()
        {
            UnsubscribeChaseTarget();
            _patrolTargetPicker = null;
        }

        void Update()
        {
            if (!_isPlaying || SegmentCount == 0)
                return;

            var dt = Time.deltaTime;
            switch (_moveMode)
            {
                case MoveMode.Chase:
                    TickChaseMovement(dt);
                    break;
                case MoveMode.JoinPath:
                    TickJoinPathMovement(dt);
                    break;
                case MoveMode.TurningBack:
                    TickTurningBack(dt);
                    break;
                default:
                    if (path != null)
                    {
                        TickPathMovement(dt);
                        TickPatrolTargetScan();
                    }
                    break;
            }
        }

        /// <summary>开局：先汇入最近路点，再沿路径巡逻。</summary>
        public void BeginRunPatrol()
        {
            EndChaseImmediate(silent: true);
            _isPlaying = true;
            _patrolTargetScanBlockedUntil = 0f;
            _pathMoveSign = 1;
            _trail.Clear();

            if (path == null || path.WaypointCount < 2)
            {
                _moveMode = MoveMode.Patrol;
                return;
            }

            var head = GetHead();
            var headPos = head != null ? head.position : transform.position;
            var nearestWp = path.FindNearestWaypointIndex(headPos);
            _joinTargetDistance = path.GetCumulativeDistance(nearestWp);
            _headPathDistance = path.FindClosestDistance(headPos);

            if (Mathf.Abs(_headPathDistance - _joinTargetDistance) <=
                Mathf.Max(0.1f, joinPathArrivalTolerance))
            {
                _headPathDistance = _joinTargetDistance;
                _moveMode = MoveMode.Patrol;
                ApplySnakePoseFromPath(_headPathDistance, 1f);
                return;
            }

            _moveMode = MoveMode.JoinPath;
            if (head != null)
                _trail.Record(head.position);
        }

        public void SetPatrolTargetPicker(PatrolTargetPickerDelegate picker) => _patrolTargetPicker = picker;

        public void Play() => _isPlaying = true;

        public void Stop() => _isPlaying = false;

        public void ResetToPathStart()
        {
            EndChaseImmediate(silent: true);
            _isPlaying = false;
            _moveMode = MoveMode.Patrol;
            _headPathDistance = 0f;
            _pathMoveSign = 1;
            _trail.Clear();
            _patrolTargetScanBlockedUntil = 0f;

            if (path != null && path.WaypointCount > 0)
                ApplySnakePoseFromPath(_headPathDistance, 1f);
        }

        /// <summary>开始追踪目标；持续至目标出索敌范围、进入安全区或死亡。</summary>
        public bool BeginChase(WormChaseTarget target) => BeginChase(target, skipRangeCheck: false);

        public bool BeginChase(WormChaseTarget target, bool skipRangeCheck)
        {
            if (target == null || !target.IsAlive || target.IsInvincible || IsChaseTargetInSafeZone(target))
                return false;

            if (!skipRangeCheck && !IsTargetWithinChaseRange(target))
            {
                LogChaseRejected(target, "BeginChase", skipRangeCheck);
                return false;
            }

            UnsubscribeChaseTarget();
            _activeChaseTarget = target;
            _moveMode = MoveMode.Chase;
            _trail.Clear();
            _chaseAimPosition = target.transform.position;
            _nextChaseTargetRefreshAt = 0f;

            var head = GetHead();
            if (head != null)
                _trail.Record(head.position);

            _activeChaseTarget.Died += OnChaseTargetDied;
            LogChaseAcquired(target, "BeginChase", skipRangeCheck);
            ChaseStarted?.Invoke(_activeChaseTarget);
            return true;
        }

        public void CancelChase()
        {
            if (_moveMode != MoveMode.Chase)
                return;

            EndChaseAndResumePatrol(_activeChaseTarget, fireOutOfRangeEvent: true);
        }

        public bool IsTargetIgnored(WormChaseTarget target) => false;

        public void AutoCollectSegments()
        {
            ResolveSegments();
            if (autoDetectSegmentSpacing)
                TryAutoDetectSegmentSpacing();
            CaptureSegmentAlignment();
        }

        void OnChaseTargetDied(WormChaseTarget target, WormChaseDeathReason reason)
            => EndChaseAndResumePatrol();

        void EndChaseImmediate(bool silent)
        {
            UnsubscribeChaseTarget();
            _moveMode = MoveMode.Patrol;
            _trail.Clear();

            if (!silent)
                ChaseEnded?.Invoke();
        }

        void EndChaseAndResumePatrol(WormChaseTarget lostTarget = null, bool fireOutOfRangeEvent = false)
        {
            if (_moveMode != MoveMode.Chase)
                return;

            var endedTarget = lostTarget != null ? lostTarget : _activeChaseTarget;
            UnsubscribeChaseTarget();
            BlockPatrolTargetScan(postChasePatrolCooldownSeconds);
            RejoinPathAfterChase();

            if (fireOutOfRangeEvent && endedTarget != null)
                ChaseTargetOutOfRange?.Invoke(endedTarget);

            ChaseEnded?.Invoke();
        }

        void RejoinPathAfterChase()
        {
            _trail.Clear();

            if (path == null || path.WaypointCount < 2)
            {
                _moveMode = MoveMode.Patrol;
                return;
            }

            var headPos = GetHeadPosition();
            _headPathDistance = path.FindClosestDistance(headPos);
            var nearestWp = path.FindNearestWaypointIndex(headPos);
            _joinTargetDistance = path.GetCumulativeDistance(nearestWp);
            var joinTolerance = Mathf.Max(0.1f, joinPathArrivalTolerance);

            if (Mathf.Abs(_headPathDistance - _joinTargetDistance) > joinTolerance * 2f)
            {
                _moveMode = MoveMode.JoinPath;
                var head = GetHead();
                if (head != null)
                    _trail.Record(head.position);
                return;
            }

            _headPathDistance = _joinTargetDistance;
            _moveMode = MoveMode.Patrol;
            ApplySnakePoseFromPath(_headPathDistance, 1f);
        }

        void UnsubscribeChaseTarget()
        {
            if (_activeChaseTarget != null)
                _activeChaseTarget.Died -= OnChaseTargetDied;
            _activeChaseTarget = null;
        }

        void TickPatrolTargetScan()
        {
            if (_patrolTargetPicker == null || Time.time < _nextPatrolScanAt)
                return;

            if (Time.time < _patrolTargetScanBlockedUntil)
                return;

            _nextPatrolScanAt = Time.time + Mathf.Max(0.05f, patrolTargetScanInterval);
            var target = _patrolTargetPicker();
            if (target == null)
                return;

            BeginChase(target, skipRangeCheck: false);
        }

        void TickJoinPathMovement(float dt)
        {
            if (path == null || path.WaypointCount < 2)
            {
                _moveMode = MoveMode.Patrol;
                return;
            }

            var step = moveSpeed * dt;
            var delta = _joinTargetDistance - _headPathDistance;
            if (Mathf.Abs(delta) <= step + Mathf.Max(0.1f, joinPathArrivalTolerance))
            {
                _headPathDistance = _joinTargetDistance;
                _moveMode = MoveMode.Patrol;
                ApplySnakePoseFromPath(_headPathDistance, rotationLerpSpeed * dt, _pathMoveSign);
                return;
            }

            var sign = delta > 0f ? 1 : -1;
            if (!IsHeadFacingPathSign(sign))
            {
                BeginTurnAround(sign, MoveMode.JoinPath);
                return;
            }

            _headPathDistance += step * sign;
            ClampHeadDistanceForMode();
            ApplySnakePoseFromPath(_headPathDistance, rotationLerpSpeed * dt, sign);
        }

        void TickTurningBack(float dt)
        {
            var head = GetHead();
            if (head == null)
            {
                CompleteTurnAround();
                return;
            }

            _turnAroundElapsed += dt;
            var duration = Mathf.Max(0.05f, turnAroundSeconds);
            var t = Mathf.Clamp01(_turnAroundElapsed / duration);
            var targetRot = Quaternion.LookRotation(_turnTargetForward, Vector3.up);
            head.rotation = Quaternion.Slerp(_turnStartHeadRotation, targetRot, t);

            if (path != null && path.WaypointCount >= 2)
            {
                _headPathDistance += moveSpeed * dt * 0.55f * _pendingPathMoveSign;
                ClampHeadDistanceForMode();
                ApplySnakePoseFromPath(_headPathDistance, rotationLerpSpeed * dt, _pendingPathMoveSign);
            }

            if (t >= 1f)
                CompleteTurnAround();
        }

        void BeginTurnAround(int pendingSign, MoveMode resumeMode = MoveMode.Patrol)
        {
            _pendingPathMoveSign = pendingSign;
            _resumeMoveMode = resumeMode;
            var head = GetHead();
            if (head != null)
            {
                _turnStartHeadRotation = head.rotation;
                var fwd = head.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f)
                    fwd = head.forward;
                _turnTargetForward = (-fwd).normalized;
            }

            _turnAroundElapsed = 0f;
            _moveMode = MoveMode.TurningBack;
        }

        void CompleteTurnAround()
        {
            if (_resumeMoveMode == MoveMode.Patrol)
                _pathMoveSign = _pendingPathMoveSign;
            _moveMode = _resumeMoveMode;
        }

        bool IsHeadFacingPathSign(int desiredSign)
        {
            if (desiredSign == 0)
                return true;

            var head = GetHead();
            if (head == null || path == null)
                return true;

            var tangent = path.GetTangentAtDistance(_headPathDistance);
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-4f)
                return true;

            var forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f)
                return true;

            forward.Normalize();
            var pathDir = tangent.normalized * desiredSign;
            return Vector3.Dot(forward, pathDir) > 0.2f;
        }

        void RequestPathDirectionChange(int newSign)
        {
            if (newSign == _pathMoveSign)
                return;

            if (!IsHeadFacingPathSign(newSign))
            {
                BeginTurnAround(newSign, MoveMode.Patrol);
                return;
            }

            _pathMoveSign = newSign;
        }

        void TickChaseMovement(float dt)
        {
            if (_activeChaseTarget == null || !_activeChaseTarget.IsAlive)
            {
                EndChaseAndResumePatrol();
                return;
            }

            if (Time.time >= _nextChaseTargetRefreshAt && !RefreshChaseTargetState())
                return;

            var head = GetHead();
            if (head == null)
                return;

            var aim = GetChaseTargetSamplePosition(_activeChaseTarget);
            var planarDist = Mathf.Sqrt(PlanarDistanceSqr(head.position, aim));
            var slowDist = Mathf.Max(0.35f, chaseCloseSlowDistance);
            var speedMul = planarDist >= slowDist
                ? 1f
                : Mathf.Max(chaseCloseMinSpeedMultiplier, planarDist / slowDist);

            MoveHeadTowardPlanar(aim, dt, moveSpeed * speedMul);
            AppendChasePathCreep(head, dt, speedMul);

            _trail.Record(head.position);
            _trail.TrimToLength(SegmentCount * segmentSpacing);
            ApplySnakePoseFromTrail(rotationLerpSpeed * dt);
        }

        /// <summary>刷新追击目标位置；若目标离开索敌范围、进入安全区或无敌则放弃追击。</summary>
        bool RefreshChaseTargetState()
        {
            _nextChaseTargetRefreshAt = Time.time + Mathf.Max(0.05f, chaseTargetRefreshInterval);

            if (_activeChaseTarget == null || !_activeChaseTarget.IsAlive)
            {
                EndChaseAndResumePatrol();
                return false;
            }

            _chaseAimPosition = GetChaseTargetSamplePosition(_activeChaseTarget);

            if (_activeChaseTarget.IsInvincible ||
                IsChaseTargetInSafeZone(_activeChaseTarget) ||
                !IsTargetWithinChaseRange(_chaseAimPosition))
            {
                var lostTarget = _activeChaseTarget;
                LogChaseReleased(lostTarget, "OutOfRangeOrSafeZone");
                EndChaseAndResumePatrol(lostTarget, fireOutOfRangeEvent: true);
                return false;
            }

            return true;
        }

        void AppendChasePathCreep(Transform head, float dt, float speedScale = 1f)
        {
            if (path == null || path.WaypointCount < 2 || head == null || dt <= 0f)
                return;

            var tangent = path.GetTangentAtDistance(path.FindClosestDistance(head.position));
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-4f)
                return;

            var creep = moveSpeed * 0.25f * dt * Mathf.Max(0.35f, speedScale);
            head.position += tangent.normalized * creep;
        }

        Vector3 GetHeadPosition()
        {
            var head = GetHead();
            return head != null ? head.position : transform.position;
        }

        void MoveHeadTowardPlanar(Vector3 destination, float dt, float speed)
        {
            var head = GetHead();
            if (head == null)
                return;

            var current = head.position;
            var flatDest = new Vector3(destination.x, current.y, destination.z);
            var toTarget = flatDest - current;
            var step = Mathf.Max(0.01f, speed) * dt;
            if (toTarget.sqrMagnitude > step * step)
                head.position = current + toTarget.normalized * step;
            else if (toTarget.sqrMagnitude > 1e-6f)
                head.position = flatDest;
        }

        void MoveHeadTowardPlanar(Vector3 destination, float dt)
        {
            MoveHeadTowardPlanar(destination, dt, moveSpeed);
        }

        void MoveHeadToward(Vector3 destination, float dt)
        {
            MoveHeadTowardPlanar(destination, dt);
        }

        static bool IsChaseTargetInSafeZone(WormChaseTarget target)
        {
            if (target == null)
                return false;

            var participant = target.GetComponent<WormDodgeParticipant>();
            if (participant == null)
                participant = target.GetComponentInParent<WormDodgeParticipant>();

            return participant != null && participant.IsInSafeZone;
        }

        /// <summary>由 <see cref="WormCatchTrigger"/> 在 trigger 碰撞时调用。</summary>
        public bool NotifyTriggerCatch(WormChaseTarget target)
        {
            if (target == null || !target.IsAlive || target.IsInvincible || !_isPlaying)
                return false;

            target.Die(WormChaseDeathReason.CaughtByWorm);
            return true;
        }

        void TickPathMovement(float dt)
        {
            if (path.WaypointCount < 2)
                return;

            if (_pathMoveSign < 0 && !IsHeadFacingPathSign(-1))
            {
                BeginTurnAround(-1, MoveMode.Patrol);
                return;
            }

            if (_pathMoveSign > 0 && !IsHeadFacingPathSign(1))
            {
                BeginTurnAround(1, MoveMode.Patrol);
                return;
            }

            _headPathDistance += moveSpeed * dt * _pathMoveSign;
            ClampHeadDistanceForMode();
            ApplySnakePoseFromPath(_headPathDistance, rotationLerpSpeed * dt);
        }

        void ClampHeadDistanceForMode()
        {
            if (path == null)
                return;

            BounceHeadDistanceAtPathEnds();
        }

        void BounceHeadDistanceAtPathEnds()
        {
            if (path == null)
                return;

            var len = path.TotalLength;
            if (len <= 1e-5f)
                return;

            if (_headPathDistance >= len)
            {
                _headPathDistance = len;
                ReversePathDirectionAtEndpoint(-1);
            }
            else if (_headPathDistance <= 0f)
            {
                _headPathDistance = 0f;
                ReversePathDirectionAtEndpoint(1);
            }
        }

        void ReversePathDirectionAtEndpoint(int newSign)
        {
            if (newSign == 0)
                return;

            if (newSign == _pathMoveSign)
            {
                SnapHeadFacingPathSign(newSign);
                return;
            }

            _pathMoveSign = newSign;
            _pendingPathMoveSign = newSign;
            if (_moveMode == MoveMode.TurningBack)
                _moveMode = _resumeMoveMode;

            SnapHeadFacingPathSign(newSign);
            ApplySnakePoseFromPath(_headPathDistance, 9999f, newSign);
        }

        void SnapHeadFacingPathSign(int sign)
        {
            var head = GetHead();
            if (head == null || path == null || sign == 0)
                return;

            var tangent = path.GetTangentAtDistance(_headPathDistance);
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-6f)
                return;

            head.rotation = Quaternion.LookRotation(tangent.normalized * sign, Vector3.up);
        }

        void ApplySnakePoseFromPath(float headDistance, float rotationStep, int? facingSignOverride = null)
        {
            var count = SegmentCount;
            if (count == 0 || _alignOffsets == null || _alignOffsets.Length != count)
                CaptureSegmentAlignment();

            var facingSign = facingSignOverride ?? ResolvePathFacingSign();

            for (var i = 0; i < count; i++)
            {
                var seg = segmentsHeadToTail[i];
                if (seg == null)
                    continue;

                var segDistance = headDistance - i * segmentSpacing * facingSign;
                segDistance = Mathf.Clamp(segDistance, 0f, path.TotalLength);

                var targetPos = path.SampleAtDistance(segDistance, out var tangent);
                if (facingSign < 0)
                    tangent = -tangent;

                ApplySegmentPose(seg, i, targetPos, tangent, rotationStep);
            }
        }

        int ResolvePathFacingSign()
        {
            if (_moveMode == MoveMode.TurningBack)
                return _pendingPathMoveSign != 0 ? _pendingPathMoveSign : 1;

            return _pathMoveSign != 0 ? _pathMoveSign : 1;
        }

        void ApplySnakePoseFromTrail(float rotationStep)
        {
            var count = SegmentCount;
            if (count == 0 || _alignOffsets == null || _alignOffsets.Length != count)
                CaptureSegmentAlignment();

            for (var i = 0; i < count; i++)
            {
                var seg = segmentsHeadToTail[i];
                if (seg == null)
                    continue;

                var behind = i * segmentSpacing;
                if (!_trail.TrySample(behind, out var targetPos, out var tangent))
                {
                    if (i == 0 && GetHead() != null)
                    {
                        targetPos = GetHead().position;
                        tangent = GetHead().forward;
                    }
                    else
                        continue;
                }

                ApplySegmentPose(seg, i, targetPos, tangent, rotationStep);
            }
        }

        void ApplySegmentPose(Transform seg, int index, Vector3 targetPos, Vector3 tangent, float rotationStep)
        {
            seg.position = targetPos;
            var targetRot = BuildTangentRotation(tangent, _alignOffsets[index]);

            if (index == 0)
            {
                seg.rotation = rotationStep >= 1f
                    ? targetRot
                    : Quaternion.RotateTowards(seg.rotation, targetRot, rotationStep);
                return;
            }

            var prev = segmentsHeadToTail[index - 1];
            if (prev == null)
                return;

            targetRot = ClampJointRotation(prev.rotation, targetRot, maxJointAngle);
            seg.rotation = rotationStep >= 1f
                ? targetRot
                : Quaternion.RotateTowards(seg.rotation, targetRot, rotationStep);
        }

        Transform GetHead()
        {
            return SegmentCount > 0 ? segmentsHeadToTail[0] : null;
        }

        void ResolveSegments()
        {
            if (segmentsHeadToTail != null && segmentsHeadToTail.Length > 0)
                return;

            var list = new List<Transform>();
            var head = transform.Find("tou");
            if (head != null)
                list.Add(head);

            for (var i = 1; i < 64; i++)
            {
                var body = transform.Find($"shenti{i}");
                if (body == null)
                    break;
                list.Add(body);
            }

            if (list.Count <= 1)
            {
                for (var c = 0; c < transform.childCount; c++)
                {
                    var child = transform.GetChild(c);
                    if (child.name == "tou" || child.name.StartsWith("shenti", StringComparison.Ordinal))
                        list.Add(child);
                }

                list.Sort(CompareSegmentOrder);
            }

            segmentsHeadToTail = list.ToArray();
        }

        static int CompareSegmentOrder(Transform a, Transform b)
        {
            if (a.name == "tou") return -1;
            if (b.name == "tou") return 1;

            var ai = ParseBodyIndex(a.name);
            var bi = ParseBodyIndex(b.name);
            return ai.CompareTo(bi);
        }

        static int ParseBodyIndex(string name)
        {
            if (!name.StartsWith("shenti", StringComparison.Ordinal))
                return int.MaxValue;

            return int.TryParse(name.Substring(6), out var idx) ? idx : int.MaxValue;
        }

        void TryAutoDetectSegmentSpacing()
        {
            if (SegmentCount < 2)
                return;

            var sum = 0f;
            var count = 0;
            for (var i = 1; i < SegmentCount; i++)
            {
                var a = segmentsHeadToTail[i - 1];
                var b = segmentsHeadToTail[i];
                if (a == null || b == null)
                    continue;

                var d = Vector3.Distance(a.position, b.position);
                if (d > 0.01f)
                {
                    sum += d;
                    count++;
                }
            }

            if (count > 0)
                segmentSpacing = sum / count;
        }

        void CaptureSegmentAlignment()
        {
            var count = SegmentCount;
            _alignOffsets = new Quaternion[count];
            if (count == 0)
                return;

            Vector3 referenceForward;
            if (count >= 2 && segmentsHeadToTail[0] != null && segmentsHeadToTail[1] != null)
                referenceForward = segmentsHeadToTail[1].position - segmentsHeadToTail[0].position;
            else if (segmentsHeadToTail[0] != null)
                referenceForward = segmentsHeadToTail[0].forward;
            else
                referenceForward = transform.right;

            if (referenceForward.sqrMagnitude < 1e-6f)
                referenceForward = Vector3.right;
            referenceForward.Normalize();

            if (path != null && path.WaypointCount >= 2 &&
                path.TryGetSegmentDirection(0, 1, out var pathDir))
            {
                if (Vector3.Dot(referenceForward, pathDir) < 0f)
                    referenceForward = -referenceForward;
            }

            var referenceLook = Quaternion.LookRotation(referenceForward, Vector3.up);
            for (var i = 0; i < count; i++)
            {
                var seg = segmentsHeadToTail[i];
                if (seg == null)
                    continue;
                _alignOffsets[i] = Quaternion.Inverse(referenceLook) * seg.rotation;
            }
        }

        static Quaternion BuildTangentRotation(Vector3 tangent, Quaternion alignOffset)
        {
            if (tangent.sqrMagnitude < 1e-6f)
                return alignOffset;

            tangent.Normalize();
            var up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(tangent, up)) > 0.98f)
                up = Vector3.forward;

            return Quaternion.LookRotation(tangent, up) * alignOffset;
        }

        static Quaternion ClampJointRotation(Quaternion prevWorld, Quaternion desiredWorld, float maxAngle)
        {
            var relative = Quaternion.Inverse(prevWorld) * desiredWorld;
            var euler = relative.eulerAngles;
            var pitch = NormalizeAngle(euler.x);
            var yaw = NormalizeAngle(euler.z);
            pitch = Mathf.Clamp(pitch, -maxAngle, maxAngle);
            yaw = Mathf.Clamp(yaw, -maxAngle, maxAngle);
            return prevWorld * Quaternion.Euler(pitch, 0f, yaw);
        }

        static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Collect Segments")]
        void EditorAutoCollectContext() => AutoCollectSegments();

        void OnDrawGizmos()
        {
            if (!drawRangeGizmos && !drawChaseTargetLink)
                return;

            if (!Application.isPlaying && (segmentsHeadToTail == null || SegmentCount == 0))
                ResolveSegments();

            var head = GetHead();
            var origin = head != null ? head.position : transform.position;

            if (drawRangeGizmos)
            {
                Gizmos.color = chasePlanarRangeColor;
                DrawPlanarRangeGizmo(origin, ChaseDetectRadius);

                Gizmos.color = chaseVerticalBandColor;
                DrawPlanarRangeGizmo(origin + Vector3.up * ChaseMaxVerticalDelta, ChaseDetectRadius);
                DrawPlanarRangeGizmo(origin + Vector3.down * ChaseMaxVerticalDelta, ChaseDetectRadius);
                Gizmos.DrawLine(
                    origin + new Vector3(ChaseDetectRadius, ChaseMaxVerticalDelta, 0f),
                    origin + new Vector3(ChaseDetectRadius, -ChaseMaxVerticalDelta, 0f));
            }

            DrawChaseTargetLinkGizmo();
        }

        void OnDrawGizmosSelected()
        {
            if (!drawRangeGizmos)
                return;

            var head = GetHead();
            var origin = head != null ? head.position : transform.position;

            if (Application.isPlaying && _moveMode == MoveMode.JoinPath && path != null)
            {
                var joinPos = path.SampleAtDistance(_joinTargetDistance, out _);
                Gizmos.color = new Color(0.2f, 0.85f, 0.35f, 0.9f);
                Gizmos.DrawWireSphere(joinPos, 0.35f);
                if (head != null)
                    Gizmos.DrawLine(head.position, joinPos);
            }

            if (Application.isPlaying)
            {
                Gizmos.color = Color.white;
                var labelPos = origin + Vector3.up * 0.6f;
                UnityEditor.Handles.Label(
                    labelPos,
                    $"Chase planar={ChaseDetectRadius:F1}m  vertical=±{ChaseMaxVerticalDelta:F1}m  mode={_moveMode}");
            }
        }

        void DrawChaseTargetLinkGizmo()
        {
            if (!drawChaseTargetLink)
                return;

            var head = GetHead() ?? transform.Find("tou");
            if (head == null)
                return;

            if (!Application.isPlaying)
                return;

            if (_activeChaseTarget == null && !IsChasing)
                return;

            if (_activeChaseTarget == null)
                return;

            var targetPos = GetChaseTargetSamplePosition(_activeChaseTarget);
            Gizmos.color = chaseTargetLinkColor;
            Gizmos.DrawLine(head.position, targetPos);
            Gizmos.DrawWireSphere(targetPos, chaseTargetLinkNodeRadius);
        }

        static void DrawPlanarRangeGizmo(Vector3 center, float radius, int segments = 48)
        {
            if (radius <= 0f)
                return;

            var prev = center + new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, point);
                prev = point;
            }
        }
#endif
    }
}

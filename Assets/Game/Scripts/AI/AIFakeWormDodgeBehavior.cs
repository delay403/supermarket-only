using System.Collections;
using Game.EscapeSnake;
using Game.Player;
using Game.WormDodge;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// 躲避虫子 AI：先到最近路点（起点），再朝终点前进；检测范围内有虫则跳安全区或后退。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-36)]
    public sealed class AIFakeWormDodgeBehavior : MonoBehaviour
    {
        enum Phase
        {
            IdleWander,
            MoveToStart,
            MoveToEnd,
            Backpedal,
            ApproachSafeZone,
            Jumping,
            SafeZoneStay,
            ReturnToNavMesh,
        }

        enum IdleSubPhase { Moving, Paused }

        [Header("References")]
        [SerializeField] private NavMeshAIPatrolAgent patrol;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerRig rig;
        [SerializeField] private WormDodgeAiPath aiPath;

        [Header("Identity")]
        [SerializeField] private string displayName = "AI跑者";

        [Header("Path")]
        [SerializeField] private bool autoFindPathOnAwake = true;

        [Header("Movement")]
        [SerializeField] private float arrivalTolerance = 1.1f;
        [SerializeField] private float repathIntervalSeconds = 0.25f;

        [Header("Threat detect (worm + safe zone)")]
        [SerializeField] private float threatDetectRadius = 20f;
        [Tooltip("AI 反应虫子的最大高度差（米）。")]
        [SerializeField] private float threatMaxVerticalDelta = 4f;
        [Tooltip("虫子离开检测范围后，延迟恢复前进（秒）。")]
        [SerializeField] private float threatReleaseDelaySeconds = 0.65f;
        [Tooltip("非躲避阶段时，威胁检测的最小间隔（秒）。躲避/释放延迟期间仍每帧检测。")]
        [SerializeField] private float threatEvaluationInterval = 0.15f;
        [Tooltip("躲避时可选安全区的最大三维距离（米）。")]
        [SerializeField] private float maxSafeZoneEvasionDistance = 14f;
        [Tooltip("躲避时允许跳上的最大高度差（米）。")]
        [SerializeField] private float maxSafeZoneVerticalRise = 5.5f;

        [Header("Jump")]
        [Tooltip("与目标高度差超过该值且 NavMesh 无法直达时，才使用弧线跳跃。")]
        [SerializeField] private float jumpRequiredHeightDelta = 1.25f;
        [SerializeField] private float jumpDurationSeconds = 0.55f;
        [SerializeField] private float jumpArcHeight = 1.35f;
        [SerializeField] private float navMeshSampleRadius = 2.5f;
        [Tooltip("安全区内未检测到虫子后，延迟跳回 NavMesh 的最短/最长秒数。")]
        [SerializeField] private float safeZoneReturnDelayMin = 0.5f;
        [SerializeField] private float safeZoneReturnDelayMax = 3f;
        [Tooltip("从安全区跳回 NavMesh 后，短时间内不再跳上安全区，避免来回弹跳。")]
        [SerializeField] private float safeZoneReturnReentryCooldown = 2f;

        [Header("Lobby idle")]
        [SerializeField] private float lobbyWanderRadius = 5f;
        [SerializeField] private float lobbyMoveSecondsMin = 1f;
        [SerializeField] private float lobbyMoveSecondsMax = 3f;
        [SerializeField] private float lobbyPauseSeconds = 2f;

        [Header("Animator")]
        [SerializeField] private string speedFloatParam = "Velocity";
        [SerializeField] private string movingBoolParam = "IsMoving";
        [SerializeField] private string jumpTriggerParam = "Jump";
        [SerializeField] private string isGroundBoolParam = "IsGround";

#if UNITY_EDITOR
        [Header("Editor Gizmo")]
        [SerializeField] private bool drawRangeGizmos = true;
        [SerializeField] private bool drawMoveTargetLink = true;
        [SerializeField] private Color threatDetectGizmoColor = new Color(1f, 0.55f, 0.15f, 0.85f);
        [SerializeField] private Color safeZonePickGizmoColor = new Color(0.2f, 0.85f, 0.45f, 0.55f);
        [SerializeField] private Color safeZoneVerticalGizmoColor = new Color(0.25f, 0.75f, 1f, 0.75f);
        [SerializeField] private Color moveTargetLinkColor = new Color(0.25f, 0.65f, 1f, 0.95f);
        [SerializeField] private Color wormThreatLinkColor = new Color(1f, 0.2f, 0.15f, 0.95f);
        [SerializeField] private Color safeZoneLinkColor = new Color(0.15f, 0.95f, 0.45f, 0.95f);
#endif

        [Header("Debug (Runtime)")]
        [SerializeField] private bool debugWormInDetectRange;
        [SerializeField] private bool debugWormChasingMe;
        [SerializeField] private float debugWormPlanarDistance;
        [SerializeField] private float debugWormVerticalDelta;
        [SerializeField] private bool debugInSafeZone;
        [SerializeField] private Phase debugPhase;

        Phase _phase = Phase.IdleWander;
        IdleSubPhase _idleSubPhase = IdleSubPhase.Paused;
        bool _runActive;
        float _idlePhaseEndsAt;
        Vector3 _idleWanderAnchor;
        int _startWaypointIndex;
        int _pathIndex;
        int _pathIndexAtEvasionStart;
        float _nextRepathAt;
        Vector3 _activeNavDestination;
        bool _hasActiveNavDestination;

        WormSafeZone _activeSafeZone;
        WormDodgeParticipant _participant;
        Coroutine _jumpRoutine;

        Vector3 _moveTargetGizmo;
        bool _hasMoveTargetGizmo;
        float _wormThreatReleaseUntil;
        Vector3 _navMeshReturnPoint;
        bool _hasNavMeshReturnPoint;
        bool _safeZoneReturnScheduled;
        float _safeZoneReturnAllowedAt;
        float _safeZoneReentryBlockedUntil;
        int _isGroundBoolId;
        bool _hasIsGroundBool;

        static WormChainMotor[] _wormMotorCache;
        static float _wormMotorsCacheAt;
        WormChainMotor _primaryThreatMotor;
        static WormSafeZone[] _zoneCache;
        static float _zoneCacheAt;
        NavMeshPath _navPathScratch;

        int _threatEvalFrame = -1;
        bool _cachedThreatInRange;
        bool _cachedWormChasingMe;
        bool _staleThreatInRange;
        float _nextThreatEvalTime;
        bool _animatorGroundedApplied = true;

        Transform ActorTransform => agent != null ? agent.transform : transform;
        public string DisplayName => displayName;

        bool IsEvasionPhase =>
            _phase == Phase.Backpedal ||
            _phase == Phase.ApproachSafeZone ||
            _phase == Phase.Jumping ||
            _phase == Phase.SafeZoneStay;

        bool IsPathMovementPaused => _phase == Phase.Jumping || _phase == Phase.SafeZoneStay;

        bool CanAttemptSafeZoneEvasion => Time.time >= _safeZoneReentryBlockedUntil;

        public void SetDisplayName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                displayName = name.Trim();
        }

        void Awake()
        {
            if (agent == null) agent = GetComponentInChildren<NavMeshAgent>();
            if (patrol == null) patrol = GetComponentInChildren<NavMeshAIPatrolAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (rig == null) rig = GetComponent<PlayerRig>();
            if (autoFindPathOnAwake && aiPath == null)
                aiPath = FindNearestAiPath(ActorTransform.position);
            _participant = GetComponentInChildren<WormDodgeParticipant>();
            if (patrol != null) patrol.enabled = false;
            CacheAnimatorGroundParam();
        }

        void OnEnable()
        {
            WormDodgeDirector.RunReset += OnDirectorRunReset;
            WormDodgeDirector.RunStarted += OnDirectorRunStarted;
        }

        void OnDisable()
        {
            WormDodgeDirector.RunReset -= OnDirectorRunReset;
            WormDodgeDirector.RunStarted -= OnDirectorRunStarted;
        }

        static void OnDirectorRunReset() => InvalidateWormMotorCache();

        void OnDirectorRunStarted()
        {
            InvalidateWormMotorCache();
            _primaryThreatMotor = null;
            _wormThreatReleaseUntil = 0f;
            InvalidateThreatCache();
        }

        static void InvalidateWormMotorCache()
        {
            _wormMotorCache = null;
            _wormMotorsCacheAt = 0f;
        }

        void CacheAnimatorGroundParam()
        {
            _hasIsGroundBool = false;
            if (animator == null || string.IsNullOrEmpty(isGroundBoolParam))
                return;

            _isGroundBoolId = Animator.StringToHash(isGroundBoolParam);
            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.type != AnimatorControllerParameterType.Bool || p.nameHash != _isGroundBoolId)
                    continue;

                _hasIsGroundBool = true;
                break;
            }
        }

        void Start() => ResetToLobbyIdle();

        void Update()
        {
            if (_phase == Phase.Jumping)
                return;

            if (WormDodgeRuntimeDebug.InspectorFieldsEnabled)
                RefreshDebugFields();

            if (!IsRunActiveForAi())
            {
                TickIdleWander();
                patrol?.RefreshLocomotionAnimator();
                return;
            }

            EnsureParticipantResolved();

            if (!IsEvasionPhase && IsWormInDetectRange())
                TryHandleWormThreat();

            switch (_phase)
            {
                case Phase.MoveToStart:
                    TickMoveToStart();
                    break;
                case Phase.MoveToEnd:
                    TickMoveToEnd();
                    break;
                case Phase.Backpedal:
                    TickBackpedal();
                    break;
                case Phase.ApproachSafeZone:
                    TickApproachSafeZone();
                    break;
                case Phase.SafeZoneStay:
                    TickSafeZoneStay();
                    break;
            }

            if (_phase != Phase.Jumping)
                SetAnimatorGrounded(true);

            patrol?.RefreshLocomotionAnimator();
        }

        void RefreshDebugFields()
        {
            debugPhase = _phase;
            debugInSafeZone = _participant != null && _participant.IsInSafeZone;
            if (!IsRunActiveForAi())
            {
                debugWormInDetectRange = false;
                return;
            }

            RefreshThreatCache(forceFullEval: true);
            debugWormInDetectRange = _cachedThreatInRange;
            debugWormChasingMe = _cachedWormChasingMe;
        }

        void InvalidateThreatCache()
        {
            _threatEvalFrame = -1;
            _nextThreatEvalTime = 0f;
        }

        void RefreshThreatCache(bool forceFullEval)
        {
            if (_threatEvalFrame == Time.frameCount && !forceFullEval)
                return;

            var mustEvalEveryFrame = IsEvasionPhase || Time.time < _wormThreatReleaseUntil;
            if (!forceFullEval && !mustEvalEveryFrame)
            {
                if (Time.time < _nextThreatEvalTime)
                {
                    _cachedThreatInRange = _staleThreatInRange;
                    _threatEvalFrame = Time.frameCount;
                    return;
                }

                _nextThreatEvalTime = Time.time + Mathf.Max(0.05f, threatEvaluationInterval)
                    + (GetInstanceID() & 0x7) * (threatEvaluationInterval * 0.125f);
            }

            if (_threatEvalFrame == Time.frameCount && !forceFullEval)
                return;

            _threatEvalFrame = Time.frameCount;
            _cachedThreatInRange = EvaluateWormThreat(out _, out _, out _cachedWormChasingMe);
            _staleThreatInRange = _cachedThreatInRange;
        }

        bool IsWormInDetectRange()
        {
            RefreshThreatCache(forceFullEval: false);
            return _cachedThreatInRange;
        }

        bool IsRunActiveForAi()
        {
            if (_runActive)
                return true;

            var director = WormDodgeDirector.Instance;
            return director != null && director.IsRunActive;
        }

        public void Configure(
            WormDodgeAiPath path,
            NavMeshAgent navAgent = null,
            NavMeshAIPatrolAgent patrolAgent = null,
            Animator anim = null,
            PlayerRig playerRig = null)
        {
            if (path != null) aiPath = path;
            if (navAgent != null) agent = navAgent;
            if (patrolAgent != null) patrol = patrolAgent;
            if (anim != null) animator = anim;
            if (playerRig != null) rig = playerRig;
            CacheAnimatorGroundParam();
        }

        public void ResetToLobbyIdle(Transform sharedSpawn = null)
        {
            StopJumpRoutine();
            _runActive = false;
            _phase = Phase.IdleWander;
            _hasNavMeshReturnPoint = false;
            _safeZoneReturnScheduled = false;
            _safeZoneReentryBlockedUntil = 0f;
            ClearSafeZoneState();
            var spawnPos = ResolveSpawnPosition(sharedSpawn);
            _idleWanderAnchor = spawnPos;
            WarpToNavMesh(spawnPos);
            AgentStop();
            BeginNextIdleAction();
            SetLocomotionIdle();
        }

        public void ResetToRunStart(Transform sharedSpawn = null)
        {
            StopJumpRoutine();
            _runActive = true;
            ClearSafeZoneState();
            InvalidateThreatCache();
            EnsureParticipantResolved();

            if (TryResolveAssignedPathRunStart(out var spawnPos, out var startIndex))
            {
                _startWaypointIndex = startIndex;
                _pathIndex = startIndex;
                _phase = Phase.MoveToEnd;
            }
            else
            {
                spawnPos = ResolveSpawnPosition(sharedSpawn);
                if (aiPath != null && aiPath.WaypointCount > 0)
                {
                    _startWaypointIndex = aiPath.StartWaypointIndex;
                    _pathIndex = _startWaypointIndex;
                    _phase = Phase.MoveToStart;
                }
                else
                {
                    _phase = Phase.MoveToEnd;
                }
            }

            SyncAgentToWorldPosition(spawnPos);
            SyncParticipantToPathStart();

            if (_phase == Phase.MoveToEnd && aiPath != null)
                EnterMoveToEndPhase();

            _nextRepathAt = 0f;
            _hasActiveNavDestination = false;
            _wormThreatReleaseUntil = 0f;
            _hasNavMeshReturnPoint = false;
            _safeZoneReturnScheduled = false;
            _safeZoneReentryBlockedUntil = 0f;
            _primaryThreatMotor = null;
            InvalidateWormMotorCache();
            AgentResetPath();
            AgentResume();
            SetLocomotionIdle();
        }

        bool TryResolveAssignedPathRunStart(out Vector3 spawnPos, out int startIndex)
        {
            spawnPos = default;
            startIndex = 0;
            if (aiPath == null || aiPath.WaypointCount == 0)
                return false;

            startIndex = aiPath.StartWaypointIndex;
            spawnPos = ResolvePointOnNavMesh(aiPath.GetWaypointWorld(startIndex));
            return true;
        }

        void SyncParticipantToPathStart()
        {
            if (_participant == null || aiPath == null || aiPath.WaypointCount == 0)
                return;

            var startTransform = aiPath.GetWaypointTransform(aiPath.StartWaypointIndex);
            if (startTransform != null)
                _participant.TeleportTo(startTransform);
        }

        static WormDodgeAiPath FindNearestAiPath(Vector3 worldPos)
        {
#if UNITY_2023_1_OR_NEWER
            var paths = Object.FindObjectsByType<WormDodgeAiPath>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var paths = Object.FindObjectsOfType<WormDodgeAiPath>();
#endif
            WormDodgeAiPath best = null;
            var bestScore = float.MaxValue;
            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (path == null || path.WaypointCount == 0)
                    continue;

                var start = path.GetWaypointWorld(path.StartWaypointIndex);
                var score = (start - worldPos).sqrMagnitude;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = path;
            }

            return best;
        }

        void TickIdleWander()
        {
            if (agent == null || !agent.enabled || !EnsureAgentOnNavMesh())
                return;

            if (Time.time >= _idlePhaseEndsAt)
                BeginNextIdleAction();

            if (_idleSubPhase == IdleSubPhase.Paused)
            {
                AgentStop();
                SetLocomotionIdle();
                return;
            }

            AgentResume();
        }

        void BeginNextIdleAction()
        {
            if (Random.value < 0.4f)
            {
                _idleSubPhase = IdleSubPhase.Paused;
                _idlePhaseEndsAt = Time.time + lobbyPauseSeconds;
                AgentStop();
                return;
            }

            _idleSubPhase = IdleSubPhase.Moving;
            _idlePhaseEndsAt = Time.time + Random.Range(lobbyMoveSecondsMin, lobbyMoveSecondsMax);
            PickIdleDestination();
            AgentResume();
        }

        void PickIdleDestination()
        {
            if (agent == null || !EnsureAgentOnNavMesh())
                return;

            for (var i = 0; i < 8; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var dist = Random.Range(1f, lobbyWanderRadius);
                var dest = _idleWanderAnchor + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                if (NavMesh.SamplePosition(dest, out var hit, lobbyWanderRadius, NavMesh.AllAreas))
                    AgentSetDestination(hit.position);
            }
        }

        void TickMoveToStart()
        {
            if (aiPath == null || !EnsureAgentOnNavMesh())
                return;

            if (IsWormInDetectRange())
            {
                TryHandleWormThreat();
                return;
            }

            var target = ResolveWaypointOnNavMesh(_startWaypointIndex);
            MoveToward(target);

            if (HasReachedWaypoint(target))
                EnterMoveToEndPhase();
        }

        void EnterMoveToEndPhase()
        {
            _pathIndex = _startWaypointIndex;
            _phase = Phase.MoveToEnd;
            _nextRepathAt = 0f;
            _hasActiveNavDestination = false;
            AgentResetPath();
            AgentResume();

            if (aiPath == null)
                return;

            var endIndex = aiPath.EndWaypointIndex;
            while (_pathIndex < endIndex)
            {
                var waypoint = ResolveWaypointOnNavMesh(_pathIndex);
                if (!HasReachedWaypoint(waypoint))
                    break;

                _pathIndex++;
            }
        }

        void TickMoveToEnd()
        {
            if (aiPath == null || !EnsureAgentOnNavMesh())
                return;

            if (IsWormInDetectRange())
            {
                TryHandleWormThreat();
                return;
            }

            var endIndex = aiPath.EndWaypointIndex;
            var target = ResolveWaypointOnNavMesh(_pathIndex);
            MoveToward(target);

            if (!HasReachedWaypoint(target))
                return;

            if (_pathIndex >= endIndex)
            {
                AgentStop();
                SetLocomotionIdle();
                return;
            }

            _pathIndex++;
            _nextRepathAt = 0f;
            _hasActiveNavDestination = false;
        }

        bool TryHandleWormThreat()
        {
            if (!IsWormInDetectRange())
                return false;

            BeginEvasionRecord();
            _phase = Phase.Backpedal;

            if (CanAttemptSafeZoneEvasion && TryBeginSafeZoneJumpEvasion())
                return true;

            if (TryPathBackpedalMove())
                return true;

            if (TryFleeFromWormOnNavMesh())
                return true;

            if (CanAttemptSafeZoneEvasion && TryBeginSafeZoneEvasion())
                return true;

            if (TryFleeFromWormOnNavMesh(10f))
                return true;

            AgentStop();
            SetLocomotionIdle();
            return true;
        }

        bool TryGetPathRetreatDirection(out Vector3 dir)
        {
            dir = default;
            if (aiPath == null || aiPath.WaypointCount == 0)
                return false;

            var backIndex = ResolveBackpedalWaypointIndex();
            if (backIndex < 0)
                return false;

            var target = aiPath.GetWaypointWorld(Mathf.Clamp(backIndex, 0, aiPath.WaypointCount - 1));
            dir = target - ActorTransform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.04f)
                return false;

            dir.Normalize();
            return true;
        }

        bool CanBackpedalOnPath()
        {
            if (aiPath == null || aiPath.WaypointCount == 0)
                return false;

            if (_pathIndex > _startWaypointIndex)
                return true;

            return _startWaypointIndex > 0;
        }

        int ResolveBackpedalWaypointIndex()
        {
            if (_pathIndex > _startWaypointIndex)
                return _pathIndex - 1;

            if (_startWaypointIndex > 0)
                return _startWaypointIndex - 1;

            return _startWaypointIndex;
        }

        void BeginEvasionRecord()
        {
            _pathIndexAtEvasionStart = _pathIndex;
            _hasActiveNavDestination = false;
            _nextRepathAt = 0f;
            if (!_hasNavMeshReturnPoint)
                RecordNavMeshReturnPoint();
            AgentStop();
            AgentResetPath();
        }

        void RecordNavMeshReturnPoint()
        {
            var pos = ActorTransform.position;
            if (NavMesh.SamplePosition(pos, out var hit, navMeshSampleRadius * 2f, NavMesh.AllAreas))
                _navMeshReturnPoint = hit.position;
            else
                _navMeshReturnPoint = pos;

            _hasNavMeshReturnPoint = true;
        }

        bool TryPathBackpedalMove()
        {
            if (aiPath == null || !CanBackpedalOnPath() || !EnsureAgentOnNavMesh())
                return false;

            var minIndex = _startWaypointIndex;
            for (var tryIndex = ResolveBackpedalWaypointIndex(); tryIndex >= minIndex; tryIndex--)
            {
                var target = ResolveWaypointOnNavMesh(tryIndex);
                if (!TryBuildNavPath(target, out var path))
                    continue;

                if (path.status != NavMeshPathStatus.PathComplete)
                    continue;

                MoveToward(target, allowTowardWorm: true);
                AgentResume();

                if (HasReachedWaypoint(target) && _pathIndex != tryIndex)
                {
                    _pathIndex = tryIndex;
                    _nextRepathAt = 0f;
                    _hasActiveNavDestination = false;
                }

                return true;
            }

            return false;
        }

        void TickBackpedal()
        {
            if (aiPath == null || !EnsureAgentOnNavMesh())
                return;

            if (!IsWormInDetectRange())
            {
                ResumePathFromSafeZone();
                return;
            }

            if (CanAttemptSafeZoneEvasion && TryBeginSafeZoneJumpEvasion())
                return;

            if (TryPathBackpedalMove())
                return;

            if (TryFleeFromWormOnNavMesh())
                return;

            if (CanAttemptSafeZoneEvasion && TryBeginSafeZoneEvasion())
                return;

            if (TryFleeFromWormOnNavMesh(10f))
                return;

            AgentStop();
            SetLocomotionIdle();
        }

        void TickSafeZoneStay()
        {
            AgentStop();
            AgentResetPath();
            SetLocomotionIdle();
            SetAnimatorGrounded(true);

            if (_participant != null && !_participant.IsInSafeZone && _activeSafeZone != null)
                EnterSafeZone(_activeSafeZone);

            if (IsWormInDetectRange())
            {
                _safeZoneReturnScheduled = false;
                return;
            }

            if (!_safeZoneReturnScheduled)
            {
                _safeZoneReturnScheduled = true;
                var minDelay = Mathf.Max(0f, safeZoneReturnDelayMin);
                var maxDelay = Mathf.Max(minDelay, safeZoneReturnDelayMax);
                _safeZoneReturnAllowedAt = Time.time + Random.Range(minDelay, maxDelay);
                return;
            }

            if (Time.time < _safeZoneReturnAllowedAt)
                return;

            _safeZoneReturnScheduled = false;
            BeginJumpReturnToNavMesh();
        }

        void TickApproachSafeZone()
        {
            if (_activeSafeZone == null)
            {
                ResumePathFromSafeZone();
                return;
            }

            if (!EnsureAgentOnNavMesh())
                return;

            if (!IsWormInDetectRange())
            {
                if (_hasNavMeshReturnPoint && (!EnsureAgentOnNavMesh() || _participant?.IsInSafeZone == true))
                    BeginJumpReturnToNavMesh();
                else
                    ResumePathFromSafeZone();
                return;
            }

            var landing = ResolveSafeZoneLanding(_activeSafeZone);
            if (IsDestinationTowardWorm(landing))
            {
                if (TryFleeFromWormOnNavMesh())
                {
                    _phase = Phase.Backpedal;
                    return;
                }
            }

            MoveTowardSafeZone(landing);

            var planarReach = arrivalTolerance * 2.5f;
            if (PlanarDistanceSqr(ActorTransform.position, landing) <= planarReach * planarReach &&
                ShouldJumpToSafeZone(landing))
            {
                BeginJump(landing, Phase.SafeZoneStay);
                return;
            }

            if (_participant != null && _participant.IsInSafeZone)
            {
                _phase = Phase.SafeZoneStay;
                AgentStop();
                return;
            }

            if (HasReachedWaypoint(landing))
            {
                EnterSafeZone(_activeSafeZone);
                _phase = Phase.SafeZoneStay;
                AgentStop();
            }
        }

        bool TryBeginSafeZoneEvasion()
        {
            if (!TryPickNearestSafeZone(out var landing, out var zone))
                return false;

            _activeSafeZone = zone;
            landing = ResolveSafeZoneLanding(zone);

            if (ShouldJumpToSafeZone(landing))
            {
                BeginJump(landing, Phase.SafeZoneStay);
                return true;
            }

            _phase = Phase.ApproachSafeZone;
            AgentResume();
            return true;
        }

        bool TryBeginSafeZoneJumpEvasion()
        {
            if (!TryPickNearestSafeZone(out var landing, out var zone))
                return false;

            landing = ResolveSafeZoneLanding(zone);
            if (!ShouldJumpToSafeZone(landing))
                return false;

            _activeSafeZone = zone;
            BeginJump(landing, Phase.SafeZoneStay);
            return true;
        }

        bool ShouldJumpToSafeZone(Vector3 landing)
        {
            var heightDelta = landing.y - ActorTransform.position.y;
            if (Mathf.Abs(heightDelta) > jumpRequiredHeightDelta * 0.35f)
                return true;

            if (!EnsureAgentOnNavMesh())
                return true;

            if (!NavMesh.SamplePosition(landing, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                return true;

            // 采样点若落到平台下方地面，说明需要跳跃而非沿 NavMesh 走过去。
            if (Mathf.Abs(hit.position.y - landing.y) > 0.6f)
                return true;

            return ShouldJumpToReach(landing);
        }

        void BeginJumpReturnToNavMesh()
        {
            if (_phase == Phase.Jumping)
                return;

            if (!_hasNavMeshReturnPoint)
                return;

            BeginJump(_navMeshReturnPoint, Phase.ReturnToNavMesh);
        }

        bool TryFleeFromWormOnNavMesh(float distance = 6f)
        {
            var motor = ResolveWormMotor();
            if (motor == null || motor.HeadTransform == null)
                return false;

            var origin = ActorTransform.position;
            var wormPos = motor.HeadTransform.position;
            var away = origin - wormPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = -ActorTransform.forward;

            away.Normalize();
            var baseDir = TryGetPathRetreatDirection(out var pathDir) ? pathDir : away;

            var angles = new[] { 0f, 20f, -20f, 40f, -40f, 65f, -65f, 90f, -90f, 120f, -120f, 155f, -155f, 180f };
            for (var i = 0; i < angles.Length; i++)
            {
                var dir = Quaternion.Euler(0f, angles[i], 0f) * baseDir;
                var dest = origin + dir * distance;
                if (!NavMesh.SamplePosition(dest, out var hit, navMeshSampleRadius * 2f, NavMesh.AllAreas))
                    continue;

                var step = hit.position - origin;
                step.y = 0f;
                if (step.sqrMagnitude < 0.25f)
                    continue;

                if (Vector3.Dot(step.normalized, baseDir) < 0.1f)
                    continue;

                MoveToward(hit.position, allowTowardWorm: true);
                AgentResume();
                return true;
            }

            return false;
        }

        bool IsDestinationTowardWorm(Vector3 destination)
        {
            var motor = ResolveWormMotor();
            if (motor == null || motor.HeadTransform == null)
                return false;

            var toDest = destination - ActorTransform.position;
            var toWorm = motor.HeadTransform.position - ActorTransform.position;
            toDest.y = 0f;
            toWorm.y = 0f;
            if (toDest.sqrMagnitude < 0.04f || toWorm.sqrMagnitude < 0.04f)
                return false;

            return Vector3.Dot(toDest.normalized, toWorm.normalized) > 0.2f;
        }

        void EnsureParticipantResolved()
        {
            if (_participant == null)
                _participant = GetComponentInChildren<WormDodgeParticipant>(true);
        }

        Vector3 GetThreatSamplePosition()
        {
            EnsureParticipantResolved();
            var actorChest = ActorTransform.position + Vector3.up * 0.9f;
            if (_participant == null)
                return actorChest;

            var detect = _participant.GetChaseDetectPosition();
            var maxVertical = Mathf.Max(0.5f, threatMaxVerticalDelta);
            if (Mathf.Abs(detect.y - actorChest.y) > maxVertical * 2f)
                return actorChest;

            return detect;
        }

        Vector3 GetClosestPointOnWorm(WormChainMotor motor, out float planarDistance, out float verticalDelta)
        {
            var sample = GetThreatSamplePosition();
            var maxVertical = Mathf.Max(0.5f, threatMaxVerticalDelta);
            var planarRadius = Mathf.Max(0.5f, threatDetectRadius);
            var bestPoint = sample;
            planarDistance = float.MaxValue;
            verticalDelta = float.MaxValue;

            if (motor == null)
                return bestPoint;

            if (TryGetClosestSegmentInThreatRange(
                    motor,
                    sample,
                    planarRadius,
                    maxVertical,
                    out planarDistance,
                    out verticalDelta,
                    out bestPoint))
                return bestPoint;

            if (motor.HeadTransform == null)
                return bestPoint;

            bestPoint = motor.HeadTransform.position;
            planarDistance = motor.GetChasePlanarDistance(sample);
            verticalDelta = motor.GetChaseVerticalDelta(sample);
            return bestPoint;
        }

        bool TryGetClosestSegmentInThreatRange(
            WormChainMotor motor,
            Vector3 sample,
            float planarRadius,
            float maxVertical,
            out float planarDistance,
            out float verticalDelta,
            out Vector3 closestPoint)
        {
            planarDistance = float.MaxValue;
            verticalDelta = float.MaxValue;
            closestPoint = sample;

            if (motor == null)
                return false;

            var found = false;
            var bestPlanar = float.MaxValue;
            var bestVertical = float.MaxValue;
            var bestPoint = sample;

            void Consider(Transform pointTransform)
            {
                if (pointTransform == null || !pointTransform.gameObject.activeInHierarchy)
                    return;

                ConsiderPoint(pointTransform.position);
            }

            void ConsiderPoint(Vector3 point)
            {
                if (!IsPointInThreatCylinder(sample, point, planarRadius, maxVertical, out var planarDist, out var vert))
                    return;

                if (planarDist >= bestPlanar)
                    return;

                found = true;
                bestPlanar = planarDist;
                bestVertical = vert;
                bestPoint = point;
            }

            Consider(motor.HeadTransform);

            var count = motor.SegmentCount;
            if (count <= 0)
            {
                if (!found)
                    return false;

                planarDistance = bestPlanar;
                verticalDelta = bestVertical;
                closestPoint = bestPoint;
                return true;
            }

            var step = count <= 16 ? 1 : Mathf.Max(1, count / 16);
            for (var i = 0; i < count; i += step)
                Consider(motor.GetSegmentTransform(i));

            if (step > 1)
            {
                Consider(motor.GetSegmentTransform(count - 1));
                if (count > 2)
                    Consider(motor.GetSegmentTransform(count / 2));
            }

            if (!found)
                return false;

            planarDistance = bestPlanar;
            verticalDelta = bestVertical;
            closestPoint = bestPoint;
            return true;
        }

        bool TryEvaluateMotorThreat(
            WormChainMotor motor,
            Vector3 sample,
            float planarRadius,
            float maxVertical,
            WormChaseTarget chaseTarget,
            out float effectivePlanar,
            out float effectiveVertical,
            out bool chasingMe)
        {
            effectivePlanar = float.MaxValue;
            effectiveVertical = float.MaxValue;
            chasingMe = false;

            if (motor == null || motor.HeadTransform == null)
                return false;

            chasingMe = motor.IsChasing &&
                        chaseTarget != null &&
                        motor.ActiveChaseTarget == chaseTarget;

            var headPlanar = motor.GetChasePlanarDistance(sample);
            var headVertical = motor.GetChaseVerticalDelta(sample);

            if (chasingMe)
            {
                effectivePlanar = headPlanar;
                effectiveVertical = headVertical;
                return true;
            }

            var wormAcquires = chaseTarget != null && motor.IsTargetWithinChaseRange(sample);
            if (wormAcquires)
            {
                effectivePlanar = headPlanar;
                effectiveVertical = headVertical;
                return true;
            }

            var headInCylinder = IsPointInThreatCylinder(
                sample,
                motor.HeadTransform.position,
                planarRadius,
                maxVertical,
                out headPlanar,
                out headVertical);
            if (headInCylinder)
            {
                effectivePlanar = headPlanar;
                effectiveVertical = headVertical;
                return true;
            }

            if (headPlanar > planarRadius + 2f && Mathf.Abs(headVertical) > maxVertical + 1.5f)
                return false;

            var segmentInRange = TryGetClosestSegmentInThreatRange(
                motor,
                sample,
                planarRadius,
                maxVertical,
                out var segPlanar,
                out var segVertical,
                out _);

            if (!segmentInRange)
                return false;

            if (segmentInRange && segPlanar <= headPlanar)
            {
                effectivePlanar = segPlanar;
                effectiveVertical = segVertical;
            }
            else
            {
                effectivePlanar = headPlanar;
                effectiveVertical = headVertical;
            }

            return true;
        }

        bool EvaluateWormThreat(out float planarDistance, out float verticalDelta, out bool wormChasingMe)
        {
            planarDistance = float.MaxValue;
            verticalDelta = float.MaxValue;
            wormChasingMe = false;
            _primaryThreatMotor = null;

            EnsureParticipantResolved();
            var chaseTarget = _participant != null ? _participant.ChaseTarget : null;
            var sample = GetThreatSamplePosition();
            var maxVertical = Mathf.Max(0.5f, threatMaxVerticalDelta);
            var planarRadius = Mathf.Max(0.5f, threatDetectRadius);

            if (!IsRunActiveForAi())
            {
                debugWormPlanarDistance = float.MaxValue;
                debugWormVerticalDelta = float.MaxValue;
                return false;
            }

            if (Time.time < _wormThreatReleaseUntil)
            {
                UpdateNearestThreatDistances(sample, planarRadius, maxVertical, chaseTarget, ref planarDistance, ref verticalDelta, ref wormChasingMe);
                debugWormPlanarDistance = planarDistance;
                debugWormVerticalDelta = verticalDelta;
                return true;
            }

            RefreshWormMotorCache();
            if (_wormMotorCache == null || _wormMotorCache.Length == 0)
            {
                debugWormPlanarDistance = float.MaxValue;
                debugWormVerticalDelta = float.MaxValue;
                return false;
            }

            var threatened = false;

            for (var i = 0; i < _wormMotorCache.Length; i++)
            {
                var motor = _wormMotorCache[i];
                if (!IsMotorActiveForThreat(motor))
                    continue;

                if (!TryEvaluateMotorThreat(
                        motor,
                        sample,
                        planarRadius,
                        maxVertical,
                        chaseTarget,
                        out var motorPlanar,
                        out var motorVertical,
                        out var chasingMe))
                    continue;

                if (chasingMe)
                    wormChasingMe = true;

                threatened = true;
                if (motorPlanar >= planarDistance)
                    continue;

                planarDistance = motorPlanar;
                verticalDelta = motorVertical;
                _primaryThreatMotor = motor;
            }

            debugWormPlanarDistance = planarDistance;
            debugWormVerticalDelta = verticalDelta;

            if (!threatened)
                return false;

            _wormThreatReleaseUntil = Time.time + Mathf.Max(0.1f, threatReleaseDelaySeconds);
            return true;
        }

        void UpdateNearestThreatDistances(
            Vector3 sample,
            float planarRadius,
            float maxVertical,
            WormChaseTarget chaseTarget,
            ref float planarDistance,
            ref float verticalDelta,
            ref bool wormChasingMe)
        {
            RefreshWormMotorCache();
            if (_wormMotorCache == null)
                return;

            for (var i = 0; i < _wormMotorCache.Length; i++)
            {
                var motor = _wormMotorCache[i];
                if (!IsMotorActiveForThreat(motor))
                    continue;

                if (!TryEvaluateMotorThreat(
                        motor,
                        sample,
                        planarRadius,
                        maxVertical,
                        chaseTarget,
                        out var motorPlanar,
                        out var motorVertical,
                        out var chasingMe))
                    continue;

                if (chasingMe)
                    wormChasingMe = true;

                if (motorPlanar >= planarDistance)
                    continue;

                planarDistance = motorPlanar;
                verticalDelta = motorVertical;
                _primaryThreatMotor = motor;
            }
        }

        void ResumePathFromSafeZone()
        {
            if (_phase == Phase.SafeZoneStay || (_participant != null && _participant.IsInSafeZone))
            {
                if (_hasNavMeshReturnPoint)
                    BeginJumpReturnToNavMesh();
                return;
            }

            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                SyncAgentToWorldPosition(ActorTransform.position);
                if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                    return;
            }

            if (aiPath == null)
            {
                _phase = Phase.MoveToEnd;
                AgentResume();
                return;
            }

            var resume = aiPath.FindForwardResumeWaypointIndex(
                ActorTransform.position,
                _startWaypointIndex);
            _pathIndex = Mathf.Clamp(
                Mathf.Max(_pathIndexAtEvasionStart, resume),
                _startWaypointIndex,
                aiPath.EndWaypointIndex);
            _phase = Phase.MoveToEnd;
            _hasActiveNavDestination = false;
            _nextRepathAt = 0f;
            _hasNavMeshReturnPoint = false;
            _safeZoneReturnScheduled = false;
            _safeZoneReentryBlockedUntil = 0f;
            ClearSafeZoneState();

            if (!EnsureAgentOnNavMesh())
                SyncAgentToWorldPosition(ActorTransform.position);

            AgentResetPath();
            AgentResume();

            var target = ResolveWaypointOnNavMesh(_pathIndex);
            MoveToward(target);
        }

        bool ShouldJumpToReach(Vector3 destination)
        {
            if (!TryBuildNavPath(destination, out var path))
                return true;

            if (path.status != NavMeshPathStatus.PathComplete)
                return true;

            if (path.corners == null || path.corners.Length == 0)
                return true;

            var pathEnd = path.corners[path.corners.Length - 1];
            var endHeightGap = Mathf.Abs(destination.y - pathEnd.y);
            if (endHeightGap > 0.75f)
                return true;

            var heightDelta = destination.y - ActorTransform.position.y;
            return heightDelta > jumpRequiredHeightDelta && path.corners.Length <= 2;
        }

        bool TryBuildNavPath(Vector3 destination, out NavMeshPath path)
        {
            path = _navPathScratch ??= new NavMeshPath();
            path.ClearCorners();

            if (!EnsureAgentOnNavMesh())
                return false;

            if (!NavMesh.SamplePosition(destination, out var hit, navMeshSampleRadius * 2f, NavMesh.AllAreas))
                return false;

            return agent.CalculatePath(hit.position, path);
        }

        void BeginJump(Vector3 destination, Phase resumePhase)
        {
            StopJumpRoutine();
            if (resumePhase == Phase.SafeZoneStay)
            {
                RecordNavMeshReturnPoint();
                _pathIndexAtEvasionStart = _pathIndex;
                _hasActiveNavDestination = false;
                _nextRepathAt = 0f;
                AgentStop();
                AgentResetPath();
            }
            else if (resumePhase != Phase.ReturnToNavMesh)
            {
                BeginEvasionRecord();
            }

            _jumpRoutine = StartCoroutine(JumpRoutine(destination, resumePhase));
        }

        IEnumerator JumpRoutine(Vector3 destination, Phase resumePhase)
        {
            _phase = Phase.Jumping;
            _hasActiveNavDestination = false;
            patrol?.SetLocomotionAnimatorIdle();

            if (agent != null && agent.enabled)
            {
                AgentStop();
                AgentResetPath();
                agent.enabled = false;
            }

            TriggerJumpAnim();
            SetAnimatorGrounded(false);

            var actor = ActorTransform;
            var start = actor.position;
            var duration = Mathf.Max(0.2f, jumpDurationSeconds);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pos = Vector3.Lerp(start, destination, t);
                pos.y += jumpArcHeight * Mathf.Sin(t * Mathf.PI);
                actor.position = pos;
                yield return null;
            }

            actor.position = destination;

            if (agent != null)
            {
                agent.enabled = true;
                if (NavMesh.SamplePosition(destination, out var hit, navMeshSampleRadius * 2f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
                else
                    agent.Warp(destination);

                if (agent.isOnNavMesh)
                    AgentResetPath();
            }

            if (resumePhase == Phase.SafeZoneStay)
            {
                EnterSafeZone(_activeSafeZone);
                _phase = Phase.SafeZoneStay;
                _safeZoneReturnScheduled = false;
                AgentStop();
                AgentResetPath();
            }
            else if (resumePhase == Phase.ReturnToNavMesh)
            {
                ClearSafeZoneState();
                _hasNavMeshReturnPoint = false;
                _safeZoneReentryBlockedUntil = Time.time + Mathf.Max(0f, safeZoneReturnReentryCooldown);
                ResumePathFromSafeZone();
            }
            else
            {
                ResumePathFromSafeZone();
            }

            SetAnimatorGrounded(true);
            SetLocomotionIdle();
            _jumpRoutine = null;
        }

        void EnterSafeZone(WormSafeZone zone)
        {
            if (_participant == null || zone == null)
                return;

            zone.RegisterParticipant(_participant);
            WormDodgeDirector.Instance?.NotifyEnteredSafeZone(_participant, zone);
        }

        void ClearSafeZoneState()
        {
            _activeSafeZone = null;
            if (_participant == null || !_participant.IsInSafeZone)
                return;

            WormDodgeDirector.Instance?.NotifyExitedSafeZone(_participant);
        }

        bool TryPickNearestSafeZone(out Vector3 landing, out WormSafeZone zone)
        {
            landing = default;
            zone = null;
            RefreshZoneCache();

            var pos = ActorTransform.position;
            var bestScore = float.MaxValue;

            for (var i = 0; i < _zoneCache.Length; i++)
            {
                var candidate = _zoneCache[i];
                if (candidate == null)
                    continue;

                if (!candidate.IsEligibleForAiEvasion(
                        pos,
                        Mathf.Max(threatDetectRadius, maxSafeZoneEvasionDistance),
                        maxSafeZoneEvasionDistance,
                        maxSafeZoneVerticalRise))
                    continue;

                var candidateLanding = candidate.GetAiEvasionLandingPosition();

                var score = (candidateLanding - pos).sqrMagnitude;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                landing = candidateLanding;
                zone = candidate;
            }

            return zone != null;
        }

        Vector3 ResolveSafeZoneLanding(WormSafeZone safeZone)
        {
            if (safeZone == null)
                return ActorTransform.position;

            return safeZone.GetAiEvasionLandingPosition();
        }

        static void RefreshZoneCache()
        {
            if (_zoneCache != null && Time.time - _zoneCacheAt < 0.5f)
                return;

            _zoneCache = WormSafeZoneUtility.GetZones();
            _zoneCacheAt = Time.time;
        }

        static void RefreshWormMotorCache()
        {
            if (_wormMotorCache != null && Time.time - _wormMotorsCacheAt < 0.35f)
                return;

#if UNITY_2023_1_OR_NEWER
            _wormMotorCache = Object.FindObjectsByType<WormChainMotor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            _wormMotorCache = Object.FindObjectsOfType<WormChainMotor>();
#endif
            _wormMotorsCacheAt = Time.time;
        }

        static bool IsPointInThreatCylinder(
            Vector3 sample,
            Vector3 point,
            float planarRadius,
            float maxVertical,
            out float planarDistance,
            out float verticalDelta)
        {
            var planar = point - sample;
            planar.y = 0f;
            planarDistance = planar.magnitude;
            verticalDelta = Mathf.Abs(point.y - sample.y);
            return planarDistance <= planarRadius && verticalDelta <= maxVertical;
        }

        static bool IsMotorActiveForThreat(WormChainMotor motor) =>
            motor != null &&
            motor.isActiveAndEnabled &&
            motor.HeadTransform != null &&
            (motor.IsPlaying || motor.IsChasing);

        WormChainMotor ResolveWormMotor()
        {
            if (_primaryThreatMotor != null &&
                _primaryThreatMotor.isActiveAndEnabled &&
                _primaryThreatMotor.HeadTransform != null)
                return _primaryThreatMotor;

            RefreshWormMotorCache();
            if (_wormMotorCache == null || _wormMotorCache.Length == 0)
                return null;

            var sample = GetThreatSamplePosition();
            var maxVertical = Mathf.Max(0.5f, threatMaxVerticalDelta);
            var planarRadius = Mathf.Max(0.5f, threatDetectRadius);
            WormChainMotor best = null;
            var bestPlanar = float.MaxValue;
            EnsureParticipantResolved();
            var chaseTarget = _participant != null ? _participant.ChaseTarget : null;

            for (var i = 0; i < _wormMotorCache.Length; i++)
            {
                var motor = _wormMotorCache[i];
                if (!IsMotorActiveForThreat(motor))
                    continue;

                if (!TryEvaluateMotorThreat(
                        motor,
                        sample,
                        planarRadius,
                        maxVertical,
                        chaseTarget,
                        out var planar,
                        out _,
                        out _))
                    continue;

                if (planar >= bestPlanar)
                    continue;

                bestPlanar = planar;
                best = motor;
            }

            return best;
        }

        void MoveToward(Vector3 target, bool allowTowardWorm = false)
        {
            if (IsPathMovementPaused)
                return;

            if (_runActive && IsWormInDetectRange())
            {
                if (!IsEvasionPhase)
                {
                    TryHandleWormThreat();
                    return;
                }
            }

            if (!EnsureAgentOnNavMesh())
                return;

            var destination = ResolvePointOnNavMesh(target);
            SetMoveTargetGizmo(destination);

            var destChanged = !_hasActiveNavDestination ||
                              PlanarDistanceSqr(_activeNavDestination, destination) > 0.2f * 0.2f;
            var repathDue = Time.time >= _nextRepathAt;

            if (!destChanged && !repathDue && agent.hasPath)
                return;

            AgentResume();
            if (!agent.SetDestination(destination))
            {
                if (NavMesh.SamplePosition(destination, out var hit, navMeshSampleRadius * 2f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
            }

            _activeNavDestination = destination;
            _hasActiveNavDestination = true;
            _nextRepathAt = Time.time + Mathf.Max(0.05f, repathIntervalSeconds);
        }

        void MoveTowardSafeZone(Vector3 target)
        {
            if (!EnsureAgentOnNavMesh())
                return;

            var approach = new Vector3(target.x, ActorTransform.position.y, target.z);
            var destination = ResolvePointOnNavMesh(approach);
            SetMoveTargetGizmo(destination);

            var destChanged = !_hasActiveNavDestination ||
                              PlanarDistanceSqr(_activeNavDestination, destination) > 0.2f * 0.2f;
            var repathDue = Time.time >= _nextRepathAt;

            if (!destChanged && !repathDue && agent.hasPath)
                return;

            AgentResume();
            if (!agent.SetDestination(destination))
            {
                if (NavMesh.SamplePosition(destination, out var hit, navMeshSampleRadius * 2f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
            }

            _activeNavDestination = destination;
            _hasActiveNavDestination = true;
            _nextRepathAt = Time.time + Mathf.Max(0.05f, repathIntervalSeconds);
        }

        Vector3 ResolveWaypointOnNavMesh(int waypointIndex)
        {
            if (aiPath == null)
                return ActorTransform.position;

            return ResolvePointOnNavMesh(aiPath.GetWaypointWorld(waypointIndex));
        }

        Vector3 ResolvePointOnNavMesh(Vector3 worldPos)
        {
            if (NavMesh.SamplePosition(worldPos, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                return hit.position;

            return worldPos;
        }

        bool HasReachedWaypoint(Vector3 targetOnNavMesh)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (agent.pathPending)
                    return false;

                if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                    return false;

                if (agent.hasPath)
                {
                    var stop = agent.stoppingDistance + Mathf.Max(0.15f, arrivalTolerance * 0.35f);
                    if (!float.IsInfinity(agent.remainingDistance) && agent.remainingDistance <= stop)
                        return true;
                }
            }

            // 仅当 Agent 无有效路径时才回退到平面距离，避免“未走动却判定到达”。
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || !agent.hasPath)
                return HasReached(targetOnNavMesh);

            return false;
        }

        bool HasReached(Vector3 target)
        {
            var planar = target - ActorTransform.position;
            planar.y = 0f;
            return planar.sqrMagnitude <= arrivalTolerance * arrivalTolerance;
        }

        static float PlanarDistanceSqr(Vector3 a, Vector3 b)
        {
            var p = a - b;
            p.y = 0f;
            return p.sqrMagnitude;
        }

        Vector3 ResolveSpawnPosition(Transform overrideSpawn)
        {
            if (overrideSpawn != null)
                return overrideSpawn.position;

            if (_participant == null)
                _participant = GetComponentInChildren<WormDodgeParticipant>();

            return _participant != null && _participant.RespawnPoint != null
                ? _participant.RespawnPoint.position
                : ActorTransform.position;
        }

        void WarpToNavMesh(Vector3 worldPos) => SyncAgentToWorldPosition(worldPos);

        void SyncAgentToWorldPosition(Vector3 worldPos)
        {
            if (agent == null)
            {
                ActorTransform.position = worldPos;
                return;
            }

            if (NavMesh.SamplePosition(worldPos, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                if (!agent.enabled)
                    agent.enabled = true;

                agent.Warp(hit.position);
                AgentResetPath();
            }
            else
            {
                ActorTransform.position = worldPos;
                agent.enabled = false;
            }
        }

        bool EnsureAgentOnNavMesh()
        {
            if (agent == null)
                return false;

            if (!agent.enabled)
                agent.enabled = true;

            if (agent.isOnNavMesh)
                return true;

            if (!NavMesh.SamplePosition(ActorTransform.position, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                return false;

            agent.Warp(hit.position);
            AgentResetPath();
            return agent.isOnNavMesh;
        }

        void AgentStop()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
        }

        void AgentResume()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = false;
        }

        void AgentResetPath()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
        }

        void AgentSetDestination(Vector3 pos)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.SetDestination(pos);
        }

        void StopJumpRoutine()
        {
            if (_jumpRoutine == null)
                return;

            StopCoroutine(_jumpRoutine);
            _jumpRoutine = null;
            SetAnimatorGrounded(true);
        }

        void TriggerJumpAnim()
        {
            if (animator == null)
                return;

            var id = Animator.StringToHash(jumpTriggerParam);
            animator.SetTrigger(id);
        }

        void SetAnimatorGrounded(bool grounded)
        {
            if (animator == null)
                return;

            if (!_hasIsGroundBool)
                CacheAnimatorGroundParam();

            if (!_hasIsGroundBool)
                return;

            if (_animatorGroundedApplied == grounded)
                return;

            _animatorGroundedApplied = grounded;
            animator.SetBool(_isGroundBoolId, grounded);
        }

        void SetLocomotionIdle()
        {
            patrol?.SetLocomotionAnimatorIdle();
            SetAnimatorGrounded(true);
        }

        void SetMoveTargetGizmo(Vector3 pos)
        {
            _moveTargetGizmo = pos;
            _hasMoveTargetGizmo = true;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (Application.isPlaying && !WormDodgeRuntimeDebug.SceneGizmosEnabled)
                return;

            if (!drawRangeGizmos && !drawMoveTargetLink)
                return;

            var origin = ActorTransform.position;

            if (drawRangeGizmos)
            {
                var threatCenter = Application.isPlaying
                    ? GetThreatSamplePosition()
                    : origin + Vector3.up * 0.9f;

                Gizmos.color = threatDetectGizmoColor;
                DrawThreatCylinderGizmo(threatCenter, threatDetectRadius, threatMaxVerticalDelta);

                var motor = ResolveWormMotor();
                if (motor != null && motor.HeadTransform != null)
                {
                    Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.75f);
                    DrawPlanarRangeGizmo(motor.HeadTransform.position, motor.ChaseDetectRadius);
                }

                Gizmos.color = safeZonePickGizmoColor;
                Gizmos.DrawWireSphere(origin, maxSafeZoneEvasionDistance);

                Gizmos.color = safeZoneVerticalGizmoColor;
                Gizmos.DrawLine(origin, origin + Vector3.up * maxSafeZoneVerticalRise);
                DrawPlanarRangeGizmo(origin + Vector3.up * maxSafeZoneVerticalRise, 0.75f);
            }

            if (drawMoveTargetLink && _hasMoveTargetGizmo)
            {
                Gizmos.color = moveTargetLinkColor;
                Gizmos.DrawLine(origin, _moveTargetGizmo);
            }

            if (!Application.isPlaying)
                return;

            if (IsRunActiveForAi() && debugWormInDetectRange)
            {
                var motor = ResolveWormMotor();
                if (motor != null)
                {
                    Gizmos.color = wormThreatLinkColor;
                    var wormPoint = GetClosestPointOnWorm(motor, out _, out _);
                    Gizmos.DrawLine(origin, wormPoint);
                    Gizmos.DrawWireSphere(wormPoint, 0.25f);
                }
            }

            if (_activeSafeZone != null)
            {
                Gizmos.color = safeZoneLinkColor;
                var landing = ResolveSafeZoneLanding(_activeSafeZone);
                Gizmos.DrawLine(origin, landing);
                Gizmos.DrawWireSphere(landing, 0.35f);
            }
            else if (IsEvasionPhase && IsRunActiveForAi() &&
                     TryPickNearestSafeZone(out var previewLanding, out _))
            {
                Gizmos.color = new Color(safeZoneLinkColor.r, safeZoneLinkColor.g, safeZoneLinkColor.b, 0.45f);
                Gizmos.DrawLine(origin, previewLanding);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!drawRangeGizmos)
                return;

            var origin = ActorTransform.position;
            UnityEditor.Handles.Label(
                origin + Vector3.up * 2f,
                $"Phase={_phase}  wormThreat={debugWormInDetectRange}  chasingMe={debugWormChasingMe}\n" +
                $"worm planar={debugWormPlanarDistance:F1}m  vert={debugWormVerticalDelta:F1}m\n" +
                $"AI threat={threatDetectRadius:F1}m  SafeZone={maxSafeZoneEvasionDistance:F1}m");
        }

        static void DrawThreatCylinderGizmo(Vector3 center, float radius, float halfHeight, int segments = 32)
        {
            if (radius <= 0f || halfHeight <= 0f)
                return;

            var top = center + Vector3.up * halfHeight;
            var bottom = center - Vector3.up * halfHeight;
            DrawPlanarRangeGizmo(top, radius, segments);
            DrawPlanarRangeGizmo(center, radius, segments);
            DrawPlanarRangeGizmo(bottom, radius, segments);

            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(bottom + offset, top + offset);
            }
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

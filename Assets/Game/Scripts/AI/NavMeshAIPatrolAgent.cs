using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Simple NavMeshAgent-driven AI:
    /// - Patrol between points (loop/ping-pong/random)
    /// - Optional chase a target (auto pathfinding)
    /// - Drives only idle/move animation via configurable Animator params.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public sealed class NavMeshAIPatrolAgent : MonoBehaviour
    {
        public enum MoveMode
        {
            Patrol,
            ChaseTarget
        }

        public enum PatrolMode
        {
            Loop,
            PingPong,
            Random
        }

        [Header("Mode")]
        [SerializeField] private MoveMode mode = MoveMode.Patrol;
        [SerializeField] private Transform chaseTarget;

        [Header("Patrol")]
        [SerializeField] private PatrolMode patrolMode = PatrolMode.Loop;
        [SerializeField] private Transform[] patrolPoints;

        [Tooltip("到达巡逻点后停留秒数（固定值；若启用下方随机区间则忽略此项）。")]
        [SerializeField] private float waitAtPointSeconds = 0.25f;

        [Tooltip("到达点后随机等待：最小秒数。")]
        [SerializeField] private float waitAtPointSecondsMin = 0.35f;

        [Tooltip("到达点后随机等待：最大秒数（应 ≥ Min）。")]
        [SerializeField] private float waitAtPointSecondsMax = 2.75f;

        [Tooltip("勾选后在到达点时使用 [Min, Max] 随机等待；否则使用 waitAtPointSeconds。")]
        [SerializeField] private bool useRandomWaitAtPatrolPoint = true;

        [Header("Patrol · 途中随机停顿")]
        [Tooltip("在向当前巡逻点移动途中，按间隔骰随机停下来发呆一小会儿。")]
        [SerializeField] private bool enableRandomMidPathPauses;

        [SerializeField] private float midPathPauseCheckIntervalSeconds = 2f;

        [Range(0f, 1f)]
        [SerializeField] private float midPathPauseChance = 0.07f;

        [SerializeField] private float midPathPauseDurationMinSeconds = 0.35f;

        [SerializeField] private float midPathPauseDurationMaxSeconds = 2f;

        [Header("Repath")]
        [SerializeField] private float repathIntervalSeconds = 0.2f;
        [SerializeField] private float repathDistanceThreshold = 0.5f;

        [Tooltip("叠在 NavMeshAgent.stoppingDistance 之上：进入该半径即视为到达巡逻点，便于多角色错开站位、减少顶死揉搓。")]
        [SerializeField] private float patrolArrivalExtraToleranceMeters = 0.85f;

        [Header("Animator Params (match your AI controller)")]
        [Tooltip("Optional float parameter (e.g. Velocity/Speed). Written as world speed in m/s.")]
        [SerializeField] private string speedFloatParam = "Velocity";
        [Tooltip("Optional bool parameter (e.g. IsMoving/Moving). True when speed > epsilon.")]
        [SerializeField] private string movingBoolParam = "IsMoving";
        [SerializeField] private float movingEpsilon = 0.05f;

        private NavMeshAgent _agent;
        private Animator _anim;

        private int _speedFloatId;
        private int _movingBoolId;
        private bool _hasSpeedFloat;
        private bool _hasMovingBool;

        private int _patrolIndex;
        private int _patrolDir = 1;
        private float _waitUntil;
        private float _midPathPauseUntil;
        private float _nextMidPathDiceRollUnscaledTime;

        private float _nextRepathAt;
        private Vector3 _lastChaseTargetPos;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _anim = GetComponent<Animator>();
            CacheAnimatorParams();
        }

        private void OnEnable()
        {
            _waitUntil = 0f;
            _midPathPauseUntil = 0f;
            _nextMidPathDiceRollUnscaledTime = 0f;
            _nextRepathAt = 0f;
            if (chaseTarget != null) _lastChaseTargetPos = chaseTarget.position;
        }

        private void CacheAnimatorParams()
        {
            _speedFloatId = Animator.StringToHash(speedFloatParam);
            _movingBoolId = Animator.StringToHash(movingBoolParam);
            _hasSpeedFloat = HasParam(speedFloatParam, AnimatorControllerParameterType.Float);
            _hasMovingBool = HasParam(movingBoolParam, AnimatorControllerParameterType.Bool);
        }

        private bool HasParam(string name, AnimatorControllerParameterType type)
        {
            if (_anim == null || string.IsNullOrEmpty(name)) return false;
            var ps = _anim.parameters;
            for (var i = 0; i < ps.Length; i++)
            {
                if (ps[i].type == type && ps[i].name == name)
                    return true;
            }
            return false;
        }

        private void Update()
        {
            TickMovement();
            TickAnimator();
        }

        private void TickMovement()
        {
            if (_agent == null || !_agent.enabled) return;

            switch (mode)
            {
                case MoveMode.ChaseTarget:
                    TickChase();
                    break;
                default:
                    TickPatrol();
                    break;
            }
        }

        private void TickChase()
        {
            if (chaseTarget == null)
            {
                _agent.isStopped = true;
                return;
            }

            _agent.isStopped = false;

            var now = Time.time;
            if (now < _nextRepathAt) return;

            var pos = chaseTarget.position;
            var moved = (pos - _lastChaseTargetPos).sqrMagnitude;
            var thresholdSqr = repathDistanceThreshold * repathDistanceThreshold;

            // Avoid spamming SetDestination every frame.
            if (moved >= thresholdSqr || !_agent.hasPath)
            {
                _agent.SetDestination(pos);
                _lastChaseTargetPos = pos;
            }

            _nextRepathAt = now + Mathf.Max(0.02f, repathIntervalSeconds);
        }

        private void TickPatrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                _agent.isStopped = true;
                return;
            }

            if (Time.time < _waitUntil || Time.time < _midPathPauseUntil)
            {
                _agent.isStopped = true;
                return;
            }

            var current = patrolPoints[Mathf.Clamp(_patrolIndex, 0, patrolPoints.Length - 1)];
            if (current == null)
            {
                AdvancePatrolIndex();
                ScheduleWaitAfterArrival();
                return;
            }

            if (!_agent.hasPath)
                _agent.SetDestination(current.position);

            if (HasReachedDestination())
            {
                AdvancePatrolIndex();
                ScheduleWaitAfterArrival();
                return;
            }

            TryRollMidPathPause();
            if (Time.time < _midPathPauseUntil)
            {
                _agent.isStopped = true;
                return;
            }

            _agent.isStopped = false;
        }

        private void ScheduleWaitAfterArrival()
        {
            var span = Mathf.Max(0f, ComputePatrolWaitSeconds());
            _waitUntil = Time.time + span;
            _midPathPauseUntil = 0f;
            _nextMidPathDiceRollUnscaledTime = Time.unscaledTime + Mathf.Max(0.05f, midPathPauseCheckIntervalSeconds);
        }

        private float ComputePatrolWaitSeconds()
        {
            if (useRandomWaitAtPatrolPoint)
            {
                var lo = Mathf.Min(waitAtPointSecondsMin, waitAtPointSecondsMax);
                var hi = Mathf.Max(waitAtPointSecondsMin, waitAtPointSecondsMax);
                return Random.Range(lo, hi);
            }

            return waitAtPointSeconds;
        }

        private void TryRollMidPathPause()
        {
            if (!enableRandomMidPathPauses || midPathPauseChance <= 0f)
                return;

            if (!_agent.hasPath || _agent.pathPending)
                return;

            var stopping = Mathf.Max(_agent.stoppingDistance, 0.05f);
            if (_agent.remainingDistance <= stopping)
                return;

            var nowU = Time.unscaledTime;
            if (nowU < _nextMidPathDiceRollUnscaledTime)
                return;

            var interval = Mathf.Max(0.15f, midPathPauseCheckIntervalSeconds);
            _nextMidPathDiceRollUnscaledTime = nowU + interval;

            if (Random.value > midPathPauseChance)
                return;

            var dLo = Mathf.Min(midPathPauseDurationMinSeconds, midPathPauseDurationMaxSeconds);
            var dHi = Mathf.Max(midPathPauseDurationMinSeconds, midPathPauseDurationMaxSeconds);
            var pause = Mathf.Max(0f, Random.Range(dLo, dHi));
            if (pause <= 0f)
                return;

            _midPathPauseUntil = Time.time + pause;
        }

        private bool HasReachedDestination()
        {
            if (_agent.pathPending) return false;
            var arrivalRadius = Mathf.Max(_agent.stoppingDistance, 0.01f) +
                                Mathf.Max(0f, patrolArrivalExtraToleranceMeters);
            if (_agent.remainingDistance > arrivalRadius) return false;
            if (_agent.hasPath && _agent.velocity.sqrMagnitude > 0.01f) return false;
            return true;
        }

        private void AdvancePatrolIndex()
        {
            var n = patrolPoints != null ? patrolPoints.Length : 0;
            if (n <= 0) return;

            switch (patrolMode)
            {
                case PatrolMode.Random:
                    if (n == 1) { _patrolIndex = 0; break; }
                    var next = _patrolIndex;
                    // Pick a different index to avoid "standing still".
                    while (next == _patrolIndex) next = Random.Range(0, n);
                    _patrolIndex = next;
                    break;

                case PatrolMode.PingPong:
                    _patrolIndex += _patrolDir;
                    if (_patrolIndex >= n)
                    {
                        _patrolDir = -1;
                        _patrolIndex = Mathf.Max(0, n - 2);
                    }
                    else if (_patrolIndex < 0)
                    {
                        _patrolDir = 1;
                        _patrolIndex = Mathf.Min(n - 1, 1);
                    }
                    break;

                default:
                    _patrolIndex = (_patrolIndex + 1) % n;
                    break;
            }

            // Refresh destination immediately.
            _agent.ResetPath();
        }

        private void TickAnimator()
        {
            if (_anim == null) return;

            // Use planar speed so slopes don't look like "moving fast" due to gravity.
            var v = _agent != null ? _agent.velocity : Vector3.zero;
            v.y = 0f;
            var speed = v.magnitude;

            if (_hasSpeedFloat) _anim.SetFloat(_speedFloatId, speed);
            if (_hasMovingBool) _anim.SetBool(_movingBoolId, speed > Mathf.Max(0f, movingEpsilon));
        }

        /// <summary>
        /// 巡逻脚本被禁用时（如 AI 自行寻路到钓点），仍可用 NavMeshAgent 速度驱动移动动画。
        /// </summary>
        public void RefreshLocomotionAnimator()
        {
            TickAnimator();
        }

        /// <summary>强制 Idle：Velocity=0、IsMoving=false（钓鱼等静止阶段使用）。</summary>
        public void SetLocomotionAnimatorIdle()
        {
            if (_anim == null)
                return;

            if (_hasSpeedFloat)
                _anim.SetFloat(_speedFloatId, 0f);
            if (_hasMovingBool)
                _anim.SetBool(_movingBoolId, false);
        }

        public void SetModePatrol()
        {
            mode = MoveMode.Patrol;
            if (_agent != null) _agent.ResetPath();
        }

        public void SetModeChase(Transform target)
        {
            mode = MoveMode.ChaseTarget;
            chaseTarget = target;
            if (_agent != null) _agent.ResetPath();
            if (chaseTarget != null) _lastChaseTargetPos = chaseTarget.position;
        }

        private void OnValidate()
        {
            if (repathIntervalSeconds < 0.02f) repathIntervalSeconds = 0.02f;
            if (repathDistanceThreshold < 0f) repathDistanceThreshold = 0f;
            if (waitAtPointSeconds < 0f) waitAtPointSeconds = 0f;
            if (waitAtPointSecondsMin < 0f) waitAtPointSecondsMin = 0f;
            if (waitAtPointSecondsMax < waitAtPointSecondsMin)
                waitAtPointSecondsMax = waitAtPointSecondsMin;
            if (midPathPauseCheckIntervalSeconds < 0.05f)
                midPathPauseCheckIntervalSeconds = 0.05f;
            if (midPathPauseDurationMaxSeconds < midPathPauseDurationMinSeconds)
                midPathPauseDurationMaxSeconds = midPathPauseDurationMinSeconds;
            if (movingEpsilon < 0f) movingEpsilon = 0f;
            if (patrolArrivalExtraToleranceMeters < 0f) patrolArrivalExtraToleranceMeters = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);

            for (var i = 0; i < patrolPoints.Length; i++)
            {
                var a = patrolPoints[i];
                if (a == null) continue;
                Gizmos.DrawSphere(a.position, 0.15f);

                var b = patrolPoints[(i + 1) % patrolPoints.Length];
                if (b == null) continue;
                Gizmos.DrawLine(a.position, b.position);
            }
        }
    }
}


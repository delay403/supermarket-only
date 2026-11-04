using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// AI 专属「虚假攀爬」：按随机间隔打断巡逻，先 <see cref="NavMeshAgent"/> 寻路到 <see cref="climbApproachPoint"/>，
    /// 到达后再持续纯垂直上移并播放攀爬动画（无滑翔/坠落），直至与玩家过远回到出生点；若地图塔高有限则 Y 封顶但不结束攀爬状态。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class AIFakeClimbBehavior : MonoBehaviour
    {
        enum Phase
        {
            Normal,
            ApproachingClimb,
            Climbing,
        }

        [Header("References")]
        [Tooltip("必须先走到该点附近（NavMesh 可到达），再开始攀爬。请放在烘焙网格上或靠近网格处。")]
        [SerializeField] private Transform climbApproachPoint;
        [SerializeField] private Transform player;
        [SerializeField] private Transform respawnPoint;
        [Tooltip("对齐塔心/圆柱中心用；攀爬开始时用于 XZ 收缩；不填则用世界原点 XZ。")]
        [SerializeField] private Transform towerAlignTarget;
        [SerializeField] private Tower.TowerManager towerManager;

        [Header("Patrol / agent")]
        [SerializeField] private NavMeshAIPatrolAgent patrol;
        [SerializeField] private NavMeshAgent agent;

        [Header("Approach (寻路至爬塔点)")]
        [Tooltip("在 climbApproachPoint 附近投射 NavMesh 时的最大搜索半径（米）。")]
        [SerializeField] private float approachPointSampleRadius = 3f;
        [Tooltip("叠在 stoppingDistance 上：进入该水平距离即视为到达爬塔点。")]
        [SerializeField] private float approachArrivalExtraToleranceMeters = 0.85f;
        [Tooltip("寻路超时（秒）：未到爬塔点则放弃本次攀爬并恢复巡逻。")]
        [SerializeField] private float approachTimeoutSeconds = 60f;
        [SerializeField] private float repathIntervalSeconds = 0.35f;

        [Header("Distance · respawn")]
        [Tooltip("与玩家的水平距离超过此值则隐藏并重置到出生点。")]
        [SerializeField] private float hideDistanceFromPlayerHorizontal = 72f;

        [Tooltip("回到出生点后，延后此秒数（含 0）内随机再显示，避免多名 AI 与玩家同时现身。")]
        [SerializeField] private float respawnAppearDelayMaxSeconds = 5f;

        [Tooltip("在出生点水平面 X/Z 上叠加的随机偏移（± 本值，米）；用于错开站位。")]
        [SerializeField] private float respawnHorizontalJitterMeters = 15f;

        [Header("Climb cadence")]
        [SerializeField] private float climbIntervalSecondsMin = 12f;
        [SerializeField] private float climbIntervalSecondsMax = 38f;

        [Header("Climb motion")]
        [Tooltip("该 AI 的虚假攀爬竖直速度（m/s），每个角色可不同。")]
        [SerializeField] private float fakeClimbSpeed = 1.8f;
        [Tooltip("与玩家攀爬类似：开始爬时 XZ 朝塔心收缩的最大半径。")]
        [SerializeField] private float attachRadiusXZ = 2f;

        [Header("Animator (optional, match Player controller)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string climbBoolParam = "Climb";
        [SerializeField] private string climbSpeedFloatParam = "ClimbSpeed";
        [SerializeField] private string movingBoolParam = "IsMoving";
        [SerializeField] private string speedFloatParam = "Velocity";
        [Tooltip("ClimbSpeed 动画乘子 = 竖直速度 / 本值（与玩家侧思路一致）。")]
        [SerializeField] private float climbSpeedToAnimDivisor = 4f;

        [Header("Visuals")]
        [Tooltip("仅隐藏渲染：不填则缓存自身与子节点上所有 Renderer。")]
        [SerializeField] private Transform visualRoot;

        Phase _phase = Phase.Normal;
        float _nextClimbReadyAt;
        Vector3 _sampledApproachPosition;
        float _approachStartedAt;
        float _nextRepathAt;
        Renderer[] _renderers;
        int _climbBoolId;
        int _climbSpeedId;
        int _movingBoolId;
        int _speedFloatId;
        Coroutine _respawnRoutine;
        bool _hasClimbBool;
        bool _hasClimbSpeed;
        bool _hasMovingBool;
        bool _hasSpeedFloat;

        void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (patrol == null) patrol = GetComponent<NavMeshAIPatrolAgent>();
            if (animator == null) animator = GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController == null)
                animator = GetComponentInChildren<Animator>();

            CacheRenderers();
            CacheAnimatorParams();
        }

        void Start()
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }

            if (respawnPoint == null)
                Debug.LogWarning($"{nameof(AIFakeClimbBehavior)} on {name}: 未设置 {nameof(respawnPoint)}，远距离重生将无法正确落位。", this);

            if (climbApproachPoint == null)
                Debug.LogWarning(
                    $"{nameof(AIFakeClimbBehavior)} on {name}: 未设置 {nameof(climbApproachPoint)}，无法先寻路再攀爬。",
                    this);

            if (towerManager == null)
                towerManager = FindFirstObjectByType<Tower.TowerManager>();

            ScheduleNextClimb(Random.Range(0.5f, 3f));
        }

        void Update()
        {
            if (ShouldRespawnForPlayerDistance())
            {
                RespawnAtSpawnPoint();
                return;
            }

            switch (_phase)
            {
                case Phase.ApproachingClimb:
                    TickApproachingClimb();
                    break;
                case Phase.Climbing:
                    TickClimb();
                    break;
                default:
                    TickNormal();
                    break;
            }
        }

        void TickNormal()
        {
            if (Time.time < _nextClimbReadyAt) return;
            if (climbApproachPoint == null)
            {
                ScheduleNextClimb();
                return;
            }

            TryBeginApproachClimb();
        }

        void TryBeginApproachClimb()
        {
            if (agent == null)
            {
                ScheduleNextClimb();
                return;
            }

            if (!TrySampleApproachPosition(out _sampledApproachPosition))
            {
                ScheduleNextClimb();
                return;
            }

            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out var hitAgent, 3f, NavMesh.AllAreas))
                    agent.Warp(hitAgent.position);
                else
                {
                    ScheduleNextClimb();
                    return;
                }
            }

            if (patrol != null && patrol.enabled)
                patrol.enabled = false;

            agent.enabled = true;
            agent.isStopped = false;
            agent.ResetPath();
            agent.SetDestination(_sampledApproachPosition);
            _approachStartedAt = Time.time;
            _nextRepathAt = Time.time;
            _phase = Phase.ApproachingClimb;
            ScheduleNextClimb();
        }

        bool TrySampleApproachPosition(out Vector3 sampled)
        {
            var raw = climbApproachPoint != null ? climbApproachPoint.position : transform.position;
            var r = Mathf.Max(0.25f, approachPointSampleRadius);
            if (!NavMesh.SamplePosition(raw, out var hit, r, NavMesh.AllAreas))
            {
                sampled = raw;
                return false;
            }

            sampled = hit.position;
            return true;
        }

        void TickApproachingClimb()
        {
            if (agent == null || climbApproachPoint == null)
            {
                AbortApproachAndResumePatrol();
                return;
            }

            if (Time.time - _approachStartedAt > Mathf.Max(5f, approachTimeoutSeconds))
            {
                AbortApproachAndResumePatrol();
                return;
            }

            var now = Time.time;
            if (now >= _nextRepathAt)
            {
                if (TrySampleApproachPosition(out var dest))
                {
                    _sampledApproachPosition = dest;
                    if (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid)
                        agent.SetDestination(dest);
                }

                _nextRepathAt = now + Mathf.Max(0.05f, repathIntervalSeconds);
            }

            if (HasReachedApproachPoint())
                TryBeginClimb();
        }

        bool HasReachedApproachPoint()
        {
            if (agent.pathPending) return false;
            var arrivalRadius = Mathf.Max(agent.stoppingDistance, 0.01f) +
                                Mathf.Max(0f, approachArrivalExtraToleranceMeters);

            var p = transform.position;
            var dx = p.x - _sampledApproachPosition.x;
            var dz = p.z - _sampledApproachPosition.z;
            if (dx * dx + dz * dz > arrivalRadius * arrivalRadius)
                return false;

            if (agent.velocity.sqrMagnitude > 0.02f) return false;
            return true;
        }

        void AbortApproachAndResumePatrol()
        {
            _phase = Phase.Normal;
            if (agent != null)
            {
                agent.ResetPath();
                agent.isStopped = false;
            }

            if (patrol != null)
                patrol.enabled = true;

            ScheduleNextClimb();
        }

        void TryBeginClimb()
        {
            if (agent == null)
            {
                AbortApproachAndResumePatrol();
                return;
            }

            if (patrol != null && patrol.enabled)
                patrol.enabled = false;

            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;

            SnapTowTowerCenter();

            _phase = Phase.Climbing;
            ApplyClimbAnimator(true, fakeClimbSpeed);
        }

        void TickClimb()
        {
            var step = Mathf.Max(0.01f, fakeClimbSpeed) * Time.deltaTime;
            var p = transform.position;
            var newY = p.y + step;
            var ceiling = ComputeClimbCeilingWorldY();
            if (!float.IsPositiveInfinity(ceiling))
                newY = Mathf.Min(newY, ceiling);

            p.y = newY;
            transform.position = p;
            ApplyClimbAnimator(true, fakeClimbSpeed);
        }

        bool ShouldRespawnForPlayerDistance()
        {
            if (player == null || hideDistanceFromPlayerHorizontal <= 0f)
                return false;

            var a = transform.position;
            var b = player.position;
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz > hideDistanceFromPlayerHorizontal * hideDistanceFromPlayerHorizontal;
        }

        void RespawnAtSpawnPoint()
        {
            if (_respawnRoutine != null)
                return;

            _respawnRoutine = StartCoroutine(CoRespawnAtSpawnPointWithJitter());
        }

        IEnumerator CoRespawnAtSpawnPointWithJitter()
        {
            try
            {
                if (_phase == Phase.Climbing || _phase == Phase.ApproachingClimb)
                    ApplyClimbAnimator(false, 0f);

                if (_phase != Phase.Normal)
                    _phase = Phase.Normal;

                if (patrol != null)
                    patrol.enabled = false;

                if (agent != null)
                {
                    agent.enabled = true;
                    agent.isStopped = true;
                    agent.ResetPath();
                }

                SetRenderersEnabled(false);

                var basePos = respawnPoint != null ? respawnPoint.position : transform.position;
                var j = Mathf.Max(0f, respawnHorizontalJitterMeters);
                var dest = basePos + new Vector3(Random.Range(-j, j), 0f, Random.Range(-j, j));

                if (NavMesh.SamplePosition(dest, out var hit, 8f, NavMesh.AllAreas))
                    dest = hit.position;
                else
                    dest = new Vector3(dest.x, basePos.y, dest.z);

                transform.position = dest;

                if (agent != null)
                    TryWarpAgentToNavMesh(agent, dest);

                var delayMax = Mathf.Max(0f, respawnAppearDelayMaxSeconds);
                if (delayMax > 0.0001f)
                    yield return new WaitForSeconds(Random.Range(0f, delayMax));

                if (agent != null)
                    agent.isStopped = false;

                if (patrol != null)
                    patrol.enabled = true;

                SetRenderersEnabled(true);
                ScheduleNextClimb();
            }
            finally
            {
                _respawnRoutine = null;
            }
        }

        void OnDestroy()
        {
            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }
        }

        static void TryWarpAgentToNavMesh(NavMeshAgent navAgent, Vector3 worldPos)
        {
            if (navAgent == null) return;
            const float sampleDistance = 4f;
            if (NavMesh.SamplePosition(worldPos, out var hit, sampleDistance, NavMesh.AllAreas))
                navAgent.Warp(hit.position);
            else
                navAgent.Warp(worldPos);
        }

        float ComputeClimbCeilingWorldY()
        {
            if (towerManager == null) return float.PositiveInfinity;
            if (towerManager.MaxHeight <= 0f) return float.PositiveInfinity;
            return towerManager.BaseY + towerManager.MaxHeight;
        }

        void SnapTowTowerCenter()
        {
            var p = transform.position;
            var center = towerAlignTarget != null ? towerAlignTarget.position : Vector3.zero;
            var radial = new Vector3(p.x - center.x, 0f, p.z - center.z);
            var r = Mathf.Max(0.05f, attachRadiusXZ);
            if (radial.sqrMagnitude > r * r)
                radial = radial.normalized * r;

            transform.position = new Vector3(center.x + radial.x, p.y, center.z + radial.z);
        }

        void ScheduleNextClimb(float delayExtra = 0f)
        {
            var lo = Mathf.Max(0.5f, Mathf.Min(climbIntervalSecondsMin, climbIntervalSecondsMax));
            var hi = Mathf.Max(lo, Mathf.Max(climbIntervalSecondsMin, climbIntervalSecondsMax));
            _nextClimbReadyAt = Time.time + delayExtra + Random.Range(lo, hi);
        }

        void CacheRenderers()
        {
            var root = visualRoot != null ? visualRoot : transform;
            _renderers = root.GetComponentsInChildren<Renderer>(true);
        }

        void SetRenderersEnabled(bool on)
        {
            if (_renderers == null) return;
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = on;
            }
        }

        void CacheAnimatorParams()
        {
            if (animator == null) return;
            _climbBoolId = Animator.StringToHash(climbBoolParam);
            _climbSpeedId = Animator.StringToHash(climbSpeedFloatParam);
            _movingBoolId = Animator.StringToHash(movingBoolParam);
            _speedFloatId = Animator.StringToHash(speedFloatParam);

            _hasClimbBool = HasParam(climbBoolParam, AnimatorControllerParameterType.Bool);
            _hasClimbSpeed = HasParam(climbSpeedFloatParam, AnimatorControllerParameterType.Float);
            _hasMovingBool = HasParam(movingBoolParam, AnimatorControllerParameterType.Bool);
            _hasSpeedFloat = HasParam(speedFloatParam, AnimatorControllerParameterType.Float);
        }

        bool HasParam(string name, AnimatorControllerParameterType type)
        {
            if (animator == null || string.IsNullOrEmpty(name)) return false;
            var ps = animator.parameters;
            for (var i = 0; i < ps.Length; i++)
            {
                if (ps[i].type == type && ps[i].name == name)
                    return true;
            }
            return false;
        }

        void ApplyClimbAnimator(bool climbing, float verticalSpeed)
        {
            if (animator == null) return;

            if (_hasClimbBool) animator.SetBool(_climbBoolId, climbing);
            if (_hasClimbSpeed)
            {
                var div = Mathf.Max(0.01f, climbSpeedToAnimDivisor);
                animator.SetFloat(_climbSpeedId, climbing ? verticalSpeed / div : 0f);
            }

            if (climbing)
            {
                if (_hasMovingBool) animator.SetBool(_movingBoolId, true);
                if (_hasSpeedFloat) animator.SetFloat(_speedFloatId, 0f);
            }
        }

#if UNITY_2023_1_OR_NEWER
        static T FindFirstObjectByType<T>() where T : Object => Object.FindFirstObjectByType<T>();
#else
        static T FindFirstObjectByType<T>() where T : Object => Object.FindObjectOfType<T>();
#endif

        void OnValidate()
        {
            if (hideDistanceFromPlayerHorizontal < 0f) hideDistanceFromPlayerHorizontal = 0f;
            if (fakeClimbSpeed < 0f) fakeClimbSpeed = 0f;
            if (attachRadiusXZ < 0.01f) attachRadiusXZ = 0.01f;
            if (climbSpeedToAnimDivisor < 0.01f) climbSpeedToAnimDivisor = 0.01f;
            if (approachPointSampleRadius < 0.25f) approachPointSampleRadius = 0.25f;
            if (approachArrivalExtraToleranceMeters < 0f) approachArrivalExtraToleranceMeters = 0f;
            if (approachTimeoutSeconds < 5f) approachTimeoutSeconds = 5f;
            if (repathIntervalSeconds < 0.05f) repathIntervalSeconds = 0.05f;
            if (respawnAppearDelayMaxSeconds < 0f) respawnAppearDelayMaxSeconds = 0f;
            if (respawnHorizontalJitterMeters < 0f) respawnHorizontalJitterMeters = 0f;
        }
    }
}

using System.Collections;
using Game.Fishing;
using Game.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// AI 虚假钓鱼：NavMesh 巡逻 → 到点模拟抛竿/抽杆/展示 → 继续巡逻。无真实钓获逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-35)]
    public sealed class AIFakeFishingBehavior : MonoBehaviour
    {
        enum Phase
        {
            Patrol,
            ApproachingSpot,
            Fishing,
        }

        [Header("References")]
        [SerializeField] private NavMeshAIPatrolAgent patrol;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private AISimpleRodVisual rodVisual;
        [SerializeField] private PlayerRig rig;
        [SerializeField] private FishingPresentationFx presentationFx;

        [Header("Fishing spots")]
        [SerializeField] private Transform[] fishingSpots;
        [SerializeField] private float spotSampleRadius = 2f;
        [SerializeField] private float approachArrivalTolerance = 0.9f;
        [SerializeField] private float approachTimeoutSeconds = 45f;
        [Tooltip("寻路开始后至少等待该秒数再判定到达，避免路径尚未计算时误判。")]
        [SerializeField] private float approachMinSecondsBeforeArrival = 0.05f;

        [Header("Cadence")]
        [SerializeField] private float fishingIntervalMinSeconds = 8f;
        [SerializeField] private float fishingIntervalMaxSeconds = 24f;

        [Header("Animation timing")]
        [Tooltip("放杆动画完整播放时长（秒）；此期间不触发「放杆结束」。")]
        [SerializeField] private float castAnimationSeconds = 3f;
        [Tooltip("收杆阶段持续时长随机区间（秒）；进入收杆状态后只触发一次「收杆」。")]
        [SerializeField] private float reelDurationMinSeconds = 5f;
        [SerializeField] private float reelDurationMaxSeconds = 30f;
        [Tooltip("钓获展示动画时长（秒）。")]
        [SerializeField] private float fakeShowcaseSeconds = 2.2f;
        [Tooltip("触发「收杆结束」后等待收线/挂背的时长。")]
        [SerializeField] private float reelEndMountBackDelaySeconds = 0.65f;

        [Header("Cast target")]
        [Tooltip("无 CastTarget 子节点时：沿钓点 forward × 此距离作为水平落点。")]
        [SerializeField] private float castForwardDistance = 10f;
        [Tooltip("无 CastTarget 子节点时：落点 Y = 钓点 Y + 此偏移（请设为水面高度）。")]
        [SerializeField] private float waterSurfaceYOffset = 0f;

        [Header("Showcase fish")]
        [Tooltip("展示阶段随机选取其一挂到 HandFishRoot。")]
        [SerializeField] private GameObject[] showcaseFishPrefabs;
        [SerializeField] private Vector3 handFishLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 handFishLocalEuler = new Vector3(-90f, 180f, 0f);

        [Header("Celebrate")]
        [SerializeField] private AIFakeCatchCelebrateEffect celebrateEffect = AIFakeCatchCelebrateEffect.DefaultPool;
        [SerializeField] private GameObject celebrateStyle1Prefab;
        [SerializeField] private GameObject celebrateStyle2Prefab;
        [SerializeField] private GameObject celebrateStyle3Prefab;
        [SerializeField] private GameObject celebrateStyle4Prefab;
        [SerializeField] private float celebrateDurationSeconds = 2f;

        [Header("Animator triggers")]
        [SerializeField] private string castTriggerParam = "放杆";
        [SerializeField] private string castEndTriggerParam = "放杆结束";
        [SerializeField] private string reelTriggerParam = "收杆";
        [SerializeField] private string reelEndTriggerParam = "收杆结束";
        [SerializeField] private string catchShowcaseTriggerParam = "钓获展示";

        Phase _phase = Phase.Patrol;
        float _nextFishingReadyAt;
        Vector3 _sampledSpotPosition;
        Vector3 _castTarget;
        float _approachStartedAt;
        float _approachArrivalReadyAt;
        Coroutine _fishingRoutine;
        FishingLineVisual _lineVisual;
        GameObject _showcaseFishInstance;

        int _castTriggerId;
        int _castEndTriggerId;
        int _reelTriggerId;
        int _reelEndTriggerId;
        int _catchShowcaseTriggerId;
        bool _hasCastTrigger;
        bool _hasCastEndTrigger;
        bool _hasReelTrigger;
        bool _hasReelEndTrigger;
        bool _hasCatchShowcaseTrigger;

        Transform ActorTransform => agent != null ? agent.transform : transform;

        const string CastTargetChildName = "CastTarget";

        void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (patrol == null) patrol = GetComponent<NavMeshAIPatrolAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (rodVisual == null) rodVisual = GetComponent<AISimpleRodVisual>();
            if (rig == null) rig = GetComponent<PlayerRig>();
            if (rig == null && rodVisual != null)
                rig = rodVisual.Rig;
            if (presentationFx == null) presentationFx = GetComponent<FishingPresentationFx>();
            CacheTriggerIds();
            ScheduleNextFishing();
        }

        void Update()
        {
            switch (_phase)
            {
                case Phase.ApproachingSpot:
                    TickApproach();
                    patrol?.RefreshLocomotionAnimator();
                    break;
                case Phase.Fishing:
                    patrol?.SetLocomotionAnimatorIdle();
                    break;
            }
        }

        void CacheTriggerIds()
        {
            _castTriggerId = Animator.StringToHash(castTriggerParam);
            _castEndTriggerId = Animator.StringToHash(castEndTriggerParam);
            _reelTriggerId = Animator.StringToHash(reelTriggerParam);
            _reelEndTriggerId = Animator.StringToHash(reelEndTriggerParam);
            _catchShowcaseTriggerId = Animator.StringToHash(catchShowcaseTriggerParam);
            _hasCastTrigger = HasTrigger(castTriggerParam);
            _hasCastEndTrigger = HasTrigger(castEndTriggerParam);
            _hasReelTrigger = HasTrigger(reelTriggerParam);
            _hasReelEndTrigger = HasTrigger(reelEndTriggerParam);
            _hasCatchShowcaseTrigger = HasTrigger(catchShowcaseTriggerParam);
        }

        bool HasTrigger(string name)
        {
            if (animator == null || string.IsNullOrEmpty(name))
                return false;

            var ps = animator.parameters;
            for (var i = 0; i < ps.Length; i++)
            {
                if (ps[i].type == AnimatorControllerParameterType.Trigger && ps[i].name == name)
                    return true;
            }

            return false;
        }

        void ScheduleNextFishing()
        {
            var min = Mathf.Max(1f, fishingIntervalMinSeconds);
            var max = Mathf.Max(min, fishingIntervalMaxSeconds);
            _nextFishingReadyAt = Time.time + Random.Range(min, max);
        }

        void TickApproach()
        {
            if (agent == null || !agent.isOnNavMesh)
                return;

            if (Time.time - _approachStartedAt > approachTimeoutSeconds)
            {
                CancelFishingAttempt();
                return;
            }

            if (Time.time < _approachArrivalReadyAt)
                return;

            if (HasReachedApproachDestination())
                BeginFishingAtSpot();
        }

        bool HasReachedApproachDestination()
        {
            if (agent.pathPending)
                return false;

            var actorPos = ActorTransform.position;
            var flatToSpot = _sampledSpotPosition - actorPos;
            flatToSpot.y = 0f;
            var tolerance = Mathf.Max(0.1f, approachArrivalTolerance);
            var toleranceSqr = tolerance * tolerance;
            var flatDistSqr = flatToSpot.sqrMagnitude;

            // 已到达 NavMesh 终点：Unity 常会清掉 hasPath，此时以世界坐标距离为准。
            if (!agent.hasPath)
                return flatDistSqr <= toleranceSqr;

            // 路径仍在时：距离目标足够近且 agent 基本停住。
            if (flatDistSqr <= toleranceSqr)
            {
                var planarSpeed = agent.velocity;
                planarSpeed.y = 0f;
                return planarSpeed.sqrMagnitude <= 0.25f;
            }

            var arrivalRadius = Mathf.Max(agent.stoppingDistance, 0.05f) + tolerance;
            if (agent.remainingDistance > arrivalRadius)
                return false;

            var vel = agent.velocity;
            vel.y = 0f;
            return vel.sqrMagnitude <= 0.01f;
        }

        void BeginFishingAtSpot()
        {
            _phase = Phase.Fishing;
            patrol.enabled = false;
            StopAgentForFishing();
            patrol?.SetLocomotionAnimatorIdle();

            FaceCastDirection();
            _fishingRoutine = StartCoroutine(CoFakeFishingSession());
        }

        void StopAgentForFishing()
        {
            if (agent == null)
                return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        void FaceCastDirection()
        {
            var forward = _castTarget - ActorTransform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
                ActorTransform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        void EnsureLineVisualFromRod()
        {
            if (rodVisual == null)
                return;

            rodVisual.EnsureRodSpawned();
            _lineVisual = rodVisual.LineVisual;
        }

        IEnumerator CoFakeFishingSession()
        {
            ResetFishingTriggers();
            rodVisual?.MountOnHand();
            EnsureLineVisualFromRod();

            FireTrigger(_castTriggerId, _hasCastTrigger);
            PlayLineCastAnimation();

            yield return new WaitForSeconds(Mathf.Max(0.1f, castAnimationSeconds));

            FireTrigger(_reelTriggerId, _hasReelTrigger);

            var reelMin = Mathf.Max(0.5f, reelDurationMinSeconds);
            var reelMax = Mathf.Max(reelMin, reelDurationMaxSeconds);
            yield return new WaitForSeconds(Random.Range(reelMin, reelMax));

            if (presentationFx != null)
                presentationFx.PlayWaterSplash(_castTarget, "AIFakeCatch");

            yield return StartShowcasePhase();

            FireTrigger(_reelEndTriggerId, _hasReelEndTrigger);
            _lineVisual?.PlayRetractAnimation();
            yield return new WaitForSeconds(Mathf.Max(0.05f, reelEndMountBackDelaySeconds));
            rodVisual?.MountOnBack();
            _lineVisual?.SetRetractedImmediate();
            ResetFishingTriggers();

            _fishingRoutine = null;
            ResumePatrol();
        }

        void PlayLineCastAnimation()
        {
            if (_lineVisual == null)
                return;

            var actorPos = ActorTransform.position;
            var planarToTarget = _castTarget - actorPos;
            planarToTarget.y = 0f;

            var forward = planarToTarget.sqrMagnitude > 0.01f
                ? planarToTarget.normalized
                : ActorTransform.forward;

            var distance = planarToTarget.sqrMagnitude > 0.01f
                ? planarToTarget.magnitude
                : Mathf.Max(1f, castForwardDistance);

            _lineVisual.PlayCastAnimation(_castTarget.y, forward, distance);
        }

        IEnumerator StartShowcasePhase()
        {
            FireTrigger(_catchShowcaseTriggerId, _hasCatchShowcaseTrigger);
            SpawnShowcaseFish();
            PlayCelebrateEffect();

            var duration = Mathf.Max(0.05f, fakeShowcaseSeconds);
            yield return new WaitForSeconds(duration);

            ClearShowcaseFish();
            presentationFx?.StopFootCelebrate();
        }

        void SpawnShowcaseFish()
        {
            ClearShowcaseFish();

            if (showcaseFishPrefabs == null || showcaseFishPrefabs.Length == 0)
                return;

            GameObject prefab = null;
            for (var attempt = 0; attempt < showcaseFishPrefabs.Length && prefab == null; attempt++)
            {
                var candidate = showcaseFishPrefabs[Random.Range(0, showcaseFishPrefabs.Length)];
                if (candidate != null)
                    prefab = candidate;
            }

            if (prefab == null)
                return;

            var socket = ResolveHandFishSocket();
            if (socket == null)
                return;

            _showcaseFishInstance = Instantiate(prefab, socket, false);
            _showcaseFishInstance.transform.localPosition = handFishLocalPosition;
            _showcaseFishInstance.transform.localRotation = Quaternion.Euler(handFishLocalEuler);
            _showcaseFishInstance.transform.localScale = Vector3.one;
        }

        void ClearShowcaseFish()
        {
            if (_showcaseFishInstance == null)
                return;

            Destroy(_showcaseFishInstance);
            _showcaseFishInstance = null;
        }

        Transform ResolveHandFishSocket()
        {
            if (rig == null && rodVisual != null)
                rig = rodVisual.Rig;
            if (rig == null)
                rig = GetComponent<PlayerRig>();

            rig?.EnsureSceneSocketsResolved();
            if (rig?.handFishSocket != null)
                return rig.handFishSocket;

            var characterRoot = rig != null ? rig.characterSocket : null;
            if (characterRoot == null)
                characterRoot = transform;

            return CharacterHierarchySockets.FindDeepChildByName(
                characterRoot,
                CharacterHierarchySockets.HandFishRootTransformName);
        }

        void PlayCelebrateEffect()
        {
            if (presentationFx == null)
                return;

            var prefab = ResolveCelebratePrefab();
            var duration = Mathf.Max(0.1f, celebrateDurationSeconds);
            presentationFx.PlayFootCelebrateForAi(prefab, duration);
        }

        GameObject ResolveCelebratePrefab()
        {
            return celebrateEffect switch
            {
                AIFakeCatchCelebrateEffect.Style1 => celebrateStyle1Prefab,
                AIFakeCatchCelebrateEffect.Style2 => celebrateStyle2Prefab,
                AIFakeCatchCelebrateEffect.Style3 => celebrateStyle3Prefab,
                AIFakeCatchCelebrateEffect.Style4 => celebrateStyle4Prefab,
                _ => null,
            };
        }

        void FireTrigger(int id, bool has)
        {
            if (!has || animator == null)
                return;

            animator.SetTrigger(id);
        }

        void ResetFishingTriggers()
        {
            if (animator == null)
                return;

            if (_hasCastTrigger) animator.ResetTrigger(_castTriggerId);
            if (_hasCastEndTrigger) animator.ResetTrigger(_castEndTriggerId);
            if (_hasReelTrigger) animator.ResetTrigger(_reelTriggerId);
            if (_hasReelEndTrigger) animator.ResetTrigger(_reelEndTriggerId);
            if (_hasCatchShowcaseTrigger) animator.ResetTrigger(_catchShowcaseTriggerId);
        }

        void CancelFishingAttempt()
        {
            if (_fishingRoutine != null)
            {
                StopCoroutine(_fishingRoutine);
                _fishingRoutine = null;
            }

            FireTrigger(_castEndTriggerId, _hasCastEndTrigger);
            _lineVisual?.StopAllLineAnimations();
            _lineVisual?.SetRetractedImmediate();
            ClearShowcaseFish();
            presentationFx?.StopFootCelebrate();
            rodVisual?.MountOnBack();
            ResetFishingTriggers();
            ResumePatrol();
        }

        void ResumePatrol()
        {
            _phase = Phase.Patrol;
            if (agent != null)
            {
                agent.isStopped = false;
                agent.ResetPath();
            }

            if (patrol != null)
                patrol.enabled = true;

            ScheduleNextFishing();
        }

        void LateUpdate()
        {
            if (_phase != Phase.Patrol || fishingSpots == null || fishingSpots.Length == 0)
                return;

            if (Time.time < _nextFishingReadyAt || patrol == null || !patrol.enabled)
                return;

            if (!TryPickFishingSpot(out _, out _sampledSpotPosition, out _castTarget))
                return;

            _phase = Phase.ApproachingSpot;
            _approachStartedAt = Time.time;
            _approachArrivalReadyAt = Time.time + Mathf.Max(0.05f, approachMinSecondsBeforeArrival);
            patrol.enabled = false;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
                agent.SetDestination(_sampledSpotPosition);
            }
        }

        bool TryPickFishingSpot(out Transform spot, out Vector3 sampledPosition, out Vector3 castTarget)
        {
            spot = fishingSpots[Random.Range(0, fishingSpots.Length)];
            sampledPosition = spot != null ? spot.position : ActorTransform.position;
            castTarget = sampledPosition;

            if (spot != null)
            {
                var explicitCast = spot.Find(CastTargetChildName);
                if (explicitCast != null)
                {
                    castTarget = explicitCast.position;
                }
                else
                {
                    var forward = spot.forward;
                    forward.y = 0f;
                    if (forward.sqrMagnitude < 0.01f)
                        forward = ActorTransform.forward;
                    castTarget = spot.position + forward.normalized * castForwardDistance;
                    castTarget.y = spot.position.y + waterSurfaceYOffset;
                }
            }

            if (agent != null &&
                NavMesh.SamplePosition(sampledPosition, out var hit, spotSampleRadius, NavMesh.AllAreas))
            {
                sampledPosition = hit.position;
                return true;
            }

            return agent == null;
        }
    }
}

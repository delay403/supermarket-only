using UnityEngine;
using UnityEngine.AI;

namespace Game.SupermarketOrganize
{
    /// <summary>扫地机器人表现：NavMesh 寻路、拾取/放置抛物线动画。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SupermarketSweepRobotAgent : MonoBehaviour
    {
        enum Phase
        {
            Hidden = 0,
            Idle = 1,
            GoPick = 2,
            Pick = 3,
            GoPlace = 4,
            Place = 5,
        }

        const string LogTag = "[SweepRobot]";
        const float NavSampleRadius = 12f;
        const float NavVerticalProbeStep = 0.35f;
        const float NavVerticalProbeAbove = 8f;
        const float NavVerticalProbeBelow = 12f;
        const int HumanoidAgentTypeId = 0;
        const float DestinationRefreshInterval = 0.75f;
        const float DestinationRetargetThreshold = 0.36f;
        const float StatusLogInterval = 1f;

        [SerializeField] private GameObject visualRoot;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Transform handleTransform;
        [SerializeField] private float pickStopDistance = 0.55f;
        [SerializeField] private float placeStopDistance = 0.85f;
        [SerializeField] private bool debugLog = true;

        SupermarketOrganizeService _service;
        Phase _phase = Phase.Hidden;
        SupermarketItem _targetItem;
        SupermarketContainerZone _targetZone;
        bool _taskRunning;
        Vector3 _currentNavDestination;
        float _lastDestinationSetAt = -1f;
        float _nextStatusLogAt;

        public bool IsBusy => _taskRunning;

        void Awake()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
            EnsureHandleTransform();
            Log("Awake");
            LogAgentSnapshot("Awake");
        }

        void Start()
        {
            EnsureOnNavMesh();
        }

        public void Configure(SupermarketOrganizeService service)
        {
            _service = service;
            EnsureHandleTransform();
            Log($"Configure service={(service != null ? "ok" : "null")}");
        }

        public void SetActiveVisual(bool active)
        {
            Log($"SetActiveVisual active={active} phase={_phase}");

            if (visualRoot != null)
                visualRoot.SetActive(active);
            else
                gameObject.SetActive(active);

            if (agent != null)
            {
                if (active)
                {
                    agent.enabled = true;
                    var onNavMesh = EnsureOnNavMesh();
                    StopAndResetPath();
                    Log($"SetActiveVisual enabled agent onNavMesh={onNavMesh}");
                    LogAgentSnapshot("SetActiveVisual(active)");
                }
                else
                {
                    StopAndResetPath();
                    agent.enabled = false;
                    Log("SetActiveVisual disabled agent");
                }
            }
            else
            {
                Log("SetActiveVisual agent=null");
            }

            _phase = active ? Phase.Idle : Phase.Hidden;
            if (!active)
            {
                _taskRunning = false;
                _targetItem = null;
                _targetZone = null;
                _lastDestinationSetAt = -1f;
            }
        }

        public bool HasHeldItemPendingPlace
        {
            get
            {
                if (_service == null)
                    return false;

                SupermarketItem held;
                return _service.TryGetRobotHeldItem(handleTransform, out held);
            }
        }

        public bool TryBeginTask()
        {
            if (_service == null)
            {
                Log("TryBeginTask failed: service=null");
                return false;
            }

            if (_taskRunning)
            {
                Log("TryBeginTask skipped: task already running");
                return false;
            }

            if (_phase == Phase.Hidden)
            {
                Log("TryBeginTask failed: phase=Hidden");
                return false;
            }

            if (agent == null)
            {
                Log("TryBeginTask failed: agent=null");
                return false;
            }

            if (_service.TryGetRobotHeldItem(handleTransform, out var heldItem) && heldItem != null)
                return BeginPlaceHeldItem(heldItem);

            if (!_service.TryFindRobotTask(transform.position, out _targetItem, out _targetZone)
                || _targetItem == null || _targetZone == null)
            {
                Log($"TryBeginTask failed: no task pos={FormatVector(transform.position)}");
                return false;
            }

            Log($"TryBeginTask found item={_targetItem.name} zone={_targetZone.name} itemPos={FormatVector(_targetItem.transform.position)} zonePos={FormatVector(_targetZone.transform.position)}");

            if (!EnsureOnNavMesh())
            {
                Log("TryBeginTask failed: EnsureOnNavMesh=false");
                LogAgentSnapshot("TryBeginTask(no navmesh)");
                return false;
            }

            _taskRunning = true;
            _phase = Phase.GoPick;
            _lastDestinationSetAt = -1f;
            _nextStatusLogAt = 0f;

            var navigated = EnsureNavigationTo(_targetItem.transform.position);
            Log($"TryBeginTask started GoPick navigated={navigated}");
            LogAgentSnapshot("TryBeginTask(started)");
            return navigated;
        }

        bool BeginPlaceHeldItem(SupermarketItem heldItem)
        {
            _targetItem = heldItem;
            _targetZone = _service.FindBestPlaceZoneForItem(heldItem.ItemId, transform.position);
            _taskRunning = true;
            _phase = Phase.GoPlace;
            _lastDestinationSetAt = -1f;
            _nextStatusLogAt = 0f;

            if (_targetZone == null)
            {
                Log($"BeginPlaceHeldItem waiting: {heldItem.name} has no available shelf");
                return true;
            }

            if (!EnsureOnNavMesh())
            {
                Log("BeginPlaceHeldItem failed: EnsureOnNavMesh=false");
                _taskRunning = false;
                _targetItem = null;
                _targetZone = null;
                return false;
            }

            Log($"BeginPlaceHeldItem item={heldItem.name} zone={_targetZone.name}");
            var navigated = EnsureNavigationTo(GetZoneNavTarget(_targetZone));
            Log($"BeginPlaceHeldItem navigated={navigated}");
            return true;
        }

        void Update()
        {
            if (!_taskRunning || _service == null || agent == null || !agent.enabled)
                return;

            MaybeLogMovementStatus();

            switch (_phase)
            {
                case Phase.GoPick:
                    TickGoPick();
                    break;
                case Phase.GoPlace:
                    TickGoPlace();
                    break;
            }
        }

        void TickGoPick()
        {
            if (_targetItem == null || _targetItem.State != SupermarketItemState.OnGround)
            {
                Log($"TickGoPick abort: item missing or state={_targetItem?.State.ToString() ?? "null"}");
                FinishTask();
                return;
            }

            if (!EnsureNavigationTo(_targetItem.transform.position))
            {
                Log("TickGoPick waiting: EnsureNavigationTo=false");
                return;
            }

            if (IsApproachingDestination(pickStopDistance))
                return;

            Log($"TickGoPick arrived pickStopDistance={pickStopDistance} remaining={agent.remainingDistance:F2}");
            _phase = Phase.Pick;
            StopAndResetPath();
            _lastDestinationSetAt = -1f;

            if (!_service.TryRobotPickUp(_targetItem, handleTransform, OnPickComplete))
            {
                Log("TickGoPick failed: TryRobotPickUp=false");
                FinishTask();
            }
            else
            {
                Log("TickGoPick picking item (arc animation)");
            }
        }

        void TickGoPlace()
        {
            if (_targetItem == null || _targetItem.State != SupermarketItemState.InRobotHandle)
            {
                Log($"TickGoPlace abort: item missing or itemState={_targetItem?.State.ToString() ?? "null"}");
                FinishTask();
                return;
            }

            if (_targetZone == null || _targetZone.IsFull || !_targetZone.MatchesItem(_targetItem.ItemId))
            {
                _targetZone = _service.FindBestPlaceZoneForItem(_targetItem.ItemId, transform.position);
                if (_targetZone == null)
                {
                    Log($"TickGoPlace waiting: no shelf space for {_targetItem.name}");
                    StopAndResetPath();
                    _lastDestinationSetAt = -1f;
                    return;
                }

                Log($"TickGoPlace retarget zone={_targetZone.name}");
                _lastDestinationSetAt = -1f;
            }

            if (!EnsureNavigationTo(GetZoneNavTarget(_targetZone)))
            {
                Log("TickGoPlace waiting: EnsureNavigationTo=false");
                return;
            }

            if (IsApproachingDestination(placeStopDistance))
                return;

            Log($"TickGoPlace arrived placeStopDistance={placeStopDistance} remaining={agent.remainingDistance:F2}");
            _phase = Phase.Place;
            StopAndResetPath();
            _lastDestinationSetAt = -1f;

            if (!_service.TryRobotPlace(_targetItem, _targetZone, OnPlaceComplete))
            {
                Log("TickGoPlace failed: shelf unavailable, will retry");
                _targetZone = null;
                _phase = Phase.GoPlace;
                return;
            }

            Log("TickGoPlace placing item (arc animation)");
        }

        void OnPickComplete()
        {
            if (_targetItem == null || _targetItem.State != SupermarketItemState.InRobotHandle)
            {
                Log("OnPickComplete failed: held item missing");
                FinishTask();
                return;
            }

            _targetZone = _service.FindBestPlaceZoneForItem(_targetItem.ItemId, transform.position);
            if (_targetZone == null)
            {
                Log($"OnPickComplete waiting: no shelf space for {_targetItem.name}");
                _phase = Phase.GoPlace;
                _lastDestinationSetAt = -1f;
                _nextStatusLogAt = 0f;
                return;
            }

            Log($"OnPickComplete -> GoPlace zone={_targetZone.name}");
            _phase = Phase.GoPlace;
            _lastDestinationSetAt = -1f;
            _nextStatusLogAt = 0f;
            var navigated = EnsureNavigationTo(GetZoneNavTarget(_targetZone));
            Log($"OnPickComplete navigated={navigated}");
            LogAgentSnapshot("OnPickComplete");
        }

        void OnPlaceComplete()
        {
            Log("OnPlaceComplete task done");
            FinishTask();
        }

        void FinishTask()
        {
            Log($"FinishTask phase={_phase}");
            _taskRunning = false;
            _targetItem = null;
            _targetZone = null;
            _phase = Phase.Idle;
            _lastDestinationSetAt = -1f;
            StopAndResetPath();
        }

        void StopAndResetPath()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                if (debugLog && agent != null)
                    Log($"StopAndResetPath skipped enabled={agent.enabled} onNavMesh={agent.isOnNavMesh}");
                return;
            }

            agent.isStopped = true;
            agent.ResetPath();
        }

        bool EnsureNavigationTo(Vector3 worldTarget)
        {
            if (agent == null || !agent.enabled)
            {
                Log("EnsureNavigationTo failed: agent missing or disabled");
                return false;
            }

            if (!EnsureOnNavMesh())
            {
                Log("EnsureNavigationTo failed: not on navmesh");
                return false;
            }

            var usedAgentTypeSample = TryFindNavMeshPosition(worldTarget, agent.agentTypeID, out var navTarget)
                || TryFindNavMeshPosition(worldTarget, HumanoidAgentTypeId, out navTarget)
                || TrySampleNavMeshPoint(worldTarget, out navTarget);
            if (!usedAgentTypeSample)
                navTarget = worldTarget;

            var elapsed = Time.time - _lastDestinationSetAt;
            var needsUpdate = elapsed < 0f
                || !agent.hasPath
                || agent.pathStatus == NavMeshPathStatus.PathInvalid
                || (navTarget - _currentNavDestination).sqrMagnitude > DestinationRetargetThreshold * DestinationRetargetThreshold
                || elapsed >= DestinationRefreshInterval;

            if (!needsUpdate)
                return true;

            if (!agent.isOnNavMesh)
            {
                Log("EnsureNavigationTo failed: lost navmesh before SetDestination");
                return false;
            }

            agent.isStopped = false;
            var setOk = agent.SetDestination(navTarget);
            _currentNavDestination = navTarget;
            _lastDestinationSetAt = Time.time;

            Log($"EnsureNavigationTo target={FormatVector(worldTarget)} navTarget={FormatVector(navTarget)} agentTypeSample={usedAgentTypeSample} setOk={setOk} pathStatus={agent.pathStatus} hasPath={agent.hasPath}");
            return setOk;
        }

        bool IsApproachingDestination(float stopDistance)
        {
            if (!agent.isOnNavMesh)
                return true;

            if (agent.pathPending)
                return true;

            if (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid)
                return true;

            if (float.IsInfinity(agent.remainingDistance))
                return true;

            return agent.remainingDistance > stopDistance;
        }

        bool EnsureOnNavMesh()
        {
            if (agent == null || !agent.enabled)
            {
                Log("EnsureOnNavMesh failed: agent missing or disabled");
                return false;
            }

            if (agent.isOnNavMesh)
                return true;

            var preferredType = agent.agentTypeID;
            if (TryWarpAgentToNavMesh(preferredType))
                return true;

            if (preferredType != HumanoidAgentTypeId && TryWarpAgentToNavMesh(HumanoidAgentTypeId))
            {
                agent.agentTypeID = HumanoidAgentTypeId;
                Log($"EnsureOnNavMesh switched agentTypeId -> Humanoid ({HumanoidAgentTypeId})");
                return true;
            }

            Log($"EnsureOnNavMesh failed pos={FormatVector(transform.position)} preferredType={preferredType}");
            return false;
        }

        bool TryWarpAgentToNavMesh(int agentTypeId)
        {
            if (!TryFindNavMeshPosition(transform.position, agentTypeId, out var navPos))
            {
                Log($"TryWarpAgentToNavMesh sample failed type={agentTypeId} pos={FormatVector(transform.position)}");
                return false;
            }

            var before = transform.position;
            agent.enabled = false;
            transform.position = navPos;
            agent.enabled = true;

            if (!agent.isOnNavMesh)
                agent.Warp(navPos);

            var ok = agent.isOnNavMesh;
            Log($"TryWarpAgentToNavMesh type={agentTypeId} from={FormatVector(before)} to={FormatVector(navPos)} ok={ok}");
            return ok;
        }

        static bool TryFindNavMeshPosition(Vector3 reference, int agentTypeId, out Vector3 navPos)
        {
            var x = reference.x;
            var z = reference.z;
            var topY = reference.y + NavVerticalProbeAbove;
            var bottomY = reference.y - NavVerticalProbeBelow;

            for (var y = topY; y >= bottomY; y -= NavVerticalProbeStep)
            {
                if (TrySampleNavMeshPoint(new Vector3(x, y, z), agentTypeId, out navPos))
                    return true;
            }

            navPos = reference;
            return false;
        }

        void MaybeLogMovementStatus()
        {
            if (!debugLog || Time.time < _nextStatusLogAt)
                return;

            _nextStatusLogAt = Time.time + StatusLogInterval;
            LogAgentSnapshot($"phase={_phase}");
        }

        void LogAgentSnapshot(string context)
        {
            if (!debugLog)
                return;

            if (agent == null)
            {
                Log($"{context} | agent=null pos={FormatVector(transform.position)}");
                return;
            }

            var onNavMesh = agent.isOnNavMesh;
            var isStopped = onNavMesh && agent.isStopped;
            var remaining = onNavMesh ? agent.remainingDistance : float.PositiveInfinity;
            var destination = onNavMesh ? agent.destination : Vector3.positiveInfinity;

            Log(
                $"{context} | pos={FormatVector(transform.position)} enabled={agent.enabled} onNavMesh={onNavMesh} " +
                $"agentTypeId={agent.agentTypeID} isStopped={isStopped} hasPath={agent.hasPath} pathPending={agent.pathPending} " +
                $"pathStatus={agent.pathStatus} remaining={(float.IsInfinity(remaining) ? "Infinity" : remaining.ToString("F2"))} " +
                $"velocity={FormatVector(agent.velocity)} destination={FormatVector(destination)} speed={agent.speed:F2}");
        }

        void Log(string message)
        {
            if (!debugLog)
                return;

            Debug.Log($"{LogTag} {message}", this);
        }

        static string FormatVector(Vector3 value) =>
            $"({value.x:F2},{value.y:F2},{value.z:F2})";

        static bool TrySampleNavMeshPoint(Vector3 worldPos, out Vector3 navPos)
        {
            if (NavMesh.SamplePosition(worldPos, out var hit, NavSampleRadius, NavMesh.AllAreas))
            {
                navPos = hit.position;
                return true;
            }

            navPos = worldPos;
            return false;
        }

        static bool TrySampleNavMeshPoint(Vector3 worldPos, int agentTypeId, out Vector3 navPos)
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agentTypeId,
                areaMask = NavMesh.AllAreas,
            };

            if (NavMesh.SamplePosition(worldPos, out var hit, NavSampleRadius, filter))
            {
                navPos = hit.position;
                return true;
            }

            if (agentTypeId != 0 && NavMesh.SamplePosition(worldPos, out hit, NavSampleRadius, NavMesh.AllAreas))
            {
                navPos = hit.position;
                return true;
            }

            navPos = worldPos;
            return false;
        }

        static Vector3 GetZoneNavTarget(SupermarketContainerZone zone)
        {
            if (zone == null)
                return Vector3.zero;

            if (TryFindNavMeshPosition(zone.transform.position, HumanoidAgentTypeId, out var navPos)
                || TrySampleNavMeshPoint(zone.transform.position, out navPos))
                return navPos;

            return zone.transform.position;
        }

        void EnsureHandleTransform()
        {
            if (handleTransform != null)
                return;

            var handleGo = transform.Find("HandleTransform");
            if (handleGo == null)
            {
                handleGo = new GameObject("HandleTransform").transform;
                handleGo.SetParent(transform, false);
                handleGo.localPosition = new Vector3(0f, 0.35f, 0.15f);
            }

            handleTransform = handleGo;
        }
    }
}

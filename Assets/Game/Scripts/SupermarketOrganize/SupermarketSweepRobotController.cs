using Game.Reward;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>扫地机器人：免费解锁，驱动 NavMesh 机器人自动归位物品。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketSweepRobotController : MonoBehaviour
    {
        [SerializeField] private SupermarketOrganizeService service;
        [SerializeField] private SupermarketSweepRobotAgent agent;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private bool debugLog = true;

        const string LogTag = "[SweepRobot]";

        float _nextSweepAt;
        bool _wasActive;
        float _nextIdleLogAt;

        public bool IsActive => SupermarketOrganizeProgressionService.IsSweepRobotActive();

        public float RemainingSeconds => SupermarketOrganizeProgressionService.GetSweepRobotRemainingSeconds();

        public SupermarketSweepRobotAgent Agent => agent;

        void Awake() => ResolveAgentReference();

        public void Configure(SupermarketOrganizeService organizeService, Transform playerRoot = null)
        {
            service = organizeService;
            if (playerRoot != null)
                playerTransform = playerRoot;

            ResolveAgentReference();
            agent?.Configure(service);
            _nextSweepAt = Time.time;
            Log($"Configure service={(service != null ? "ok" : "null")} agent={(agent != null ? agent.name : "null")}");
        }

        public void BindAgent(SupermarketSweepRobotAgent sweepAgent)
        {
            agent = sweepAgent;
            agent?.Configure(service);
        }

        void Update()
        {
            if (agent == null)
                ResolveAgentReference();

            if (agent == null)
            {
                MaybeLogIdle("Update skipped: agent=null");
                return;
            }

            var gameRunning = service != null && service.IsGameRunning;
            var sweepActive = IsActive;
            var active = gameRunning && sweepActive;
            if (active != _wasActive)
            {
                Log($"Active changed -> {active} (gameRunning={gameRunning} sweepActive={sweepActive} remaining={RemainingSeconds:F0}s)");
                agent.SetActiveVisual(active);
                _wasActive = active;
                if (active)
                    _nextSweepAt = Time.time;
            }

            if (!active)
            {
                MaybeLogIdle($"Idle inactive gameRunning={gameRunning} sweepActive={sweepActive}");
                return;
            }

            if (agent.IsBusy)
            {
                MaybeLogIdle("Idle waiting: agent busy");
                return;
            }

            var config = SupermarketOrganizeProgressionConfig.ResolveRuntime();
            var interval = config != null ? config.sweepRobotIntervalSeconds : 15f;
            var retryDelay = agent.HasHeldItemPendingPlace ? 1f : interval;

            if (agent.HasHeldItemPendingPlace && Time.time >= _nextSweepAt)
            {
                Log("Retry place for robot-held item");
                if (agent.TryBeginTask())
                    _nextSweepAt = Time.time + 1f;
                else
                    _nextSweepAt = Time.time + 1f;
                return;
            }

            if (Time.time < _nextSweepAt)
                return;

            Log($"TryBeginTask at t={Time.time:F1}");
            if (agent.TryBeginTask())
            {
                Log($"TryBeginTask success nextSweepIn={retryDelay:F0}s");
                _nextSweepAt = Time.time + retryDelay;
            }
            else
            {
                Log("TryBeginTask failed retryIn=1s");
                _nextSweepAt = Time.time + 1f;
            }
        }

        public void TryUnlockFree()
        {
            FreeRewardGate.Grant(OnRewardGranted);
        }

        void OnRewardGranted()
        {
            var config = SupermarketOrganizeProgressionConfig.ResolveRuntime();
            var duration = config != null ? config.sweepRobotUnlockDurationSeconds : 300f;
            SupermarketOrganizeProgressionService.ActivateSweepRobot(duration);
            _nextSweepAt = Time.time;
        }

        void ResolveAgentReference()
        {
            if (agent != null)
                return;

            agent = GetComponentInChildren<SupermarketSweepRobotAgent>(true);
            if (agent != null)
                return;

#if UNITY_2023_1_OR_NEWER
            agent = FindFirstObjectByType<SupermarketSweepRobotAgent>(FindObjectsInactive.Include);
#else
            agent = FindObjectOfType<SupermarketSweepRobotAgent>(true);
#endif
            Log($"ResolveAgentReference agent={(agent != null ? agent.name : "null")}");
        }

        void MaybeLogIdle(string message)
        {
            if (!debugLog || Time.time < _nextIdleLogAt)
                return;

            _nextIdleLogAt = Time.time + 3f;
            Log(message);
        }

        void Log(string message)
        {
            if (!debugLog)
                return;

            Debug.Log($"{LogTag} {message}", this);
        }
    }
}

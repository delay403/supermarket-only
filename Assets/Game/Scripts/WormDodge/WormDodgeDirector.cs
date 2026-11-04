using System;
using System.Collections.Generic;
using Game.Core;
using Game.AI;
using Game.Economy;
using Game.Collectibles.Stats;
using Game.EscapeSnake;
using Game.Save;
using Game.UI;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// 躲避虫子局内导演：体力开局、存档点、胜负、楼层奖励、排行榜、结算。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeDirector : MonoBehaviour
    {
        public static WormDodgeDirector Instance { get; private set; }
        public static event Action RunReset;
        public static event Action RunStarted;

        [Header("References")]
        [SerializeField] private WormChainMotor wormMotor;
        [SerializeField] private WormDodgeParticipant[] participants;
        [SerializeField] private Transform runStartSpawn;

        [Header("Run")]
        [SerializeField] private bool autoStartRunOnPlay;
        [SerializeField] private bool openHomeOnSceneStart = true;
        [SerializeField] private float reviveInvincibilitySeconds = 2f;
        [SerializeField] private float reviveChasePauseSeconds = 2f;

        [Header("Debug")]
        [Tooltip("关闭可显著降低 CPU/GC：Inspector 调试字段、Scene 检测范围/移动目标 Gizmo。")]
        [SerializeField] private bool enableRuntimeDebugFeatures;

        [Header("Floor indexing")]
        [Tooltip("判定区世界高度聚类容差（米）。高度差在此范围内的区域视为同一层。")]
        [SerializeField] private float floorHeightTolerance = WormFloorHeightIndex.DefaultHeightTolerance;

        int _floorsThisRun;
        long _coinsThisRun;
        int _highestFloorThisRun;
        float _runStartTime;
        float _leaderboardSyncTimer;
        float _nextFloorPollTime;
        bool _runActive;
        bool _defeatPending;
        bool _reviveUsedThisRun;
        bool _doubleRewardClaimed;
        bool _runFinalized;
        float _chaseBlockedUntil;
        WormDodgeEndReason _pendingDefeatReason;
        Transform _lastCheckpoint;
        WormDodgeParticipant _player;
        WormFloorSettlementZone[] _floorZones;
        WormDodgeFloorRewardZone[] _floorRewardZones;
        List<float> _floorHeightBands;
        WormSafeZone[] _safeZones;

        public bool IsRunActive => _runActive;
        public bool IsDefeatPending => _defeatPending;
        public bool RuntimeDebugFeaturesEnabled => enableRuntimeDebugFeatures;
        public int FloorsThisRun => _floorsThisRun;
        public long CoinsThisRun => _coinsThisRun;
        public int HighestFloorThisRun => _highestFloorThisRun;
        public float RunElapsedSeconds => _runActive ? Time.time - _runStartTime : 0f;

        public event Action RunPausedForDefeat;
        public event Action RunResumedFromRevive;
        public event Action<WormDodgeResultViewModel> RunEnded;
        public event Action<int, long> FloorReached;
        public event Action<WormDodgeFloorRewardViewModel> FloorRewardPopupRequested;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ApplyRuntimeDebugSettings();
            ResolveReferences();
            EnsureSafeZonesCached();
        }

        void OnValidate()
        {
            if (Application.isPlaying && Instance == this)
                ApplyRuntimeDebugSettings();
        }

        void ApplyRuntimeDebugSettings() => WormDodgeRuntimeDebug.SetAllEnabled(enableRuntimeDebugFeatures);

        public void SetRuntimeDebugFeatures(bool enabled)
        {
            enableRuntimeDebugFeatures = enabled;
            ApplyRuntimeDebugSettings();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            UnhookMotor();
            UnhookParticipants();
        }

        void Start()
        {
            WormDodgeStaminaService.RefreshRegen();
            ResolveReferences();
            WormDodgeLeaderboardService.SyncFromParticipants(participants);
            HookMotor();
            HookParticipants();

            if (autoStartRunOnPlay)
                TryStartRunFromHome();
            else if (openHomeOnSceneStart)
                ShowHome();

            WormDodgeLeaderboardHud.RefreshAllInScene();
        }

        void Update()
        {
            if (_runActive && !_defeatPending)
            {
                if (Time.time >= _nextFloorPollTime)
                {
                    _nextFloorPollTime = Time.time + 0.35f;
                    PollParticipantFloorZones();
                }

                _leaderboardSyncTimer += Time.deltaTime;
                if (_leaderboardSyncTimer >= 1f)
                {
                    _leaderboardSyncTimer = 0f;
                    SyncAllToLeaderboard();
                }
            }
        }

        void PollParticipantFloorZones()
        {
            ResolveReferences();
            if (participants == null || participants.Length == 0)
                return;

            EnsureFloorZonesCached();
            if (_floorZones == null || _floorZones.Length == 0)
                return;

            for (var i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                if (participant == null || !participant.IsAliveForLeaderboard)
                    continue;

                var sample = participant.transform.position + Vector3.up * 0.9f;
                for (var z = 0; z < _floorZones.Length; z++)
                {
                    var zone = _floorZones[z];
                    if (zone == null || !zone.ContainsWorldPoint(sample))
                        continue;

                    NotifyParticipantReachedFloor(participant, zone);
                }
            }
        }

        void EnsureFloorZonesCached()
        {
            if (_floorZones != null && _floorZones.Length > 0 && _floorHeightBands != null && _floorHeightBands.Count > 0)
                return;

#if UNITY_2023_1_OR_NEWER
            _floorZones = FindObjectsByType<WormFloorSettlementZone>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            _floorRewardZones = FindObjectsByType<WormDodgeFloorRewardZone>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            _floorZones = FindObjectsOfType<WormFloorSettlementZone>();
            _floorRewardZones = FindObjectsOfType<WormDodgeFloorRewardZone>();
#endif
            RebuildFloorHeightBands();
        }

        void RebuildFloorHeightBands()
        {
            var heights = new List<float>();
            AppendZoneHeights(_floorZones, heights);
            AppendZoneHeights(_floorRewardZones, heights);
            _floorHeightBands = WormFloorHeightIndex.BuildBandHeightsAscending(
                heights,
                floorHeightTolerance);
        }

        static void AppendZoneHeights<T>(T[] zones, List<float> heights) where T : Component
        {
            if (zones == null || heights == null)
                return;

            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone == null)
                    continue;
                heights.Add(WormFloorHeightIndex.SampleZoneWorldHeight(zone));
            }
        }

        public int ResolveFloorIndexFromWorldHeight(float worldHeight) =>
            WormFloorHeightIndex.ResolveFloorIndex(worldHeight, _floorHeightBands, floorHeightTolerance);

        void InvalidateFloorZoneCache()
        {
            _floorZones = null;
            _floorRewardZones = null;
            _floorHeightBands = null;
        }

        void EnsureSafeZonesCached()
        {
            if (_safeZones != null && _safeZones.Length > 0)
                return;

#if UNITY_2023_1_OR_NEWER
            _safeZones = FindObjectsByType<WormSafeZone>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            _safeZones = FindObjectsOfType<WormSafeZone>();
#endif
            WormSafeZoneUtility.SetDirectorZones(_safeZones);
        }

        void InvalidateSafeZoneCache()
        {
            _safeZones = null;
            WormSafeZoneUtility.InvalidateCache();
        }

        public void ShowHome()
        {
            PauseRunSilently();
            ResetAllAiToLobbyIdle();
            UiRouter.OpenWormDodgeHome();
            RefreshHud();
        }

        public bool TryStartRunFromHome()
        {
            if (!WormDodgeStaminaService.TryConsumeForRun())
            {
                Debug.LogWarning("[WormDodge] 体力不足，无法开始游戏。");
                return false;
            }

            UiCoordinator.Close(UiPanelId.WormDodgeHome);
            BeginRun();
            return true;
        }

        public bool TryPlayAgainFromResult()
        {
            if (_defeatPending)
                FinalizeRun(_pendingDefeatReason, applyWithoutUi: true);

            if (!WormDodgeStaminaService.TryConsumeForRun())
                return false;

            UiCoordinator.Close(UiPanelId.WormDodgeResult);
            UiCoordinator.Close(UiPanelId.WormDodgeHome);
            BeginRun();
            return true;
        }

        public void BeginRun()
        {
            ResolveReferences();
            _runActive = true;
            _defeatPending = false;
            _reviveUsedThisRun = false;
            _doubleRewardClaimed = false;
            _runFinalized = false;
            _floorsThisRun = 0;
            _coinsThisRun = 0;
            _highestFloorThisRun = 0;
            _runStartTime = Time.time;
            _chaseBlockedUntil = 0f;
            _lastCheckpoint = runStartSpawn != null ? runStartSpawn : ResolveFallbackRunStartSpawn();

            RunReset?.Invoke();
            InvalidateFloorZoneCache();
            InvalidateSafeZoneCache();
            ResolveReferences();
            HookParticipants();
            ResetAllParticipantsForRunStart();

            HookAllMotors();
            ResetAndStartAllWormMotors();
            WormDodgeLeaderboardService.ResetForNewRun(participants);
            WormDodgeLeaderboardService.ForceRefresh();
            SyncAllToLeaderboard();
            RunStarted?.Invoke();
            RefreshHud();
        }

        static WormChainMotor[] GetAllWormMotors()
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<WormChainMotor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            return FindObjectsOfType<WormChainMotor>();
#endif
        }

        void ResetAndStartAllWormMotors()
        {
            var motors = GetAllWormMotors();
            if (motors.Length == 0)
            {
                Debug.LogWarning("[WormDodgeDirector] 场景中未找到 WormChainMotor。");
                return;
            }

            for (var i = 0; i < motors.Length; i++)
            {
                var motor = motors[i];
                if (motor == null)
                    continue;

                motor.ResetToPathStart();
                motor.BeginRunPatrol();
            }
        }

        void StopAllWormMotors(bool cancelChase = true)
        {
            var motors = GetAllWormMotors();
            for (var i = 0; i < motors.Length; i++)
            {
                var motor = motors[i];
                if (motor == null)
                    continue;

                if (cancelChase)
                    motor.CancelChase();

                motor.Stop();
            }
        }

        void ResumeAllWormMotors()
        {
            var motors = GetAllWormMotors();
            for (var i = 0; i < motors.Length; i++)
                motors[i]?.Play();
        }

        void CancelChaseForParticipant(WormDodgeParticipant participant)
        {
            if (participant == null)
                return;

            var chaseTarget = participant.ChaseTarget;
            if (chaseTarget == null)
                return;

            var motors = GetAllWormMotors();
            for (var i = 0; i < motors.Length; i++)
            {
                var motor = motors[i];
                if (motor != null && motor.IsChasing && motor.ActiveChaseTarget == chaseTarget)
                    motor.CancelChase();
            }
        }

        void ResetAllParticipantsForRunStart()
        {
            ResolveReferences();
            if (participants == null)
                return;

            for (var i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                if (participant == null)
                    continue;

                Transform spawnPoint;
                if (participant.IsPlayer)
                    spawnPoint = runStartSpawn != null ? runStartSpawn : ResolveFallbackRunStartSpawn();
                else
                    spawnPoint = participant.RespawnPoint != null
                        ? participant.RespawnPoint
                        : runStartSpawn != null ? runStartSpawn : ResolveFallbackRunStartSpawn();

                if (!participant.IsPlayer)
                {
                    var behavior = participant.GetComponentInParent<AIFakeWormDodgeBehavior>(true);
                    if (behavior != null)
                    {
                        behavior.ResetToRunStart();
                        continue;
                    }
                }

                participant.ResetToRunStart(spawnPoint);
            }
        }

        void ResetAllAiToLobbyIdle()
        {
            ResolveReferences();
            if (participants == null)
                return;

            for (var i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                if (participant == null || participant.IsPlayer)
                    continue;

                var behavior = participant.GetComponentInParent<AIFakeWormDodgeBehavior>();
                behavior?.ResetToLobbyIdle(participant.RespawnPoint);
            }
        }

        public void NotifyEnteredSafeZone(WormDodgeParticipant participant, WormSafeZone zone)
        {
            if (participant == null)
                return;

            zone?.RegisterParticipant(participant);
            participant.IsInSafeZone = true;
            if (participant.IsPlayer && zone != null && _runActive && !_defeatPending)
            {
                _lastCheckpoint = zone.SpawnPoint;
                participant.SetCheckpoint(zone.SpawnPoint);
            }

            CancelChaseForParticipant(participant);
        }

        public void NotifyExitedSafeZone(WormDodgeParticipant participant)
        {
            if (participant == null)
                return;

            participant.IsInSafeZone = false;
            EnsureSafeZonesCached();
            if (_safeZones != null)
            {
                for (var i = 0; i < _safeZones.Length; i++)
                    _safeZones[i]?.UnregisterParticipant(participant);
            }
        }

        public void NotifyParticipantReachedFloor(WormDodgeParticipant participant, WormFloorSettlementZone zone)
        {
            if (!_runActive || _defeatPending || participant == null || zone == null)
                return;

            if (zone.FloorIndex <= participant.HighestFloorReached)
                return;

            participant.RecordFloorReached(zone.FloorIndex);

            if (participant.IsPlayer)
            {
                _highestFloorThisRun = zone.FloorIndex;
                _floorsThisRun = zone.FloorIndex;
                if (zone.CoinReward > 0L)
                    _coinsThisRun += zone.CoinReward;

                FloorReached?.Invoke(zone.FloorIndex, zone.CoinReward);
            }

            SyncAllToLeaderboard();
            RefreshHud();
        }

        public void NotifyPlayerReachedFloor(WormDodgeParticipant player, WormFloorSettlementZone zone)
            => NotifyParticipantReachedFloor(player, zone);

        public void NotifyFloorRewardPopup(WormDodgeFloorRewardZone zone)
        {
            if (!_runActive || _defeatPending || zone == null)
                return;

            if (zone.FloorIndex > _highestFloorThisRun)
            {
                _highestFloorThisRun = zone.FloorIndex;
                _floorsThisRun = zone.FloorIndex;
                SyncAllToLeaderboard();
            }

            var vm = new WormDodgeFloorRewardViewModel
            {
                FloorIndex = zone.FloorIndex,
                CoinReward = zone.CoinReward,
            };
            FloorRewardPopupRequested?.Invoke(vm);
            UiRouter.OpenWormDodgeFloorReward(vm);
            RefreshHud();
        }

        public void GrantFloorPopupReward(long amount, bool doubled)
        {
            if (amount <= 0L)
                return;

            var grant = doubled ? amount * 2L : amount;
            _coinsThisRun += grant;
            RefreshHud();
        }

        public void GrantPickupCoin(long amount)
        {
            if (!_runActive || _defeatPending || amount <= 0L)
                return;

            _coinsThisRun += amount;
            RefreshHud();
        }

        public void NotifyPlayerReachedFinish(WormDodgeParticipant player)
        {
            if (!_runActive || _defeatPending || player == null || !player.IsPlayer)
                return;

            FinalizeRun(WormDodgeEndReason.Victory);
        }

        void OnParticipantEliminated(WormDodgeParticipant participant, WormChaseDeathReason reason)
        {
            if (!_runActive || _defeatPending || participant == null)
                return;

            if (participant.IsPlayer)
            {
                HandlePlayerDefeat(reason == WormChaseDeathReason.CaughtByWorm
                    ? WormDodgeEndReason.Caught
                    : WormDodgeEndReason.Eliminated);
                return;
            }

            SyncAllToLeaderboard();
            RefreshHud();
        }

        void HandlePlayerDefeat(WormDodgeEndReason reason)
        {
            _defeatPending = true;
            _pendingDefeatReason = reason;
            StopAllWormMotors();
            WormDodgeLeaderboardService.MarkPlayerEliminated(_highestFloorThisRun);
            SyncAllToLeaderboard();

            var save = SaveService.Data;
            save?.EnsureWormDodgeSectionInitialized();

            var vm = BuildResultViewModel(reason, isVictory: false, canRevive: !_reviveUsedThisRun);
            RunPausedForDefeat?.Invoke();
            UiRouter.OpenWormDodgeResult(vm);
            RefreshHud();
        }

        public bool TryRevivePlayer()
        {
            if (!_defeatPending || _reviveUsedThisRun || _player == null)
                return false;

            _reviveUsedThisRun = true;
            _defeatPending = false;

            var invincibility = Mathf.Max(0f, reviveInvincibilitySeconds);
            var chasePause = Mathf.Max(invincibility, reviveChasePauseSeconds);
            _player.ChaseTarget?.ReviveWithInvincibility(invincibility);

            var point = _lastCheckpoint != null ? _lastCheckpoint : runStartSpawn;
            if (point == null)
                point = ResolveFallbackRunStartSpawn();
            if (point != null)
                _player.TeleportTo(point);

            PauseChaseTargeting(chasePause);
            ResumeAllWormMotors();
            SyncAllToLeaderboard();
            RunResumedFromRevive?.Invoke();
            RefreshHud();
            return true;
        }

        void PauseChaseTargeting(float seconds)
        {
            if (seconds <= 0f)
                return;

            _chaseBlockedUntil = Mathf.Max(_chaseBlockedUntil, Time.time + seconds);
            var motors = GetAllWormMotors();
            for (var i = 0; i < motors.Length; i++)
            {
                var motor = motors[i];
                if (motor == null)
                    continue;

                motor.CancelChase();
                motor.BlockPatrolTargetScan(seconds);
            }
        }

        Transform ResolveFallbackRunStartSpawn()
        {
            if (runStartSpawn != null)
                return runStartSpawn;

            var named = GameObject.Find("RunStartSpawn") ?? GameObject.Find("SpawnPoint");
            return named != null ? named.transform : null;
        }

        public void FinalizeRunAndReturnHome()
        {
            if (_defeatPending)
                FinalizeRun(_pendingDefeatReason, applyWithoutUi: true);

            ShowHome();
        }

        public void FinalizeRun(WormDodgeEndReason reason, bool applyWithoutUi = false)
        {
            if (_runFinalized)
                return;

            if (!_runActive && !_defeatPending)
                return;

            _runFinalized = true;

            var isVictory = reason == WormDodgeEndReason.Victory;
            _runActive = false;
            _defeatPending = false;
            StopAllWormMotors();

            var save = SaveService.Data;
            if (save != null)
            {
                save.EnsureWormDodgeSectionInitialized();
                var wd = save.wormDodge;
                wd.totalRuns++;
                wd.totalCoinsEarned += _coinsThisRun;
                if (_highestFloorThisRun > wd.bestFloorsReached)
                    wd.bestFloorsReached = _highestFloorThisRun;

                if (isVictory)
                {
                    wd.totalVictories++;
                    var clearTime = Time.time - _runStartTime;
                    if (wd.bestClearTimeSeconds <= 0f || clearTime < wd.bestClearTimeSeconds)
                        wd.bestClearTimeSeconds = clearTime;
                }
            }

            if (_coinsThisRun > 0)
            {
                var bonusCoins = WormDodgeStatsAggregator.Compute().ApplyCoinBonus(_coinsThisRun);
                RewardApplier.Apply(new Rewards { coins = bonusCoins });
            }

            SaveService.Save();

            var vm = BuildResultViewModel(reason, isVictory, canRevive: false);
            RunEnded?.Invoke(vm);

            if (!applyWithoutUi)
                UiRouter.OpenWormDodgeResult(vm);

            UiCoordinator.Hud?.RefreshEconomyDisplay();
            RefreshHud();
        }

        public bool TryApplyDoubleRunReward()
        {
            if (_doubleRewardClaimed || _coinsThisRun <= 0L)
                return false;

            _doubleRewardClaimed = true;
            var bonusCoins = WormDodgeStatsAggregator.Compute().ApplyCoinBonus(_coinsThisRun);
            RewardApplier.Apply(new Rewards { coins = bonusCoins });
            SaveService.Save();
            UiCoordinator.Hud?.RefreshEconomyDisplay();
            return true;
        }

        WormDodgeResultViewModel BuildResultViewModel(WormDodgeEndReason reason, bool isVictory, bool canRevive)
        {
            var save = SaveService.Data;
            save?.EnsureWormDodgeSectionInitialized();
            var clearTime = Mathf.Max(0f, Time.time - _runStartTime);

            return new WormDodgeResultViewModel
            {
                IsVictory = isVictory,
                EndReason = reason,
                FloorsReached = _highestFloorThisRun,
                CoinsEarned = _coinsThisRun,
                BestFloorsRecord = save?.wormDodge?.bestFloorsReached ?? _highestFloorThisRun,
                RunClearTimeSeconds = clearTime,
                BestClearTimeSeconds = save?.wormDodge?.bestClearTimeSeconds ?? 0f,
                CanRevive = canRevive,
                DoubleRewardClaimed = _doubleRewardClaimed,
            };
        }

        static Vector3 GetParticipantChaseSamplePosition(WormDodgeParticipant participant)
        {
            if (participant == null)
                return Vector3.zero;

            return participant.GetChaseDetectPosition();
        }

        void PauseRunSilently()
        {
            _runActive = false;
            _defeatPending = false;
            StopAllWormMotors();
        }

        WormChaseTarget PickChaseTargetDuringPatrol(WormChainMotor motor)
        {
            if (!_runActive || _defeatPending || motor == null)
                return null;

            if (!motor.IsPatrolling || motor.IsChasing || motor.IsReturningToPath)
                return null;

            if (Time.time < _chaseBlockedUntil)
                return null;

            var list = BuildEligibleTargetsInChaseRange(motor);
            if (list.Count == 0)
                return null;

            return PickWeightedTarget(list).ChaseTarget;
        }

        static WormDodgeParticipant PickWeightedTarget(List<WormDodgeParticipant> list)
        {
            if (list.Count == 1)
                return list[0];

            var ai = new List<WormDodgeParticipant>();
            var players = new List<WormDodgeParticipant>();
            for (var i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p == null)
                    continue;

                if (p.IsPlayer)
                    players.Add(p);
                else
                    ai.Add(p);
            }

            if (ai.Count > 0 && players.Count > 0)
            {
                var pool = UnityEngine.Random.value < 0.65f ? ai : players;
                return pool[UnityEngine.Random.Range(0, pool.Count)];
            }

            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        List<WormDodgeParticipant> BuildEligibleTargetsInChaseRange(WormChainMotor motor)
        {
            var list = new List<WormDodgeParticipant>();
            if (motor == null || participants == null)
                return list;

            for (var i = 0; i < participants.Length; i++)
            {
                var p = participants[i];
                if (p == null || !p.IsEligibleForChase)
                    continue;

                if (!motor.IsTargetWithinChaseRange(GetParticipantChaseSamplePosition(p)))
                    continue;

                list.Add(p);
            }

            return list;
        }

        void SyncAllToLeaderboard()
        {
            ResolveReferences();
            if (_player != null)
                _player.RecordFloorReached(_highestFloorThisRun);

            WormDodgeLeaderboardService.SyncFromParticipants(participants);
        }

        public void RefreshHud()
        {
            WormDodgeHudSection.RefreshAllInScene();
            WormDodgeLeaderboardHud.RefreshAllInScene();
        }

        void ResolveReferences()
        {
            if (wormMotor == null)
                wormMotor = FindObjectOfType<WormChainMotor>();

            participants = GatherSceneParticipants();

            if (_player == null && participants != null)
            {
                for (var i = 0; i < participants.Length; i++)
                {
                    if (participants[i] != null && participants[i].IsPlayer)
                    {
                        _player = participants[i];
                        break;
                    }
                }
            }
        }

        static WormDodgeParticipant[] GatherSceneParticipants()
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<WormDodgeParticipant>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
#else
            return FindObjectsOfType<WormDodgeParticipant>(true);
#endif
        }

        void HookAllMotors()
        {
            UnhookAllMotors();
            var motors = GetAllWormMotors();
            for (var i = 0; i < motors.Length; i++)
            {
                var motor = motors[i];
                if (motor == null)
                    continue;

                motor.SetPatrolTargetPicker(() => PickChaseTargetDuringPatrol(motor));
            }
        }

        void UnhookAllMotors()
        {
            var motors = GetAllWormMotors();
            for (var i = 0; i < motors.Length; i++)
                motors[i]?.SetPatrolTargetPicker(null);
        }

        void HookMotor() => HookAllMotors();

        void UnhookMotor() => UnhookAllMotors();

        void HookParticipants()
        {
            UnhookParticipants();
            if (participants == null)
                return;

            for (var i = 0; i < participants.Length; i++)
            {
                var p = participants[i];
                if (p != null)
                    p.Eliminated += OnParticipantEliminated;
            }
        }

        void UnhookParticipants()
        {
            if (participants == null)
                return;

            for (var i = 0; i < participants.Length; i++)
            {
                var p = participants[i];
                if (p != null)
                    p.Eliminated -= OnParticipantEliminated;
            }
        }

        public void RefreshSceneServices()
        {
            if (!GameProductSettings.IsWormDodgeContentEnabled)
                return;

            ResolveReferences();
            HookAllMotors();
            HookParticipants();
            WormDodgeStaminaService.RefreshRegen();
            WormDodgeLeaderboardService.SyncFromParticipants(participants);
            WormDodgeLeaderboardService.ForceRefresh();
            if (_runActive)
                ResumeAllWormMotors();
            RefreshHud();

            if (openHomeOnSceneStart && !_runActive && !autoStartRunOnPlay)
                ShowHome();
        }
    }

    public enum WormDodgeEndReason
    {
        Caught,
        Eliminated,
        Victory,
        Manual
    }
}

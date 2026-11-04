using System;
using System.Collections;
using Game.Audio;
using Game.Collectibles.Stats;
using Game.Core;
using Game.Economy;
using Game.Fishing.Config;
using Game.Maps;
using Game.Player;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 挂在玩家根节点：在钓鱼区域内可开始/取消钓鱼；收杆增长进度，满则随机获得鱼并奖励金币。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFishingController : MonoBehaviour
    {
        public event Action<FishingSessionState> SessionStarted;
        public event Action GameplayStarted;
        public event Action SessionCancelled;
        public event Action<FishingService.ReelResult> ReelApplied;
        public event Action<FishingService.CatchResult> CatchCompleted;

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private FishingStatsAggregator statsAggregator;
        [SerializeField] private float gameplayStartDelaySeconds = 1f;
        [Tooltip("抽杆连点最小间隔（秒）；仅限制逻辑，不隐藏按钮。")]
        [SerializeField] private float reelInputMinIntervalSeconds = 0.15f;

        FishingZone _insideZone;
        FishingZone _activeZone;
        bool _sessionActive;
        bool _gameplayActive;
        float _progress;
        int _catchCoinMultiplier = 1;
        bool _motorWasEnabled;
        Vector3 _anchorPosition;
        Quaternion _anchorRotation;
        Coroutine _gameplayDelayCoroutine;
        float _reelLockedUntilUnscaled;

        public bool IsInsideFishingZone => _insideZone != null;
        public bool IsSessionActive => _sessionActive;
        /// <summary>放杆/收杆表现已开始，进度与抽杆等逻辑是否可用。</summary>
        public bool IsGameplayActive => _sessionActive && _gameplayActive;

        /// <summary>抽杆连点间隔中（不影响按钮显示）。</summary>
        public bool IsReelInputAllowed =>
            _gameplayActive && Time.unscaledTime >= _reelLockedUntilUnscaled;

        /// <summary>HUD 是否显示抽杆按钮（抛竿/收鱼展示阶段为 false）。</summary>
        public bool CanShowReelButton()
        {
            if (!_sessionActive || !_gameplayActive)
                return false;

            var lineDriver = GetComponent<PlayerFishingLineDriver>();
            var visual = lineDriver != null ? lineDriver.LineVisual : null;
            return visual == null || visual.State != FishingLineVisual.LineVisualState.CastAnimating;
        }

        public FishingZone ActiveZone => _activeZone;

        /// <summary>当前或最近所在钓鱼区的配置（进行中优先 ActiveZone，否则为 InsideZone）。</summary>
        public FishingZoneDef CurrentZoneDef =>
            _activeZone != null ? _activeZone.ZoneDef : _insideZone?.ZoneDef;
        public float Progress01 => _activeZone?.ZoneDef != null && _gameplayActive
            ? Mathf.Clamp01(_progress / FishingService.GetProgressMax(_activeZone.ZoneDef))
            : 0f;

        public float ProgressCurrent => _gameplayActive ? _progress : 0f;
        public float ProgressMax => _activeZone?.ZoneDef != null
            ? FishingService.GetProgressMax(_activeZone.ZoneDef)
            : 0f;

        public int CatchCoinMultiplier => Mathf.Max(1, _catchCoinMultiplier);

        public void SetCatchCoinMultiplier(int multiplier) =>
            _catchCoinMultiplier = Mathf.Max(1, multiplier);

        void Awake()
        {
            if (!GameProductSettings.IsFishingContentEnabled)
            {
                enabled = false;
                return;
            }

            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (statsAggregator == null) statsAggregator = GetComponent<FishingStatsAggregator>();
            if (statsAggregator == null)
                statsAggregator = gameObject.AddComponent<FishingStatsAggregator>();

            if (GetComponent<PlayerFishingLineDriver>() == null)
                gameObject.AddComponent<PlayerFishingLineDriver>();

            EnsureFishingInputComponents();
            EnsureFishingPresentationComponents();
        }

        /// <summary>保证键盘 F/X 测试与 HUD 按钮能调到本控制器（各组件 DisallowMultiple，不会重复添加）。</summary>
        void EnsureFishingInputComponents()
        {
            if (!GameProductSettings.IsFishingContentEnabled)
                return;

            if (GetComponent<FishingHudInputBridge>() == null)
                gameObject.AddComponent<FishingHudInputBridge>();

            if (GetComponent<FishingPlaytestInput>() == null)
                gameObject.AddComponent<FishingPlaytestInput>();
        }

        void EnsureFishingPresentationComponents()
        {
            if (!GameProductSettings.IsFishingContentEnabled)
                return;

            if (GetComponent<PlayerFishingAnimatorDriver>() == null)
                gameObject.AddComponent<PlayerFishingAnimatorDriver>();

            if (GetComponent<PlayerFishingCatchPresenter>() == null)
                gameObject.AddComponent<PlayerFishingCatchPresenter>();

            if (GetComponent<FishingPresentationFx>() == null)
                gameObject.AddComponent<FishingPresentationFx>();

            if (GetComponent<FishingCatchNotificationPresenter>() == null)
                gameObject.AddComponent<FishingCatchNotificationPresenter>();

            if (GetComponent<FishingCameraFeedback>() == null)
                gameObject.AddComponent<FishingCameraFeedback>();

            if (GetComponent<PlayerFishingCatchShowcaseDriver>() == null)
                gameObject.AddComponent<PlayerFishingCatchShowcaseDriver>();

            if (GetComponent<PlayerAutoFishingController>() == null)
                gameObject.AddComponent<PlayerAutoFishingController>();
        }

        void OnEnable()
        {
            SceneTransitionCoroutineHost.SceneTransitionStarting += OnSceneTransitionStarting;
            if (GameplayModuleContext.IsModuleActive(GameplayModule.Fishing))
                DiscountOfferEventManager.RegisterFishing(this);
        }

        void OnDisable()
        {
            SceneTransitionCoroutineHost.SceneTransitionStarting -= OnSceneTransitionStarting;
            DiscountOfferEventManager.UnregisterFishing(this);
            if (_sessionActive)
                CancelSession(FishingCancelReason.Forced);
        }

        void Start()
        {
            RefreshNearbyFishingZones();
        }

        void RefreshNearbyFishingZones()
        {
            if (motor == null)
                return;

            var cc = characterController != null ? characterController : motor.GetComponent<CharacterController>();
            if (cc == null)
                return;

            var center = cc.bounds.center;
            var halfExtents = cc.bounds.extents;
            var hits = Physics.OverlapBox(center, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < hits.Length; i++)
            {
                var zone = hits[i].GetComponent<FishingZone>();
                if (zone != null)
                    zone.NotifyPlayerEnter(motor);
            }
        }

        public void NotifyEnterZone(FishingZone zone)
        {
            if (zone == null || !GameProductSettings.IsFishingContentEnabled)
                return;
            _insideZone = zone;
            Debug.Log($"[PlayerFishingController] 进入钓鱼区 '{zone.name}'");
        }

        public void NotifyExitZone(FishingZone zone)
        {
            if (zone == null)
                return;

            if (_insideZone == zone)
                _insideZone = null;

            if (_sessionActive && _activeZone == zone)
                CancelSession(FishingCancelReason.LeftZone);
        }

        /// <summary>在钓鱼区域内开始钓鱼（可由 UI 钓鱼按钮调用）。</summary>
        public bool TryStartFishing()
        {
            if (!GameProductSettings.IsFishingContentEnabled)
            {
                Debug.LogWarning("[PlayerFishingController] 当前产品形态未启用钓鱼（GameProductSettings）。");
                return false;
            }

            if (_sessionActive)
            {
                Debug.Log("[PlayerFishingController] 已在钓鱼中。");
                return false;
            }

            var lineDriver = GetComponent<PlayerFishingLineDriver>();
            if (lineDriver != null && lineDriver.IsCatchRetrieveBlockingRefish)
            {
                Debug.Log("[PlayerFishingController] 收鱼中，请等待鱼完全升起后再钓鱼。");
                return false;
            }

            var catchNotifier = GetComponent<FishingCatchNotificationPresenter>();
            if (catchNotifier != null && catchNotifier.IsAwaitingCatchRewardToast)
            {
                Debug.Log("[PlayerFishingController] 钓获结算中，请等待奖励提示出现后再操作。");
                return false;
            }

            if (lineDriver != null && lineDriver.IsPostCatchPresentationActive)
                lineDriver.ForceResetPostCatchPresentation();

            if (_insideZone == null)
            {
                Debug.LogWarning("[PlayerFishingController] 未在钓鱼区内，无法开始。请走进 FishingZone 触发器。");
                return false;
            }

            if (_insideZone.ZoneDef == null)
            {
                Debug.LogWarning($"[PlayerFishingController] 钓鱼区 '{_insideZone.name}' 未绑定 FishingZoneDef。");
                return false;
            }

            _activeZone = _insideZone;
            _sessionActive = true;
            _gameplayActive = false;
            _progress = 0f;
            _reelLockedUntilUnscaled = 0f;
            _catchCoinMultiplier = 1;
            _anchorPosition = _activeZone.GetFishingWorldPosition();
            _anchorRotation = _activeZone.GetFishingWorldRotation();

            _motorWasEnabled = motor != null && motor.enabled;
            if (motor != null)
                motor.enabled = false;

            transform.SetPositionAndRotation(_anchorPosition, _anchorRotation);
            if (characterController != null)
                characterController.Move(Vector3.zero);

            SessionStarted?.Invoke(new FishingSessionState
            {
                zone = _activeZone.ZoneDef,
                progressMax = FishingService.GetProgressMax(_activeZone.ZoneDef),
            });

            if (_gameplayDelayCoroutine != null)
                StopCoroutine(_gameplayDelayCoroutine);
            _gameplayDelayCoroutine = StartCoroutine(CoEnableGameplayAfterDelay());

            if (AutoFishingService.IsPassActive)
                AutoFishingService.SetModeEnabled(true);

            Debug.Log($"[PlayerFishingController] 开始钓鱼 zone={_activeZone.ZoneDef.name}");
            return true;
        }

        IEnumerator CoEnableGameplayAfterDelay()
        {
            var delay = Mathf.Max(0f, gameplayStartDelaySeconds);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            _gameplayDelayCoroutine = null;
            if (!_sessionActive)
                yield break;

            _gameplayActive = true;
            GameplayStarted?.Invoke();
        }

        /// <summary>取消进行中的钓鱼。</summary>
        public void CancelFishing()
        {
            if (!_sessionActive)
                return;
            CancelSession(FishingCancelReason.PlayerCancel);
        }

        /// <summary>收杆：增长进度；满则结算钓获。</summary>
        public bool TryReel()
        {
            if (!_sessionActive || !_gameplayActive || _activeZone?.ZoneDef == null || !IsReelInputAllowed)
                return false;

            var lineDriver = GetComponent<PlayerFishingLineDriver>();
            var visual = lineDriver != null ? lineDriver.LineVisual : null;
            if (visual != null && visual.State == FishingLineVisual.LineVisualState.CastAnimating)
                return false;

            statsAggregator?.Recompute();
            var stats = statsAggregator != null
                ? statsAggregator.Current
                : default;

            var reel = FishingService.ApplyReel(_activeZone.ZoneDef, _progress, stats);
            _progress = reel.progressCurrent;
            _reelLockedUntilUnscaled = Time.unscaledTime + Mathf.Max(0f, reelInputMinIntervalSeconds);
            ReelApplied?.Invoke(reel);
            AudioGameplayFeedback.PlayFishingReelPressRandom();

            if (!reel.completed)
                return true;

            var catchResult = FishingService.RollCatch(_activeZone.ZoneDef, stats, CatchCoinMultiplier);
            CatchCompleted?.Invoke(catchResult);
            EndSessionAfterCatch();
            return true;
        }

        void MaintainAnchorTransform()
        {
            transform.SetPositionAndRotation(_anchorPosition, _anchorRotation);
            if (characterController != null)
                characterController.Move(Vector3.zero);
        }

        void Update()
        {
            if (!_sessionActive)
                return;

            MaintainAnchorTransform();
        }

        void CancelSession(FishingCancelReason reason)
        {
            if (!_sessionActive)
                return;

            if (_gameplayDelayCoroutine != null)
            {
                StopCoroutine(_gameplayDelayCoroutine);
                _gameplayDelayCoroutine = null;
            }

            _sessionActive = false;
            _gameplayActive = false;
            _activeZone = null;
            _progress = 0f;

            if (motor != null)
                motor.enabled = _motorWasEnabled;

            SessionCancelled?.Invoke();
        }

        void EndSessionAfterCatch()
        {
            if (_gameplayDelayCoroutine != null)
            {
                StopCoroutine(_gameplayDelayCoroutine);
                _gameplayDelayCoroutine = null;
            }

            _sessionActive = false;
            _gameplayActive = false;
            _activeZone = null;
            _progress = 0f;

            if (motor != null)
                motor.enabled = _motorWasEnabled;
        }

        void OnSceneTransitionStarting()
        {
            if (_sessionActive)
                CancelSession(FishingCancelReason.SceneChange);
        }

        enum FishingCancelReason
        {
            PlayerCancel = 0,
            LeftZone = 1,
            SceneChange = 2,
            Forced = 3,
        }
    }

    public struct FishingSessionState
    {
        public FishingZoneDef zone;
        public float progressMax;
    }
}

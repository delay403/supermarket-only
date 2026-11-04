using Game.Collectibles.Equipment;
using Game.Collectibles.Stats;
using Game.Core;
using Game.Core.Config;
using Game.Fishing;
using Game.Maps;
using Game.Player;
using Game.UI.Framework;
using UnityEngine;

namespace Game.Features.StrengthTraining
{
    /// <summary>
    /// 力量训练池控制器：进入区域后弹出确认/解锁面板，确认后在当前位置开始训练，周期性提升 baseStrength。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStrengthTrainingController : MonoBehaviour
    {
        const float MoveInterruptThreshold = 0.12f;
        const float ExitValidationDelaySeconds = 0.18f;

        [SerializeField] private LegacyPlayerInput input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerFishingController fishing;
        [SerializeField] private FishingStatsAggregator fishingStats;
        [SerializeField] private EquipmentSpawner equipment;
        [SerializeField] private PlayerStrengthTrainingAnimatorDriver trainingAnimator;

        StrengthTrainingZone _activeZone;
        StrengthTrainingZone _pendingPanelZone;
        StrengthTrainingZone _deferredExitZone;
        float _deferredExitCheckTime;
        bool _sessionActive;
        float _sessionElapsed;
        float _intervalAccumulator;
        float _sessionStrengthGain;
        float _baseStrengthAtSessionStart;
        Vector3 _anchorPosition;
        Quaternion _anchorRotation;
        bool _motorWasEnabled;
        FishingLineVisual _lineVisual;
        bool _searchedLineVisual;

        public bool IsSessionActive => _sessionActive;
        public float SessionStrengthGain => _sessionStrengthGain;
        public string ActiveGroundId => _activeZone != null ? _activeZone.GroundId : string.Empty;

        void Awake()
        {
            if (input == null) input = GetComponent<LegacyPlayerInput>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (fishing == null) fishing = GetComponent<PlayerFishingController>();
            if (fishingStats == null) fishingStats = GetComponent<FishingStatsAggregator>();
            if (equipment == null) equipment = GetComponent<EquipmentSpawner>();
            if (trainingAnimator == null)
                trainingAnimator = GetComponent<PlayerStrengthTrainingAnimatorDriver>();
            if (trainingAnimator == null)
                trainingAnimator = gameObject.AddComponent<PlayerStrengthTrainingAnimatorDriver>();
        }

        void CacheLineVisual()
        {
            if (_searchedLineVisual && _lineVisual != null)
                return;
            _searchedLineVisual = true;
            _lineVisual = GetComponentInChildren<FishingLineVisual>(true);
        }

        void OnEnable()
        {
            SceneTransitionCoroutineHost.SceneTransitionStarting += OnSceneTransitionStarting;
        }

        void OnDisable()
        {
            SceneTransitionCoroutineHost.SceneTransitionStarting -= OnSceneTransitionStarting;
            if (_sessionActive)
                EndSession(showResult: false);
        }

        void Update()
        {
            if (_deferredExitZone != null && Time.unscaledTime >= _deferredExitCheckTime)
            {
                var zone = _deferredExitZone;
                _deferredExitZone = null;
                if (_sessionActive && _activeZone == zone && !zone.ContainsPoint(transform.position))
                    EndSession(showResult: true);
            }

            if (!_sessionActive)
                return;

            if (ShouldInterruptSession())
            {
                EndSession(showResult: true);
                return;
            }

            MaintainAnchorTransform();
            TickSession(Time.deltaTime);
        }

        public void NotifyEnterZone(StrengthTrainingZone zone)
        {
            if (zone == null || !GameProductSettings.IsFishingContentEnabled)
                return;

            if (_sessionActive)
                return;

            if (fishing != null && fishing.IsSessionActive)
                return;

            if (_pendingPanelZone == zone)
                return;

            if (_pendingPanelZone != null)
            {
                _pendingPanelZone = null;
                UiCoordinator.Close(UiPanelId.StrengthTrainingUnlock);
            }

            _pendingPanelZone = zone;
            var mode = StrengthTrainingService.IsGroundAvailableToday(zone.GroundId)
                ? StrengthTrainingPanelMode.ConfirmStart
                : StrengthTrainingPanelMode.Unlock;

            UiRouter.OpenStrengthTrainingUnlock(
                zone.GroundId,
                mode,
                zone.AllowFreeUnlock,
                zone.AllowGemUnlock,
                zone.PanelIcon,
                OnPanelStartRequested);
        }

        public void NotifyExitZone(StrengthTrainingZone zone)
        {
            if (zone == null)
                return;

            if (_pendingPanelZone == zone)
            {
                _pendingPanelZone = null;
                UiCoordinator.Close(UiPanelId.StrengthTrainingUnlock);
            }

            if (!_sessionActive || _activeZone != zone)
                return;

            _deferredExitZone = zone;
            _deferredExitCheckTime = Time.unscaledTime + ExitValidationDelaySeconds;
        }

        void OnPanelStartRequested()
        {
            var zone = _pendingPanelZone;
            _pendingPanelZone = null;
            if (zone == null || _sessionActive)
                return;

            if (!StrengthTrainingService.IsGroundAvailableToday(zone.GroundId))
                return;

            if (fishing != null && fishing.IsSessionActive)
                return;

            StartSession(zone);
        }

        void StartSession(StrengthTrainingZone zone)
        {
            _deferredExitZone = null;
            _activeZone = zone;
            _sessionActive = true;
            _sessionElapsed = 0f;
            _intervalAccumulator = 0f;
            _sessionStrengthGain = 0f;
            _baseStrengthAtSessionStart = StrengthTrainingService.GetBaseStrength();

            _anchorPosition = transform.position;
            var toCenter = zone.GetTrainingWorldPosition() - transform.position;
            toCenter.y = 0f;
            _anchorRotation = toCenter.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toCenter.normalized, Vector3.up)
                : zone.GetTrainingWorldRotation();

            _motorWasEnabled = motor != null && motor.enabled;
            if (motor != null)
                motor.enabled = false;

            if (characterController != null)
                characterController.Move(Vector3.zero);

            zone.StartTrainingEffect();

            equipment?.RefreshRod();
            equipment?.MountRodOnHand();

            var animSpeed = StrengthTrainingService.GetAnimSpeedForGround(zone.GroundId);
            trainingAnimator?.BeginPresentation(animSpeed);

            CacheLineVisual();
            _lineVisual?.SetGuideLineTarget(zone.GetTrainingWorldPosition());
        }

        void TickSession(float deltaTime)
        {
            if (deltaTime <= 0f || _activeZone == null)
                return;

            _sessionElapsed += deltaTime;
            _intervalAccumulator += deltaTime;

            var interval = StrengthTrainingService.GetIntervalSeconds();
            var perTick = StrengthTrainingService.GetBonusPerInterval(_activeZone.GroundId);
            fishingStats?.Recompute();
            var trainMul = fishingStats != null ? fishingStats.Current.totalTrainSpeedMul : 0f;
            var effectivePerTick = perTick * (1f + Mathf.Max(0f, trainMul));
            while (_intervalAccumulator >= interval)
            {
                _intervalAccumulator -= interval;
                _sessionStrengthGain += effectivePerTick;
                StrengthTrainingService.AddBaseStrength(effectivePerTick);
            }
        }

        void MaintainAnchorTransform()
        {
            transform.SetPositionAndRotation(_anchorPosition, _anchorRotation);
            if (characterController != null)
                characterController.Move(Vector3.zero);
        }

        bool ShouldInterruptSession()
        {
            if (fishing != null && fishing.IsSessionActive)
                return true;

            if (input == null)
                return false;

            if (input.Move.sqrMagnitude > MoveInterruptThreshold * MoveInterruptThreshold)
                return true;
            if (input.GlidePressed || input.UiActionPressedFromUi)
                return true;

            return false;
        }

        void OnSceneTransitionStarting()
        {
            if (_sessionActive)
                EndSession(showResult: true);
        }

        void EndSession(bool showResult)
        {
            if (!_sessionActive)
                return;

            _deferredExitZone = null;
            _activeZone?.StopTrainingEffect();
            _lineVisual?.ClearGuideLine();
            equipment?.MountRodOnBack();
            trainingAnimator?.EndPresentation();
            _sessionActive = false;
            _activeZone = null;
            _intervalAccumulator = 0f;

            if (motor != null)
                motor.enabled = _motorWasEnabled;

            if (showResult && _sessionElapsed > 0.05f)
            {
                Debug.Log(
                    $"[StrengthTraining] session +{_sessionStrengthGain:0.##} strength, total={StrengthTrainingService.GetBaseStrength():0.##}");
            }
        }
    }
}

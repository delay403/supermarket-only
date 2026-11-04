using System.Collections;
using Game.Reward;
using Game.Collectibles.Stats;
using Game.Core.Config;
using Game.Maps;
using Game.Player;
using Game.Tower;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;

namespace Game.Features.Treadmill
{
    /// <summary>
    /// 挂在玩家根节点：进入 <see cref="TreadmillZone"/> 后弹出解锁/确认面板；
    /// 确认后走向跑步点、播放跑步动画并按档位累计永久爬塔速度。
    /// 移动/跳跃输入、离开区域或场景切换会打断并弹出结算。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerTreadmillController : MonoBehaviour
    {
        const float MoveInterruptThreshold = 0.12f;
        const float ApproachArriveDistance = 0.08f;

        [SerializeField] private LegacyPlayerInput input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerAnimatorDriver anim;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private ClimbController climb;

        TreadmillZone _activeZone;
        TreadmillZone _pendingPanelZone;
        bool _sessionActive;
        bool _approaching;
        float _sessionElapsed;
        float _intervalAccumulator;
        float _sessionRunningBonusGain;
        float _sessionInstantBonusGain;
        float _permanentBonusAtSessionStart;
        Vector3 _anchorPosition;
        Quaternion _anchorRotation;
        bool _motorWasEnabled;
        Coroutine _approachCoroutine;

        public bool IsSessionActive => _sessionActive;
        public bool IsApproaching => _approaching;
        public float SessionElapsedSeconds => _sessionElapsed;
        public float SessionRunningBonusGain => _sessionRunningBonusGain;
        public float SessionInstantBonusGain => _sessionInstantBonusGain;
        public float SessionBonusGain => _sessionRunningBonusGain + _sessionInstantBonusGain;
        public TreadmillTier ActiveTier => _activeZone != null ? _activeZone.Tier : TreadmillTier.Basic;

        /// <summary>距下一次属性提升的进度 [0,1]，与 <see cref="TreadmillService.GetIntervalSeconds"/> 对齐。</summary>
        public float SessionIntervalProgress01
        {
            get
            {
                if (!_sessionActive)
                    return 0f;
                var interval = TreadmillService.GetIntervalSeconds();
                return interval > 0.001f ? Mathf.Clamp01(_intervalAccumulator / interval) : 0f;
            }
        }

        /// <summary>跑步中领取收益时，计入本次锻炼结算。</summary>
        public void RecordSessionInstantBonus(float bonus)
        {
            if (!_sessionActive || bonus <= 0f)
                return;

            _sessionInstantBonusGain += bonus;
        }

        void Awake()
        {
            if (input == null) input = GetComponent<LegacyPlayerInput>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (anim == null) anim = GetComponentInChildren<PlayerAnimatorDriver>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (climb == null) climb = GetComponent<ClimbController>();
        }

        void OnEnable()
        {
            SceneTransitionCoroutineHost.SceneTransitionStarting += OnSceneTransitionStarting;
        }

        void OnDisable()
        {
            SceneTransitionCoroutineHost.SceneTransitionStarting -= OnSceneTransitionStarting;
            CancelApproach();
            if (_sessionActive)
                EndSession(TreadmillEndReason.Forced, showResult: false);
        }

        void Update()
        {
            if (_approaching)
                return;

            if (!_sessionActive)
                return;

            if (ShouldInterruptSession())
            {
                EndSession(TreadmillEndReason.PlayerInput, showResult: true);
                return;
            }

            MaintainAnchorTransform();
            TickSession(Time.deltaTime);
        }

        public void NotifyEnterZone(TreadmillZone zone)
        {
            if (zone == null)
                return;

            if (_sessionActive || _approaching)
            {
                if (_activeZone == zone)
                    return;
                return;
            }

            if (climb != null && climb.State != ClimbController.ClimbState.Inactive)
                return;

            _pendingPanelZone = zone;
            var mode = TreadmillService.IsTierAvailableToday(zone.Tier)
                ? TreadmillPanelMode.ConfirmStart
                : TreadmillPanelMode.Unlock;

            UiCoordinator.Open(UiPanelId.TreadmillUnlock, new UiOpenPayload
            {
                TreadmillUnlock = new TreadmillUnlockOpenPayload
                {
                    Tier = zone.Tier,
                    Mode = mode,
                    AllowFreeUnlock = zone.AllowFreeUnlock,
                    AllowGemUnlock = zone.AllowGemUnlock,
                    Icon = zone.PanelIcon,
                    OnStartRequested = OnPanelStartRequested,
                },
            });
        }

        public void NotifyExitZone(TreadmillZone zone)
        {
            if (zone == null)
                return;

            if (_pendingPanelZone == zone)
                _pendingPanelZone = null;

            if (!_sessionActive || _activeZone != zone)
                return;

            EndSession(TreadmillEndReason.LeftZone, showResult: true);
        }

        void OnPanelStartRequested()
        {
            var zone = _pendingPanelZone;
            _pendingPanelZone = null;
            if (zone == null || _sessionActive || _approaching)
                return;
            if (!TreadmillService.IsTierAvailableToday(zone.Tier))
                return;
            if (climb != null && climb.State != ClimbController.ClimbState.Inactive)
                return;

            BeginApproachThenSession(zone);
        }

        void BeginApproachThenSession(TreadmillZone zone)
        {
            CancelApproach();
            _approachCoroutine = StartCoroutine(CoApproachThenSession(zone));
        }

        void CancelApproach()
        {
            if (_approachCoroutine != null)
            {
                StopCoroutine(_approachCoroutine);
                _approachCoroutine = null;
            }

            _approaching = false;
            anim?.SetForcedLocomotionSpeed(null);
        }

        IEnumerator CoApproachThenSession(TreadmillZone zone)
        {
            _approaching = true;
            var targetPos = zone.GetRunningWorldPosition();
            var targetRot = zone.GetRunningWorldRotation();
            var moveSpeed = ResolveApproachMoveSpeed();
            var runAnimSpeed = ConfigRegistry.Balance != null
                ? ConfigRegistry.Balance.treadmillRunAnimSpeed
                : 5f;

            anim?.SetForcedLocomotionSpeed(runAnimSpeed);

            while (HorizontalDistance(transform.position, targetPos) > ApproachArriveDistance)
            {
                var to = targetPos - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f)
                    break;

                var step = to.normalized * (moveSpeed * Time.deltaTime);
                if (characterController != null && characterController.enabled)
                    characterController.Move(step);
                else
                    transform.position += step;

                var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 12f);
                yield return null;
            }

            transform.SetPositionAndRotation(targetPos, targetRot);
            if (characterController != null)
                characterController.Move(Vector3.zero);

            _approaching = false;
            _approachCoroutine = null;
            StartSession(zone);
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static float ResolveApproachMoveSpeed()
        {
            var bal = ConfigRegistry.Balance;
            if (bal != null && bal.treadmillApproachMoveSpeed > 0.01f)
                return bal.treadmillApproachMoveSpeed;
            return bal != null && bal.moveSpeed > 0.01f ? bal.moveSpeed : 5f;
        }

        void StartSession(TreadmillZone zone)
        {
            _activeZone = zone;
            _sessionActive = true;
            _sessionElapsed = 0f;
            _intervalAccumulator = 0f;
            _sessionRunningBonusGain = 0f;
            _sessionInstantBonusGain = 0f;
            _permanentBonusAtSessionStart = TreadmillService.GetPermanentClimbSpeedBonus();
            _anchorPosition = zone.GetRunningWorldPosition();
            _anchorRotation = zone.GetRunningWorldRotation();

            _motorWasEnabled = motor != null && motor.enabled;
            if (motor != null)
                motor.enabled = false;

            var runSpeed = ConfigRegistry.Balance != null
                ? ConfigRegistry.Balance.treadmillRunAnimSpeed
                : 5f;
            anim?.SetForcedLocomotionSpeed(runSpeed);
            zone.StartRunningEffect();
        }

        void TickSession(float deltaTime)
        {
            if (deltaTime <= 0f || _activeZone == null)
                return;

            _sessionElapsed += deltaTime;
            _intervalAccumulator += deltaTime;

            var interval = TreadmillService.GetIntervalSeconds();
            var perTick = TreadmillService.GetBonusPerInterval(_activeZone.Tier);
            while (_intervalAccumulator >= interval)
            {
                _intervalAccumulator -= interval;
                _sessionRunningBonusGain += perTick;
                TreadmillService.AddPermanentClimbSpeedBonus(perTick);
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
            if (input == null)
                return false;

            if (input.Move.sqrMagnitude > MoveInterruptThreshold * MoveInterruptThreshold)
                return true;
            if (input.ClimbHeld)
                return true;
            if (input.GlidePressed || input.UiActionPressedFromUi)
                return true;

            if (climb != null && climb.State != ClimbController.ClimbState.Inactive)
                return true;

            return false;
        }

        void OnSceneTransitionStarting()
        {
            CancelApproach();
            if (!_sessionActive)
                return;
            EndSession(TreadmillEndReason.SceneChange, showResult: true);
        }

        void EndSession(TreadmillEndReason reason, bool showResult)
        {
            if (!_sessionActive)
                return;

            var tier = _activeZone != null ? _activeZone.Tier : TreadmillTier.Basic;
            var elapsed = _sessionElapsed;
            var runningGain = _sessionRunningBonusGain;
            var instantGain = _sessionInstantBonusGain;
            var sessionGain = runningGain + instantGain;
            var permanentAtStart = _permanentBonusAtSessionStart;
            var totalPermanent = TreadmillService.GetPermanentClimbSpeedBonus();
            var coreClimbSpeed = StatsAggregator.ComputeBaseAndTreadmillClimbSpeed(totalPermanent);

            _activeZone?.StopRunningEffect();
            _sessionActive = false;
            _activeZone = null;
            _intervalAccumulator = 0f;

            anim?.SetForcedLocomotionSpeed(null);
            if (motor != null)
                motor.enabled = _motorWasEnabled;

            if (showResult && elapsed > 0.05f)
            {
                UiCoordinator.Open(UiPanelId.TreadmillResult, new UiOpenPayload
                {
                    TreadmillResult = new TreadmillResultViewModel
                    {
                        Tier = tier,
                        DurationSeconds = elapsed,
                        SessionRunningGain = runningGain,
                        SessionInstantGain = instantGain,
                        SessionClimbSpeedGain = sessionGain,
                        PermanentBonusAtSessionStart = permanentAtStart,
                        TotalPermanentClimbSpeedBonus = totalPermanent,
                        CoreClimbSpeed = coreClimbSpeed,
                    },
                });
            }
        }

        enum TreadmillEndReason
        {
            PlayerInput = 0,
            LeftZone = 1,
            SceneChange = 2,
            Forced = 3,
        }
    }
}

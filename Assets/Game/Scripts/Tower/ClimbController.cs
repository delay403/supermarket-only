using UnityEngine;
using Game.Core.Config;
using Game.Glide;
using Game.Player;
using Game.Core;
using Game.Collectibles.Stats;
using Game.Audio;
using Game.Reward;

namespace Game.Tower
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ClimbController : MonoBehaviour
    {
        private const float MinGlideHeightFallbackMeters = 25f;
        private const float WindHeightReferenceCapMeters = 1000f;

        public enum ClimbState
        {
            Inactive = 0,
            Idle = 1,
            Moving = 2,
        }

        [Header("References")]
        [SerializeField] private LegacyPlayerInput input;
        [SerializeField] private PlayerMotor freeMoveMotor;
        [SerializeField] private PlayerAnimatorDriver anim;
        [SerializeField] private GlideController glide;
        [SerializeField] private ThirdPersonCamera cam;
        [SerializeField] private TowerManager tower;
        [SerializeField] private StatsAggregator stats;
        [SerializeField] private Stamina stamina;
        [SerializeField] private Hud.HudController hud;

        [Header("Tuning (fallback if no config)")]
        [SerializeField] private float climbSpeed = 2.0f;
        [SerializeField] private float baseAnimSpeed = 1.0f;
        [SerializeField] private float attachRadius = 2.0f;
        [Tooltip("竖直攀爬速度（m/s）写入 Animator ClimbSpeed 乘子时除以该值。例：5 表示 10m/s → 乘子 2（5:1 运动:播放对应）。")]
        [SerializeField] private float climbSpeedToAnimMultiplierDivisor = 4f;

        [Header("Facing")]
        [SerializeField] private bool forceWorldYawWhenEnteringClimb = true;
        [SerializeField] private float climbEnterWorldYawDegrees = 180f;

        public ClimbState State { get; private set; } = ClimbState.Inactive;

        /// <summary>为 true 时相当于持续按住向上攀爬（仍可叠加玩家手动输入）。<see cref="EnterClimb"/> 默认开启；<see cref="ExitClimb"/> 清零。</summary>
        public bool AutoAscendEnabled { get; set; }

        private CharacterController _cc;
        private float _towerBaseY;
        private float _suppressAutoEnterUntilUnscaledTime = -1f;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<LegacyPlayerInput>();
            if (freeMoveMotor == null) freeMoveMotor = GetComponent<PlayerMotor>();
            if (anim == null) anim = GetComponentInChildren<PlayerAnimatorDriver>();
            if (glide == null) glide = GetComponent<GlideController>();
            if (cam == null && Camera.main != null) cam = Camera.main.GetComponent<ThirdPersonCamera>();
            if (stats == null) stats = UnityObjectFinder.FindFirst<StatsAggregator>();
            if (stamina == null) stamina = GetComponent<Stamina>();
            if (tower == null) tower = UnityObjectFinder.FindFirst<TowerManager>();
            if (hud == null) hud = Game.UI.Framework.UiCoordinator.Hud;
            if (hud == null) hud = UnityObjectFinder.FindFirst<Hud.HudController>();

            var bal = ConfigRegistry.Balance;
            if (bal != null)
            {
                climbSpeed = bal.climbSpeedBase;
                baseAnimSpeed = bal.climbSpeedAnimBase;
            }
        }

        public void BindAnimatorDriver(PlayerAnimatorDriver driver) => anim = driver;

        private void Update()
        {
            if (State == ClimbState.Inactive) return;

            if (tower != null)
                tower.UpdateVisibility(transform.position.y);

            // Only allow upward climb; ignore downward input completely. 攀爬中 Move 为零，仅用 ClimbUpAxis。
            var moveAxisUp = input != null ? input.ClimbUpAxis : 0f;
            if (AutoAscendEnabled)
                moveAxisUp = Mathf.Max(moveAxisUp, 1f);
            // 已登顶且尚未完成开箱/HUD「跳跃」前：视为不再向上爬，避免自动攀爬持续扣体力、播攀爬动作。
            if (AtSummitWaitingForChestOrHudJump())
                moveAxisUp = 0f;
            var wantsMove = moveAxisUp > 0.1f;
            if (stamina != null)
                stamina.TickDrainWhileMoving(wantsMove);

            var useBonusSpeed = stamina == null || stamina.HasReserve;

            State = wantsMove ? ClimbState.Moving : ClimbState.Idle;

            cam?.SetClimbWindShakeActive(State == ClimbState.Moving);
            TickClimbAmbientWindAudio();

            var effectiveClimbSpeed = GetEffectiveClimbSpeed(useBonusSpeed);
            var v = wantsMove ? effectiveClimbSpeed : 0f;
            _cc.Move(new Vector3(0f, v * Time.deltaTime, 0f));

            if (tower != null && tower.MaxHeight > 0f)
            {
                var capY = tower.BaseY + tower.MaxHeight;
                var pos = transform.position;
                if (pos.y > capY)
                {
                    pos.y = capY;
                    transform.position = pos;
                }
            }

            if (anim != null)
            {
                anim.SetClimb(true);
                anim.SetGlide(false);
                var div = Mathf.Max(0.01f, climbSpeedToAnimMultiplierDivisor);
                anim.SetClimbSpeed(wantsMove ? v / div : 0f);
                var drivenAnim = anim.GetEffectiveAnimator();
                if (drivenAnim != null) drivenAnim.speed = baseAnimSpeed;
            }

            if (hud != null)
            {
                var height = transform.position.y - _towerBaseY;
                hud.SetHeight(height);
                if (stamina != null) hud.SetStamina(stamina.Current, stamina.Max);
            }
        }

        /// <summary>
        /// 有限塔高、玩家已到帽高且尚未标记「登顶滑翔已完成」：此时停在塔顶等宝箱/HUD，不应再视为持续攀爬。
        /// </summary>
        bool AtSummitWaitingForChestOrHudJump()
        {
            if (tower == null || tower.MaxHeight <= 0f)
                return false;
            if (tower.SummitGlideCompletedForSession)
                return false;
            var h = transform.position.y - tower.BaseY;
            return h >= tower.MaxHeight - 0.02f;
        }

        /// <summary>
        /// 短落等非爬塔流程触地后，玩家可能仍在爬塔触发器内，<see cref="ClimbTrigger"/> 会再次 <see cref="EnterClimb"/>。
        /// 在此时间（unscaled 秒）内忽略自动进入爬塔。
        /// </summary>
        public void SuppressAutoEnterFromTowerTrigger(float durationSeconds)
        {
            _suppressAutoEnterUntilUnscaledTime = Time.unscaledTime + Mathf.Max(0f, durationSeconds);
        }

        public void EnterClimb()
        {
            if (Time.unscaledTime < _suppressAutoEnterUntilUnscaledTime)
                return;
            if (State != ClimbState.Inactive) return;
            if (tower == null)
                tower = UnityObjectFinder.FindFirst<TowerManager>();
            if (tower != null)
                _towerBaseY = tower.BaseY;

            if (forceWorldYawWhenEnteringClimb)
                transform.rotation = Quaternion.Euler(0f, climbEnterWorldYawDegrees, 0f);

            // Force camera & animations.
            if (cam != null) cam.EnterClimbCamera(snapToClimbOffset: true);
            if (anim != null) anim.SetClimb(true);

            if (input != null) input.SetClimbContextActive(true);

            if (freeMoveMotor != null) freeMoveMotor.enabled = false;
            State = ClimbState.Idle;
            AutoAscendEnabled = true;

            if (hud != null) hud.SetClimbOnlyHudVisible(true);

            Debug.Log($"[Climb] EnterClimb: {name} started climbing.");

            SnapToTowerAttachRadius();
        }

        /// <summary>将玩家 XZ 限制在塔心 <see cref="TowerManager.towerRoot"/> 的 attach 半径内（与 <see cref="AIFakeClimbBehavior"/> 一致）。</summary>
        void SnapToTowerAttachRadius()
        {
            var pos = transform.position;
            var center = tower != null && tower.towerRoot != null
                ? tower.towerRoot.position
                : Vector3.zero;
            var radial = new Vector3(pos.x - center.x, 0f, pos.z - center.z);
            var r = Mathf.Max(0.05f, attachRadius);
            if (radial.sqrMagnitude <= r * r)
                return;

            radial = radial.normalized * r;
            if (_cc != null)
                _cc.enabled = false;
            transform.position = new Vector3(center.x + radial.x, pos.y, center.z + radial.z);
            if (_cc != null)
                _cc.enabled = true;
        }

        /// <param name="enableFreeMoveMotor">
        /// 为 false 时不启用 <see cref="PlayerMotor"/>（接 Animator 空翻/滑翔等：避免离开攀爬与进入空中逻辑之间存在一帧或多帧自由移动输入影响位移与朝向）。
        /// </param>
        public void ExitClimb(bool enableFreeMoveMotor = true)
        {
            if (State == ClimbState.Inactive) return;

            cam?.SetClimbWindShakeActive(false);

            if (input != null) input.SetClimbContextActive(false);

            if (stamina != null) stamina.ResetFull();

            if (cam != null) cam.ExitClimbCamera();
            if (anim != null)
            {
                var exitHeight = Mathf.Max(0f, transform.position.y - _towerBaseY);
                var minGlide = glide != null ? glide.MinGlideHeightMeters : MinGlideHeightFallbackMeters;
                anim.ApplyClimbExitHeightForAnimator(exitHeight, minGlide);
                anim.SetClimbSpeed(0f);
                anim.SetClimb(false);
                var drivenAnim = anim.GetEffectiveAnimator();
                if (drivenAnim != null) drivenAnim.speed = 1f;
            }

            if (enableFreeMoveMotor && freeMoveMotor != null) freeMoveMotor.enabled = true;
            State = ClimbState.Inactive;
            AutoAscendEnabled = false;

            if (hud != null) hud.SetClimbOnlyHudVisible(false);

            Debug.Log($"[Climb] ExitClimb: {name} stopped climbing.");
        }

        private float GetEffectiveClimbSpeed(bool applyEquipmentAndBuffs)
        {
            var bal = ConfigRegistry.Balance;
            var staminaMul = bal != null ? bal.climbSpeedWhenStaminaEmptyMultiplier : 0.2f;
            if (staminaMul <= 0.001f)
                staminaMul = 0.2f;
            staminaMul = Mathf.Clamp(staminaMul, 0.01f, 1f);

            var s = stats;
            var v = s != null ? Mathf.Max(0f, s.Current.climbSpeed) : Mathf.Max(0f, climbSpeed);
            if (!applyEquipmentAndBuffs)
                v *= staminaMul;
            if (RewardBuffService.IsClimbBuffActive())
                v *= 2f;
            return v;
        }

        /// <summary>相对塔基底高度 / min(塔配置最大高度, 1000m) → 风量驱动。</summary>
        private void TickClimbAmbientWindAudio()
        {
            var mgr = AudioManager.Instance;
            if (mgr == null || tower == null)
                return;

            var denom = Mathf.Max(1f, Mathf.Min(WindHeightReferenceCapMeters, tower.MaxHeight > 1f ? tower.MaxHeight : WindHeightReferenceCapMeters));
            var hRel = Mathf.Max(0f, transform.position.y - _towerBaseY);
            var drive = Mathf.Clamp01(hRel / denom);
            mgr.ReportAmbientWindDrive01(drive);
        }
    }
}

using System.Collections;
using UnityEngine;
using Game.Core.Config;
using Game.Player;
using Game.Save;
using Game.Tower;
using Game.Economy;
using Game.Core;
using Game.UI.Framework;
using Game.UI.ViewModels;
using Game.Collectibles.Stats;
using Game.VFX;
using Game.Audio;
using Game.Maps;

namespace Game.Glide
{
    /// <summary>
    /// 爬塔结束后的滑翔流程：高度分支、攀爬起跳弧、坠落/滑翔两段运动、结算。
    /// 空翻由 Animator 触发：短时抛物线 YZ 位移后接入坠落段。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(10)]
    public sealed class GlideController : MonoBehaviour
    {
        private const float GroundHeightEndEpsilon = 0.15f;
        private const float GroundProbeTopOffset = 2f;
        private const float GroundProbeMaxDistance = 300f;
        private const string LogPrefix = "[Glide] ";

        private enum TowerExitLifecycle
        {
            PreGlideJump,
            Fall,
            Glide
        }

        private enum GlideMotionPhase
        {
            Fall,
            Glide
        }

        private enum GlideEndReason
        {
            GroundHeight,
            Failsafe
        }

        [Header("References")]
        [SerializeField] private ClimbController climb;
        [SerializeField] private Stamina stamina;
        [SerializeField] private TowerManager tower;
        [SerializeField] private RunwayManager runway;
        [SerializeField] private ThirdPersonCamera cam;
        [SerializeField] private PlayerAnimatorDriver anim;
        [SerializeField] private Hud.HudController hud;
        [SerializeField] private LegacyPlayerInput input;
        [SerializeField] private StatsAggregator stats;

        [Header("滑翔会话 — 门控碰撞体")]
        [Tooltip(
            "进入完整滑翔会话（攀爬起跳弧 / 空翻接滑翔）时将该碰撞体 isTrigger 设为 true；滑翔结束（触地或其它终止）时恢复原值。短落（低于最低滑翔高度）不会触发。")]
        [SerializeField] private BoxCollider glideSessionGateCollider;

        [Header("Runtime")]
        public bool isGliding;

        /// <summary>
        /// 本帧滑翔/起跳轨迹的瞬时速度矢量（不含短落空翻）。
        /// 模长可为 UI 气流、粒子等与 <see cref="GameplayVfxManager"/> 借阅气流共用。
        /// </summary>
        public Vector3 LastTrajectoryVelocity => _lastTrajectoryVelocity;

        /// <summary>瞬时合速度（m/s）。</summary>
        public float TrajectorySpeedMetersPerSecond => _lastTrajectoryVelocity.magnitude;

        /// <summary>攀爬起跳竖直弧阶段（尚未进入坠落/滑翔粒子段）。</summary>
        public bool IsPreGlideJumpAscent =>
            isGliding && _lifecycle == TowerExitLifecycle.PreGlideJump;

        /// <summary>将当前合速度映射到 0~1，默认参考与音效坠落风速 cap 对齐。</summary>
        public float GetTrajectorySpeed01(float referenceSpeedMps = 200f)
        {
            return Mathf.Clamp01(_lastTrajectoryVelocity.magnitude / Mathf.Max(1e-3f, referenceSpeedMps));
        }

        [Header("爬塔高度分支")]
        [Tooltip("爬塔高度（相对塔基底）低于此值时：仅下落，无滑翔流程、无结算与金币。")]
        [SerializeField] private float minGlideHeightMeters = 25f;

        /// <summary>与 <see cref="minGlideHeightMeters"/> 相同；攀爬结束时空翻/Jump 的 Animator 阈值应与此一致。</summary>
        public float MinGlideHeightMeters => minGlideHeightMeters;

        /// <summary>当前相对 <see cref="TowerManager.towerRoot"/> 的水平滑翔距离（米），与结算一致。</summary>
        public float CurrentGlideDistanceFromTowerMeters =>
            DistanceManager.HorizontalGlideMetersFromTowerRoot(transform.position, tower);

        /// <summary>进入滑翔会话时的相对爬塔高度（米），与 <see cref="RewardCalculator.CalculateGlideSettlement"/> 中塔高参数一致。</summary>
        public float GlideSessionTowerHeightMeters =>
            tower != null ? Mathf.Max(0f, _startPos.y - tower.BaseY) : Mathf.Max(0f, _startPos.y);

        [Tooltip("≥此相对塔高（米）时：空格/到顶只会 ExitClimb，让 Animator 走「Climb→空翻」，禁止抢先 StartGlide（否则会 isGliding=true，BeginBackflipPhase 被跳过）。须与 Animator 里 Height 条件一致（例如 39）。")]
        [SerializeField] private float minHeightForAnimatorBackflipPathMeters = 39f;
        [Tooltip("后备：若状态机上未挂 SMB，可用 Layer0 当前状态名匹配后调用 BeginBackflipPhase（默认「空翻」）。")]
        [SerializeField] private string backflipAnimatorStateName = "空翻";

        [Header("角色朝向（滑翔）")]
        [Tooltip("若为真，坠落段俯仰为 Fall Body Euler，滑翔段为 Glide Body Euler，二者在坠落→滑翔混合区 Slerp；关闭则脚本不改 Transform 旋转。")]
        [SerializeField] private bool applyGlideFallBodyPitch;
        [Tooltip("相对「进入滑翔瞬间」的世界 Yaw 只做俯仰/横滚：仅 X、Z 生效，Y 被忽略（保留攀爬朝向）。")]
        [SerializeField] private Vector3 fallBodyEulerDegrees = new Vector3(45f, 0f, 0f);
        [Tooltip("同上，仅 X、Z；Y 忽略。")]
        [SerializeField] private Vector3 glideBodyEulerDegrees = new Vector3(0f, 0f, 0f);
        [Tooltip("后空翻结束接坠落段时，从当前旋转过渡到「坠落俯仰」的时长（秒），减轻瞬切感。")]
        [SerializeField] private float backflipToFallRotationBlendSeconds = 0.28f;
        [Tooltip("后空翻接坠落时：俯仰 Slerp 结束后再从 FreeMove 切到滑翔强制跟随机位，减轻机位瞬切。开启「滑翔自由轨道」时始终用 FreeMove，此项无效果。")]
        [SerializeField] private bool deferGlideCameraUntilBackflipRotationBlend = true;
        [Tooltip("后空翻抛物线在终点处的切向速度映射到坠落段初始下落速度，避免从 0 突然加速。")]
        [SerializeField] private bool warmStartFallSpeedFromBackflipTangent = true;

        [Header("坠落 → 滑翔 轨迹")]
        [Tooltip("坠落段：前向(+Z)速度 = 垂直下落速度 × 此系数。增大则同等下落时飞得更远、轨迹更平、更不易贴地擦跑道。")]
        [SerializeField] private float fallHorizontalPerVerticalSpeed = 0.34f;
        [Tooltip("滑翔段：垂直下沉速度 = 前向速度 × 此系数。减小则滑翔更「平」、离地间隙更大。")]
        [SerializeField] private float glideVerticalSpeedPerHorizontal = 0.013f;
        [Tooltip("从起跳顶点起算，累计下落达到「总可下落高度×此比例」后切入滑翔段。减小会更早进入滑翔（陡坠变短，弧线更高）。")]
        [SerializeField] private float fallToGlideVerticalDescentFraction = 0.58f;
        [Tooltip("在「达到进入滑翔的累计下落预算」之前的最后一段下落中，按 SmoothStep 把速度从坠落矢量过渡到滑翔矢量；0=与旧版一样接近瞬时切换，0.2~0.35 较平滑。")]
        [SerializeField] private float fallToGlideBlendFraction = 0.28f;
        [Tooltip("沿本帧位移方向射线检测跑道时，与命中面的预留间隙（米）；过小易与 CharacterController 反复挤压导致弹起。")]
        [SerializeField] private float runwayApproachSkinMeters = 0.12f;

        private enum BackflipHorizontalReference
        {
            WorldPositiveZ = 0,
            LocalBackOnGroundPlane = 1,
        }

        [Header("空翻 · 抛物线（挂 BackflipEnterStateMachineBehaviour）")]
        [Tooltip("空翻时长（秒），轨迹在此时间内从起点插值到终点并叠加拱形。")]
        [SerializeField] private float backflipDurationSeconds = 0.8f;
        [Tooltip("沿水平参考方向的位移长度（米）。WorldPositiveZ=世界 +Z；LocalBack=地面投影 -forward。")]
        [SerializeField] private float backflipHorizontalDistanceMeters = 8f;
        [Tooltip("抛物线拱高（米）：在轨迹中点相对两端连线的额外抬高，0 则仅为 SmoothStep 直线插值。")]
        [SerializeField] private float backflipArcPeakMeters = 2.5f;
        [Tooltip("结束时相对起点的净抬高（米），可与拱高组合形成先后仰再落回的 YZ 弧。")]
        [SerializeField] private float backflipEndVerticalOffsetMeters;
        [SerializeField] private BackflipHorizontalReference backflipHorizontalReference = BackflipHorizontalReference.WorldPositiveZ;

        [Header("攀爬中启动滑翔 · 起跳弧")]
        [Tooltip("起跳方向在世界坐标下的比例（通常 Y>0 抬高，Z<0 略向后）；沿该方向以恒定速度移动直到达到目标高度。")]
        [SerializeField] private Vector3 preGlideJumpBias = new Vector3(0f, 6.25f, -4f);
        [SerializeField] private float preGlideJumpSpeedMps = 10f;

        [Header("短落：重力下落")]
        [Tooltip("短落阶段垂直加速度（m/s²），通常用负值表示向下重力。")]
        [SerializeField] private float shortDropGravity = -35f;

        [Header("滑行结算呈现")]
        [Tooltip("落地后延迟多久再发奖并弹出结算面板（秒），便于播放落地/Victory 动画。")]
        [SerializeField] private float settlementPresentationDelaySeconds = 2f;

        [Header("落地 · 流星砸击过渡")]
        [Tooltip("滑翔结束双脚着地后的直立朝向：在「进入滑翔时锁定」的 Yaw（见 _uprightRotation）基础上再绕世界 Y 轴旋转该度数（常用 180 转身朝向跑道另一侧）。短落 BeginShortDrop 不使用此项。")]
        [SerializeField] private float glideLandingYawOffsetDegrees = 180f;
        [Tooltip("着地进入结算镜头后，摄像机相对 pivot 的偏移绕世界竖轴旋转（典型 180° 绕到角色正面看着角色）；时长或角度 ≤0 则关闭。")]
        [SerializeField] private float glideLandingCameraYawOrbitDegrees = 180f;
        [SerializeField] private float glideLandingCameraOrbitDurationSeconds = 1.1f;
        [Tooltip("进入 Settlement 后延迟多久开始绕轨（秒），可先播转身/砸镜再绕镜头。")]
        [SerializeField] private float glideLandingCameraOrbitDelaySeconds;
        [Tooltip("关闭（默认）：第一次触地立刻砸击表现 + 结算，角色不再沿跑道位移，残留物与结算位置一致。\n开启：触地后多帧站直 blend，并可配置极小前向滑行（易产生残留物与角色最终位置偏差）。")]
        [SerializeField] private bool enableLandingSkidOutro;
        [SerializeField] private float landingSkidMaxDurationSeconds = 0.45f;
        [SerializeField] private float landingSkidMinDurationSeconds = 0.12f;
        [Tooltip("触地后可选的沿 +Z 额外滑行（米/秒初值比例）；默认 0 = 撞击即停。")]
        [SerializeField] private float landingSkidSpeedRetention;
        [Tooltip("触地后不低于该前向速度再衰减（0=完全不在跑道上滑行）。")]
        [SerializeField] private float landingSkidMinInitialSpeedMps;
        [SerializeField] private float landingSkidDecelerationMps2 = 22f;
        [Tooltip("站直旋转 Slerp 时长（秒）。")]
        [SerializeField] private float landingUprightBlendSeconds = 0.38f;
        [SerializeField] private float landingGroundStickDownMps = 1.8f;
        [Header("落地撞击残留")]
        [Tooltip("可选：在跑道撞击点生成并挂到 runwayRoot，超时销毁。")]
        [SerializeField] private GameObject landingImpactRemnantPrefab;
        [SerializeField] private float landingImpactRemnantLifetimeSeconds = 15f;
        [SerializeField] private float landingImpactRemnantSurfaceOffset = 0.02f;
        [Header("落地镜头冲击")]
        [SerializeField] private float landingCamShakePosPeakMeters = 0.32f;
        [SerializeField] private float landingCamShakeEulerPeakDegrees = 5.5f;
        [SerializeField] private float landingCamShakeDurationSeconds = 0.34f;
        [Tooltip("速度越快，镜头冲击略强（在以上峰值为基准上乘以该系数）。")]
        [SerializeField] private float landingCamShakeSpeedScale = 0.018f;

        [Header("VFX pooled（GameplayVfxManager）")]
        [SerializeField] private Transform glideAerialWindAnchor;

        [SerializeField] private Vector3 glideAerialWindLocalOffset;

        [Tooltip("脚部/履带附近挂气流；默认为本物体。")]
        [SerializeField] private Transform settlementFxAnchor;

        [SerializeField] private Vector3 settlementFxWorldOffset;

        [SerializeField] private float landingDustStrongMinSpeed = 17f;

        [Header("气流粒子 · SimulationSpeed")]
        [Tooltip("坠落段风速标定：达到该合速度左右时 SimulationSpeed 趋近上限（与音效坠落 cap 可同量级）。")]
        [SerializeField] private float aerialWindFallSpeedReference = 200f;

        [SerializeField] private float aerialWindFallSimulationMin = 0.85f;
        [SerializeField] private float aerialWindFallSimulationMax = 3.2f;
        [Tooltip("<1 在低速时更早拉高仿真倍率。")]
        [SerializeField] private float aerialWindFallSimulationExponent = 0.9f;

        [Tooltip("滑翔段水平+下沉合速度参考（m/s），通常小于坠落 cap；可调以适配你们加速倍率。")]
        [SerializeField] private float aerialWindGlideSpeedReference = 48f;

        [SerializeField] private float aerialWindGlideSimulationMin = 0.65f;
        [SerializeField] private float aerialWindGlideSimulationMax = 2.35f;
        [SerializeField] private float aerialWindGlideSimulationExponent = 1f;

        [Tooltip("回收到池中时恢复为 1（或Prefab默认），避免影响下一位借阅者初帧。")]
        [SerializeField] private float aerialWindReleaseSimulationSpeedReset = 1f;

        [Header("Debug")]
        [SerializeField] private bool glideDebugLogging;
        [SerializeField] private float glideDebugLogInterval = 0.5f;
        [Tooltip("控制台输出空翻：起点、每帧 offset/位置/Z 增量、结束快照；用于排查位移被覆盖或参数为 0。")]
        [SerializeField] private bool backflipDebugLogging;

        private CharacterController _cc;
        private Vector3 _startPos;
        private float _glideFailsafeDeadline = -1f;
        private float _planClimbHeight;
        private float _fallDownSpeed;
        private float _glideForwardSpeed;
        private float _glideDownSpeed;
        private float _cachedFallPhaseAccel;
        private float _cachedFallPhaseMaxSpeed;
        private float _cachedGlidePhaseAccel;
        private float _cachedGlidePhaseMaxSpeed;
        private bool _reachedTopOnStart;
        private float _lockedRunwayX;
        private bool _useLandingToEnd;
        private Quaternion _uprightRotation;
        private bool _poseApplied;

        private bool _shortDropActive;
        private float _shortDropVerticalVelocity;

        private bool _gateColliderStored;
        private bool _gateColliderPreviousIsTrigger;

        private TowerExitLifecycle _lifecycle = TowerExitLifecycle.PreGlideJump;
        private float _preJumpStartWorldY;
        private GlideMotionPhase _motionPhase;
        private float _startFeetY;
        private float _groundReferenceY;
        private float _verticalDropTotal;
        private float _cumDescentDuringFall;
        private float _glideStartUnscaledTime;
        private float _nextGlideDebugLogUnscaledTime;

        private bool _backflipActive;
        private float _backflipElapsed;
        private bool _backflipReachedTop;
        private Vector3 _backflipStartWorldPos;
        private bool _backflipCcSuppressed;
        private bool _backflipCcWasEnabled;
        private bool _backflipRootMotionWasEnabled;
        private bool _backflipRootMotionRestorePending;

        private bool _pendingGlideCameraAfterBackflipBlend;

        /// <summary>
        /// 滑翔、短落、后空翻等爬塔结束后的空中流程任一进行中（与 <see cref="isGliding"/> 相比包含更多过渡态）。
        /// </summary>
        public bool IsAerialSequenceActive => isGliding || _shortDropActive || _backflipActive;

        /// <summary>
        /// 自 <see cref="FinalizeGlideSession"/> 起直至 <see cref="ResetRuntimeStateForRespawn"/>：禁止打开商店/地图等（结算结果界面仍可打开）。
        /// </summary>
        public bool BlockShopNavigationUntilRespawn { get; private set; }

        private bool _glideFallPetYawFixActive;

        private bool _landingSkidOutroActive;
        private float _landingSkidOutroElapsed;
        private float _landingSkidSpeedZ;
        private Quaternion _landingStartRotation;
        private GlideEndReason _landingPendingEndReason;
        private bool _impactLandNeedsAnimatorRootRestore;
        private GameObject _borrowedAerialWindFx;
        private GameObject _windParticleCacheOwner;
        private ParticleSystem[] _borrowedWindParticleSystems = System.Array.Empty<ParticleSystem>();
        private Vector3 _lastTrajectoryVelocity;
        private float _shortDropVerticalImpactSpeed;
        private bool _impactLandRootMotionPrev;
        private Coroutine _impactLandRootMotionRestoreRoutine;

        private float _fallEntryRotationBlendSecondsLeft;
        private float _fallEntryRotationBlendDuration;
        private Quaternion _fallEntryRotationFrom;

        private Coroutine _presentSettlementRoutine;

        private bool ShouldLogBackflip() => backflipDebugLogging || glideDebugLogging;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (climb == null) climb = GetComponent<ClimbController>();
            if (stamina == null) stamina = GetComponent<Stamina>();
            if (tower == null) tower = UnityObjectFinder.FindFirst<TowerManager>();
            if (runway == null) runway = UnityObjectFinder.FindFirst<RunwayManager>();
            if (cam == null && Camera.main != null) cam = Camera.main.GetComponent<ThirdPersonCamera>();
            if (anim == null) anim = GetComponentInChildren<PlayerAnimatorDriver>();
            if (hud == null) hud = UiCoordinator.Hud;
            if (hud == null) hud = UnityObjectFinder.FindFirst<Hud.HudController>();
            if (input == null) input = GetComponent<LegacyPlayerInput>();
            if (stats == null) stats = UnityObjectFinder.FindFirst<StatsAggregator>();
        }

        /// <summary>替换攀爬角色模型后由 <see cref="PlayerGameplayAnimatorBinder"/> 刷新。</summary>
        public void BindAnimatorDriver(PlayerAnimatorDriver driver) => anim = driver;

        /// <summary>滑翔/空翻/短落期间锁定移动；若开启滑翔轨道自由视角，仍读取鼠标/虚拟视角轴。</summary>
        private void LockInputForAerialSequence()
        {
            if (input == null) return;
            var allowLook = cam != null && cam.UsesGlideOrbitalFreeLook;
            input.SetLocked(true, allowLook);
        }

        void ClearGlideFallPetYawFixIfActive()
        {
            if (!_glideFallPetYawFixActive) return;
            UiCoordinator.Equipment?.EndPetGlideFallBackflipYawFix();
            _glideFallPetYawFixActive = false;
        }

        /// <summary>进入坠落/滑翔镜头：轨道自由滑翔时保持 FreeMove 与现有机位参数，仅叠加抖动；否则切强制跟随滑翔机位。</summary>
        private void ConfigureGlideCameraForFallEntry()
        {
            if (cam == null) return;
            var useFallShake = _motionPhase == GlideMotionPhase.Fall;
            if (cam.UsesGlideOrbitalFreeLook)
            {
                cam.SetMode(PlayerMode.FreeMove);
                cam.SetGlideShakeOverlayActive(true);
                cam.SetGlideCameraFallShake(useFallShake);
            }
            else
            {
                cam.SetGlideShakeOverlayActive(false);
                cam.SetMode(PlayerMode.Glide);
                cam.SetGlideCameraFallShake(useFallShake);
            }
        }

        private void Update()
        {
            // 空翻位移在 LateUpdate：Animator（含 StateMachineBehaviour.OnStateEnter）默认在 Update 之后执行，
            // BeginBackflipPhase 若放在 Update 里驱动会晚一帧甚至整段 Z 不刷新。
            if (_backflipActive)
                return;

            if (_shortDropActive)
            {
                TickShortDrop();
                return;
            }

            if (_landingSkidOutroActive)
            {
                TickLandingSkidOutro();
                return;
            }

            if (!isGliding)
            {
                TryAutoStartGlide();
                return;
            }

            TickGlide();
        }

        private void LateUpdate()
        {
            if (_backflipActive)
                TickBackflip();
            else
                TryBeginBackflipPhaseAnimatorFallback();
        }

        /// <summary>
        /// 攀爬中空格/到顶：低于「空翻高度阈值」仍走旧逻辑 StartGlide（短落或 PreGlideJump）；
        /// 不低于阈值时<strong>只 ExitClimb</strong>，交给 Animator「Climb→空翻」与 SMB/<see cref="TryBeginBackflipPhaseAnimatorFallback"/> 启动抛物线，避免抢先 FullGlide。
        /// </summary>
        private void TryAutoStartGlide()
        {
            if (isGliding || _shortDropActive || _backflipActive) return;
            if (climb == null || tower == null) return;
            if (climb.State == ClimbController.ClimbState.Inactive) return;

            var y = transform.position.y;
            var reachedTop = tower.MaxHeight > 0f && (y - tower.BaseY) >= tower.MaxHeight - 0.01f;

            if (reachedTop && !tower.SummitGlideCompletedForSession)
                return;

            var requested = input != null && input.GlidePressed;

            if (!requested && !reachedTop)
                return;

            var climbH = Mathf.Max(0f, y - tower.BaseY);
            if (climbH >= minHeightForAnimatorBackflipPathMeters)
            {
                climb.ExitClimb(enableFreeMoveMotor: false);
                LockInputForAerialSequence();
                if (glideDebugLogging)
                    Debug.Log(
                        $"{LogPrefix}[路径] 高度 {climbH:F2}m ≥ {minHeightForAnimatorBackflipPathMeters} → 仅 ExitClimb，等待 Animator「空翻」与 BeginBackflipPhase（不调用 StartGlide）。",
                        this);
                return;
            }

            StartGlide(reachedTop);
        }

        /// <summary>Animator 未挂 <see cref="BackflipEnterStateMachineBehaviour"/> 时，进入「空翻」状态仍能通过状态名触发抛物线。</summary>
        private void TryBeginBackflipPhaseAnimatorFallback()
        {
            if (isGliding || _shortDropActive || _backflipActive) return;
            if (string.IsNullOrEmpty(backflipAnimatorStateName)) return;

            var a = anim != null ? anim.GetEffectiveAnimator() : GetComponentInChildren<Animator>();
            if (a == null) return;

            var info = a.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(backflipAnimatorStateName))
                return;

            BeginBackflipPhase();
        }

        /// <summary>由 Animator「空翻」状态上的 <see cref="BackflipEnterStateMachineBehaviour"/> 调用：抛物线 YZ 位移，结束后进入滑翔坠落段。</summary>
        public void BeginBackflipPhase()
        {
            if (_backflipActive || isGliding || _shortDropActive)
            {
                if (backflipDebugLogging)
                    Debug.LogWarning(
                        $"{LogPrefix}[Backflip] BeginBackflipPhase SKIPPED — alreadyActive={_backflipActive} isGliding={isGliding} shortDrop={_shortDropActive}",
                        this);
                return;
            }

            if (climb != null && climb.State != ClimbController.ClimbState.Inactive)
                climb.ExitClimb(enableFreeMoveMotor: false);

            LockInputForAerialSequence();
            var motor = GetComponent<PlayerMotor>();
            if (motor != null) motor.enabled = false;

            if (cam != null) cam.SetMode(PlayerMode.FreeMove);
            var d = BackflipHorizontalWorldDir();
            var yawEnd = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            cam?.BeginBackflipCameraBehindPlayer(backflipDurationSeconds, yawEnd);

            _lockedRunwayX = runway != null ? runway.CenterXWorld : transform.position.x;
            var p = transform.position;
            p.x = _lockedRunwayX;
            transform.position = p;

            _backflipStartWorldPos = transform.position;
            _backflipReachedTop = ComputeReachedTopOfTower();
            _backflipElapsed = 0f;
            _backflipActive = true;

            if (_cc != null)
            {
                _backflipCcWasEnabled = _cc.enabled;
                _cc.enabled = false;
                _backflipCcSuppressed = true;
            }

            var animatorRef = anim != null ? anim.GetEffectiveAnimator() : GetComponentInChildren<Animator>();
            if (animatorRef != null)
            {
                _backflipRootMotionRestorePending = true;
                _backflipRootMotionWasEnabled = animatorRef.applyRootMotion;
                animatorRef.applyRootMotion = false;
            }
            else
                _backflipRootMotionRestorePending = false;

            if (ShouldLogBackflip())
            {
                var dir0 = BackflipHorizontalWorldDir();
                Debug.Log(
                    $"{LogPrefix}[Backflip] BEGIN\n" +
                    $"  startWorld={_backflipStartWorldPos} lockX={_lockedRunwayX:F3}\n" +
                    $"  dur={backflipDurationSeconds}s horizDist={backflipHorizontalDistanceMeters} arcPeak={backflipArcPeakMeters} endYOff={backflipEndVerticalOffsetMeters}\n" +
                    $"  horizRef={backflipHorizontalReference} horizDir={dir0}\n" +
                    $"  ccEnabledBefore={_backflipCcWasEnabled} ccSuppressedNow={_backflipCcSuppressed} animator={(animatorRef != null ? animatorRef.name : "null")} applyRootMotion→false (restore on landing)\n" +
                    $"  eulerY={transform.eulerAngles.y:F2} reachedTop={_backflipReachedTop}",
                    this);
            }
        }

        /// <summary>仅恢复 CharacterController。applyRootMotion 在空翻期间保持关闭，避免接 Glide 状态的一帧内 Animator 与脚本争抢根位移/旋转。</summary>
        private void RestoreBackflipPhysicsOverrides()
        {
            if (_backflipCcSuppressed && _cc != null)
            {
                _cc.enabled = _backflipCcWasEnabled;
                _backflipCcSuppressed = false;
            }
        }

        private void RestoreAnimatorRootMotionAfterAerial()
        {
            if (!_backflipRootMotionRestorePending) return;
            _backflipRootMotionRestorePending = false;
            var a = anim != null ? anim.GetEffectiveAnimator() : GetComponentInChildren<Animator>();
            if (a != null)
                a.applyRootMotion = _backflipRootMotionWasEnabled;
        }

        private Vector3 BackflipHorizontalWorldDir()
        {
            if (backflipHorizontalReference == BackflipHorizontalReference.WorldPositiveZ)
                return new Vector3(0f, 0f, 1f);

            var f = transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-8f)
                return new Vector3(0f, 0f, 1f);
            f.Normalize();
            return -f;
        }

        private bool ComputeReachedTopOfTower()
        {
            if (tower == null || tower.MaxHeight <= 0f) return false;
            return transform.position.y - tower.BaseY >= tower.MaxHeight - 0.01f;
        }

        /// <summary>抛物线位移对时间的导数近似（世界 m/s），用于接入坠落段时预热速度。</summary>
        private Vector3 BackflipParabolaWorldVelocityAtEnd()
        {
            var dur = Mathf.Max(0.001f, backflipDurationSeconds);
            const float deltaU = 0.06f;
            var u0 = Mathf.Max(0f, 1f - deltaU);
            return (EvaluateBackflipOffset01(1f) - EvaluateBackflipOffset01(u0)) / (deltaU * dur);
        }

        private void ApplyWarmStartFallSpeedFromBackflipParabolaTangent()
        {
            var v = BackflipParabolaWorldVelocityAtEnd();
            var horizScale = Mathf.Max(0.01f, fallHorizontalPerVerticalSpeed);
            var fromDown = Mathf.Max(0f, -v.y);
            var fromZ = v.z > 0f ? v.z / horizScale : 0f;
            var warm = Mathf.Max(fromDown, fromZ, 1.25f);
            _fallDownSpeed = Mathf.Min(warm, _cachedFallPhaseMaxSpeed);
        }

        /// <param name="u">0…1 归一化时间</param>
        private Vector3 EvaluateBackflipOffset01(float u)
        {
            u = Mathf.Clamp01(u);
            var dirH = BackflipHorizontalWorldDir();
            var endChord = dirH * Mathf.Max(0f, backflipHorizontalDistanceMeters) +
                           Vector3.up * backflipEndVerticalOffsetMeters;
            var baseline = Vector3.Lerp(Vector3.zero, endChord, SmoothStep01(u));
            var peak = Mathf.Max(0f, backflipArcPeakMeters);
            var arc = Vector3.up * (4f * peak * u * (1f - u));
            return baseline + arc;
        }

        private void TickBackflip()
        {
            var dur = Mathf.Max(0.001f, backflipDurationSeconds);
            var posBefore = transform.position;
            _backflipElapsed += Time.deltaTime;
            var u = Mathf.Clamp01(_backflipElapsed / dur);
            var offset = EvaluateBackflipOffset01(u);
            var pos = _backflipStartWorldPos + offset;
            pos.x = _lockedRunwayX;
            transform.position = pos;

            if (backflipDebugLogging)
            {
                var dzFrame = transform.position.z - posBefore.z;
                var dzFromStart = transform.position.z - _backflipStartWorldPos.z;
                Debug.Log(
                    $"{LogPrefix}[Backflip] LateUpdate t={_backflipElapsed:F4}/{dur:F3}s u={u:F4} dt={Time.deltaTime:F5}\n" +
                    $"  offset(localCurve)={offset}  posAfter={transform.position}\n" +
                    $"  dZ_thisFrame={dzFrame:F5}  dZ_sinceBackflipStart={dzFromStart:F3}  (startZ={_backflipStartWorldPos.z:F3})\n" +
                    $"  cc.enabled={(_cc != null && _cc.enabled)}",
                    this);
            }

            if (_backflipElapsed >= dur)
                FinishBackflipAndStartGlideFall();
        }

        private void FinishBackflipAndStartGlideFall()
        {
            var posEnd = transform.position;
            var rotEnd = transform.rotation;

            if (ShouldLogBackflip())
                Debug.Log(
                    $"{LogPrefix}[Backflip] END snap — worldPos={posEnd} totalΔZ={(posEnd.z - _backflipStartWorldPos.z):F3} totalΔY={(posEnd.y - _backflipStartWorldPos.y):F3} → EnterGlideFall reachedTop={_backflipReachedTop}",
                    this);

            _backflipActive = false;
            RestoreBackflipPhysicsOverrides();
            if (_cc != null && _cc.enabled)
            {
                var afterCc = transform.position;
                transform.SetPositionAndRotation(posEnd, rotEnd);
                if (ShouldLogBackflip() && (afterCc - posEnd).sqrMagnitude > 1e-8f)
                    Debug.LogWarning(
                        $"{LogPrefix}[Backflip] CC re-enable nudged pos by {(afterCc - posEnd)}; re-applied scripted end pose.",
                        this);
            }

            if (glideDebugLogging && !backflipDebugLogging)
                Debug.Log($"{LogPrefix}Backflip end → glide fall reachedTop={_backflipReachedTop}", this);
            cam?.EndBackflipCameraBehindPlayerSnap();
            StartGlideFallPhaseAfterBackflip(_backflipReachedTop);
        }

        private void StartGlideFallPhaseAfterBackflip(bool reachedTop)
        {
            if (glideDebugLogging)
                Debug.Log(
                    $"{LogPrefix}[路径] Animator「空翻」状态结束（抛物线位移走完）→ BeginGlideSessionCore → Apex→Fall。**此处才有后空翻脚本位移。**",
                    this);

            BeginGlideSessionCore(reachedTop, afterBackflipEnd: true);
            EnterGlideFallPhaseFromApex(smoothEntryFromBackflip: true);
            if (input != null)
                input.SetLocked(true, true);
        }

        public void StartGlide(bool reachedTop = false)
        {
            if (isGliding || _shortDropActive || _backflipActive) return;

            var height = tower != null ? Mathf.Max(0f, transform.position.y - tower.BaseY) : transform.position.y;
            if (height < minGlideHeightMeters)
            {
                BeginShortDropSequence(reachedTop);
                return;
            }

            StartFullGlideSequence(reachedTop);
        }

        /// <summary>
        /// 塔顶 HUD「跳跃」：与「攀爬相对高度 ≥ 空翻阈值」时空格/自动起跳一致，直接 <see cref="BeginBackflipPhase"/> → 抛物线 → 坠落 → 滑翔；矮塔回退 <see cref="StartGlide"/>。
        /// </summary>
        public void StartGlideFromSummitHudUsingBackflipPath()
        {
            if (isGliding || _shortDropActive || _backflipActive)
                return;

            var climbH = tower != null ? Mathf.Max(0f, transform.position.y - tower.BaseY) : 0f;
            var reachedTop = tower != null && tower.MaxHeight > 0f && climbH >= tower.MaxHeight - 0.02f;

            if (climbH >= minHeightForAnimatorBackflipPathMeters)
            {
                BeginBackflipPhase();
                return;
            }

            StartGlide(reachedTop);
        }

        private void BeginShortDropSequence(bool reachedTop)
        {
            ReleaseBorrowedAerialWindFx();
            _shortDropActive = true;
            _reachedTopOnStart = reachedTop;
            _shortDropVerticalVelocity = 0f;

            if (climb != null) climb.ExitClimb(enableFreeMoveMotor: false);

            LockInputForAerialSequence();
            var motor = GetComponent<PlayerMotor>();
            if (motor != null) motor.enabled = false;

            if (cam != null) cam.SetMode(PlayerMode.FreeMove);

            if (anim != null)
            {
                anim.SetClimb(false);
                anim.SetGlide(false);
                anim.SetVictory(false);
            }

            _lockedRunwayX = runway != null ? runway.CenterXWorld : transform.position.x;
            var p = transform.position;
            p.x = _lockedRunwayX;
            transform.position = p;

            var shortH = tower != null ? Mathf.Max(0f, transform.position.y - tower.BaseY) : transform.position.y;
            if (TryGetGroundSurfaceUnderFeet(out var sg))
                _groundReferenceY = sg;
            else
                _groundReferenceY = tower != null ? tower.BaseY : FeetWorldY() - 50f;

            if (glideDebugLogging)
                Debug.Log($"{LogPrefix}ShortDrop start climbH={shortH:F2} < min={minGlideHeightMeters} (no reward / no settlement)", this);
        }

        private void TickShortDrop()
        {
            var dt = Time.deltaTime;
            _shortDropVerticalVelocity += shortDropGravity * dt;
            var move = new Vector3(0f, _shortDropVerticalVelocity * dt, 0f);
            _cc.Move(move);
            var pos = transform.position;
            pos.x = _lockedRunwayX;
            transform.position = pos;

            if (FeetReachedGroundHeight())
            {
                _shortDropVerticalImpactSpeed = Mathf.Abs(_shortDropVerticalVelocity);
                EndShortDropNoReward();
                return;
            }

            if (hud != null && tower != null)
            {
                hud.SetHeight(transform.position.y - tower.BaseY);
                hud.SetStamina(0f, stamina != null ? stamina.Max : 0f);
            }
        }

        private void EndShortDropNoReward()
        {
            if (glideDebugLogging)
                Debug.Log($"{LogPrefix}ShortDrop end feetY={FeetWorldY():F2}", this);

            if (TryGetGroundSurfaceHitUnderFeet(out var groundHitShort))
            {
                var remnantPos = new Vector3(_lockedRunwayX, groundHitShort.point.y, transform.position.z);
                SpawnPooledLandingDust(remnantPos, groundHitShort.normal, _shortDropVerticalImpactSpeed);
            }
            else if (TryGetGroundSurfaceUnderFeet(out var gy))
            {
                var remnantPos = new Vector3(_lockedRunwayX, gy, transform.position.z);
                SpawnPooledLandingDust(remnantPos, Vector3.up, _shortDropVerticalImpactSpeed);
            }

            AudioManager.Instance?.PlaySfx(SfxId.GlideLand, 0.9f);

            _shortDropActive = false;
            RestoreUprightPose();

            if (climb != null)
            {
                climb.ExitClimb();
                climb.SuppressAutoEnterFromTowerTrigger(2.5f);
            }

            if (input != null) input.SetLocked(false);
            var motor = GetComponent<PlayerMotor>();
            if (motor != null) motor.enabled = true;

            if (cam != null) cam.SetMode(PlayerMode.FreeMove);

            RestoreAnimatorRootMotionAfterAerial();

            if (anim != null)
            {
                anim.SetClimb(false);
                anim.SetGlide(false);
                anim.SetVictory(false);
            }
        }

        /// <param name="afterBackflipEnd">为 true 时不把 X 重拉到跑道中心，避免空翻落点相对跑道中心产生水平瞬移。</param>
        private void BeginGlideSessionCore(bool reachedTop, bool afterBackflipEnd = false)
        {
            ClearGlideFallPetYawFixIfActive();
            _pendingGlideCameraAfterBackflipBlend = false;
            ReleaseBorrowedAerialWindFx();
            isGliding = true;
            PushGlideGateTriggerOn();
            _reachedTopOnStart = reachedTop;
            _glideStartUnscaledTime = Time.unscaledTime;
            _nextGlideDebugLogUnscaledTime = Time.unscaledTime;

            if (climb != null) climb.ExitClimb(enableFreeMoveMotor: false);

            LockInputForAerialSequence();
            var motor = GetComponent<PlayerMotor>();
            if (motor != null) motor.enabled = false;

            if (cam != null) cam.SetMode(PlayerMode.FreeMove);

            if (anim != null)
            {
                anim.SetClimb(false);
                anim.SetGlide(false);
                var drivenAnim = anim.GetEffectiveAnimator();
                if (drivenAnim != null) drivenAnim.speed = 1f;
            }

            _startPos = transform.position;

            if (afterBackflipEnd)
                _lockedRunwayX = transform.position.x;
            else
            {
                _lockedRunwayX = runway != null ? runway.CenterXWorld : transform.position.x;
                var p = transform.position;
                p.x = _lockedRunwayX;
                transform.position = p;
            }

            // 保留进入滑翔时角色的世界 Yaw（攀爬朝向），不再强制面向 +Z。
            _uprightRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            _poseApplied = false;

            var height = tower != null ? Mathf.Max(0f, transform.position.y - tower.BaseY) : transform.position.y;
            _planClimbHeight = Mathf.Max(0.01f, height);
            _useLandingToEnd = runway != null && runway.runwayRoot != null;

            if (stats != null) stats.Recompute();

            var bal = ConfigRegistry.Balance;
            _cachedFallPhaseAccel = bal != null ? bal.glideFallAccel : 20f;
            _cachedFallPhaseMaxSpeed = bal != null ? bal.glideFallMaxSpeed : 80f;
            _cachedGlidePhaseAccel = bal != null ? bal.glideForwardAccel : 30f;
            _cachedGlidePhaseMaxSpeed = bal != null ? bal.glideForwardMaxSpeed : 22f;

            var mult = bal != null ? bal.glideHorizontalDistanceMultiplier : 4f;
            var glideTuning = Mathf.Max(0.1f, mult / 4f);
            _cachedGlidePhaseAccel *= glideTuning;
            _cachedGlidePhaseMaxSpeed *= glideTuning;

            _glideFailsafeDeadline = -1f;
            _fallDownSpeed = 0f;
            _glideForwardSpeed = 0f;
            _glideDownSpeed = 0f;
            _cumDescentDuringFall = 0f;
            _lastTrajectoryVelocity = Vector3.zero;
        }

        private void StartFullGlideSequence(bool reachedTop)
        {
            BeginGlideSessionCore(reachedTop);

            var bal = ConfigRegistry.Balance;
            var mult = bal != null ? bal.glideHorizontalDistanceMultiplier : 4f;
            var glideTuning = Mathf.Max(0.1f, mult / 4f);

            _lifecycle = TowerExitLifecycle.PreGlideJump;
            _preJumpStartWorldY = transform.position.y;

            if (glideDebugLogging)
            {
                Debug.Log(
                    $"{LogPrefix}[路径] 攀爬中触发 StartGlide（空格/到顶）→ **未经过 Animator「空翻」**；使用脚本「起跳弧」preJumpBias → Apex→Fall。\n" +
                    $"{LogPrefix}FullGlide start climbH={_planClimbHeight:F2} preJumpBias={preGlideJumpBias} preJumpV={preGlideJumpSpeedMps} " +
                    $"fall vZ=vDown*{fallHorizontalPerVerticalSpeed} glide vDown=vFwd*{glideVerticalSpeedPerHorizontal} glideTuning={glideTuning:F3}",
                    this);
            }
        }

        /// <param name="smoothEntryFromBackflip">从后空翻接入时：俯仰用短时间 Slerp，并用抛物线末端速度预热坠落速度。</param>
        private void EnterGlideFallPhaseFromApex(bool smoothEntryFromBackflip = false)
        {
            _startFeetY = FeetWorldY();
            if (TryGetGroundSurfaceUnderFeet(out var sampledGroundY))
                _groundReferenceY = sampledGroundY;
            else
                _groundReferenceY = tower != null ? tower.BaseY : _startFeetY - _planClimbHeight;
            _verticalDropTotal = Mathf.Max(0.05f, _startFeetY - _groundReferenceY);
            _cumDescentDuringFall = 0f;
            _motionPhase = GlideMotionPhase.Fall;
            if (_verticalDropTotal < 0.08f)
                _motionPhase = GlideMotionPhase.Glide;

            var bal = ConfigRegistry.Balance;
            var failsafe = bal != null ? bal.glideFailsafeTimeoutSeconds : 600f;
            _glideFailsafeDeadline = Time.time + Mathf.Max(30f, failsafe);

            _lifecycle = _motionPhase == GlideMotionPhase.Glide ? TowerExitLifecycle.Glide : TowerExitLifecycle.Fall;
            _fallDownSpeed = 0f;
            _glideForwardSpeed = 0f;
            _glideDownSpeed = 0f;

            _fallEntryRotationBlendSecondsLeft = 0f;
            _fallEntryRotationBlendDuration = 0f;

            if (applyGlideFallBodyPitch)
            {
                if (smoothEntryFromBackflip && backflipToFallRotationBlendSeconds > 1e-4f)
                {
                    _fallEntryRotationFrom = transform.rotation;
                    _fallEntryRotationBlendDuration = backflipToFallRotationBlendSeconds;
                    _fallEntryRotationBlendSecondsLeft = _fallEntryRotationBlendDuration;
                }
                else
                    transform.rotation = GlideBodyRotationFromEuler(fallBodyEulerDegrees);

                _poseApplied = true;
            }
            else
                _poseApplied = false;

            if (smoothEntryFromBackflip && warmStartFallSpeedFromBackflipTangent)
                ApplyWarmStartFallSpeedFromBackflipParabolaTangent();

            var deferCam = cam != null && !cam.UsesGlideOrbitalFreeLook &&
                           smoothEntryFromBackflip && deferGlideCameraUntilBackflipRotationBlend &&
                           applyGlideFallBodyPitch && backflipToFallRotationBlendSeconds > 1e-4f;
            _pendingGlideCameraAfterBackflipBlend = deferCam;
            if (!deferCam)
                ConfigureGlideCameraForFallEntry();

            if (anim != null)
            {
                anim.SetGlide(true);
                var drivenAnim = anim.GetEffectiveAnimator();
                if (drivenAnim != null) drivenAnim.speed = 1f;
            }

            if (smoothEntryFromBackflip && _motionPhase == GlideMotionPhase.Fall)
            {
                if (UiCoordinator.Equipment != null &&
                    UiCoordinator.Equipment.TryBeginPetGlideFallBackflipYawFix())
                    _glideFallPetYawFixActive = true;
            }

            if (glideDebugLogging)
            {
                Debug.Log(
                    $"{LogPrefix}Apex→Fall feetY0={_startFeetY:F2} groundY={_groundReferenceY:F2} dropTotal={_verticalDropTotal:F2} phase0={_motionPhase}",
                    this);
            }
        }

        private float FeetWorldY()
        {
            if (_cc == null) return transform.position.y;
            return transform.position.y + _cc.center.y - _cc.height * 0.5f;
        }

        private static bool IsSelfCollider(Collider c, Transform self)
        {
            return c != null && (c.transform == self || c.transform.IsChildOf(self));
        }

        private bool TryGetGroundSurfaceHitUnderFeet(out RaycastHit bestHit)
        {
            bestHit = default;
            var feet = FeetWorldY();
            var probeY = Mathf.Max(transform.position.y, feet) + GroundProbeTopOffset;
            var origin = new Vector3(_lockedRunwayX, probeY, transform.position.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, GroundProbeMaxDistance, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return false;

            var bestDist = float.PositiveInfinity;
            var found = false;
            for (var i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null || IsSelfCollider(h.collider, transform))
                    continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    bestHit = h;
                    found = true;
                }
            }

            return found;
        }

        private bool TryGetGroundSurfaceUnderFeet(out float surfaceY)
        {
            if (!TryGetGroundSurfaceHitUnderFeet(out var h))
            {
                surfaceY = 0f;
                return false;
            }

            surfaceY = h.point.y;
            return true;
        }

        private void SnapFeetToGroundSurface(float surfaceWorldY)
        {
            if (_cc == null) return;
            var feet = FeetWorldY();
            var targetFeetY = surfaceWorldY + GroundHeightEndEpsilon * 0.5f;
            var deltaY = targetFeetY - feet;
            if (Mathf.Abs(deltaY) > 1.75f || Mathf.Abs(deltaY) < 0.0005f)
                return;
            _cc.Move(new Vector3(0f, deltaY, 0f));
            var pos = transform.position;
            pos.x = _lockedRunwayX;
            transform.position = pos;
        }

        private void SpawnLandingImpactRemnantAt(Vector3 worldPos, Vector3 worldNormal)
        {
            if (landingImpactRemnantPrefab == null) return;

            var up = worldNormal.sqrMagnitude > 1e-6f ? worldNormal.normalized : Vector3.up;
            var yawQ = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            var forward = Vector3.ProjectOnPlane(yawQ * Vector3.forward, up);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            forward.Normalize();
            var rot = Quaternion.LookRotation(forward, up);

            var go = Instantiate(
                landingImpactRemnantPrefab,
                worldPos + up * Mathf.Max(0f, landingImpactRemnantSurfaceOffset),
                rot);
            if (runway != null && runway.runwayRoot != null)
                go.transform.SetParent(runway.runwayRoot, true);

            if (landingImpactRemnantLifetimeSeconds > 0.01f)
                Destroy(go, landingImpactRemnantLifetimeSeconds);
        }

        private void ReleaseBorrowedAerialWindFx()
        {
            if (_borrowedAerialWindFx != null)
            {
                RestoreBorrowedWindParticlesSimulationSpeed();
                var mgr = GameplayVfxManager.Instance;
                if (mgr != null)
                    mgr.Release(_borrowedAerialWindFx);
            }

            _borrowedAerialWindFx = null;
            _windParticleCacheOwner = null;
            _borrowedWindParticleSystems = System.Array.Empty<ParticleSystem>();
        }

        private void TickBorrowedAerialWindFx()
        {
            if (!isGliding || _lifecycle == TowerExitLifecycle.PreGlideJump)
            {
                ReleaseBorrowedAerialWindFx();
                return;
            }

            var want = _motionPhase == GlideMotionPhase.Fall ? VfxKind.AerialFallWind : VfxKind.AerialGlideWind;
            var binderExisting = _borrowedAerialWindFx != null
                ? _borrowedAerialWindFx.GetComponent<PooledVfxInstance>()
                : null;
            if (binderExisting != null && binderExisting.KindId == want)
                return;

            ReleaseBorrowedAerialWindFx();
            var mgr = GameplayVfxManager.Instance;
            if (mgr == null)
                return;

            var parent = glideAerialWindAnchor != null ? glideAerialWindAnchor : transform;
            _borrowedAerialWindFx = mgr.BorrowFollow(
                want,
                parent,
                glideAerialWindLocalOffset,
                Quaternion.identity,
                Vector3.one);

            _windParticleCacheOwner = null;
        }

        private void EnsureBorrowedWindParticleSystemsCache()
        {
            if (_borrowedAerialWindFx != _windParticleCacheOwner)
            {
                _windParticleCacheOwner = _borrowedAerialWindFx;
                _borrowedWindParticleSystems = _borrowedAerialWindFx != null
                    ? _borrowedAerialWindFx.GetComponentsInChildren<ParticleSystem>(true)
                    : System.Array.Empty<ParticleSystem>();
            }
        }

        private void RestoreBorrowedWindParticlesSimulationSpeed()
        {
            EnsureBorrowedWindParticleSystemsCache();
            var reset = Mathf.Max(0.01f, aerialWindReleaseSimulationSpeedReset);
            for (var i = 0; i < _borrowedWindParticleSystems.Length; i++)
            {
                var sys = _borrowedWindParticleSystems[i];
                if (sys == null)
                    continue;
                var main = sys.main;
                main.simulationSpeed = reset;
            }
        }

        /// <summary>
        /// 按轨迹速率与 VfxKind（坠/滑）分别映射 Unity 粒子 <c>simulationSpeed</c>，减轻高速时拖尾「追不上」体感。
        /// </summary>
        private void ApplyBorrowedAerialWindSimulationSpeed()
        {
            if (_borrowedAerialWindFx == null)
                return;

            var binder = _borrowedAerialWindFx.GetComponent<PooledVfxInstance>();
            var kind = binder != null ? binder.KindId : VfxKind.AerialFallWind;
            var speed = _lastTrajectoryVelocity.magnitude;

            float refSpeed, minS, maxS, exp;
            if (kind == VfxKind.AerialGlideWind)
            {
                refSpeed = Mathf.Max(1e-3f, aerialWindGlideSpeedReference);
                minS = aerialWindGlideSimulationMin;
                maxS = Mathf.Max(minS, aerialWindGlideSimulationMax);
                exp = Mathf.Max(0.1f, aerialWindGlideSimulationExponent);
            }
            else
            {
                refSpeed = Mathf.Max(1e-3f, aerialWindFallSpeedReference);
                minS = aerialWindFallSimulationMin;
                maxS = Mathf.Max(minS, aerialWindFallSimulationMax);
                exp = Mathf.Max(0.1f, aerialWindFallSimulationExponent);
            }

            var u = Mathf.Clamp01(speed / refSpeed);
            u = Mathf.Pow(u, exp);
            var sim = Mathf.Lerp(minS, maxS, u);

            EnsureBorrowedWindParticleSystemsCache();
            for (var i = 0; i < _borrowedWindParticleSystems.Length; i++)
            {
                var sys = _borrowedWindParticleSystems[i];
                if (sys == null)
                    continue;
                var main = sys.main;
                main.simulationSpeed = sim;
            }
        }

        private void SpawnPooledLandingDust(Vector3 contactWorld, Vector3 worldNormal, float landingSpeedMetric)
        {
            var mgr = GameplayVfxManager.Instance;
            if (mgr == null)
                return;

            var kind = landingSpeedMetric >= landingDustStrongMinSpeed ? VfxKind.LandingDustStrong : VfxKind.LandingDustWeak;
            var up = worldNormal.sqrMagnitude > 1e-6f ? worldNormal.normalized : Vector3.up;
            var yawQ = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            var forward = Vector3.ProjectOnPlane(yawQ * Vector3.forward, up);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            forward.Normalize();
            var rot = Quaternion.LookRotation(forward, up);
            var offset = Mathf.Max(0f, landingImpactRemnantSurfaceOffset);
            var p = contactWorld + up * offset;
            mgr.BorrowWorld(kind, p, rot, Vector3.one, -1f);
            mgr.BorrowWorld(VfxKind.LandingGroundSlam, p, rot, Vector3.one, -1f);
        }

        private void ReportGlideFallAmbientWind()
        {
            if (_lifecycle == TowerExitLifecycle.PreGlideJump)
                return;
            if (_motionPhase != GlideMotionPhase.Fall)
                return;

            var mgr = AudioManager.Instance;
            if (mgr == null || tower == null)
                return;

            const float speedCap = 200f;
            const float heightCap = 1000f;
            var denomH = Mathf.Max(1f, Mathf.Min(heightCap, tower.MaxHeight > 1f ? tower.MaxHeight : heightCap));
            var hRel = Mathf.Max(0f, transform.position.y - tower.BaseY);
            var h01 = Mathf.Clamp01(hRel / denomH);
            var s01 = Mathf.Clamp01(_fallDownSpeed / speedCap);
            var drive = Mathf.Clamp01(h01 * 0.35f + s01 * 0.65f);
            mgr.ReportAmbientWindDrive01(drive);
        }

        private void StopImpactLandRootMotionRestoreRoutine()
        {
            if (_impactLandRootMotionRestoreRoutine == null) return;
            StopCoroutine(_impactLandRootMotionRestoreRoutine);
            _impactLandRootMotionRestoreRoutine = null;
        }

        private IEnumerator CoRestoreRootMotionAfterImpactLand()
        {
            yield return null;
            yield return null;
            _impactLandRootMotionRestoreRoutine = null;
            RestoreAnimatorRootMotionAfterAerial();
            if (_impactLandNeedsAnimatorRootRestore)
            {
                var a = anim != null ? anim.GetEffectiveAnimator() : GetComponentInChildren<Animator>();
                if (a != null) a.applyRootMotion = _impactLandRootMotionPrev;
                _impactLandNeedsAnimatorRootRestore = false;
            }
        }

        private void TickPreGlideJump()
        {
            var dt = Time.deltaTime;
            var bias = preGlideJumpBias;
            bias.y = Mathf.Max(0.01f, bias.y);
            var jumpDir = bias.sqrMagnitude > 1e-6f ? bias.normalized : Vector3.up;
            _lastTrajectoryVelocity = jumpDir * Mathf.Max(0f, preGlideJumpSpeedMps);
            _cc.Move(jumpDir * (preGlideJumpSpeedMps * dt));
            var pos = transform.position;
            pos.x = _lockedRunwayX;
            transform.position = pos;

            if (transform.position.y >= _preJumpStartWorldY + bias.y - 0.02f)
                EnterGlideFallPhaseFromApex();
        }

        private void TickGlide()
        {
            if (_lifecycle == TowerExitLifecycle.PreGlideJump)
            {
                TickBorrowedAerialWindFx();
                TickPreGlideJump();
                ApplyBorrowedAerialWindSimulationSpeed();
                if (hud != null && tower != null)
                {
                    hud.SetHeight(transform.position.y - tower.BaseY);
                    hud.SetStamina(0f, stamina != null ? stamina.Max : 0f);
                }

                return;
            }

            if (TryConsumeFailsafe())
                return;

            if (cam != null)
                cam.SetGlideCameraFallShake(_motionPhase == GlideMotionPhase.Fall);

            if (runway != null)
                runway.UpdateVisibility(transform.position.z);

            // 贴地则不再更新滑翔俯仰/积分位移，直接进撞击或结算。
            if (_lifecycle != TowerExitLifecycle.PreGlideJump && FeetReachedGroundHeight())
            {
                CompleteGlideGroundLanding();
                return;
            }

            if (applyGlideFallBodyPitch && _lifecycle != TowerExitLifecycle.PreGlideJump)
            {
                if (_fallEntryRotationBlendSecondsLeft > 0f)
                {
                    _fallEntryRotationBlendSecondsLeft -= Time.deltaTime;
                    var d = Mathf.Max(1e-4f, _fallEntryRotationBlendDuration);
                    var uBlend = 1f - Mathf.Clamp01(_fallEntryRotationBlendSecondsLeft / d);
                    uBlend = SmoothStep01(uBlend);
                    var qFall = GlideBodyRotationFromEuler(fallBodyEulerDegrees);
                    transform.rotation = Quaternion.Slerp(_fallEntryRotationFrom, qFall, uBlend);
                    _poseApplied = true;
                    if (_fallEntryRotationBlendSecondsLeft <= 0f)
                        _fallEntryRotationBlendSecondsLeft = 0f;
                }
                else
                    ApplyGlideBodyRotationForCurrentPhase();
            }

            if (_pendingGlideCameraAfterBackflipBlend && _fallEntryRotationBlendSecondsLeft <= 0f)
            {
                ConfigureGlideCameraForFallEntry();
                _pendingGlideCameraAfterBackflipBlend = false;
            }

            var dt = Time.deltaTime;

            var move = ComputeMoveStep(dt);
            ClampMoveAgainstRunwaySurface(ref move);
            ApplyHorizontalLockAndMove(move);
            TryTransitionFallToGlideAfterMove(move);

            TickBorrowedAerialWindFx();
            ApplyBorrowedAerialWindSimulationSpeed();
            ReportGlideFallAmbientWind();

            if (FeetReachedGroundHeight())
            {
                CompleteGlideGroundLanding();
                return;
            }

            MaybeLogGlidePeriodicSnapshot();

            if (hud != null && tower != null)
            {
                hud.SetHeight(transform.position.y - tower.BaseY);
                hud.SetStamina(0f, stamina != null ? stamina.Max : 0f);
            }
        }

        private bool TryConsumeFailsafe()
        {
            if (_glideFailsafeDeadline <= 0f || Time.time < _glideFailsafeDeadline)
                return false;
            FinalizeGlideSession(GlideEndReason.Failsafe);
            return true;
        }

        /// <summary>第一次触地：默认本帧内砸击表现 + 结算（无沿跑道位移）；可选多帧 outro。</summary>
        private void CompleteGlideGroundLanding()
        {
            ReleaseBorrowedAerialWindFx();
            if (enableLandingSkidOutro)
            {
                BeginLandingSkidOutro(GlideEndReason.GroundHeight);
                return;
            }

            ApplyGroundImpactPresentationCore(out _, out _);
            transform.rotation = GlideLandingUprightRotation();
            _poseApplied = false;
            FinalizeGlideSession(GlideEndReason.GroundHeight);
        }

        /// <summary>贴地瞬间的共用逻辑：对齐脚底、在<strong>当前跑道锁定 X 与角色 Z</strong> 生成残留、清零滑翔速度、镜头/跑道反馈、暂时关闭 RootMotion。</summary>
        private void ApplyGroundImpactPresentationCore(out float equivFwd, out float landingSkidSpeedZOut)
        {
            var verticalImpact = Mathf.Max(_fallDownSpeed, _glideDownSpeed);
            var horizScale = Mathf.Max(0f, fallHorizontalPerVerticalSpeed);
            equivFwd = _motionPhase == GlideMotionPhase.Glide
                ? _glideForwardSpeed
                : _fallDownSpeed * horizScale;

            var landingBurstMetric = Mathf.Max(equivFwd, verticalImpact);

            if (TryGetGroundSurfaceHitUnderFeet(out var groundHit))
            {
                SnapFeetToGroundSurface(groundHit.point.y);
                var remnantPos = new Vector3(_lockedRunwayX, groundHit.point.y, transform.position.z);
                SpawnLandingImpactRemnantAt(remnantPos, groundHit.normal);
                SpawnPooledLandingDust(remnantPos, groundHit.normal, landingBurstMetric);
            }
            else if (TryGetGroundSurfaceUnderFeet(out var sy))
            {
                SnapFeetToGroundSurface(sy);
                var remnantPos = new Vector3(_lockedRunwayX, sy, transform.position.z);
                SpawnLandingImpactRemnantAt(remnantPos, Vector3.up);
                SpawnPooledLandingDust(remnantPos, Vector3.up, landingBurstMetric);
            }

            _glideForwardSpeed = 0f;
            _glideDownSpeed = 0f;
            _fallDownSpeed = 0f;
            _motionPhase = GlideMotionPhase.Glide;
            _lifecycle = TowerExitLifecycle.Glide;

            landingSkidSpeedZOut = Mathf.Max(
                Mathf.Max(0f, landingSkidMinInitialSpeedMps),
                Mathf.Max(0f, equivFwd) * Mathf.Clamp01(landingSkidSpeedRetention));

            var animatorRef = anim != null ? anim.GetEffectiveAnimator() : GetComponentInChildren<Animator>();
            if (animatorRef != null)
            {
                _impactLandRootMotionPrev = animatorRef.applyRootMotion;
                _impactLandNeedsAnimatorRootRestore = animatorRef.applyRootMotion;
                animatorRef.applyRootMotion = false;
            }

            var impactIntensitySpeed = Mathf.Max(equivFwd, landingSkidSpeedZOut);

            if (cam != null)
            {
                var speedBoost = 1f + Mathf.Max(0f, equivFwd) * landingCamShakeSpeedScale;
                cam.TriggerLandingImpactShake(
                    landingCamShakePosPeakMeters * speedBoost,
                    landingCamShakeEulerPeakDegrees * speedBoost,
                    landingCamShakeDurationSeconds);
            }

            AudioManager.Instance?.PlaySfx(SfxId.GlideLand, Mathf.Clamp01(0.45f + landingBurstMetric * 0.02f));

            if (runway != null)
                runway.PlayLandingImpact(transform.position.z, impactIntensitySpeed);
        }

        private void BeginLandingSkidOutro(GlideEndReason reason)
        {
            if (_landingSkidOutroActive)
                return;
            if (reason != GlideEndReason.GroundHeight)
            {
                FinalizeGlideSession(reason);
                return;
            }

            ReleaseBorrowedAerialWindFx();

            _landingSkidOutroActive = true;
            _landingSkidOutroElapsed = 0f;
            _landingPendingEndReason = reason;
            _landingStartRotation = transform.rotation;

            ApplyGroundImpactPresentationCore(out _, out _landingSkidSpeedZ);
        }

        private void TickLandingSkidOutro()
        {
            var dt = Time.deltaTime;
            _landingSkidOutroElapsed += dt;

            _landingSkidSpeedZ = Mathf.MoveTowards(
                _landingSkidSpeedZ,
                0f,
                Mathf.Max(0f, landingSkidDecelerationMps2) * dt);

            var stick = new Vector3(0f, -Mathf.Max(0.01f, landingGroundStickDownMps) * dt, _landingSkidSpeedZ * dt);
            _cc.Move(stick);
            var pos = transform.position;
            pos.x = _lockedRunwayX;
            transform.position = pos;

            var uprightT = landingUprightBlendSeconds > 1e-4f
                ? SmoothStep01(Mathf.Clamp01(_landingSkidOutroElapsed / landingUprightBlendSeconds))
                : 1f;
            transform.rotation = Quaternion.Slerp(_landingStartRotation, GlideLandingUprightRotation(), uprightT);
            _poseApplied = uprightT < 0.999f;

            if (runway != null)
                runway.UpdateVisibility(transform.position.z);

            if (cam != null)
                cam.SetGlideCameraFallShake(false);

            if (hud != null && tower != null)
            {
                hud.SetHeight(transform.position.y - tower.BaseY);
                hud.SetStamina(0f, stamina != null ? stamina.Max : 0f);
            }

            var minDur = Mathf.Max(0.01f, landingSkidMinDurationSeconds);
            var maxDur = Mathf.Max(minDur, landingSkidMaxDurationSeconds);
            var blendDone = uprightT >= 0.985f;
            var skidLow = _landingSkidSpeedZ < 0.35f;
            var afterMin = _landingSkidOutroElapsed >= minDur;
            var timeCap = _landingSkidOutroElapsed >= maxDur;
            var noForwardSkid = _landingSkidSpeedZ < 0.04f;
            if (timeCap ||
                (afterMin && noForwardSkid && blendDone) ||
                (afterMin && !noForwardSkid && skidLow))
                CompleteLandingSkidOutro();
        }

        private void CompleteLandingSkidOutro()
        {
            _landingSkidOutroActive = false;
            _landingSkidOutroElapsed = 0f;
            _landingSkidSpeedZ = 0f;
            var r = _landingPendingEndReason;
            _landingPendingEndReason = GlideEndReason.Failsafe;
            // RestoreUprightPose() in FinalizeGlideSession 仍按「滑翔中段」用 _uprightRotation；此处锁定滑翔落地的最终 Yaw（含偏移）。
            transform.rotation = GlideLandingUprightRotation();
            _poseApplied = false;
            FinalizeGlideSession(r);
        }

        private float FallToGlideDescentBudgetMeters =>
            Mathf.Clamp01(fallToGlideVerticalDescentFraction) * _verticalDropTotal;

        private float FallToGlideBlendStartMeters(float budgetMeters) =>
            budgetMeters * Mathf.Clamp01(1f - Mathf.Max(0f, fallToGlideBlendFraction));

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        /// <summary>与位移混合同步：0=坠落俯仰，1=滑翔俯仰。</summary>
        private float CurrentFallToGlideTrajectoryBlend01()
        {
            if (_motionPhase == GlideMotionPhase.Glide)
                return 1f;

            var budget = FallToGlideDescentBudgetMeters;
            var blendStart = FallToGlideBlendStartMeters(budget);
            if (budget - blendStart < 1e-3f)
                return 0f;
            if (_cumDescentDuringFall < blendStart)
                return 0f;
            var t = Mathf.InverseLerp(blendStart, budget, _cumDescentDuringFall);
            return SmoothStep01(Mathf.Clamp01(t));
        }

        /// <summary>在 <see cref="_uprightRotation"/>（进入滑翔时锁定的 Yaw）上叠加俯仰/横滚；欧拉角的 Y 不参与，避免改朝向。</summary>
        private Quaternion GlideBodyRotationFromEuler(Vector3 eulerDeg)
        {
            return Quaternion.Euler(eulerDeg.x, 0f, eulerDeg.z) * _uprightRotation;
        }

        private void ApplyGlideBodyRotationForCurrentPhase()
        {
            var t = CurrentFallToGlideTrajectoryBlend01();
            var qFall = GlideBodyRotationFromEuler(fallBodyEulerDegrees);
            var qGlide = GlideBodyRotationFromEuler(glideBodyEulerDegrees);
            transform.rotation = Quaternion.Slerp(qFall, qGlide, t);
            _poseApplied = true;
        }

        private Vector3 ComputeMoveStep(float dt)
        {
            if (_motionPhase == GlideMotionPhase.Glide)
            {
                _glideForwardSpeed = Mathf.MoveTowards(
                    _glideForwardSpeed,
                    _cachedGlidePhaseMaxSpeed,
                    Mathf.Max(0f, _cachedGlidePhaseAccel) * dt);
                _glideDownSpeed = _glideForwardSpeed * Mathf.Max(0f, glideVerticalSpeedPerHorizontal);
                _lastTrajectoryVelocity = new Vector3(0f, -_glideDownSpeed, _glideForwardSpeed);
                return _lastTrajectoryVelocity * dt;
            }

            var fallHorizScale = Mathf.Max(0f, fallHorizontalPerVerticalSpeed);
            _fallDownSpeed = Mathf.MoveTowards(
                _fallDownSpeed,
                _cachedFallPhaseMaxSpeed,
                Mathf.Max(0f, _cachedFallPhaseAccel) * dt);
            var fallVel = new Vector3(
                0f,
                -_fallDownSpeed,
                _fallDownSpeed * fallHorizScale);

            var budget = FallToGlideDescentBudgetMeters;
            var blendStart = FallToGlideBlendStartMeters(budget);
            var blendT = 0f;
            var glideVel = Vector3.zero;

            if (budget - blendStart >= 1e-3f && _cumDescentDuringFall + 1e-4f >= blendStart)
            {
                _glideForwardSpeed = Mathf.MoveTowards(
                    _glideForwardSpeed,
                    _cachedGlidePhaseMaxSpeed,
                    Mathf.Max(0f, _cachedGlidePhaseAccel) * dt);
                _glideDownSpeed = _glideForwardSpeed * Mathf.Max(0f, glideVerticalSpeedPerHorizontal);
                glideVel = new Vector3(0f, -_glideDownSpeed, _glideForwardSpeed);
                blendT = SmoothStep01(Mathf.Clamp01(Mathf.InverseLerp(blendStart, budget, _cumDescentDuringFall)));
            }

            _lastTrajectoryVelocity = Vector3.Lerp(fallVel, glideVel, blendT);
            return _lastTrajectoryVelocity * dt;
        }

        private void ClampMoveAgainstRunwaySurface(ref Vector3 move)
        {
            if (!_useLandingToEnd || runway == null || runway.runwayRoot == null)
                return;

            var dist = move.magnitude;
            if (dist <= 1e-6f)
                return;

            var origin = _cc != null ? _cc.bounds.center : transform.position + Vector3.up * 0.5f;
            var dir = move / dist;
            if (!Physics.Raycast(origin, dir, out var hit, dist + 0.05f, ~0, QueryTriggerInteraction.Ignore))
                return;

            if (hit.collider == null || hit.collider.transform == null ||
                !hit.collider.transform.IsChildOf(runway.runwayRoot))
                return;

            var skin = Mathf.Max(0.01f, runwayApproachSkinMeters);
            var clamped = Mathf.Max(0f, hit.distance - skin);
            move = dir * clamped;
        }

        private void ApplyHorizontalLockAndMove(Vector3 move)
        {
            _cc.Move(move);
            var pos = transform.position;
            pos.x = _lockedRunwayX;
            transform.position = pos;
        }

        private void TryTransitionFallToGlideAfterMove(Vector3 move)
        {
            if (_lifecycle != TowerExitLifecycle.Fall || _motionPhase != GlideMotionPhase.Fall)
                return;

            _cumDescentDuringFall += Mathf.Max(0f, -move.y);
            var budget = Mathf.Clamp01(fallToGlideVerticalDescentFraction) * _verticalDropTotal;
            if (_cumDescentDuringFall < budget - 0.02f)
                return;

            var vDownEnd = Mathf.Max(0f, _fallDownSpeed);
            // 混合阶段可能已提前加速滑翔水平速度，取较大值避免切入滑翔段时回退。
            _glideForwardSpeed = Mathf.Max(_glideForwardSpeed, vDownEnd);
            _glideDownSpeed = _glideForwardSpeed * Mathf.Max(0f, glideVerticalSpeedPerHorizontal);

            if (glideDebugLogging)
            {
                Debug.Log(
                    $"{LogPrefix}Fall→Glide cumFallDescent={_cumDescentDuringFall:F2} budget={budget:F2} feetY={FeetWorldY():F2} " +
                    $"fallVDownEnd={vDownEnd:F2} → glideVForward0={_glideForwardSpeed:F2} glideVDown0={_glideDownSpeed:F2}",
                    this);
            }

            _motionPhase = GlideMotionPhase.Glide;
            _lifecycle = TowerExitLifecycle.Glide;
        }

        private bool FeetReachedGroundHeight()
        {
            if (_cc != null && _cc.isGrounded)
                return true;

            var feet = FeetWorldY();
            if (TryGetGroundSurfaceUnderFeet(out var surfaceY))
                return feet <= surfaceY + GroundHeightEndEpsilon;

            return feet <= _groundReferenceY + GroundHeightEndEpsilon;
        }

        private void MaybeLogGlidePeriodicSnapshot()
        {
            if (!glideDebugLogging || glideDebugLogInterval <= 0f)
                return;
            if (Time.unscaledTime < _nextGlideDebugLogUnscaledTime)
                return;
            _nextGlideDebugLogUnscaledTime = Time.unscaledTime + glideDebugLogInterval;

            var feet = FeetWorldY();
            var hasRay = TryGetGroundSurfaceUnderFeet(out var rayGroundY);
            var gap = hasRay ? feet - rayGroundY : float.NaN;
            var elapsed = Time.unscaledTime - _glideStartUnscaledTime;
            Debug.Log(
                $"{LogPrefix}t={elapsed:F2}s life={_lifecycle} motion={_motionPhase} feetY={feet:F2} rayGroundY={(hasRay ? rayGroundY.ToString("F2") : "?")} gap={(hasRay ? gap.ToString("F2") : "?")}m " +
                $"fallback≤{_groundReferenceY + GroundHeightEndEpsilon:F2} grounded={(_cc != null && _cc.isGrounded)} " +
                $"pos={transform.position} fallVDown={_fallDownSpeed:F2} glideVFwd={_glideForwardSpeed:F2} glideVDown={_glideDownSpeed:F2} cumFallY={_cumDescentDuringFall:F2}",
                this);
        }

        /// <summary>滑翔着地后的直立朝向：<see cref="_uprightRotation"/> 乘世界 Y 偏移（短落不走此处）。</summary>
        private Quaternion GlideLandingUprightRotation()
        {
            if (Mathf.Abs(glideLandingYawOffsetDegrees) < 0.001f)
                return _uprightRotation;
            return _uprightRotation * Quaternion.Euler(0f, glideLandingYawOffsetDegrees, 0f);
        }

        private void RestoreUprightPose()
        {
            if (!_poseApplied) return;
            transform.rotation = _uprightRotation;
            _poseApplied = false;
        }

        /// <summary>
        /// 完整滑翔会话开始时调用（<see cref="BeginGlideSessionCore"/>）。将门控碰撞体设为 Trigger；首次记录原 <see cref="Collider.isTrigger"/> 以便结束时还原。
        /// </summary>
        void PushGlideGateTriggerOn()
        {
            if (glideSessionGateCollider == null) return;
            if (!_gateColliderStored)
            {
                _gateColliderPreviousIsTrigger = glideSessionGateCollider.isTrigger;
                _gateColliderStored = true;
            }

            glideSessionGateCollider.isTrigger = true;
        }

        /// <summary>滑翔结束或重生时恢复门控碰撞体的 Trigger 状态。</summary>
        void RestoreGlideGateTriggerIfNeeded()
        {
            if (glideSessionGateCollider == null || !_gateColliderStored) return;
            glideSessionGateCollider.isTrigger = _gateColliderPreviousIsTrigger;
            _gateColliderStored = false;
        }

        public void ResetRuntimeStateForRespawn()
        {
            BlockShopNavigationUntilRespawn = false;

            if (glideDebugLogging && (isGliding || _shortDropActive))
                Debug.Log($"{LogPrefix}ResetRuntimeStateForRespawn", this);

            RestoreGlideGateTriggerIfNeeded();

            ClearGlideFallPetYawFixIfActive();

            isGliding = false;
            _shortDropActive = false;
            _backflipActive = false;
            _pendingGlideCameraAfterBackflipBlend = false;
            RestoreBackflipPhysicsOverrides();
            RestoreAnimatorRootMotionAfterAerial();
            StopPresentSettlementRoutine();
            _reachedTopOnStart = false;
            _useLandingToEnd = false;
            _glideFailsafeDeadline = -1f;
            _fallDownSpeed = 0f;
            _glideForwardSpeed = 0f;
            _glideDownSpeed = 0f;
            _motionPhase = GlideMotionPhase.Fall;
            _lifecycle = TowerExitLifecycle.PreGlideJump;
            _cumDescentDuringFall = 0f;
            _lastTrajectoryVelocity = Vector3.zero;
            _fallEntryRotationBlendSecondsLeft = 0f;
            _fallEntryRotationBlendDuration = 0f;
            RestoreUprightPose();
            if (cam != null)
            {
                cam.CancelSettlementYawOrbit();
                cam.SetGlideShakeOverlayActive(false);
                cam.EndBackflipCameraBehindPlayerSnap();
            }
            ReleaseBorrowedAerialWindFx();
            _landingSkidOutroActive = false;
            _landingSkidOutroElapsed = 0f;
            StopImpactLandRootMotionRestoreRoutine();
            if (_impactLandNeedsAnimatorRootRestore)
            {
                var a = anim != null ? anim.GetEffectiveAnimator() : GetComponentInChildren<Animator>();
                if (a != null) a.applyRootMotion = _impactLandRootMotionPrev;
                _impactLandNeedsAnimatorRootRestore = false;
            }
        }

        private void FinalizeGlideSession(GlideEndReason reason)
        {
            BlockShopNavigationUntilRespawn = true;

            ClearGlideFallPetYawFixIfActive();
            RestoreGlideGateTriggerIfNeeded();
            ReleaseBorrowedAerialWindFx();
            if (glideDebugLogging)
            {
                var elapsed = Time.unscaledTime - _glideStartUnscaledTime;
                Debug.Log(
                    $"{LogPrefix}FinalizeGlide reason={reason} t={elapsed:F2}s life={_lifecycle} motion={_motionPhase} endPos={transform.position} feetY={FeetWorldY():F2}",
                    this);
            }

            RestoreUprightPose();
            // 地面结算统一延后恢复 RootMotion（含立即结算与 outro），避免 Victory 首帧与撞击位移冲突。
            var deferRootMotionBundle = reason == GlideEndReason.GroundHeight;
            if (!deferRootMotionBundle)
                RestoreAnimatorRootMotionAfterAerial();
            isGliding = false;
            _lifecycle = TowerExitLifecycle.PreGlideJump;
            _fallEntryRotationBlendSecondsLeft = 0f;
            _fallEntryRotationBlendDuration = 0f;
            _pendingGlideCameraAfterBackflipBlend = false;

            if (cam != null)
            {
                cam.SetGlideShakeOverlayActive(false);
                cam.SetMode(PlayerMode.Settlement);
                if (reason == GlideEndReason.GroundHeight)
                    cam.BeginGlideLandingSettlementCamera(
                        glideLandingCameraYawOrbitDegrees,
                        glideLandingCameraOrbitDurationSeconds,
                        glideLandingCameraOrbitDelaySeconds);
            }

            if (anim != null)
            {
                anim.SetGlide(false);
                anim.SetVictory(true);
            }

            if (deferRootMotionBundle)
            {
                StopImpactLandRootMotionRestoreRoutine();
                _impactLandRootMotionRestoreRoutine = StartCoroutine(CoRestoreRootMotionAfterImpactLand());
            }

            StopPresentSettlementRoutine();

            var endPos = transform.position;
            var height = tower != null ? Mathf.Max(0f, _startPos.y - tower.BaseY) : _startPos.y;
            var glideDistFromTower = DistanceManager.HorizontalGlideMetersFromTowerRoot(endPos, tower);
            var reachedTopForSettlement = _reachedTopOnStart;

            _presentSettlementRoutine = StartCoroutine(CoPresentSettlement(
                reason,
                height,
                glideDistFromTower,
                reachedTopForSettlement));
        }

        void StopPresentSettlementRoutine()
        {
            if (_presentSettlementRoutine == null)
                return;
            StopCoroutine(_presentSettlementRoutine);
            _presentSettlementRoutine = null;
        }

        IEnumerator CoPresentSettlement(
            GlideEndReason reason,
            float heightMeters,
            float glideDistFromTower,
            bool reachedTopOnStart)
        {
            var wait = Mathf.Max(0f, settlementPresentationDelaySeconds);
            if (wait > 0f)
                yield return new WaitForSeconds(wait);

            _presentSettlementRoutine = null;

            if (SaveService.Data != null)
            {
                SaveService.Data.bestGlideDistanceMeters =
                    Mathf.Max(SaveService.Data.bestGlideDistanceMeters, glideDistFromTower);
            }

            var recordGlide = SaveService.Data != null ? SaveService.Data.bestGlideDistanceMeters : glideDistFromTower;

            var coinsAdd = 0f;
            var coinsMul = 0f;
            if (stats != null)
            {
                stats.Recompute();
                coinsAdd = stats.Current.totalCoinsAdd;
                coinsMul = stats.Current.totalCoinsMul;
            }

            StatBonusDebug.LogSettlementCoinModifierSources(coinsAdd, coinsMul);

            var rewards = RewardCalculator.CalculateGlideSettlement(heightMeters, glideDistFromTower, coinsAdd, coinsMul);
            RewardApplier.Apply(rewards);

            var towerMedals = 0;
            if (reachedTopOnStart)
                towerMedals = MapService.AwardTowerClearMedalsForCurrentMap();

            UiCoordinator.ShowSettlement(SettlementViewModel.FromRun(
                heightMeters,
                glideDistFromTower,
                recordGlide,
                rewards,
                towerMedals));

            if (reason == GlideEndReason.GroundHeight)
            {
                var vfxMgr = GameplayVfxManager.Instance;
                if (vfxMgr != null)
                {
                    var o = settlementFxWorldOffset;
                    var p = settlementFxAnchor != null ? settlementFxAnchor.position : transform.position;
                    vfxMgr.BorrowWorld(VfxKind.SettlementVictory, p + o, Quaternion.identity, Vector3.one, -1f);
                }

                AudioManager.Instance?.PlaySfx(SfxId.SettlementWin);
            }

            SaveService.Save();
        }
    }
}

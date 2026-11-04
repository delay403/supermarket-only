using UnityEngine;
using Game.Core.Config;
using Game.Core;

namespace Game.Player
{
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [System.Serializable]
        private struct OrbitSettings
        {
            public Vector3 offset;
            public float yawSpeed;
            public float pitchSpeed;
            public float pitchMin;
            public float pitchMax;
            public float followSharpness;
            public float pivotHeight;
            public float fov; // <=0 means no override
        }

        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private LegacyPlayerInput input;

        [Header("State")]
        [SerializeField] private PlayerMode mode = PlayerMode.FreeMove;

        [Header("Free Move Orbit (fallback if no config)")]
        [SerializeField] private Vector3 freeOffset = new Vector3(0f, 3.5f, -6.5f);
        [SerializeField] private float yawSpeed = 150f;
        [SerializeField] private float pitchSpeed = 120f;
        [SerializeField] private float pitchMin = -25f;
        [SerializeField] private float pitchMax = 65f;
        [SerializeField] private float followSharpness = 12f;
        [SerializeField] private float pivotHeight = 1.5f;

        [Header("Look Smoothing (virtual joystick)")]
        [Tooltip("右摇杆 touch-delta 角速率低通；滤除帧间抖动，略增则更丝滑、过大则跟手变慢。")]
        [SerializeField] private float lookInputSmoothSharpness = 22f;
        [Tooltip("转视角时机位/朝向追赶速率（高于 followSharpness，低于即时 snap）。")]
        [SerializeField] private float lookOrbitSharpness = 40f;

        [Header("Climb Orbit (independent)")]
        [SerializeField] private Vector3 climbOffset = new Vector3(2.5f, 2.0f, -3.0f);
        [SerializeField] private float climbFov = 55f;
        [SerializeField] private float climbYawSpeed = 120f;
        [SerializeField] private float climbPitchSpeed = 100f;
        [SerializeField] private float climbPitchMin = -10f;
        [SerializeField] private float climbPitchMax = 55f;
        [SerializeField] private float climbFollowSharpness = 14f;
        [SerializeField] private float climbPivotHeight = 1.5f;

        [Header("Collision Avoidance (Free Move)")]
        [SerializeField] private bool collisionAvoidance = true;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float probeRadius = 0.25f;
        [SerializeField] private float nearPadding = 0.12f;
        [SerializeField] private float minDistance = 1.2f;
        [SerializeField] private float returnSharpness = 16f;
        [SerializeField] private float ignoreColliderMinExtent = 0.25f;

        [Header("攀爬 · 后空翻镜头")]
        [Tooltip("后空翻期间将轨道 Yaw/Pitch 平滑对齐到角色背后机位；时长与 GlideController 空翻时长一致为宜。")]
        [SerializeField] private float backflipCamBlendPitchDegrees = 18f;

        [Header("Glide (forced follow)")]
        [SerializeField] private Vector3 glideOffset = new Vector3(4.0f, 2.5f, -6.0f);
        [SerializeField] private float glideFov = 65f;
        [SerializeField] private float glidePivotHeight = 1.5f;
        [SerializeField] private float forcedSharpness = 10f;
        [Tooltip("勾选后滑翔阶段保持 PlayerMode.FreeMove 与「自由移动」机位/速率（不切换到滑翔强制跟随，避免后空翻→坠落时镜头瞬切）；仍可鼠标环顾，并叠加 Balance 里配置的坠落/滑翔抖动。")]
        [SerializeField] private bool glideUseOrbitalFreeLook;

        [Header("Settlement (forced follow)")]
        [Tooltip("滑翔地面结算若未走「着陆呈现」流程（如超时兜底），仍用该偏移强制跟角色本地空间；正常滑翔着地由 GlideController 启动呈现机位，不使用此项。")]
        [SerializeField] private Vector3 settlementOffset = new Vector3(4.0f, 2.5f, -6.0f);
        [SerializeField] private float settlementFov = 65f;
        [SerializeField] private float settlementPivotHeight = 1.5f;

        [Header("Glide landing settlement — back → front arc")]
        [Tooltip("相对玩家朝向（仅水平 Yaw）：着陆结算<strong>起点</strong>，摄像机在角色<strong>身后</strong>的偏移（与正面偏移通常 Z 符号相反）。")]
        [SerializeField] private Vector3 glideLandingSettlementBackOffsetLocal = new Vector3(0f, 2.2f, -5.5f);

        [Tooltip("相对玩家朝向（仅水平 Yaw）：着陆结算<strong>终点</strong>，摄像机在角色<strong>正面</strong>的偏移；绕轨结束后保持该相对位置。")]
        [SerializeField] private Vector3 glideLandingSettlementFrontOffsetLocal = new Vector3(0f, 2.2f, 5.5f);
        [Tooltip("绕轨段与结算保持段：摄像机世界坐标 X/Y 使用该值，Z 仍取自轨道/保持逻辑（旋转仍 LookAt pivot）。")]
        [SerializeField] private bool glideLandingSettlementCameraLockWorldXY = true;
        [SerializeField] private Vector2 glideLandingSettlementCameraWorldXY = new Vector2(26f, 1f);

        private enum GlideLandingSettlementCamPhase
        {
            Idle,
            WaitingDelay,
            Running,
            /// <summary>绕轨结束或跳过绕轨：保持当前相对 pivot 的偏移直至退出结算呈现。</summary>
            HoldPresentation,
        }

        private GlideLandingSettlementCamPhase _glideLandingSettlementCamPhase = GlideLandingSettlementCamPhase.Idle;
        /// <summary>滑翔着地结算 UI 期间：禁用 settlementOffset 跟拍，仅用延时/绕轨/保持。</summary>
        private bool _glideLandingSettlementPresentationActive;
        private Vector3 _presentationHoldWorldOffsetFromPivot;
        private float _settlementYawOrbitDelayRemaining;
        private float _settlementYawOrbitDuration = 1f;
        private float _settlementYawOrbitElapsed;
        private bool _settlementYawOrbitHasInitialOffset;

        [Header("FOV")]
        [SerializeField] private float fovSharpness = 10f;

        [Header("Debug (mouse only)")]
        [Tooltip("PC 调试：Mouse X/Y 已是每帧位移，不再乘 deltaTime；与 yawSpeed 相乘得每帧角度。")]
        [SerializeField] private float mouseLookScale = 0.12f;

        private Camera _cam;
        private float _yaw;
        private float _pitch = 15f;
        private float _currentDistance;
        private Vector2 _smoothedLookRate;
        private bool _orbitLookActive;

        private float _glideShakeFallFrequencyHz;
        private Vector3 _glideShakeFallPositionAmplitude;
        private Vector3 _glideShakeFallEulerAmplitude;
        private float _glideShakeGlideFrequencyHz;
        private Vector3 _glideShakeGlidePositionAmplitude;
        private Vector3 _glideShakeGlideEulerAmplitude;
        private bool _glideShakeUseFallPhase = true;
        /// <summary>滑翔且使用自由轨道时由 <see cref="Game.Glide.GlideController"/> 打开：在 FreeMove 轨道更新后叠加气动抖动。</summary>
        private bool _glideShakeOverlayActive;

        private bool _climbWindShakeActive;
        private float _climbShakeFrequencyHz;
        private Vector3 _climbShakePositionAmplitude;
        private Vector3 _climbShakeEulerAmplitude;

        private bool _backflipBehindCamBlendActive;
        private float _backflipBehindCamDuration;
        private float _backflipBehindCamElapsed;
        private float _backflipYawStart;
        private float _backflipYawEnd;
        private float _backflipPitchStart;
        private float _backflipPitchEnd;

        bool _freeOffsetBlendActive;
        Vector3 _freeOffsetBlendFrom;
        Vector3 _freeOffsetBlendTo;
        float _freeOffsetBlendElapsed;
        float _freeOffsetBlendDuration;

        [Header("Landing impact (impulse)")]
        [SerializeField] private float landingImpactShakeFrequencyHz = 38f;

        private float _landingImpactDuration;
        private float _landingImpactElapsed;
        private float _landingImpactPosPeak;
        private float _landingImpactEulerPeak;
        private float _landingImpactFrequencyHz;

        /// <summary>为 true 时在下一次 target 就绪时应用开局/重生机位（含 Balance 初始观察点）。攀爬、后空翻等流程不会置位。</summary>
        private bool _needsSpawnCameraLayout = true;

        public PlayerMode CurrentMode => mode;

        public Vector3 CurrentFreeOffset => freeOffset;

        /// <summary>平滑插值自由轨道 local offset（仅 FreeMove 机位生效）。</summary>
        public void BlendFreeOffsetTo(Vector3 targetOffset, float durationSeconds)
        {
            _freeOffsetBlendFrom = freeOffset;
            _freeOffsetBlendTo = targetOffset;
            _freeOffsetBlendElapsed = 0f;
            _freeOffsetBlendDuration = Mathf.Max(0.01f, durationSeconds);
            _freeOffsetBlendActive = true;
        }

        void TickFreeOffsetBlend(float dt)
        {
            if (!_freeOffsetBlendActive)
                return;

            _freeOffsetBlendElapsed += dt;
            var u = Mathf.Clamp01(_freeOffsetBlendElapsed / _freeOffsetBlendDuration);
            u = u * u * (3f - 2f * u);
            freeOffset = Vector3.Lerp(_freeOffsetBlendFrom, _freeOffsetBlendTo, u);
            _currentDistance = Mathf.Max(minDistance, freeOffset.magnitude);
            if (u >= 1f - 1e-5f)
            {
                freeOffset = _freeOffsetBlendTo;
                _freeOffsetBlendActive = false;
            }
        }

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                if (Application.isPlaying && target != null)
                    TryApplySpawnCameraLayout();
            }
        }

        /// <summary>
        /// 重置「仅开局/重生」的相机布局需求；由 <see cref="Game.Player.RespawnService"/> 在回出生点后调用，
        /// 使 <see cref="GameBalanceConfig.freeCamInitialViewerOffsetLocalFromCharacter"/> 再次按当前角色朝向应用。
        /// </summary>
        public void MarkSpawnCameraLayoutNeeded()
        {
            _needsSpawnCameraLayout = true;
            TryApplySpawnCameraLayout();
        }

        /// <summary>开局或重生后、第一次有 follow target 时再应用一次；避免攀爬换手/后空翻转身时重复套用初始观察向量。</summary>
        void TryApplySpawnCameraLayout()
        {
            if (!_needsSpawnCameraLayout || target == null)
                return;

            var bal = ConfigRegistry.Balance;
            if (bal != null && bal.freeCamInitialViewerOffsetLocalFromCharacter.sqrMagnitude > 1e-6f)
                ApplyInitialViewerOffsetFromCharacterLocal(bal.freeCamInitialViewerOffsetLocalFromCharacter);
            else
                SyncInitialOrbitYawBehindTarget();

            _needsSpawnCameraLayout = false;
        }

        public void SetMode(PlayerMode newMode)
        {
            mode = newMode;
        }

        /// <summary>滑翔时由滑翔控制器每帧设置：true=坠落段抖动配置（SO），false=滑翔段。</summary>
        public void SetGlideCameraFallShake(bool useFallPhaseShake)
        {
            _glideShakeUseFallPhase = useFallPhaseShake;
        }

        public void SetGlideShakeOverlayActive(bool active)
        {
            _glideShakeOverlayActive = active;
        }

        /// <summary>攀爬 upward 运动时开启，模拟高处风。</summary>
        public void SetClimbWindShakeActive(bool active)
        {
            _climbWindShakeActive = active;
        }

        /// <summary>与 Inspector「滑翔仍用自由视角轨道」一致；供滑翔流程在锁定移动时决定是否仍读取视角轴。</summary>
        public bool UsesGlideOrbitalFreeLook => glideUseOrbitalFreeLook;

        /// <summary>落地/通用 impulse：在当帧机位解算后叠加衰减的位置/旋转抖动（与气动抖动独立）。</summary>
        public void TriggerLandingImpactShake(float positionMetersPeak, float eulerDegreesPeak, float durationSeconds)
        {
            TriggerLandingImpactShake(
                positionMetersPeak,
                eulerDegreesPeak,
                durationSeconds,
                landingImpactShakeFrequencyHz);
        }

        /// <param name="frequencyHz">≤0 时使用 Inspector 默认 landingImpactShakeFrequencyHz。</param>
        public void TriggerLandingImpactShake(
            float positionMetersPeak,
            float eulerDegreesPeak,
            float durationSeconds,
            float frequencyHz)
        {
            if (durationSeconds <= 0.001f)
                return;

            _landingImpactDuration = durationSeconds;
            _landingImpactElapsed = 0f;
            _landingImpactPosPeak = Mathf.Max(0f, positionMetersPeak);
            _landingImpactEulerPeak = Mathf.Max(0f, eulerDegreesPeak);
            _landingImpactFrequencyHz = frequencyHz > 0.001f
                ? frequencyHz
                : landingImpactShakeFrequencyHz;
        }

        /// <summary>后空翻：在时长内将自由视角轨道 Yaw 对齐到指定水平朝向（通常与空翻位移水平方向一致）；不传则用 CharacterRoot / target 的水平 forward。</summary>
        public void BeginBackflipCameraBehindPlayer(float durationSeconds, float? orbitYawDegrees = null)
        {
            if (target == null || durationSeconds <= 1e-4f)
            {
                _backflipBehindCamBlendActive = false;
                return;
            }

            _backflipBehindCamBlendActive = true;
            _backflipBehindCamDuration = durationSeconds;
            _backflipBehindCamElapsed = 0f;
            _backflipYawStart = _yaw;
            _backflipPitchStart = _pitch;
            _backflipYawEnd = orbitYawDegrees ?? SampleHorizontalOrbitYawFromTargetForward();
            var s = GetOrbitSettings(PlayerMode.FreeMove);
            _backflipPitchEnd = Mathf.Clamp(backflipCamBlendPitchDegrees, s.pitchMin, s.pitchMax);
        }

        /// <summary>空翻结束帧将轨道对齐到目标值，避免混合未跑满时机位残留。</summary>
        public void EndBackflipCameraBehindPlayerSnap()
        {
            _backflipBehindCamBlendActive = false;
            _yaw = _backflipYawEnd;
            var s = GetOrbitSettings(PlayerMode.FreeMove);
            _pitch = Mathf.Clamp(backflipCamBlendPitchDegrees, s.pitchMin, s.pitchMax);
        }

        /// <summary>与 target（优先 CharacterRoot）水平 forward 对齐的轨道 Yaw；freeOffset.z 为负时视点在角色身后一侧。</summary>
        float SampleHorizontalOrbitYawFromTargetForward()
        {
            if (target == null) return _yaw;
            var src = CharacterHierarchySockets.FindDeepChildByName(target, "CharacterRoot") ?? target;
            var f = src.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-8f) return target.eulerAngles.y;
            f.Normalize();
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 滑翔着地进入结算 UI：呈现阶段从<strong>角色身后标准偏移</strong>插值到<strong>正面标准偏移</strong>（时长见参数）；绕轨结束后保持正面直至 <see cref="CancelSettlementYawOrbit"/> / 回出生地。
        /// </summary>
        /// <param name="deltaYawDegrees">保留与 <see cref="GlideController"/> 调用签名兼容；当前弧线为背后→正面插值，不再叠加该绕轴角。</param>
        public void BeginGlideLandingSettlementCamera(float deltaYawDegrees, float durationSeconds, float delaySeconds = 0f)
        {
            _ = deltaYawDegrees;
            CancelSettlementYawOrbit();
            if (target == null)
                return;

            _presentationHoldWorldOffsetFromPivot = ComputeGlideLandingSettlementBackOffsetWorld();
            _glideLandingSettlementPresentationActive = true;
            _settlementYawOrbitHasInitialOffset = false;

            var wantOrbit = durationSeconds > 1e-4f;
            if (!wantOrbit)
            {
                _presentationHoldWorldOffsetFromPivot = ComputeGlideLandingSettlementFrontOffsetWorld();
                _glideLandingSettlementCamPhase = GlideLandingSettlementCamPhase.HoldPresentation;
                return;
            }

            _settlementYawOrbitDuration = Mathf.Max(1e-4f, durationSeconds);
            _settlementYawOrbitElapsed = 0f;

            if (delaySeconds > 1e-4f)
            {
                _settlementYawOrbitDelayRemaining = delaySeconds;
                _glideLandingSettlementCamPhase = GlideLandingSettlementCamPhase.WaitingDelay;
            }
            else
                _glideLandingSettlementCamPhase = GlideLandingSettlementCamPhase.Running;
        }

        /// <inheritdoc cref="BeginGlideLandingSettlementCamera"/>
        public void BeginSettlementYawOrbitAroundPivot(float deltaYawDegrees, float durationSeconds, float delaySeconds = 0f) =>
            BeginGlideLandingSettlementCamera(deltaYawDegrees, durationSeconds, delaySeconds);

        public void CancelSettlementYawOrbit()
        {
            _glideLandingSettlementCamPhase = GlideLandingSettlementCamPhase.Idle;
            _glideLandingSettlementPresentationActive = false;
            _settlementYawOrbitDelayRemaining = 0f;
            _settlementYawOrbitElapsed = 0f;
            _settlementYawOrbitHasInitialOffset = false;
        }

        Vector3 ComputeGlideLandingSettlementFrontOffsetWorld()
        {
            if (target == null)
                return glideLandingSettlementFrontOffsetLocal;

            var yawRot = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            return yawRot * glideLandingSettlementFrontOffsetLocal;
        }

        Vector3 ComputeGlideLandingSettlementBackOffsetWorld()
        {
            if (target == null)
                return glideLandingSettlementBackOffsetLocal;

            var yawRot = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            return yawRot * glideLandingSettlementBackOffsetLocal;
        }

        public void EnterClimbCamera(bool snapToClimbOffset)
        {
            mode = PlayerMode.Climb;
            if (!snapToClimbOffset || target == null) return;

            var s = GetOrbitSettings(PlayerMode.Climb);
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            var pivot = target.position + Vector3.up * s.pivotHeight;
            var desiredPos = pivot + rot * s.offset;
            if (collisionAvoidance)
                desiredPos = ApplyCollisionAvoidance(pivot, desiredPos);

            transform.position = desiredPos;
            transform.rotation = OrbitLookRotationTowardPivot(transform.position, pivot);
            _currentDistance = Mathf.Max(minDistance, s.offset.magnitude);
        }

        public void ExitClimbCamera()
        {
            SetClimbWindShakeActive(false);
            if (mode == PlayerMode.Climb)
                mode = PlayerMode.FreeMove;
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (input == null) input = UnityObjectFinder.FindFirst<LegacyPlayerInput>();
            _currentDistance = Mathf.Max(minDistance, freeOffset.magnitude);

            ApplyBalanceIfPresent();
            _needsSpawnCameraLayout = true;
            TryApplySpawnCameraLayout();
        }

        /// <summary>
        /// 自由视角轨道默认 _yaw=0 为世界坐标；若角色初始朝向非 0，相机会在「侧面/正面」。与 <see cref="SampleHorizontalOrbitYawFromTargetForward"/> 对齐后，
        /// <c>freeOffset.z &lt; 0</c> 表示在角色<strong>背后</strong>（与后空翻镜头对齐方式一致）。
        /// </summary>
        void SyncInitialOrbitYawBehindTarget()
        {
            if (target == null)
                return;
            _yaw = SampleHorizontalOrbitYawFromTargetForward();
        }

        /// <summary>
        /// 仅在开局或重生时调用：将相机放在 pivot + characterRotation * local，并反推自由轨道 yaw/pitch/<see cref="freeOffset"/>。
        /// 依赖当时角色朝向；勿在攀爬/空翻等中途再次调用（见 <see cref="TryApplySpawnCameraLayout"/>）。
        /// </summary>
        void ApplyInitialViewerOffsetFromCharacterLocal(Vector3 localOffsetFromCharacter)
        {
            var pivot = target.position + Vector3.up * pivotHeight;
            var worldOffset = target.rotation * localOffsetFromCharacter;
            var flat = new Vector3(worldOffset.x, 0f, worldOffset.z);
            if (flat.sqrMagnitude < 1e-8f)
            {
                SyncInitialOrbitYawBehindTarget();
                var rotFlat = Quaternion.Euler(_pitch, _yaw, 0f);
                freeOffset = Quaternion.Inverse(rotFlat) * worldOffset;
                _currentDistance = Mathf.Max(minDistance, freeOffset.magnitude);
                var posFlat = pivot + rotFlat * freeOffset;
                transform.position = posFlat;
                transform.rotation = OrbitLookRotationTowardPivot(posFlat, pivot);
                return;
            }

            _yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            var horizDist = flat.magnitude;
            _pitch = Mathf.Atan2(worldOffset.y, horizDist) * Mathf.Rad2Deg;
            _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            freeOffset = Quaternion.Inverse(rot) * worldOffset;
            _currentDistance = Mathf.Max(minDistance, freeOffset.magnitude);
            var pos = pivot + rot * freeOffset;
            transform.position = pos;
            transform.rotation = OrbitLookRotationTowardPivot(pos, pivot);
        }

        private void ApplyBalanceIfPresent()
        {
            var bal = ConfigRegistry.Balance;
            if (bal == null) return;

            freeOffset = bal.freeCamOffset;
            yawSpeed = bal.camYawSpeed;
            pitchSpeed = bal.camPitchSpeed;
            pitchMin = bal.camPitchMin;
            pitchMax = bal.camPitchMax;
            followSharpness = bal.camFollowSharpness;
            pivotHeight = bal.camPivotHeight;

            climbOffset = bal.climbCamOffset;
            climbFov = bal.climbCamFov;
            climbYawSpeed = bal.climbCamYawSpeed;
            climbPitchSpeed = bal.climbCamPitchSpeed;
            climbPitchMin = bal.climbCamPitchMin;
            climbPitchMax = bal.climbCamPitchMax;
            climbFollowSharpness = bal.climbCamFollowSharpness;
            climbPivotHeight = bal.climbCamPivotHeight;
            glideOffset = bal.glideCamOffset;
            glideFov = bal.glideCamFov;
            glidePivotHeight = bal.glideCamPivotHeight;

            _glideShakeFallFrequencyHz = bal.glideFallShakeFrequencyHz;
            _glideShakeFallPositionAmplitude = bal.glideFallShakePositionAmplitude;
            _glideShakeFallEulerAmplitude = bal.glideFallShakeEulerAmplitude;
            _glideShakeGlideFrequencyHz = bal.glideGlideShakeFrequencyHz;
            _glideShakeGlidePositionAmplitude = bal.glideGlideShakePositionAmplitude;
            _glideShakeGlideEulerAmplitude = bal.glideGlideShakeEulerAmplitude;

            settlementOffset = bal.settlementCamOffset;
            settlementFov = bal.settlementCamFov;
            settlementPivotHeight = bal.settlementCamPivotHeight;

            _climbShakeFrequencyHz = bal.climbWindShakeFrequencyHz;
            _climbShakePositionAmplitude = bal.climbWindShakePositionAmplitude;
            _climbShakeEulerAmplitude = bal.climbWindShakeEulerAmplitude;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (_glideLandingSettlementPresentationActive && mode != PlayerMode.Settlement)
                CancelSettlementYawOrbit();

            if (mode == PlayerMode.FreeMove)
            {
                TickFreeOffsetBlend(Time.deltaTime);
                UpdateOrbit(PlayerMode.FreeMove);
                if (_glideShakeOverlayActive)
                    ApplyGlideAeroShakeIfConfigured();
            }
            else if (mode == PlayerMode.Climb)
            {
                UpdateOrbit(PlayerMode.Climb);
                if (_climbWindShakeActive)
                    ApplyClimbWindShakeIfConfigured();
            }
            else if (mode == PlayerMode.Glide && glideUseOrbitalFreeLook)
            {
                // 与 FreeMove 相同机位与速率（保留进入滑翔前的自由视角观感）；兼容未走 GlideController 时仍设为 Glide 的情况。
                UpdateOrbit(PlayerMode.FreeMove);
                ApplyGlideAeroShakeIfConfigured();
            }
            else
            {
                var offset = mode == PlayerMode.Glide ? glideOffset : settlementOffset;
                var pivotHeight = mode == PlayerMode.Glide ? glidePivotHeight : settlementPivotHeight;
                var pivot = target.position + Vector3.up * pivotHeight;
                var desiredFov = mode == PlayerMode.Glide ? glideFov : settlementFov;

                var glideLandingSettlementHandled =
                    mode == PlayerMode.Settlement &&
                    _glideLandingSettlementPresentationActive &&
                    TickGlideLandingSettlementPresentationCamera(pivot, desiredFov);

                if (!glideLandingSettlementHandled)
                {
                    var desiredPos = pivot + target.rotation * offset;
                    transform.position = Vector3.Lerp(
                        transform.position,
                        desiredPos,
                        1f - Mathf.Exp(-forcedSharpness * Time.deltaTime)
                    );

                    var lookAt = pivot;
                    var desiredRot = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        desiredRot,
                        1f - Mathf.Exp(-forcedSharpness * Time.deltaTime)
                    );

                    _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, desiredFov, 1f - Mathf.Exp(-forcedSharpness * Time.deltaTime));

                    if (mode == PlayerMode.Glide && !glideUseOrbitalFreeLook)
                        ApplyGlideAeroShakeIfConfigured();
                }
                else if (mode == PlayerMode.Glide && !glideUseOrbitalFreeLook)
                    ApplyGlideAeroShakeIfConfigured();
            }

            ApplyLandingImpactShakeIfActive();
        }

        /// <returns>true 若本帧已由滑翔着陆结算呈现逻辑接管（不使用 settlementOffset 强制背后）。</returns>
        private bool TickGlideLandingSettlementPresentationCamera(Vector3 pivot, float desiredFov)
        {
            switch (_glideLandingSettlementCamPhase)
            {
                case GlideLandingSettlementCamPhase.WaitingDelay:
                    ApplyPresentationHoldRelativePivot(pivot, desiredFov);
                    _settlementYawOrbitDelayRemaining -= Time.deltaTime;
                    if (_settlementYawOrbitDelayRemaining <= 0f)
                    {
                        _settlementYawOrbitHasInitialOffset = true;
                        _glideLandingSettlementCamPhase = GlideLandingSettlementCamPhase.Running;
                        _settlementYawOrbitElapsed = 0f;
                    }

                    return true;

                case GlideLandingSettlementCamPhase.Running:
                    if (!_settlementYawOrbitHasInitialOffset)
                        _settlementYawOrbitHasInitialOffset = true;

                    _settlementYawOrbitElapsed += Time.deltaTime;
                    var uRun = Mathf.Clamp01(_settlementYawOrbitElapsed / _settlementYawOrbitDuration);
                    uRun = uRun * uRun * (3f - 2f * uRun);

                    var backW = ComputeGlideLandingSettlementBackOffsetWorld();
                    var frontW = ComputeGlideLandingSettlementFrontOffsetWorld();
                    var offsetWorld = Vector3.LerpUnclamped(backW, frontW, uRun);

                    var posRun = pivot + offsetWorld;
                    ApplyGlideLandingSettlementCameraWorldXYLock(ref posRun);
                    transform.position = posRun;

                    var lookAtRun = pivot;
                    var desiredRotRun = Quaternion.LookRotation(lookAtRun - transform.position, Vector3.up);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        desiredRotRun,
                        1f - Mathf.Exp(-forcedSharpness * Time.deltaTime));

                    _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, desiredFov, 1f - Mathf.Exp(-forcedSharpness * Time.deltaTime));

                    if (uRun >= 1f - 1e-5f)
                    {
                        _presentationHoldWorldOffsetFromPivot = ComputeGlideLandingSettlementFrontOffsetWorld();
                        _glideLandingSettlementCamPhase = GlideLandingSettlementCamPhase.HoldPresentation;
                    }

                    return true;

                case GlideLandingSettlementCamPhase.HoldPresentation:
                    ApplyPresentationHoldRelativePivot(pivot, desiredFov);
                    return true;

                default:
                    return false;
            }
        }

        private void ApplyPresentationHoldRelativePivot(Vector3 pivot, float desiredFov)
        {
            var pos = pivot + _presentationHoldWorldOffsetFromPivot;
            ApplyGlideLandingSettlementCameraWorldXYLock(ref pos);
            transform.position = pos;
            var desiredRot = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRot,
                1f - Mathf.Exp(-forcedSharpness * Time.deltaTime));
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, desiredFov, 1f - Mathf.Exp(-forcedSharpness * Time.deltaTime));

            // 刷新偏移：pivot  anim 或 XY 锁定后仍保持正确的相对 Z。
            _presentationHoldWorldOffsetFromPivot = transform.position - pivot;
        }

        private void ApplyGlideLandingSettlementCameraWorldXYLock(ref Vector3 worldPosition)
        {
            if (!glideLandingSettlementCameraLockWorldXY)
                return;
            worldPosition.x = glideLandingSettlementCameraWorldXY.x;
            worldPosition.y = glideLandingSettlementCameraWorldXY.y;
        }

        private void ApplyLandingImpactShakeIfActive()
        {
            if (_landingImpactDuration <= 0f || _landingImpactElapsed >= _landingImpactDuration)
                return;

            var dt = Time.deltaTime;
            _landingImpactElapsed += dt;
            var u = Mathf.Clamp01(_landingImpactElapsed / Mathf.Max(0.001f, _landingImpactDuration));
            var env = (1f - u) * (1f - u);
            var bump01 = Mathf.Clamp01(_landingImpactElapsed / 0.1f);
            var bump = Mathf.Sin(bump01 * Mathf.PI);

            var w = Mathf.Max(0.01f, _landingImpactFrequencyHz) * (Mathf.PI * 2f);
            var t = Time.time;
            var nX = Mathf.PerlinNoise(0.2f, t * w * 0.08f) - 0.5f;
            var nY = Mathf.PerlinNoise(1.7f, t * w * 0.08f) - 0.5f;
            var nZ = Mathf.PerlinNoise(2.9f, t * w * 0.08f) - 0.5f;
            var posShake = new Vector3(nX, nY * 0.65f, nZ) * 2f * _landingImpactPosPeak * env;
            var jolt = Vector3.down * (_landingImpactPosPeak * 0.35f * bump);
            transform.position += posShake + jolt;

            var eX = (Mathf.PerlinNoise(10f, t * w * 0.07f) - 0.5f) * 2f;
            var eY = (Mathf.PerlinNoise(11f, t * w * 0.07f) - 0.5f) * 2f;
            var eZ = (Mathf.PerlinNoise(12f, t * w * 0.07f) - 0.5f) * 2f;
            var euler = new Vector3(eX, eY * 0.55f, eZ) * _landingImpactEulerPeak * env;
            euler += new Vector3(bump * _landingImpactEulerPeak * 0.35f, bump * _landingImpactEulerPeak * 0.12f, 0f);
            transform.rotation *= Quaternion.Euler(euler);

            if (_landingImpactElapsed >= _landingImpactDuration)
                _landingImpactDuration = 0f;
        }

        private void ApplyClimbWindShakeIfConfigured()
        {
            var freq = _climbShakeFrequencyHz;
            var posAmp = _climbShakePositionAmplitude;
            var eulerAmp = _climbShakeEulerAmplitude;
            if (freq <= 0.0001f ||
                (posAmp.sqrMagnitude <= 1e-10f && eulerAmp.sqrMagnitude <= 1e-10f))
                return;

            var t = Time.time;
            var w = freq * (Mathf.PI * 2f);

            transform.position += new Vector3(
                Mathf.Sin(t * w * 1.07f + 1.35f) * posAmp.x,
                Mathf.Sin(t * w * 1.03f + 2.82f) * posAmp.y,
                Mathf.Sin(t * w * 1.09f + 0.71f) * posAmp.z);

            transform.rotation *= Quaternion.Euler(
                Mathf.Sin(t * w * 0.97f + 0.91f) * eulerAmp.x,
                Mathf.Sin(t * w * 1.04f + 0.22f) * eulerAmp.y,
                Mathf.Sin(t * w * 1.01f + 1.73f) * eulerAmp.z);
        }

        private void ApplyGlideAeroShakeIfConfigured()
        {
            var freq = _glideShakeUseFallPhase ? _glideShakeFallFrequencyHz : _glideShakeGlideFrequencyHz;
            var posAmp = _glideShakeUseFallPhase ? _glideShakeFallPositionAmplitude : _glideShakeGlidePositionAmplitude;
            var eulerAmp = _glideShakeUseFallPhase ? _glideShakeFallEulerAmplitude : _glideShakeGlideEulerAmplitude;
            if (freq <= 0.0001f ||
                (posAmp.sqrMagnitude <= 1e-10f && eulerAmp.sqrMagnitude <= 1e-10f))
                return;

            var t = Time.time;
            var w = freq * (Mathf.PI * 2f);
            transform.position += new Vector3(
                Mathf.Sin(t * w) * posAmp.x,
                Mathf.Sin(t * w * 1.17f + 2.1f) * posAmp.y,
                Mathf.Sin(t * w * 0.93f + 0.4f) * posAmp.z);
            transform.rotation *= Quaternion.Euler(
                Mathf.Sin(t * w * 1.05f) * eulerAmp.x,
                Mathf.Sin(t * w * 0.88f + 1.2f) * eulerAmp.y,
                Mathf.Sin(t * w * 1.11f + 0.7f) * eulerAmp.z);
        }

        private OrbitSettings GetOrbitSettings(PlayerMode forMode)
        {
            if (forMode == PlayerMode.Climb)
            {
                return new OrbitSettings
                {
                    offset = climbOffset,
                    yawSpeed = climbYawSpeed,
                    pitchSpeed = climbPitchSpeed,
                    pitchMin = climbPitchMin,
                    pitchMax = climbPitchMax,
                    followSharpness = climbFollowSharpness,
                    pivotHeight = climbPivotHeight,
                    fov = climbFov,
                };
            }

            return new OrbitSettings
            {
                offset = freeOffset,
                yawSpeed = yawSpeed,
                pitchSpeed = pitchSpeed,
                pitchMin = pitchMin,
                pitchMax = pitchMax,
                followSharpness = followSharpness,
                pivotHeight = pivotHeight,
                fov = -1f,
            };
        }

        private void UpdateOrbit(PlayerMode forMode)
        {
            var s = GetOrbitSettings(forMode);
            var dt = Time.deltaTime;
            var isVirtualLook = input != null && input.IsVirtualLookActive;
            var rawLook = input != null ? input.Look : Vector2.zero;

            if (_backflipBehindCamBlendActive)
            {
                _backflipBehindCamElapsed += dt;
                var u = Mathf.Clamp01(_backflipBehindCamElapsed / Mathf.Max(1e-4f, _backflipBehindCamDuration));
                u = u * u * (3f - 2f * u);
                _yaw = Mathf.LerpAngle(_backflipYawStart, _backflipYawEnd, u);
                _pitch = Mathf.Lerp(_backflipPitchStart, _backflipPitchEnd, u);
                if (u >= 1f - 1e-5f)
                    _backflipBehindCamBlendActive = false;
            }
            else if (isVirtualLook)
            {
                var inputSmoothT = 1f - Mathf.Exp(-lookInputSmoothSharpness * dt);
                _smoothedLookRate = Vector2.Lerp(_smoothedLookRate, rawLook, inputSmoothT);
                _yaw += _smoothedLookRate.x * s.yawSpeed * dt;
                _pitch -= _smoothedLookRate.y * s.pitchSpeed * dt;
            }
            else
            {
                var decayT = 1f - Mathf.Exp(-lookInputSmoothSharpness * dt);
                _smoothedLookRate = Vector2.Lerp(_smoothedLookRate, Vector2.zero, decayT);
                // 鼠标调试：Input.GetAxis(Mouse X/Y) 为本帧位移，勿再乘 deltaTime。
                _yaw += rawLook.x * s.yawSpeed * mouseLookScale;
                _pitch -= rawLook.y * s.pitchSpeed * mouseLookScale;
            }

            _pitch = Mathf.Clamp(_pitch, s.pitchMin, s.pitchMax);

            _orbitLookActive = _smoothedLookRate.sqrMagnitude > 1e-8f || _backflipBehindCamBlendActive;

            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            var pivot = target.position + Vector3.up * s.pivotHeight;
            var desiredPos = pivot + rot * s.offset;

            if (collisionAvoidance)
                desiredPos = ApplyCollisionAvoidance(pivot, desiredPos);

            var desiredRot = OrbitLookRotationTowardPivot(desiredPos, pivot);

            var followSharp = _orbitLookActive ? lookOrbitSharpness : s.followSharpness;
            var followT = 1f - Mathf.Exp(-followSharp * dt);
            transform.position = Vector3.Lerp(transform.position, desiredPos, followT);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, followT);

            if (s.fov > 0f)
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, s.fov, 1f - Mathf.Exp(-fovSharpness * dt));
        }

        /// <summary>
        /// 轨道机位：<see cref="_yaw"/>/<see cref="_pitch"/> + offset 决定相机<strong>相对 pivot 的空间位置</strong>；朝向一律对准 pivot，
        /// 这样改 <see cref="freeOffset"/>/<see cref="climbOffset"/> 的符号（如前/后）时镜头仍会看向角色，而不会保持世界朝向导致出画。
        /// </summary>
        static Quaternion OrbitLookRotationTowardPivot(Vector3 cameraWorld, Vector3 pivotWorld)
        {
            var dir = pivotWorld - cameraWorld;
            if (dir.sqrMagnitude < 1e-10f)
                return Quaternion.identity;
            return Quaternion.LookRotation(dir, Vector3.up);
        }

        private Vector3 ApplyCollisionAvoidance(Vector3 pivot, Vector3 desiredPos)
        {
            var desiredDelta = desiredPos - pivot;
            var desiredDist = desiredDelta.magnitude;
            if (desiredDist <= 1e-4f)
                return desiredPos;

            var dir = desiredDelta / desiredDist;

            var hitDist = desiredDist;
            if (TryGetBlockingHitDistance(pivot, dir, desiredDist, out var bestHitDist))
                hitDist = Mathf.Max(minDistance, bestHitDist - nearPadding);

            // Smooth distance: 转视角时双向平滑，避免 SphereCast 帧间跳变造成颗粒感；非转视角时仍即时缩短防穿墙。
            if (_orbitLookActive)
            {
                var collisionT = 1f - Mathf.Exp(-lookOrbitSharpness * Time.deltaTime);
                _currentDistance = Mathf.Lerp(_currentDistance, hitDist, collisionT);
            }
            else if (hitDist < _currentDistance)
                _currentDistance = hitDist;
            else
                _currentDistance = Mathf.Lerp(_currentDistance, hitDist, 1f - Mathf.Exp(-returnSharpness * Time.deltaTime));

            _currentDistance = Mathf.Clamp(_currentDistance, minDistance, desiredDist);
            return pivot + dir * _currentDistance;
        }

        private bool TryGetBlockingHitDistance(Vector3 origin, Vector3 dir, float maxDist, out float bestHitDist)
        {
            bestHitDist = maxDist;
            var hits = Physics.SphereCastAll(origin, probeRadius, dir, maxDist, collisionMask, QueryTriggerInteraction.Ignore);
            var found = false;

            for (var i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                if (h.collider.transform == target) continue;
                if (!IsColliderSignificant(h.collider)) continue;

                if (h.distance < bestHitDist)
                {
                    bestHitDist = h.distance;
                    found = true;
                }
            }

            return found;
        }

        private bool IsColliderSignificant(Collider col)
        {
            // Ignore very small colliders to avoid jittery camera.
            var b = col.bounds;
            var e = b.extents;
            var minE = Mathf.Min(e.x, Mathf.Min(e.y, e.z));
            return minE >= ignoreColliderMinExtent;
        }
    }
}


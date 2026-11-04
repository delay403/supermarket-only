using UnityEngine;

namespace Game.Player
{
    /// <summary>先于 <see cref="Tower.ClimbController"/> 采集输入，保证攀爬帧内 <see cref="ClimbUpAxis"/> 已更新。</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LegacyPlayerInput : MonoBehaviour
    {
        public enum InputBlendMode
        {
            HardwareOnly = 0,
            VirtualOnly = 1,
            Combine = 2,
            VirtualOverrideWhenActive = 3,
        }

        [Header("Axes")]
        public string moveX = "Horizontal";
        public string moveY = "Vertical";
        public string lookX = "Mouse X";
        public string lookY = "Mouse Y";

        [Header("Keys")]
        public KeyCode climbKey = KeyCode.E;
        public KeyCode glideKey = KeyCode.Space;

        [Header("Virtual Input")]
        [SerializeField] private InputBlendMode moveBlendMode = InputBlendMode.VirtualOverrideWhenActive;
        [SerializeField] private InputBlendMode lookBlendMode = InputBlendMode.VirtualOverrideWhenActive;
        [SerializeField] private float lookHardwareScale = 1f;
        [SerializeField] private float lookVirtualScale = 1f;
        [Tooltip("右摇杆 touch-delta 速率上限（归一化/秒）；过小会截断快速划屏。")]
        [SerializeField] private float virtualLookMaxRate = 3f;
        [SerializeField] private bool disableHardwareLookOnTouchPlatforms = true;

        public Vector2 Move { get; private set; }
        /// <summary>仅在攀爬中由摇杆/虚拟摇杆的「前」分量提供；此时 <see cref="Move"/> 恒为 zero，避免地面移动逻辑误用水平分量旋转或位移。</summary>
        public float ClimbUpAxis { get; private set; }
        public Vector2 Look { get; private set; }
        public bool ClimbHeld { get; private set; }
        public bool GlidePressed { get; private set; }

        /// <summary>本帧是否由 FGUI 跳跃按钮触发（不含键盘 <see cref="glideKey"/>）。供地面起跳与键盘分离时使用。</summary>
        public bool UiActionPressedFromUi { get; private set; }

        private Vector2 _virtualMove;
        private Vector2 _virtualLook;
        private bool _virtualMoveActive;
        private bool _virtualLookActive;
        private bool _uiGlidePressQueued;
        private bool _locked;
        private bool _allowLookWhileLocked;
        private bool _allowMoveWhileLocked;
        private bool _climbContextActive;

        public bool IsLocked => _locked;

        /// <summary>本帧视角由虚拟右摇杆驱动（touch-delta 速率）；相机侧用于区分鼠标 delta 与摇杆速率。</summary>
        public bool IsVirtualLookActive => _virtualLookActive;

        /// <summary>由 <see cref="Tower.ClimbController"/> 在进入/离开攀爬时切换：攀爬中通用 <see cref="Move"/> 为零，仅 <see cref="ClimbUpAxis"/> 表示向上爬。</summary>
        public void SetClimbContextActive(bool active)
        {
            _climbContextActive = active;
            if (!active)
                ClimbUpAxis = 0f;
        }

        /// <param name="allowLookWhileLocked">为真时仍读取视角输入（用于滑翔/坠落自由相机）；移动与攀爬键仍被屏蔽。</param>
        /// <param name="allowMoveWhileLocked">为真时仍读取移动轴（用于导弹下落阶段落点操控）。</param>
        public void SetLocked(bool locked, bool allowLookWhileLocked = false, bool allowMoveWhileLocked = false)
        {
            _locked = locked;
            if (!locked)
            {
                _allowLookWhileLocked = false;
                _allowMoveWhileLocked = false;
            }
            else
            {
                _allowLookWhileLocked = allowLookWhileLocked;
                _allowMoveWhileLocked = allowMoveWhileLocked;
            }

            if (_locked)
            {
                if (!_allowMoveWhileLocked)
                {
                    _virtualMove = Vector2.zero;
                    _virtualMoveActive = false;
                    Move = Vector2.zero;
                }
                ClimbHeld = false;
                GlidePressed = false;
                if (!allowLookWhileLocked)
                {
                    _virtualLook = Vector2.zero;
                    _virtualLookActive = false;
                    Look = Vector2.zero;
                }
            }
        }

        public void SetVirtualMove(Vector2 value, bool active)
        {
            _virtualMove = Vector2.ClampMagnitude(value, 1f);
            _virtualMoveActive = active;
        }

        public void SetVirtualLook(Vector2 value, bool active)
        {
            var maxRate = Mathf.Max(0.01f, virtualLookMaxRate);
            _virtualLook = Vector2.ClampMagnitude(value, maxRate);
            _virtualLookActive = active;
        }

        /// <summary>由 FGUI 跳跃按钮调用，与键盘 <see cref="glideKey"/> 按下等效（本帧 <see cref="GlidePressed"/>）。</summary>
        public void QueueGlidePressFromUi()
        {
            _uiGlidePressQueued = true;
        }

        private void Update()
        {
            UiActionPressedFromUi = false;

            var hardwareMove = new Vector2(Input.GetAxisRaw(moveX), Input.GetAxisRaw(moveY));
            hardwareMove = Vector2.ClampMagnitude(hardwareMove, 1f);

            if (_locked)
            {
                _uiGlidePressQueued = false;
                ClimbUpAxis = 0f;
                ClimbHeld = false;
                GlidePressed = false;

                if (_allowMoveWhileLocked)
                    Move = Blend(hardwareMove, _virtualMove, _virtualMoveActive, moveBlendMode);
                else
                    Move = Vector2.zero;

                if (!_allowLookWhileLocked)
                {
                    Look = Vector2.zero;
                    return;
                }

                ApplyLookAxes();
                return;
            }

            var blendedMove = Blend(hardwareMove, _virtualMove, _virtualMoveActive, moveBlendMode);
            if (_climbContextActive)
            {
                ClimbUpAxis = Mathf.Max(0f, blendedMove.y);
                Move = Vector2.zero;
            }
            else
            {
                ClimbUpAxis = 0f;
                Move = blendedMove;
            }

            ApplyLookAxes();
            ClimbHeld = Input.GetKey(climbKey);
            var keyboardGlide = Input.GetKeyDown(glideKey);
            if (_uiGlidePressQueued)
                UiActionPressedFromUi = true;
            GlidePressed = keyboardGlide || _uiGlidePressQueued;
            _uiGlidePressQueued = false;
        }

        private void ApplyLookAxes()
        {
            var hardwareLook = new Vector2(Input.GetAxis(lookX), Input.GetAxis(lookY)) * lookHardwareScale;
            if (disableHardwareLookOnTouchPlatforms && ShouldDisableHardwareLook())
                hardwareLook = Vector2.zero;
            var virtualLook = _virtualLook * lookVirtualScale;
            Look = Blend(hardwareLook, virtualLook, _virtualLookActive, lookBlendMode);
        }

        private static bool ShouldDisableHardwareLook()
        {
#if UNITY_EDITOR
            return false;
#else
#if UNITY_WEBGL && WEB_EDITOR
            // Generic WebGL build (非渠道): joystick-only camera, no mouse-follow rotation.
            return true;
#endif
            // On mobile/WebGL touch devices, Unity often maps touch drag to "Mouse X/Y".
            // That makes camera rotate while dragging UI (left joystick). Prefer virtual look only.
            if (Application.isMobilePlatform)
                return true;
            if (Application.platform == RuntimePlatform.WebGLPlayer && Input.touchCount > 0)
                return true;
            return false;
#endif
        }

        private static Vector2 Blend(Vector2 hardware, Vector2 virtualInput, bool virtualActive, InputBlendMode mode)
        {
            switch (mode)
            {
                case InputBlendMode.HardwareOnly:
                    return Vector2.ClampMagnitude(hardware, 1f);
                case InputBlendMode.VirtualOnly:
                    return Vector2.ClampMagnitude(virtualInput, 1f);
                case InputBlendMode.Combine:
                    return Vector2.ClampMagnitude(hardware + virtualInput, 1f);
                case InputBlendMode.VirtualOverrideWhenActive:
                default:
                    return Vector2.ClampMagnitude(virtualActive ? virtualInput : hardware, 1f);
            }
        }
    }
}


using UnityEngine;

namespace Game.Player
{
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

        public string moveX = "Horizontal";
        public string moveY = "Vertical";
        public string lookX = "Mouse X";
        public string lookY = "Mouse Y";
        public KeyCode actionKey = KeyCode.Space;

        [SerializeField] InputBlendMode moveBlendMode = InputBlendMode.VirtualOverrideWhenActive;
        [SerializeField] InputBlendMode lookBlendMode = InputBlendMode.VirtualOverrideWhenActive;
        [SerializeField] float lookHardwareScale = 1f;
        [SerializeField] float lookVirtualScale = 1f;
        [SerializeField] bool disableHardwareLookOnTouchPlatforms = true;

        Vector2 _virtualMove;
        Vector2 _virtualLook;
        bool _virtualMoveActive;
        bool _virtualLookActive;
        bool _locked;
        bool _allowLookWhileLocked;
        bool _allowMoveWhileLocked;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool UiActionPressedFromUi { get; private set; }
        public bool IsLocked => _locked;
        public bool IsVirtualLookActive => _virtualLookActive;

        public void SetLocked(bool locked, bool allowLookWhileLocked = false, bool allowMoveWhileLocked = false)
        {
            _locked = locked;
            _allowLookWhileLocked = locked && allowLookWhileLocked;
            _allowMoveWhileLocked = locked && allowMoveWhileLocked;
        }

        public void SetVirtualMove(Vector2 value, bool active)
        {
            _virtualMove = Vector2.ClampMagnitude(value, 1f);
            _virtualMoveActive = active;
        }

        public void SetVirtualLook(Vector2 value, bool active)
        {
            _virtualLook = value;
            _virtualLookActive = active;
        }

        public void QueueActionPressFromUi() => UiActionPressedFromUi = true;

        void Update()
        {
            UiActionPressedFromUi = false;
            var hardwareMove = Vector2.ClampMagnitude(
                new Vector2(Input.GetAxisRaw(moveX), Input.GetAxisRaw(moveY)), 1f);
            Move = !_locked || _allowMoveWhileLocked
                ? Blend(hardwareMove, _virtualMove, _virtualMoveActive, moveBlendMode, true)
                : Vector2.zero;

            if (_locked && !_allowLookWhileLocked)
            {
                Look = Vector2.zero;
                return;
            }

            var hardwareLook = disableHardwareLookOnTouchPlatforms && Application.isMobilePlatform
                ? Vector2.zero
                : new Vector2(Input.GetAxisRaw(lookX), Input.GetAxisRaw(lookY)) * lookHardwareScale;
            Look = Blend(hardwareLook, _virtualLook * lookVirtualScale, _virtualLookActive, lookBlendMode, false);
        }

        static Vector2 Blend(Vector2 hardware, Vector2 virtualInput, bool virtualActive, InputBlendMode mode, bool clamp)
        {
            Vector2 value;
            switch (mode)
            {
                case InputBlendMode.HardwareOnly: value = hardware; break;
                case InputBlendMode.VirtualOnly: value = virtualInput; break;
                case InputBlendMode.Combine: value = hardware + virtualInput; break;
                default: value = virtualActive ? virtualInput : hardware; break;
            }
            return clamp ? Vector2.ClampMagnitude(value, 1f) : value;
        }
    }
}

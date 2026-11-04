using Game.UI.Controls;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 将 UGUI <see cref="UguiVirtualJoystick"/> 连接到 <see cref="LegacyPlayerInput"/>。
    /// 左摇杆：持续位移；右摇杆：视角（可与 FGUI 桥接并存，场景中请只启用其一以免重复写入）。
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public sealed class PlayerUguiJoystickInputBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LegacyPlayerInput playerInput;

        [Tooltip("走位摇杆")]
        [SerializeField] private UguiVirtualJoystick leftJoystick;

        [Tooltip("视角摇杆（建议开启「拖拽像素 / 半径」模式以支持无限拖拽转镜头）")]
        [SerializeField] private UguiVirtualJoystick rightJoystick;

        [Header("Auto Resolve")]
        [SerializeField] private string leftJoystickObjectName = "JoystickL";
        [SerializeField] private string rightJoystickObjectName = "JoystickR";

        [Header("Move")]
        [SerializeField] private bool invertMoveX;
        [SerializeField] private bool invertMoveY = true;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 1f;
        [SerializeField] private bool invertLookY = true;
        [SerializeField] private bool rightUseTouchDeltaForRotation = true;

        private Vector2 _rightPrevValue;
        private bool _rightWasPressed;

        private void Awake()
        {
            if (playerInput == null)
                playerInput = GetComponent<LegacyPlayerInput>();
            if (playerInput == null)
                playerInput = FindObjectOfType<LegacyPlayerInput>();

            TryAutoResolveJoysticks();
        }

        void TryAutoResolveJoysticks()
        {
            var previousRight = rightJoystick;
            if (leftJoystick == null)
                leftJoystick = FindJoystickByName(leftJoystickObjectName);
            if (rightJoystick == null)
                rightJoystick = FindJoystickByName(rightJoystickObjectName);

            if (rightJoystick != null && rightJoystick != previousRight && isActiveAndEnabled)
                SubscribeRightEvents(true);
        }

        static UguiVirtualJoystick FindJoystickByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;

#if UNITY_2023_1_OR_NEWER
            var joysticks = FindObjectsByType<UguiVirtualJoystick>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
#else
            var joysticks = FindObjectsOfType<UguiVirtualJoystick>(true);
#endif
            for (var i = 0; i < joysticks.Length; i++)
            {
                var joystick = joysticks[i];
                if (joystick != null && string.Equals(joystick.name, objectName, System.StringComparison.Ordinal))
                    return joystick;
            }

            return null;
        }

        private void OnEnable()
        {
            TryAutoResolveJoysticks();
            SubscribeRightEvents(true);
        }

        private void OnDisable()
        {
            SubscribeRightEvents(false);

            if (playerInput != null)
            {
                playerInput.SetVirtualMove(Vector2.zero, false);
                playerInput.SetVirtualLook(Vector2.zero, false);
            }
        }

        private void Update()
        {
            if (leftJoystick == null || rightJoystick == null)
                TryAutoResolveJoysticks();

            PushLeftHeld();
            PushRightLook();
        }

        private void SubscribeRightEvents(bool subscribe)
        {
            if (rightJoystick == null) return;
            if (subscribe)
            {
                rightJoystick.Released += OnRightReleased;
                rightJoystick.Pressed += OnRightPressed;
            }
            else
            {
                rightJoystick.Released -= OnRightReleased;
                rightJoystick.Pressed -= OnRightPressed;
            }
        }

        /// <remarks>
        /// 走位采用每帧采样左摇杆，避免单靠 UI 回调时早于 <see cref="LegacyPlayerInput"/> 的时机问题。
        /// 视角仍建议在 Project Settings → Script Execution Order 将 EventSystem 排在摇杆脚本（-122）之前，减轻「拖拽比镜头慢一拍」感。
        /// </remarks>
        private void PushLeftHeld()
        {
            if (playerInput == null || leftJoystick == null) return;

            if (!leftJoystick.IsPressed)
            {
                playerInput.SetVirtualMove(Vector2.zero, false);
                return;
            }

            var value = leftJoystick.Value;
            if (invertMoveX) value.x = -value.x;
            if (invertMoveY) value.y = -value.y;
            playerInput.SetVirtualMove(value, true);
        }

        private void PushRightLook()
        {
            if (playerInput == null || rightJoystick == null) return;

            if (rightJoystick.IsPressed)
            {
                var dt = Mathf.Max(1e-6f, Time.deltaTime);
                Vector2 lookRate;

                if (rightUseTouchDeltaForRotation)
                {
                    var touchDeltaNorm = rightJoystick.ConsumeTouchDeltaNormalized();
                    lookRate = (touchDeltaNorm / dt) * lookSensitivity;
                }
                else
                {
                    var current = rightJoystick.Value;
                    var delta = current - _rightPrevValue;
                    _rightPrevValue = current;
                    lookRate = (delta / dt) * lookSensitivity;
                }

                if (invertLookY)
                    lookRate.y = -lookRate.y;

                playerInput.SetVirtualLook(lookRate, true);
                _rightWasPressed = true;
            }
            else if (_rightWasPressed)
            {
                playerInput.SetVirtualLook(Vector2.zero, false);
                _rightWasPressed = false;
            }
        }

        private void OnRightPressed()
        {
            _rightWasPressed = true;
            if (rightJoystick != null)
                _rightPrevValue = rightJoystick.Value;
        }

        private void OnRightReleased()
        {
            if (playerInput == null) return;
            playerInput.SetVirtualLook(Vector2.zero, false);
            _rightWasPressed = false;
        }
    }
}

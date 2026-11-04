using FairyGUI;
using UnityEngine;
using Game;

namespace Game.Player
{
    /// <summary>
    /// Connects FGUI joysticks to LegacyPlayerInput.
    /// Left joystick drives movement, right joystick drives camera look.
    /// </summary>
    public sealed class PlayerJoystickInputBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIPanel uiPanel;
        [SerializeField] private LegacyPlayerInput playerInput;

        [Header("Joystick Mapping")]
        [SerializeField] private string leftJoystickName = "JoystickL";
        [SerializeField] private string rightJoystickName = "JoystickR";

        [Header("Left (Move)")]
        [SerializeField] private float leftRadius = 120f;
        [SerializeField, Range(0f, 1f)] private float leftDeadZone = 0.1f;
        [SerializeField] private FGUIVirtualJoystick.CenterMode leftCenterMode = FGUIVirtualJoystick.CenterMode.Dynamic;
        [SerializeField] private bool leftEdgeSnap = true;
        [SerializeField] private float leftEdgePadding = 6f;
        [SerializeField] private float leftReboundDuration = 0.12f;
        [SerializeField] private bool leftReturnCenterToDefaultOnRelease = false;
        [SerializeField] private FGUIVirtualJoystick.ResponseCurveMode leftResponseCurve = FGUIVirtualJoystick.ResponseCurveMode.Linear;
        [SerializeField] private float leftResponseExponent = 1.75f;
        [SerializeField] private bool leftAxisLockAssist = true;
        [SerializeField] private float leftAxisLockThreshold = 0.2f;
        [SerializeField] private float leftAxisLockStrength = 0.85f;
        [SerializeField] private bool invertMoveX = false;
        [SerializeField] private bool invertMoveY = true;

        [Header("Right (Look)")]
        [SerializeField] private float rightRadius = 120f;
        [SerializeField, Range(0f, 1f)] private float rightDeadZone = 0.1f;
        [SerializeField] private FGUIVirtualJoystick.CenterMode rightCenterMode = FGUIVirtualJoystick.CenterMode.Dynamic;
        [SerializeField] private bool rightEdgeSnap = true;
        [SerializeField] private float rightEdgePadding = 6f;
        [SerializeField] private float rightReboundDuration = 0.12f;
        [SerializeField] private bool rightReturnCenterToDefaultOnRelease = false;
        [SerializeField] private FGUIVirtualJoystick.ResponseCurveMode rightResponseCurve = FGUIVirtualJoystick.ResponseCurveMode.Exponential;
        [SerializeField] private float rightResponseExponent = 1.6f;
        [SerializeField] private bool rightAxisLockAssist = false;
        [SerializeField] private float rightAxisLockThreshold = 0.2f;
        [SerializeField] private float rightAxisLockStrength = 0.85f;
        [SerializeField] private float lookSensitivity = 1f;
        [SerializeField] private bool invertLookY = true;
        [SerializeField] private bool rightUseTouchDeltaForRotation = true;

        private FGUIVirtualJoystick _leftJoystick;
        private FGUIVirtualJoystick _rightJoystick;

        // Rotation should be driven by per-frame joystick displacement.
        private Vector2 _rightPrevValue;
        private bool _rightWasPressed;

        private void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<LegacyPlayerInput>();
            if (playerInput == null) playerInput = FindObjectOfType<LegacyPlayerInput>();
            if (uiPanel == null) uiPanel = GetComponent<UIPanel>();
        }

        private void Start()
        {
            TryBind();
        }

        private void Update()
        {
            if (playerInput == null) return;

            // Left joystick uses position-based movement (keep moving while holding).
            _leftJoystick?.Tick(Time.deltaTime);
            _rightJoystick?.Tick(Time.deltaTime);

            if (_rightJoystick != null && _rightJoystick.IsPressed)
            {
                var dt = Mathf.Max(1e-6f, Time.deltaTime);
                Vector2 lookRate;

                if (rightUseTouchDeltaForRotation)
                {
                    // Infinite-drag friendly: use raw per-frame touch displacement, not stick Value.
                    var touchDeltaNorm = _rightJoystick.ConsumeTouchDeltaNormalized();
                    lookRate = (touchDeltaNorm / dt) * lookSensitivity;
                }
                else
                {
                    // Position-based displacement: stops rotating when stick Value saturates/clamps.
                    var current = _rightJoystick.Value;
                    var delta = current - _rightPrevValue;
                    _rightPrevValue = current;
                    lookRate = (delta / dt) * lookSensitivity;
                }

                if (invertLookY) lookRate.y = -lookRate.y;
                playerInput.SetVirtualLook(lookRate, true);
                _rightWasPressed = true;
            }
            else if (_rightWasPressed)
            {
                playerInput.SetVirtualLook(Vector2.zero, false);
                _rightWasPressed = false;
            }
        }

        private void OnDisable()
        {
            if (playerInput != null)
            {
                playerInput.SetVirtualMove(Vector2.zero, false);
                playerInput.SetVirtualLook(Vector2.zero, false);
            }
        }

        private void OnDestroy()
        {
            _leftJoystick?.Dispose();
            _rightJoystick?.Dispose();
        }

        [ContextMenu("Bind Joysticks")]
        public void TryBind()
        {
            _leftJoystick?.Dispose();
            _rightJoystick?.Dispose();
            _leftJoystick = null;
            _rightJoystick = null;

            if (uiPanel == null || uiPanel.ui == null)
            {
                Debug.LogWarning("PlayerJoystickInputBridge: UIPanel or UI root is missing.");
                return;
            }

            if (playerInput == null)
            {
                Debug.LogWarning("PlayerJoystickInputBridge: LegacyPlayerInput reference is missing.");
                return;
            }

            var root = uiPanel.ui;
            var leftRoot = FindJoystickRoot(root, leftJoystickName, true);
            var rightRoot = FindJoystickRoot(root, rightJoystickName, false);

            if (leftRoot == null || rightRoot == null)
            {
                Debug.LogWarning($"PlayerJoystickInputBridge: cannot find joystick nodes '{leftJoystickName}' / '{rightJoystickName}'. Please check hierarchy under UIPanel.ui.");
                return;
            }

            var leftVisual = ResolveVisual(leftRoot);
            var rightVisual = ResolveVisual(rightRoot);
            if (leftVisual.baseVisual == null || leftVisual.wheelVisual == null || rightVisual.baseVisual == null || rightVisual.wheelVisual == null)
            {
                Debug.LogWarning("PlayerJoystickInputBridge: joystick visual nodes are missing (base/wheel). Check joystick hierarchy and child names.");
                return;
            }

            _leftJoystick = new FGUIVirtualJoystick(
                leftRoot,
                leftVisual.baseVisual,
                leftVisual.wheelVisual,
                leftRadius,
                leftDeadZone,
                leftCenterMode,
                leftEdgeSnap,
                leftEdgePadding,
                leftReboundDuration,
                leftReturnCenterToDefaultOnRelease,
                leftResponseCurve,
                leftResponseExponent,
                leftAxisLockAssist,
                leftAxisLockThreshold,
                leftAxisLockStrength);

            _rightJoystick = new FGUIVirtualJoystick(
                rightRoot,
                rightVisual.baseVisual,
                rightVisual.wheelVisual,
                rightRadius,
                rightDeadZone,
                rightCenterMode,
                rightEdgeSnap,
                rightEdgePadding,
                rightReboundDuration,
                rightReturnCenterToDefaultOnRelease,
                rightResponseCurve,
                rightResponseExponent,
                rightAxisLockAssist,
                rightAxisLockThreshold,
                rightAxisLockStrength);

            _leftJoystick.ValueChanged += OnLeftChanged;
            _leftJoystick.Released += OnLeftReleased;
            _rightJoystick.Pressed += OnRightPressed;
            _rightJoystick.ValueChanged += OnRightChanged;
            _rightJoystick.Released += OnRightReleased;
        }

        private void OnLeftChanged(Vector2 value)
        {
            if (playerInput == null) return;
            var active = _leftJoystick != null && _leftJoystick.IsPressed;
            if (invertMoveX) value.x = -value.x;
            if (invertMoveY) value.y = -value.y;
            playerInput.SetVirtualMove(value, active);
        }

        private void OnLeftReleased()
        {
            if (playerInput == null) return;
            playerInput.SetVirtualMove(Vector2.zero, false);
        }

        private void OnRightChanged(Vector2 value)
        {
            // Intentionally empty.
            // Look driving is done in Update() by comparing prev vs current value.
        }

        private void OnRightReleased()
        {
            if (playerInput == null) return;
            playerInput.SetVirtualLook(Vector2.zero, false);
            _rightWasPressed = false;
        }

        private void OnRightPressed()
        {
            _rightWasPressed = true;
            if (_rightJoystick != null)
                _rightPrevValue = _rightJoystick.Value;
        }

        private static (GObject baseVisual, GObject wheelVisual) ResolveVisual(GComponent joystickRoot)
        {
            if (joystickRoot is UIJoystickL left && left.m_ctrl != null) return (left.m_ctrl.m_Base, left.m_ctrl.m_wheel);
            if (joystickRoot is UIJoystickR right && right.m_ctrl != null) return (right.m_ctrl.m_Base, right.m_ctrl.m_wheel);
            // if (joystickRoot is UIJoystick single && single.m_ctrl != null) return (single.m_ctrl.m_Base, single.m_ctrl.m_wheel);

            // Fallback: extension not bound or type erased -> inspect hierarchy.
            var ctrl = joystickRoot.GetChild("ctrl")?.asCom;
            if (ctrl == null && joystickRoot.numChildren > 0)
                ctrl = joystickRoot.GetChildAt(0)?.asCom;
            if (ctrl == null)
                ctrl = joystickRoot;

            // Prefer semantic names.
            var baseVisual = ctrl.GetChild("Base")
                           ?? ctrl.GetChild("base")
                           ?? ctrl.GetChild("m_Base");
            var wheelVisual = ctrl.GetChild("wheel")
                            ?? ctrl.GetChild("Wheel")
                            ?? ctrl.GetChild("m_wheel");

            if (baseVisual != null && wheelVisual != null)
                return (baseVisual, wheelVisual);

            // Last fallback: first two loader children.
            GObject firstLoader = null;
            GObject secondLoader = null;
            for (var i = 0; i < ctrl.numChildren; i++)
            {
                var child = ctrl.GetChildAt(i);
                if (child.asLoader == null) continue;
                if (firstLoader == null) firstLoader = child;
                else
                {
                    secondLoader = child;
                    break;
                }
            }
            return (firstLoader, secondLoader);
        }

        private static GComponent FindJoystickRoot(GComponent root, string preferredName, bool preferLeft)
        {
            // 1) Exact-name lookup on direct children.
            var named = root.GetChild(preferredName)?.asCom;
            if (named != null) return named;

            // 2) Recursively find typed joystick.
            var typed = FindByType(root, preferLeft ? typeof(UIJoystickL) : typeof(UIJoystickR));
            if (typed != null) return typed;

            // 3) Recursively find by fuzzy name.
            var fuzzy = FindByNameContains(root, preferLeft ? "joystickl" : "joystickr");
            if (fuzzy != null) return fuzzy;

            // 4) Last fallback: first component that contains UIctrComp child.
            return FindFirstWithCtrComp(root);
        }

        private static GComponent FindByType(GComponent root, System.Type targetType)
        {
            for (var i = 0; i < root.numChildren; i++)
            {
                var child = root.GetChildAt(i);
                if (targetType.IsInstanceOfType(child))
                    return child.asCom;

                var childCom = child.asCom;
                if (childCom == null) continue;
                var nested = FindByType(childCom, targetType);
                if (nested != null) return nested;
            }
            return null;
        }

        private static GComponent FindByNameContains(GComponent root, string keyLower)
        {
            for (var i = 0; i < root.numChildren; i++)
            {
                var child = root.GetChildAt(i);
                var childCom = child.asCom;
                if (childCom == null) continue;

                var name = child.name ?? string.Empty;
                if (name.ToLowerInvariant().Contains(keyLower))
                    return childCom;

                var nested = FindByNameContains(childCom, keyLower);
                if (nested != null) return nested;
            }
            return null;
        }

        private static GComponent FindFirstWithCtrComp(GComponent root)
        {
            for (var i = 0; i < root.numChildren; i++)
            {
                var child = root.GetChildAt(i).asCom;
                if (child == null) continue;

                for (var j = 0; j < child.numChildren; j++)
                {
                    if (child.GetChildAt(j) is UIctrComp)
                        return child;
                }

                var nested = FindFirstWithCtrComp(child);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}

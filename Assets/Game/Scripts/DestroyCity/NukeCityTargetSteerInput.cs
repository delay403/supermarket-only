using Game.Player;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 导弹下落阶段操控落点：读取左摇杆（<see cref="LegacyPlayerInput.Move"/>），
    /// 仅在发射器进入下落段后生效，且会被限制在计划落点半径内。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NukeCityTargetSteerInput : MonoBehaviour
    {
        [SerializeField] private NukeLauncherController launcher;
        [SerializeField] private Transform steerTransform;
        [SerializeField] private LegacyPlayerInput playerInput;
        [SerializeField] private float moveSpeed = 22f;
        [SerializeField] private bool allowKeyboardFallback = true;

        void Awake()
        {
            if (steerTransform == null)
                steerTransform = transform;
            if (playerInput == null)
                playerInput = FindObjectOfType<LegacyPlayerInput>();
        }

        void Update()
        {
            if (launcher == null || steerTransform == null || !launcher.IsDescentSteeringActive)
                return;

            var viewMode = NukeLaunchViewModeController.Instance;
            if (viewMode == null || !viewMode.IsMissileMode)
                return;

            var move = ReadMoveInput();
            if (move.sqrMagnitude < 0.01f)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            var forward = cam.transform.forward;
            var right = cam.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            var worldDelta = (right * move.x + forward * move.y) * (moveSpeed * Time.deltaTime);
            launcher.TrySteerLanding(steerTransform.position + worldDelta);
        }

        Vector2 ReadMoveInput()
        {
            if (playerInput != null)
                return playerInput.Move;

            if (!allowKeyboardFallback)
                return Vector2.zero;

            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        /// <summary>供触摸 / UI 摇杆直接调用。</summary>
        public void ApplySteerDelta(Vector3 worldDelta)
        {
            if (launcher == null || steerTransform == null || !launcher.IsDescentSteeringActive)
                return;

            var viewMode = NukeLaunchViewModeController.Instance;
            if (viewMode == null || !viewMode.IsMissileMode)
                return;

            launcher.TrySteerLanding(steerTransform.position + worldDelta);
        }
    }
}

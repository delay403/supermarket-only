using UnityEngine;
using Game.Audio;
using Game.Core.Config;

namespace Game.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LegacyPlayerInput input;
        [SerializeField] private ThirdPersonCamera thirdPersonCamera;
        [SerializeField] private Transform cameraPivot;

        [Header("Tuning (fallback if no GameBalanceConfig)")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float maxMoveSpeed = 5f;
        [SerializeField] private float rotationSharpness = 12f;
        [SerializeField] private float gravity = -25f;
        [Header("Footsteps (Move SFX cadence, grounded XZ speed)")]
        [SerializeField] private float footstepMinSpeed = 0.2f;
        [SerializeField] private float footstepIntervalFast = 0.26f;
        [SerializeField] private float footstepIntervalSlow = 0.62f;

        public Vector3 VelocityWorld { get; private set; }
        public bool IsGrounded => _cc != null && _cc.isGrounded;

        private CharacterController _cc;
        private float _verticalSpeed;
        private float? _queuedJumpVerticalSpeed;
        private float _footstepTimer;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<LegacyPlayerInput>();
            ResolveCameraPivot();

            ApplyBalanceIfPresent();

        }

        private void ResolveCameraPivot()
        {
            if (cameraPivot != null) return;

            if (thirdPersonCamera == null)
                thirdPersonCamera = FindObjectOfType<ThirdPersonCamera>();
            if (thirdPersonCamera != null && thirdPersonCamera.enabled)
            {
                cameraPivot = thirdPersonCamera.transform;
                return;
            }

            if (Camera.main != null)
                cameraPivot = Camera.main.transform;
        }

        private void ApplyBalanceIfPresent()
        {
            var bal = ConfigRegistry.Balance;
            if (bal == null) return;
            moveSpeed = bal.moveSpeed;
            rotationSharpness = bal.rotationSharpness;
            gravity = bal.gravity;
        }

        /// <summary>
        /// 在本帧移动逻辑开始前应用；由 <see cref="PlayerJumpController"/> 等同帧较早脚本入队。
        /// </summary>
        public void QueueJumpVerticalSpeed(float upwardSpeed)
        {
            upwardSpeed = Mathf.Max(0f, upwardSpeed);
            if (upwardSpeed < 1e-4f) return;
            _queuedJumpVerticalSpeed = upwardSpeed;
        }

        private void Update()
        {
            if (input == null) return;
            if (cameraPivot == null) ResolveCameraPivot();

            if (_queuedJumpVerticalSpeed.HasValue)
            {
                _verticalSpeed = _queuedJumpVerticalSpeed.Value;
                _queuedJumpVerticalSpeed = null;
            }

            // Camera-relative movement (XZ plane).
            var camFwd = cameraPivot != null ? cameraPivot.forward : Vector3.forward;
            var camRight = cameraPivot != null ? cameraPivot.right : Vector3.right;
            camFwd.y = 0f;
            camRight.y = 0f;
            camFwd.Normalize();
            camRight.Normalize();

            var desired = camRight * input.Move.x + camFwd * input.Move.y;
            if (desired.sqrMagnitude > 1e-4f)
            {
                var desiredRot = Quaternion.LookRotation(desired, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    desiredRot,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime)
                );
            }

            if (_cc.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -2f; // keep grounded
            _verticalSpeed += gravity * Time.deltaTime;

            var speed = ResolveMoveSpeed();
            var move = desired * speed;
            move.y = _verticalSpeed;

            var delta = move * Time.deltaTime;
            _cc.Move(delta);

            VelocityWorld = move;

            TickGroundFootsteps(desired);
        }

        /// <summary>应用超市收纳的移动速度倍率加成。</summary>
        public void SetSupermarketMoveSpeedBonus(float mul) => _supermarketMoveSpeedMul = Mathf.Max(0f, mul);

        float _supermarketMoveSpeedMul;

        float ResolveMoveSpeed()
        {
            var speed = moveSpeed * (1f + _supermarketMoveSpeedMul);
            var cap = maxMoveSpeed * (1f + _supermarketMoveSpeedMul);
            return Mathf.Max(0f, Mathf.Min(speed, cap));
        }

        private void TickGroundFootsteps(Vector3 desiredMoveDir)
        {
            if (!_cc.isGrounded)
            {
                _footstepTimer = 0f;
                return;
            }

            var planar = VelocityWorld;
            planar.y = 0f;
            var speedXZ = planar.magnitude;
            if (speedXZ < footstepMinSpeed || desiredMoveDir.sqrMagnitude < 1e-6f)
            {
                _footstepTimer = 0f;
                return;
            }

            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer > 0f) return;

            AudioGameplayFeedback.PlayMove();
            var runCap = Mathf.Max(1e-3f, ResolveMoveSpeed());
            var t = Mathf.InverseLerp(footstepMinSpeed, runCap, Mathf.Clamp(speedXZ, footstepMinSpeed, runCap));
            _footstepTimer = Mathf.Lerp(footstepIntervalSlow, footstepIntervalFast, t);
        }
    }
}


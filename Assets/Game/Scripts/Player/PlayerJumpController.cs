using System;
using Game.Audio;
using Game.Core.Config;
using Game.Tower;
using Game.WormDodge.Pickups;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 地面起跳：空格（默认）与滑翔键可共用，仅在未攀爬时生效；参数来自 <see cref="PlayerJumpConfig"/>。
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class PlayerJumpController : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerAnimatorDriver anim;
        [SerializeField] private LegacyPlayerInput input;
        [SerializeField] private ClimbController climb;
        [SerializeField] private PlayerJumpConfig jumpConfig;

        WormDodgeParticipantPickupEffects _pickupEffects;
        float _lastJumpUnscaledTime = -999f;

        /// <summary>本帧成功触发地面起跳时触发。</summary>
        public event Action Jumped;

        private void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (anim == null) anim = GetComponentInChildren<PlayerAnimatorDriver>();
            if (input == null) input = GetComponent<LegacyPlayerInput>();
            if (climb == null) climb = GetComponent<ClimbController>();
            _pickupEffects = GetComponent<WormDodgeParticipantPickupEffects>();
        }

        public void BindAnimatorDriver(PlayerAnimatorDriver driver) => anim = driver;

        private void Update()
        {
            if (jumpConfig == null || motor == null) return;
            if (input != null && input.IsLocked) return;
            if (climb != null && climb.State != ClimbController.ClimbState.Inactive) return;
            if (!motor.IsGrounded) return;

            var key = jumpConfig.useCustomJumpKey ? jumpConfig.jumpKey : KeyCode.Space;
            var keyboardDown = Input.GetKeyDown(key);
            var uiJump = input != null && input.UiActionPressedFromUi;
            if (!keyboardDown && !uiJump) return;

            if (Time.unscaledTime - _lastJumpUnscaledTime < Mathf.Max(0f, jumpConfig.minIntervalSeconds))
                return;

            var jumpMul = _pickupEffects != null ? _pickupEffects.ResolveJumpVerticalSpeedMultiplier() : 1f;
            motor.QueueJumpVerticalSpeed(jumpConfig.jumpVerticalSpeed * jumpMul);
            AudioGameplayFeedback.PlayJump();
            if (anim != null) anim.TriggerJump();
            Jumped?.Invoke();
            _lastJumpUnscaledTime = Time.unscaledTime;
        }
    }
}

using System;
using Game.Audio;
using Game.Core.Config;
using UnityEngine;

namespace Game.Player
{
    [DefaultExecutionOrder(-40)]
    public sealed class PlayerJumpController : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerAnimatorDriver anim;
        [SerializeField] LegacyPlayerInput input;
        [SerializeField] PlayerJumpConfig jumpConfig;
        float _lastJumpUnscaledTime = -999f;

        public event Action Jumped;

        void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (anim == null) anim = GetComponentInChildren<PlayerAnimatorDriver>();
            if (input == null) input = GetComponent<LegacyPlayerInput>();
        }

        public void BindAnimatorDriver(PlayerAnimatorDriver driver) => anim = driver;

        void Update()
        {
            if (jumpConfig == null)
                return;
            var key = jumpConfig.useCustomJumpKey ? jumpConfig.jumpKey : KeyCode.Space;
            if (!Input.GetKeyDown(key) && (input == null || !input.UiActionPressedFromUi))
                return;
            RequestJumpFromUi();
        }

        public void RequestJumpFromUi()
        {
            if (jumpConfig == null || motor == null || !motor.IsGrounded)
                return;
            if (input != null && input.IsLocked)
                return;
            if (Time.unscaledTime - _lastJumpUnscaledTime < Mathf.Max(0f, jumpConfig.minIntervalSeconds))
                return;

            motor.QueueJumpVerticalSpeed(jumpConfig.jumpVerticalSpeed);
            AudioGameplayFeedback.PlayJump();
            anim?.TriggerJump();
            Jumped?.Invoke();
            _lastJumpUnscaledTime = Time.unscaledTime;
        }
    }
}

using Game.Audio;
using UnityEngine;

namespace Game.WormDodge.Pickups
{
    /// <summary>
    /// 超级跳跃道具：仅玩家有效，跳跃高度 +100%，持续 10 秒。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeSuperJumpPickup : WormDodgePickupBase
    {
        [SerializeField] private float durationSeconds = 10f;
        [SerializeField] private float jumpHeightMultiplierAdd = 1f;

        protected override bool CanCollect(WormDodgeParticipant participant) =>
            participant != null && participant.IsPlayer;

        protected override void ApplyPickup(WormDodgeParticipant participant)
        {
            var effects = ResolveEffects(participant);
            effects?.ApplySuperJump(durationSeconds, jumpHeightMultiplierAdd);
        }

#if UNITY_EDITOR
        void Reset()
        {
            pickupSfx = SfxId.Jump;
        }

        void OnValidate()
        {
            durationSeconds = Mathf.Max(0.05f, durationSeconds);
            jumpHeightMultiplierAdd = Mathf.Max(0f, jumpHeightMultiplierAdd);
        }
#endif
    }
}

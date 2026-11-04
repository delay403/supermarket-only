using Game.Audio;
using UnityEngine;

namespace Game.WormDodge.Pickups
{
    /// <summary>
    /// 加速道具：玩家与 AI 拾取后最终移速 +50%，持续 5 秒。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeSpeedBoostPickup : WormDodgePickupBase
    {
        [SerializeField] private float durationSeconds = 5f;
        [SerializeField] private float finalMoveSpeedMultiplierAdd = 0.5f;

        protected override bool CanCollect(WormDodgeParticipant participant) => participant != null;

        protected override void ApplyPickup(WormDodgeParticipant participant)
        {
            var effects = ResolveEffects(participant);
            effects?.ApplySpeedBoost(durationSeconds, finalMoveSpeedMultiplierAdd);
        }

#if UNITY_EDITOR
        void Reset()
        {
            pickupSfx = SfxId.Interact;
        }

        void OnValidate()
        {
            durationSeconds = Mathf.Max(0.05f, durationSeconds);
            finalMoveSpeedMultiplierAdd = Mathf.Max(0f, finalMoveSpeedMultiplierAdd);
        }
#endif
    }
}

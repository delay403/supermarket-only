using Game.Core.Config;
using Game.Core.Content;
using UnityEngine;

namespace Game.WormDodge.Pickups
{
    /// <summary>
    /// 金币道具：仅玩家有效，数量从 <see cref="WormDodgePickupConfig"/> 随机。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeCoinPickup : WormDodgePickupBase
    {
        [SerializeField] private WormDodgePickupConfig configOverride;

        static WormDodgePickupConfig _sharedConfig;

        protected override bool CanCollect(WormDodgeParticipant participant) =>
            participant != null && participant.IsPlayer;

        protected override void ApplyPickup(WormDodgeParticipant participant)
        {
            var pickupConfig = ResolveConfig();
            if (pickupConfig == null)
            {
                Debug.LogWarning("[WormDodgeCoinPickup] 未找到 WormDodgePickupConfig。", this);
                return;
            }

            var amount = pickupConfig.RollCoinAmount();
            WormDodgeDirector.Instance?.GrantPickupCoin(amount);

            var effects = ResolveEffects(participant);
            effects?.PlayCoinPickupVfx();
        }

        WormDodgePickupConfig ResolveConfig()
        {
            if (configOverride != null)
                return configOverride;

            if (_sharedConfig == null)
            {
                _sharedConfig = ConfigRegistry.WormDodgePickupConfig
                    ?? GameContentService.Resolve<WormDodgePickupConfig>();
                if (_sharedConfig == null)
                    _sharedConfig = Resources.Load<WormDodgePickupConfig>("WormDodgePickupConfig");
            }

            return _sharedConfig;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (configOverride != null)
                return;

            configOverride = UnityEditor.AssetDatabase.LoadAssetAtPath<WormDodgePickupConfig>(
                Game.Core.Content.GameContentPaths.WormDodgePickupConfigAssetPath);
        }
#endif
    }
}

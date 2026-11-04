using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.OnlineReward
{
    [CreateAssetMenu(menuName = "Game/Online Reward/OnlineRewardDatabase", fileName = "OnlineRewardDatabase")]
    public sealed class OnlineRewardDatabase : ScriptableObject
    {
        [Tooltip("按累计在线时长递增排序；档位按列表顺序依次解锁/领取。")]
        public List<OnlineRewardTierDef> tiers = new List<OnlineRewardTierDef>();

        public int TierCount => tiers != null ? tiers.Count : 0;

        public OnlineRewardTierDef GetTier(int index)
        {
            if (tiers == null || index < 0 || index >= tiers.Count)
                return null;
            return tiers[index];
        }
    }
}

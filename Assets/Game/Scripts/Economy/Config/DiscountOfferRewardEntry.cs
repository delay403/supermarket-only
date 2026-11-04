using System;
using UnityEngine;

namespace Game.Economy.Config
{
    public enum DiscountOfferRewardKind
    {
        Coins = 0,
        Wing = 1,
        Rod = 2,
        Trail = 3,
        Pet = 4,
        Gems = 5,
    }

    [Serializable]
    public struct DiscountOfferRewardEntry
    {
        public DiscountOfferRewardKind kind;
        [Tooltip("Wing/Rod/Trail/Pet 的 catalog id；Coins 时可留空。")]
        public string itemId;
        [Tooltip("Coins 奖励数量；其他类型可填 1 表示 1 个。")]
        public long amount;
    }
}

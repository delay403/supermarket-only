using System;
using UnityEngine;

namespace Game.Economy.Config
{
    [Serializable]
    public struct DiamondCoinsExchangeTierDef
    {
        public int tierId;
        public string displayName;
        [Tooltip("消耗钻石。")]
        public long gemCost;
        [Tooltip("获得金币 = 单次领取金币 × 本系数。")]
        public float freeCoinMultiplier;
        [Tooltip("可选：该档位金币图标；留空则保留场景中 Icon 原图。")]
        public Sprite coinIcon;
    }
}

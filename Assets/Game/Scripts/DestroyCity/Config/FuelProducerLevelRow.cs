using System;
using UnityEngine;

namespace Game.DestroyCity.Config
{
    [Serializable]
    public struct FuelProducerLevelRow
    {
        [Min(1)]
        public int level;

        [Tooltip("该等级单次生产（点击或自动 tick）产出的燃料经验。")]
        [Min(0f)]
        public float xpPerTick;

        [Tooltip("从该等级升到下一级所需金币；最高级填 0。")]
        [Min(0)]
        public long upgradeCostToNext;
    }
}

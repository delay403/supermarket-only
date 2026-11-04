using System;
using Game.DailySignIn;
using UnityEngine;

namespace Game.Features.LuckyWheel
{
    [Serializable]
    public sealed class WheelSliceDef
    {
        public RewardDef reward;

        [Tooltip("抽奖权重；越大越容易抽中。未配置或 ≤0 时按 1 处理。")]
        [Min(0)]
        public int weight = 1;

        [Tooltip("为空则从 reward 类型自动解析图标（金币/钻石等）。")]
        public Sprite iconOverride;

        [Tooltip("为空则用 RewardPresentation.FormatRewardLine(reward)。")]
        public string labelOverride;
    }
}

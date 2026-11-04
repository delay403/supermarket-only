using System;
using Game.DailySignIn;
using UnityEngine;

namespace Game.Features.OnlineReward
{
    /// <summary>在线奖励单档：累计在线秒数门槛 + 奖励内容。</summary>
    [Serializable]
    public sealed class OnlineRewardTierDef
    {
        [Tooltip("档位序号（从 1 起，仅用于展示；实际按列表顺序判定）。")]
        public int tierNumber = 1;

        [Tooltip("领取所需累计在线时长（秒）。")]
        public float requiredOnlineSeconds = 60f;

        public RewardDef reward;

        [Tooltip("格子奖励图标；非空时优先显示。")]
        public Sprite rewardIcon;

        [Tooltip("大奖档：用于格子放大 / 单独底图等。")]
        public bool isGrandPrize;
    }
}

using UnityEngine;

namespace Game.DailySignIn
{
    [CreateAssetMenu(menuName = "Game/DailySignIn/SignInDayDef", fileName = "SignInDayDef")]
    public sealed class SignInDayDef : ScriptableObject
    {
        [Range(1, 7)]
        public int dayNumber = 1;
        public RewardDef reward;

        [Tooltip("签到格子奖励图标；非空时优先显示，并覆盖金币/钻石等默认图以及宠物、角色、翅膀、拖尾等配置表解析出的图标。")]
        public Sprite rewardIcon;

        [Tooltip("第 7 日等大奖：用于签到格子放大 / 单独底图等。")]
        public bool isGrandPrize;
    }
}


using Game.Economy;
using UnityEngine;

namespace Game.UI.ViewModels
{
    public sealed class SettlementViewModel
    {
        /// <summary>相对塔基底的爬塔高度（米），与结算奖励计算一致。</summary>
        public float HeightMeters;

        /// <summary>自塔根水平投影起的滑翔距离（米）。</summary>
        public float GlideDistanceFromTowerMeters;

        /// <summary>历史最远滑翔距离（米），已计入本场。</summary>
        public float RecordGlideDistanceMeters;

        public long CoinsReward;
        public long GemsReward;

        /// <summary>本场因登顶发放的奖牌数量（来自地图配置）。</summary>
        public int TowerClearMedalCount;

        /// <summary>兼容旧逻辑：是否有登顶奖牌。</summary>
        public bool AwardedTopMedal => TowerClearMedalCount > 0;

        public string Title = "滑行结算";

        public bool HasDoubleReward =>
            CoinsReward > 0 || GemsReward > 0 || TowerClearMedalCount > 0;

        /// <summary>看双倍奖励：再发放一份与本场相同的金币、钻石与奖牌。</summary>
        public Rewards BuildDoubleRewardExtra() => new Rewards
        {
            coins = CoinsReward,
            gems = GemsReward,
            medalCount = TowerClearMedalCount,
        };

        public static SettlementViewModel FromRun(
            float heightMeters,
            float glideDistanceFromTowerMeters,
            float recordGlideDistanceMeters,
            Rewards rewards,
            int towerClearMedalCount)
        {
            return new SettlementViewModel
            {
                HeightMeters = heightMeters,
                GlideDistanceFromTowerMeters = glideDistanceFromTowerMeters,
                RecordGlideDistanceMeters = recordGlideDistanceMeters,
                CoinsReward = rewards.coins,
                GemsReward = rewards.gems,
                TowerClearMedalCount = Mathf.Max(0, towerClearMedalCount),
            };
        }

        public string ToBodyText()
        {
            var medalLine = TowerClearMedalCount > 0 ? $"登顶奖章 x{TowerClearMedalCount}\n" : "";
            return
                $"滑翔距离: {GlideDistanceFromTowerMeters:0}米\n" +
                $"(最远距离: {RecordGlideDistanceMeters:0}米)\n" +
                $"爬塔高度: {HeightMeters:0}米\n" +
                medalLine +
                $"金币 x{CoinsReward}\n" +
                (GemsReward > 0 ? $"钻石 x{GemsReward}\n" : "");
        }
    }
}

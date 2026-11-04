using Game.Core.Config;
using Game.DailySignIn;
using Game.Features.OnlineReward;
using Game.Save;
using Game.UI;
using UnityEngine;

namespace Game.UI.ViewModels
{
    public sealed class OnlineRewardScreenViewModel
    {
        public double AccumulatedSeconds;
        public int ClaimedCount;
        public string NextRemainingText;
        public bool HasClaimable;
        public bool AllClaimed;
        public bool ConfigMissing;

        public int CurrentTierIndex = -1;
        public float CurrentTierRequiredSeconds;
        public string CurrentTierFriendlyTime;
        public RewardDef CurrentReward;
        public Sprite CurrentRewardIcon;
        public long CurrentRewardAmount;
        public string StatusRichText;

        public static OnlineRewardScreenViewModel Build(string timeRichColorHex = "#7CFF3A")
        {
            var vm = new OnlineRewardScreenViewModel
            {
                AccumulatedSeconds = OnlineRewardService.AccumulatedSeconds,
                ClaimedCount = OnlineRewardService.ClaimedCount,
                NextRemainingText = OnlineRewardService.FormatRemainingForNext(),
                HasClaimable = OnlineRewardService.HasClaimableReward(),
            };

            var db = ConfigRegistry.OnlineReward;
            if (db == null || db.tiers == null || db.tiers.Count == 0)
            {
                vm.ConfigMissing = true;
                vm.StatusRichText = "在线奖励未配置";
                return vm;
            }

            var data = SaveService.Data;
            var claimed = data != null ? data.onlineRewardClaimedCount : 0;
            vm.AllClaimed = claimed >= db.tiers.Count;
            FillCurrentDisplay(vm, timeRichColorHex);
            return vm;
        }

        static void FillCurrentDisplay(OnlineRewardScreenViewModel vm, string timeColorHex)
        {
            var color = NormalizeHex(timeColorHex);

            if (vm.AllClaimed || !OnlineRewardService.TryGetNextTier(out var index, out var tier))
            {
                vm.StatusRichText = "全部奖励已领取";
                vm.CurrentTierIndex = -1;
                return;
            }

            vm.CurrentTierIndex = index;
            vm.CurrentTierRequiredSeconds = Mathf.Max(0f, tier.requiredOnlineSeconds);
            vm.CurrentTierFriendlyTime = OnlineRewardService.FormatFriendlyDuration(vm.CurrentTierRequiredSeconds);
            vm.CurrentReward = tier.reward;
            vm.CurrentRewardAmount = Mathf.Max(1, (int)tier.reward.amount);
            vm.CurrentRewardIcon = tier.rewardIcon != null
                ? tier.rewardIcon
                : RewardPresentation.TryResolveIcon(tier.reward);

            var coloredTime = $"<color={color}>{vm.CurrentTierFriendlyTime}</color>";
            if (vm.HasClaimable)
            {
                vm.StatusRichText = $"已在线 {coloredTime} 可领取奖励";
            }
            else
            {
                var remain = Mathf.Max(0f, vm.CurrentTierRequiredSeconds - (float)vm.AccumulatedSeconds);
                var remainFriendly = OnlineRewardService.FormatFriendlyDuration(remain);
                var coloredRemain = $"<color={color}>{remainFriendly}</color>";
                vm.StatusRichText = $"再在线 {coloredRemain} 可领取奖励";
            }
        }

        static string NormalizeHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return "#7CFF3A";
            hex = hex.Trim();
            if (hex[0] != '#')
                hex = "#" + hex;
            return hex;
        }
    }
}

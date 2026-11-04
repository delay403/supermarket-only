using Game.Core.Config;
using Game.DailySignIn;
using Game.Save;
using UnityEngine;

namespace Game.Features.OnlineReward
{
    /// <summary>
    /// 在线奖励：累计游戏时长解锁阶梯奖励。
    /// 由 <see cref="OnlineRewardTicker"/> 或面板 Tick 推进时长。
    /// </summary>
    public static class OnlineRewardService
    {
        static float s_unsavedSeconds;
        const float AutosaveIntervalSeconds = 30f;

        public static double AccumulatedSeconds =>
            SaveService.Data?.onlineRewardAccumulatedSeconds ?? 0d;

        public static int ClaimedCount =>
            SaveService.Data?.onlineRewardClaimedCount ?? 0;

        public static OnlineRewardDatabase Database => ConfigRegistry.OnlineReward;

        public static void Tick(float deltaSeconds)
        {
            if (SaveService.Data == null || deltaSeconds <= 0f)
                return;

            SaveService.Data.onlineRewardAccumulatedSeconds += deltaSeconds;
            s_unsavedSeconds += deltaSeconds;
            if (s_unsavedSeconds >= AutosaveIntervalSeconds)
            {
                s_unsavedSeconds = 0f;
                SaveService.Save();
            }
        }

        public static bool HasClaimableReward()
        {
            return TryGetNextClaimableTier(out _, out _);
        }

        /// <summary>下一档（无论是否已达领取条件）。全部领完时返回 false。</summary>
        public static bool TryGetNextTier(out int tierIndex, out OnlineRewardTierDef tier)
        {
            tierIndex = -1;
            tier = null;
            var db = Database;
            var data = SaveService.Data;
            if (db == null || db.tiers == null || data == null)
                return false;

            var next = Mathf.Clamp(data.onlineRewardClaimedCount, 0, db.tiers.Count);
            if (next >= db.tiers.Count)
                return false;

            var candidate = db.tiers[next];
            if (candidate == null)
                return false;

            tierIndex = next;
            tier = candidate;
            return true;
        }

        public static bool TryGetNextClaimableTier(out int tierIndex, out OnlineRewardTierDef tier)
        {
            if (!TryGetNextTier(out tierIndex, out tier))
                return false;

            var data = SaveService.Data;
            if (data.onlineRewardAccumulatedSeconds < Mathf.Max(0f, tier.requiredOnlineSeconds))
            {
                tierIndex = -1;
                tier = null;
                return false;
            }

            return true;
        }

        public static bool CanClaimTier(int tierIndex)
        {
            var db = Database;
            var data = SaveService.Data;
            if (db == null || db.tiers == null || data == null)
                return false;
            if (tierIndex != data.onlineRewardClaimedCount)
                return false;
            if (tierIndex < 0 || tierIndex >= db.tiers.Count)
                return false;

            var tier = db.tiers[tierIndex];
            if (tier == null)
                return false;

            return data.onlineRewardAccumulatedSeconds >= Mathf.Max(0f, tier.requiredOnlineSeconds);
        }

        /// <param name="multiplier">1=普通领取，2=双倍奖励。</param>
        public static bool TryClaimNext(out RewardDef reward, out OnlineRewardTierDef tier, int multiplier = 1)
        {
            reward = default;
            tier = null;
            if (!TryGetNextClaimableTier(out var index, out tier))
                return false;

            multiplier = Mathf.Max(1, multiplier);
            reward = tier.reward;

            for (var i = 0; i < multiplier; i++)
                RewardGrant.Apply(tier.reward);

            if (multiplier > 1 && IsStackableCurrency(tier.reward.type))
            {
                // Apply 循环已按次数发放；对外返回展示用的合计数量
                reward.amount = tier.reward.amount * multiplier;
            }

            SaveService.Data.onlineRewardClaimedCount = index + 1;
            SaveService.Save();
            return true;
        }

        static bool IsStackableCurrency(RewardType type) =>
            type == RewardType.Coins || type == RewardType.Gems || type == RewardType.Medals || type == RewardType.Pet;

        public static float GetTierProgress01(int tierIndex)
        {
            var db = Database;
            var data = SaveService.Data;
            if (db == null || db.tiers == null || data == null)
                return 0f;
            if (tierIndex < 0 || tierIndex >= db.tiers.Count)
                return 0f;

            var tier = db.tiers[tierIndex];
            if (tier == null)
                return 0f;

            var required = Mathf.Max(0.01f, tier.requiredOnlineSeconds);
            var prevRequired = 0f;
            if (tierIndex > 0)
            {
                var prev = db.tiers[tierIndex - 1];
                if (prev != null)
                    prevRequired = Mathf.Max(0f, prev.requiredOnlineSeconds);
            }

            var span = Mathf.Max(0.01f, required - prevRequired);
            var progressed = (float)data.onlineRewardAccumulatedSeconds - prevRequired;
            return Mathf.Clamp01(progressed / span);
        }

        public static string FormatRemainingForNext()
        {
            if (!TryGetNextTier(out _, out var tier))
                return "已领完";

            var data = SaveService.Data;
            var remain = Mathf.Max(0f, tier.requiredOnlineSeconds - (float)data.onlineRewardAccumulatedSeconds);
            if (remain <= 0f)
                return "可领取";

            return FormatFriendlyDuration(remain);
        }

        /// <summary>
        /// SO 秒数友好换算：≥3600 → N小时；≥60 → N分钟；否则 N秒。
        /// </summary>
        public static string FormatFriendlyDuration(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            if (seconds >= 3600f)
            {
                var hours = Mathf.Max(1, Mathf.RoundToInt(seconds / 3600f));
                return $"{hours}小时";
            }

            if (seconds >= 60f)
            {
                var minutes = Mathf.Max(1, Mathf.RoundToInt(seconds / 60f));
                return $"{minutes}分钟";
            }

            return $"{Mathf.CeilToInt(seconds)}秒";
        }

        /// <summary>旧版 mm:ss / hh:mm:ss（倒计时条等仍可用）。</summary>
        public static string FormatDuration(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var total = Mathf.CeilToInt(seconds);
            var m = total / 60;
            var s = total % 60;
            if (m >= 60)
            {
                var h = m / 60;
                m %= 60;
                return $"{h:D2}:{m:D2}:{s:D2}";
            }

            return $"{m:D2}:{s:D2}";
        }
    }
}

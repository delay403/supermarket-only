using System;
using Game.Core.Config;
using Game.Save;

namespace Game.Reward
{
    /// <summary>
    /// 限时 Buff（攀爬双倍速度 / 滑翔结算双倍金币），存档记录在 <see cref="PlayerSaveData"/>。
    /// 持续时长由 <see cref="GameBalanceConfig.rewardBuffDurationSeconds"/> 决定（秒）。
    /// </summary>
    public static class RewardBuffService
    {
        const double DefaultDurationSeconds = 120.0;

        public static double BuffDurationSeconds
        {
            get
            {
                var b = ConfigRegistry.Balance;
                if (b != null && b.rewardBuffDurationSeconds > 0.5f)
                    return b.rewardBuffDurationSeconds;
                return DefaultDurationSeconds;
            }
        }

        /// <summary>HUD 上 Buff 未激活时显示的「满额时长」文案（mm:ss）。</summary>
        public static string HintDurationMmSs => FormatSecondsAsMmSs(BuffDurationSeconds);

        public static bool IsClimbBuffActive()
        {
            var d = SaveService.Data;
            return d != null && IsTicksActive(d.climbBuffEndUtcTicks);
        }

        public static bool IsCoinsBuffActive()
        {
            var d = SaveService.Data;
            return d != null && IsTicksActive(d.coinsBuffEndUtcTicks);
        }

        public static double GetClimbBuffRemainingSeconds() =>
            SaveService.Data != null ? RemainingSeconds(SaveService.Data.climbBuffEndUtcTicks) : 0d;

        public static double GetCoinsBuffRemainingSeconds() =>
            SaveService.Data != null ? RemainingSeconds(SaveService.Data.coinsBuffEndUtcTicks) : 0d;

        public static void ActivateClimbBuff()
        {
            var d = SaveService.Data;
            if (d == null)
                return;
            d.climbBuffEndUtcTicks = DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(BuffDurationSeconds).Ticks;
        }

        public static void ActivateCoinsBuff()
        {
            var d = SaveService.Data;
            if (d == null)
                return;
            d.coinsBuffEndUtcTicks = DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(BuffDurationSeconds).Ticks;
        }

        public static string FormatRemainingMmSs(long endUtcTicks)
        {
            var sec = RemainingSeconds(endUtcTicks);
            if (sec <= 0d)
                return HintDurationMmSs;
            return FormatSecondsAsMmSs(sec);
        }

        public static string RewardDialogDurationLine =>
            BuffDurationSeconds >= 60d && Math.Abs(BuffDurationSeconds % 60d) < 0.5d
                ? $" {(int)(BuffDurationSeconds / 60d)} 分钟内"
                : $" {BuffDurationSeconds:0} 秒内";

        static string FormatSecondsAsMmSs(double totalSeconds)
        {
            var total = (int)Math.Floor(Math.Max(0d, totalSeconds));
            var m = total / 60;
            var s = total % 60;
            return $"{m:00}:{s:00}";
        }

        static bool IsTicksActive(long endUtcTicks) =>
            endUtcTicks > 0 && DateTime.UtcNow.Ticks < endUtcTicks;

        static double RemainingSeconds(long endUtcTicks)
        {
            if (endUtcTicks <= 0)
                return 0d;
            var left = new TimeSpan(endUtcTicks - DateTime.UtcNow.Ticks).TotalSeconds;
            return left > 0d ? left : 0d;
        }
    }
}

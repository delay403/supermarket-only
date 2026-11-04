using System;
using Game.Core.Config;
using Game.Save;

namespace Game.Fishing
{
    /// <summary>
    /// 自动钓鱼免费通行证（真实时间倒计时）与自动钓鱼模式开关。
    /// </summary>
    public static class AutoFishingService
    {
        public static event Action PassChanged;
        public static event Action ModeChanged;

        public static bool IsPassActive
        {
            get
            {
                var fishing = SaveService.Data?.fishing;
                return fishing != null && IsTicksActive(fishing.autoFishingEndUtcTicks);
            }
        }

        public static bool IsModeEnabled { get; private set; }

        public static double PassDurationSeconds
        {
            get
            {
                var bal = ConfigRegistry.Balance;
                return bal != null && bal.autoFishingPassDurationSeconds > 1f
                    ? bal.autoFishingPassDurationSeconds
                    : 1800d;
            }
        }

        public static double GetRemainingSeconds()
        {
            var fishing = SaveService.Data?.fishing;
            return fishing != null ? RemainingSeconds(fishing.autoFishingEndUtcTicks) : 0d;
        }

        public static string FormatRemainingMmSs(bool showHintWhenInactive = true)
        {
            if (!IsPassActive)
                return showHintWhenInactive ? FormatSecondsAsMmSs(PassDurationSeconds) : "00:00";

            var fishing = SaveService.Data?.fishing;
            return fishing != null
                ? FormatSecondsAsMmSs(RemainingSeconds(fishing.autoFishingEndUtcTicks))
                : "00:00";
        }

        public static void ActivatePass(bool autoEnableMode = false)
        {
            var fishing = SaveService.Data?.fishing;
            if (fishing == null)
                return;

            var now = DateTime.UtcNow.Ticks;
            var addTicks = TimeSpan.FromSeconds(PassDurationSeconds).Ticks;
            var currentEnd = fishing.autoFishingEndUtcTicks;
            var baseTicks = currentEnd > now ? currentEnd : now;
            fishing.autoFishingEndUtcTicks = baseTicks + addTicks;
            SaveService.Save();
            PassChanged?.Invoke();

            if (autoEnableMode)
                SetModeEnabled(true);
        }

        public static void SetModeEnabled(bool enabled)
        {
            if (IsModeEnabled == enabled)
                return;

            if (enabled && !IsPassActive)
                enabled = false;

            IsModeEnabled = enabled;
            ModeChanged?.Invoke();
        }

        public static void PauseMode() => SetModeEnabled(false);

        public static void TickPassExpiry()
        {
            if (!IsModeEnabled || IsPassActive)
                return;

            SetModeEnabled(false);
            PassChanged?.Invoke();
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

        static string FormatSecondsAsMmSs(double totalSeconds)
        {
            var total = (int)Math.Floor(Math.Max(0d, totalSeconds));
            var m = total / 60;
            var s = total % 60;
            return $"{m:00}:{s:00}";
        }
    }
}

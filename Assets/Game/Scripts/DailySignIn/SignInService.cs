using System;
using UnityEngine;
using Game.Core.Config;
using Game.Save;

namespace Game.DailySignIn
{
    public static class SignInService
    {
        /// <summary>UTC 日历日（仅兼容旧逻辑）；签到刷新请用 <see cref="GameDayIdLocal"/>。</summary>
        public static string TodayIso => DateTime.UtcNow.ToString("yyyy-MM-dd");

        /// <summary>本地「游戏日」（默认每日 5:00 切换），与转盘每日次数等同。</summary>
        public static string GameDayIdLocal => GameDayUtility.GetCurrentGameDayIdLocal();

        public static bool CanClaimToday()
        {
            if (SaveService.Data == null) return false;
            RefreshDailyFlags();
            return !SaveService.Data.signedInToday;
        }

        public static bool TryClaimToday(out RewardDef reward)
        {
            reward = default;
            if (SaveService.Data == null) return false;
            RefreshDailyFlags();
            if (SaveService.Data.signedInToday) return false;

            var db = ConfigRegistry.SignIn;
            if (db == null || db.days == null || db.days.Count == 0) return false;

            // Resolve day by index (0..6). If config is not ordered, we just clamp.
            var idx = Mathf.Clamp(SaveService.Data.signInDayIndex, 0, 6);
            SignInDayDef dayDef = null;
            foreach (var d in db.days)
            {
                if (d == null) continue;
                if (d.dayNumber == idx + 1) { dayDef = d; break; }
            }
            if (dayDef == null) dayDef = db.days[Mathf.Clamp(idx, 0, db.days.Count - 1)];
            reward = dayDef.reward;

            RewardGrant.Apply(reward);

            SaveService.Data.signedInToday = true;
            SaveService.Data.lastSignInDateIso = GameDayIdLocal;
            SaveService.Data.signInDayIndex = (SaveService.Data.signInDayIndex + 1) % 7;
            SaveService.Save();
            return true;
        }

        public static void RefreshDailyFlags()
        {
            if (SaveService.Data == null) return;
            var today = GameDayIdLocal;
            if (SaveService.Data.lastSignInDateIso == today)
            {
                // keep signedInToday as is
                return;
            }

            // New day: allow sign-in.
            SaveService.Data.signedInToday = false;
        }

    }
}


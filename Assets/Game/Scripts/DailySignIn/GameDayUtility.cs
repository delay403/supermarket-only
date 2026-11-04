using System;

namespace Game.DailySignIn
{
    /// <summary>
    /// 以本地时间的「游戏日」为准：每日 <paramref name="resetHour"/> 点前仍算作前一天（常用于凌晨 5:00 刷新签到等）。
    /// </summary>
    public static class GameDayUtility
    {
        public const int DefaultResetHour = 5;

        public static string GetCurrentGameDayIdLocal(int resetHour = DefaultResetHour)
        {
            var now = DateTime.Now;
            var day = now.Date;
            if (now.Hour < resetHour)
                day = day.AddDays(-1);
            return day.ToString("yyyy-MM-dd");
        }
    }
}

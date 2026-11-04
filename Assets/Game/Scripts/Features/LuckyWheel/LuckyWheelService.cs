using Game.DailySignIn;
using Game.Save;

namespace Game.Features.LuckyWheel
{
    public static class LuckyWheelService
    {
        public const int MaxFreeSpinsPerGameDay = 3;

        public static void RefreshDayFlag()
        {
            if (SaveService.Data == null) return;
            var day = GameDayUtility.GetCurrentGameDayIdLocal();
            if (SaveService.Data.luckyWheelGameDayId != day)
            {
                SaveService.Data.luckyWheelGameDayId = day;
                SaveService.Data.luckyWheelFreeSpinsToday = 0;
                SaveService.Save();
            }
        }

        public static int FreeSpinsRemainingToday()
        {
            RefreshDayFlag();
            var used = SaveService.Data?.luckyWheelFreeSpinsToday ?? 0;
            return MaxFreeSpinsPerGameDay - used;
        }

        public static bool TryConsumeFreeSpin()
        {
            if (SaveService.Data == null) return false;
            RefreshDayFlag();
            if (SaveService.Data.luckyWheelFreeSpinsToday >= MaxFreeSpinsPerGameDay)
                return false;
            SaveService.Data.luckyWheelFreeSpinsToday++;
            SaveService.Save();
            return true;
        }
    }
}

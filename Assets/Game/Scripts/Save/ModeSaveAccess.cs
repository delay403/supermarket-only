using System;

namespace Game.Save
{
    public static class ModeSaveAccess
    {
        public static long Coins => SaveService.Data?.coins ?? 0L;
        public static long Gems => SaveService.Data?.gems ?? 0L;

        public static void AddCoins(long amount)
        {
            if (SaveService.Data == null || amount == 0L)
                return;
            SaveService.Data.coins = Math.Max(0L, SaveService.Data.coins + amount);
        }

        public static void AddGems(long amount)
        {
            if (SaveService.Data == null || amount == 0L)
                return;
            SaveService.Data.gems = Math.Max(0L, SaveService.Data.gems + amount);
        }
    }
}

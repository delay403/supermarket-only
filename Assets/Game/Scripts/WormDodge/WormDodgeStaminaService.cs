using System;
using Game.Save;
using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// 躲避虫子体力：上限 5，每 10 分钟恢复 1 点，开局消耗 1 点。
    /// </summary>
    public static class WormDodgeStaminaService
    {
        public const int MaxStamina = WormDodgeModeSaveData.DefaultMaxStamina;
        public const int RegenIntervalMinutes = 10;
        public const int FreeStaminaReward = 5;
        public static readonly long RegenIntervalTicks = TimeSpan.TicksPerMinute * RegenIntervalMinutes;

        public static event Action Changed;

        public static int Current
        {
            get
            {
                RefreshRegen();
                return SaveService.Data?.wormDodge?.stamina ?? MaxStamina;
            }
        }

        public static TimeSpan TimeUntilNextRegen
        {
            get
            {
                RefreshRegen();
                var wd = SaveService.Data?.wormDodge;
                if (wd == null || wd.stamina >= MaxStamina || wd.staminaLastRegenUtcTicks <= 0L)
                    return TimeSpan.Zero;

                var elapsed = DateTime.UtcNow.Ticks - wd.staminaLastRegenUtcTicks;
                var remaining = RegenIntervalTicks - elapsed;
                return remaining > 0L ? TimeSpan.FromTicks(remaining) : TimeSpan.Zero;
            }
        }

        public static void RefreshRegen()
        {
            var save = SaveService.Data;
            if (save == null)
                return;

            save.EnsureWormDodgeSectionInitialized();
            var wd = save.wormDodge;
            wd.MigrateDefaultsIfNeeded();

            if (wd.stamina >= MaxStamina)
            {
                wd.staminaLastRegenUtcTicks = 0L;
                return;
            }

            var now = DateTime.UtcNow.Ticks;
            if (wd.staminaLastRegenUtcTicks <= 0L)
            {
                wd.staminaLastRegenUtcTicks = now;
                return;
            }

            var gained = 0;
            while (wd.stamina < MaxStamina && now - wd.staminaLastRegenUtcTicks >= RegenIntervalTicks)
            {
                wd.staminaLastRegenUtcTicks += RegenIntervalTicks;
                wd.stamina++;
                gained++;
            }

            if (gained > 0)
            {
                SaveService.Save();
                Changed?.Invoke();
            }
        }

        public static bool CanStartRun() => Current >= 1;

        public static bool IsFull => Current >= MaxStamina;

        /// <summary>领取后增加体力（不超过上限）。</summary>
        public static bool TryGrantStamina(int amount = FreeStaminaReward)
        {
            RefreshRegen();
            var save = SaveService.Data;
            if (save?.wormDodge == null || amount <= 0)
                return false;

            if (save.wormDodge.stamina >= MaxStamina)
                return false;

            var before = save.wormDodge.stamina;
            save.wormDodge.stamina = Mathf.Min(MaxStamina, save.wormDodge.stamina + amount);
            if (save.wormDodge.stamina >= MaxStamina)
                save.wormDodge.staminaLastRegenUtcTicks = 0L;
            else if (save.wormDodge.staminaLastRegenUtcTicks <= 0L)
                save.wormDodge.staminaLastRegenUtcTicks = DateTime.UtcNow.Ticks;

            if (save.wormDodge.stamina <= before)
                return false;

            SaveService.Save();
            Changed?.Invoke();
            return true;
        }

        public static bool TryConsumeForRun()
        {
            RefreshRegen();
            var save = SaveService.Data;
            if (save?.wormDodge == null || save.wormDodge.stamina < 1)
                return false;

            save.wormDodge.stamina--;
            if (save.wormDodge.stamina < MaxStamina && save.wormDodge.staminaLastRegenUtcTicks <= 0L)
                save.wormDodge.staminaLastRegenUtcTicks = DateTime.UtcNow.Ticks;

            SaveService.Save();
            Changed?.Invoke();
            return true;
        }
    }
}

using System;
using Game.Save;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>超市收纳体力：上限与恢复间隔读配置，逻辑对齐躲避虫子模式。</summary>
    public static class SupermarketOrganizeStaminaService
    {
        public static event Action Changed;

        static SupermarketOrganizeHomeConfig _config;

        static SupermarketOrganizeHomeConfig Config =>
            _config ??= SupermarketOrganizeHomeConfig.ResolveRuntime();

        public static int MaxStamina => Mathf.Max(1, Config.maxStamina);

        public static int FreeStaminaReward => Mathf.Max(1, Config.staminaReward);

        public static bool IsEnabled => Config != null && Config.staminaEnabled;

        public static int RegenIntervalMinutes => Mathf.Max(1, Config.staminaRegenIntervalMinutes);

        static long RegenIntervalTicks =>
            TimeSpan.TicksPerMinute * RegenIntervalMinutes;

        public static int Current
        {
            get
            {
                RefreshRegen();
                return SaveService.Data?.supermarketOrganize?.stamina ?? MaxStamina;
            }
        }

        public static bool IsFull => Current >= MaxStamina;

        public static TimeSpan TimeUntilNextRegen
        {
            get
            {
                RefreshRegen();
                var data = SaveService.Data?.supermarketOrganize;
                if (data == null || data.stamina >= MaxStamina || data.staminaLastRegenUtcTicks <= 0L)
                    return TimeSpan.Zero;

                var elapsed = DateTime.UtcNow.Ticks - data.staminaLastRegenUtcTicks;
                var remaining = RegenIntervalTicks - elapsed;
                return remaining > 0L ? TimeSpan.FromTicks(remaining) : TimeSpan.Zero;
            }
        }

        public static void RefreshRegen()
        {
            if (!IsEnabled)
                return;

            var save = SaveService.Data;
            if (save == null)
                return;

            save.EnsureSupermarketOrganizeSectionInitialized();
            var data = save.supermarketOrganize;
            data.MigrateDefaultsIfNeeded();

            if (data.stamina >= MaxStamina)
            {
                data.staminaLastRegenUtcTicks = 0L;
                return;
            }

            var now = DateTime.UtcNow.Ticks;
            if (data.staminaLastRegenUtcTicks <= 0L)
            {
                data.staminaLastRegenUtcTicks = now;
                return;
            }

            var gained = 0;
            while (data.stamina < MaxStamina && now - data.staminaLastRegenUtcTicks >= RegenIntervalTicks)
            {
                data.staminaLastRegenUtcTicks += RegenIntervalTicks;
                data.stamina++;
                gained++;
            }

            if (gained > 0)
            {
                SaveService.Save();
                Changed?.Invoke();
            }
        }

        public static bool TryConsumeForRun(int amount = 1)
        {
            if (!IsEnabled)
                return true;

            RefreshRegen();
            var save = SaveService.Data;
            if (save == null)
                return false;

            save.EnsureSupermarketOrganizeSectionInitialized();
            var data = save.supermarketOrganize;
            if (data.stamina < amount)
                return false;

            data.stamina -= amount;
            if (data.stamina < MaxStamina && data.staminaLastRegenUtcTicks <= 0L)
                data.staminaLastRegenUtcTicks = DateTime.UtcNow.Ticks;

            SaveService.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>领取后增加体力（不超过上限）。</summary>
        public static bool TryGrantStamina(int amount = 0)
        {
            if (!IsEnabled)
                return false;

            if (amount <= 0)
                amount = FreeStaminaReward;

            RefreshRegen();
            var save = SaveService.Data;
            if (save?.supermarketOrganize == null || amount <= 0)
                return false;

            var data = save.supermarketOrganize;
            if (data.stamina >= MaxStamina)
                return false;

            var before = data.stamina;
            data.stamina = Mathf.Min(MaxStamina, data.stamina + amount);
            if (data.stamina >= MaxStamina)
                data.staminaLastRegenUtcTicks = 0L;
            else if (data.staminaLastRegenUtcTicks <= 0L)
                data.staminaLastRegenUtcTicks = DateTime.UtcNow.Ticks;

            if (data.stamina <= before)
                return false;

            SaveService.Save();
            Changed?.Invoke();
            return true;
        }

        public static void AddStamina(int amount)
        {
            if (amount <= 0)
                return;

            TryGrantStamina(amount);
        }
    }
}

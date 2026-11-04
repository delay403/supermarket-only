using Game.Core.Config;
using Game.DailySignIn;
using Game.Save;
using UnityEngine;

namespace Game.Features.Treadmill
{
    public static class TreadmillService
    {
        public const int Tier3UnlockRequiredDefault = 3;

        public static void RefreshDailyFlags()
        {
            if (SaveService.Data == null) return;
            var day = GameDayUtility.GetCurrentGameDayIdLocal();
            if (SaveService.Data.treadmillGameDayId == day)
                return;

            SaveService.Data.treadmillGameDayId = day;
            SaveService.Data.treadmillTier2UnlockedToday = false;
            SaveService.Data.treadmillTier3UnlockedToday = false;
            SaveService.Data.treadmillTier3Progress = 0;
            SaveService.Save();
        }

        public static float GetPermanentClimbSpeedBonus()
        {
            RefreshDailyFlags();
            return SaveService.Data?.treadmillPermanentClimbSpeedBonus ?? 0f;
        }

        public static bool IsTierAvailableToday(TreadmillTier tier)
        {
            RefreshDailyFlags();
            switch (tier)
            {
                case TreadmillTier.Basic:
                    return true;
                case TreadmillTier.FreeDaily:
                    return SaveService.Data != null && SaveService.Data.treadmillTier2UnlockedToday;
                case TreadmillTier.PremiumDaily:
                    return SaveService.Data != null && SaveService.Data.treadmillTier3UnlockedToday;
                default:
                    return false;
            }
        }

        public static float GetBonusPerInterval(TreadmillTier tier)
        {
            var bal = ConfigRegistry.Balance;
            switch (tier)
            {
                case TreadmillTier.FreeDaily:
                    return bal != null ? bal.treadmillTier2BonusPerInterval : 0.02f;
                case TreadmillTier.PremiumDaily:
                    return bal != null ? bal.treadmillTier3BonusPerInterval : 0.03f;
                default:
                    return bal != null ? bal.treadmillTier1BonusPerInterval : 0.01f;
            }
        }

        public static float GetIntervalSeconds()
        {
            var bal = ConfigRegistry.Balance;
            return bal != null && bal.treadmillIntervalSeconds > 0.1f
                ? bal.treadmillIntervalSeconds
                : 10f;
        }

        /// <summary>该档位跑步机持续锻炼 1 分钟可获得的永久爬塔速度加成（m/s）。</summary>
        public static float GetBonusPerMinute(TreadmillTier tier)
        {
            var interval = GetIntervalSeconds();
            if (interval <= 0.01f)
                return 0f;

            return GetBonusPerInterval(tier) * (60f / interval);
        }

        public static int GetTier3UnlockRequired()
        {
            var bal = ConfigRegistry.Balance;
            return bal != null && bal.treadmillTier3UnlockRequired > 0
                ? bal.treadmillTier3UnlockRequired
                : Tier3UnlockRequiredDefault;
        }

        public static long GetTier3GemUnlockCost()
        {
            var bal = ConfigRegistry.Balance;
            return bal != null && bal.treadmillTier3GemUnlockCost > 0
                ? bal.treadmillTier3GemUnlockCost
                : 50L;
        }

        public static int GetTier3Progress()
        {
            RefreshDailyFlags();
            return SaveService.Data?.treadmillTier3Progress ?? 0;
        }

        public static bool TryUnlockTier2()
        {
            if (SaveService.Data == null) return false;
            RefreshDailyFlags();
            if (SaveService.Data.treadmillTier2UnlockedToday)
                return true;

            SaveService.Data.treadmillTier2UnlockedToday = true;
            SaveService.Save();
            return true;
        }

        public static bool TryUnlockTier3WithGems()
        {
            if (SaveService.Data == null) return false;
            RefreshDailyFlags();
            if (SaveService.Data.treadmillTier3UnlockedToday)
                return true;

            var cost = GetTier3GemUnlockCost();
            if (SaveService.Data.gems < cost)
                return false;

            SaveService.Data.gems -= cost;
            SaveService.Data.treadmillTier3UnlockedToday = true;
            SaveService.Data.treadmillTier3Progress = 0;
            SaveService.Save();
            Game.Economy.EconomyDisplayEvents.Raise();
            return true;
        }

        /// <summary>每次领取后累计进度；满额则解锁第三档（当日）。</summary>
        public static bool TryRecordTier3Progress(out bool unlockedNow)
        {
            unlockedNow = false;
            if (SaveService.Data == null) return false;
            RefreshDailyFlags();
            if (SaveService.Data.treadmillTier3UnlockedToday)
            {
                unlockedNow = true;
                return true;
            }

            var required = GetTier3UnlockRequired();
            SaveService.Data.treadmillTier3Progress = Mathf.Min(
                required,
                SaveService.Data.treadmillTier3Progress + 1);

            if (SaveService.Data.treadmillTier3Progress >= required)
            {
                SaveService.Data.treadmillTier3UnlockedToday = true;
                SaveService.Data.treadmillTier3Progress = 0;
                unlockedNow = true;
            }

            SaveService.Save();
            return true;
        }

        public static float AddPermanentClimbSpeedBonus(float delta)
        {
            if (SaveService.Data == null || delta <= 0f)
                return GetPermanentClimbSpeedBonus();

            SaveService.Data.treadmillPermanentClimbSpeedBonus += delta;
            SaveService.Save();
            return SaveService.Data.treadmillPermanentClimbSpeedBonus;
        }

        public static float GetInstantRewardDurationSeconds()
        {
            var bal = ConfigRegistry.Balance;
            var minutes = bal != null && bal.treadmillInstantRewardMinutes > 0.1f
                ? bal.treadmillInstantRewardMinutes
                : 15f;
            return minutes * 60f;
        }

        public static float GetInstantRewardMinutes()
        {
            var bal = ConfigRegistry.Balance;
            return bal != null && bal.treadmillInstantRewardMinutes > 0.1f
                ? bal.treadmillInstantRewardMinutes
                : 15f;
        }

        public static float ComputeSessionBonus(TreadmillTier tier, float durationSeconds)
        {
            if (durationSeconds <= 0f)
                return 0f;

            var interval = GetIntervalSeconds();
            var perTick = GetBonusPerInterval(tier);
            var tickCount = Mathf.FloorToInt(durationSeconds / interval);
            return tickCount * perTick;
        }

        public static float GrantInstantReward(TreadmillTier tier)
        {
            var bonus = ComputeSessionBonus(tier, GetInstantRewardDurationSeconds());
            if (bonus <= 0f)
                return 0f;

            AddPermanentClimbSpeedBonus(bonus);
            return bonus;
        }

        public static string GetTierDisplayName(TreadmillTier tier) => tier switch
        {
            TreadmillTier.FreeDaily => "进阶跑步机",
            TreadmillTier.PremiumDaily => "高级跑步机",
            _ => "基础跑步机",
        };
    }
}

using System;
using Game.Collectibles.Stats;
using Game.Core.Config;
using Game.DailySignIn;
using Game.Economy;
using Game.Save;
using UnityEngine;

namespace Game.Features.StrengthTraining
{
    public static class StrengthTrainingService
    {
        public const int Tier3UnlockRequiredDefault = 3;

        public const string LegacyBasicGroundId = "training_basic";
        public const string LegacyFreeGroundId = "training_ad";
        public const string LegacyPremiumGroundId = "training_premium";

        /// <summary>基础力量增加时触发（训练、奖励等）。</summary>
        public static event Action<float> BaseStrengthAdded;

        static FishingModeSaveData Fishing =>
            SaveService.Data?.fishing;

        public static void RefreshDailyFlags()
        {
            if (Fishing == null) return;
            var day = GameDayUtility.GetCurrentGameDayIdLocal();
            if (Fishing.strengthTrainingGameDayId == day)
                return;

            Fishing.strengthTrainingGameDayId = day;
            Fishing.strengthTrainingTier2UnlockedToday = false;
            Fishing.strengthTrainingTier3UnlockedToday = false;
            Fishing.strengthTrainingTier3Progress = 0;
            Fishing.strengthTrainingUnlockedGroundIdsToday?.Clear();
            Fishing.strengthTrainingGroundProgressToday?.Clear();
            SaveService.Save();
        }

        public static float GetBaseStrength()
        {
            RefreshDailyFlags();
            return FishingStatsAggregator.ComputeBaseStrength();
        }

        public static StrengthTrainingGroundDef ResolveGroundDef(string groundId)
        {
            if (TryGetDatabaseDef(groundId, out var def))
                return def;

            return CreateLegacyFallbackDef(NormalizeGroundId(groundId));
        }

        public static bool IsGroundAvailableToday(string groundId)
        {
            RefreshDailyFlags();
            var def = ResolveGroundDef(groundId);
            if (def == null || Fishing == null)
                return false;

            switch (def.unlockPolicy)
            {
                case StrengthTrainingUnlockPolicy.AlwaysOpen:
                    return true;
                default:
                    return IsGroundUnlockedToday(def.groundId);
            }
        }

        public static bool IsTierAvailableToday(StrengthTrainingTier tier) =>
            IsGroundAvailableToday(LegacyTierToGroundId(tier));

        public static float GetBonusPerInterval(string groundId)
        {
            var def = ResolveGroundDef(groundId);
            if (def != null && def.bonusPerInterval >= 0f)
                return def.bonusPerInterval;

            return GetLegacyTierBonus(LegacyGroundIdToTier(groundId));
        }

        public static float GetBonusPerInterval(StrengthTrainingTier tier) =>
            GetBonusPerInterval(LegacyTierToGroundId(tier));

        public static float GetAnimSpeedForGround(string groundId)
        {
            var def = ResolveGroundDef(groundId);
            if (def != null && def.animSpeedOverride > 0.01f)
                return def.animSpeedOverride;

            var bal = ConfigRegistry.Balance;
            return bal != null && bal.strengthTrainingAnimSpeed > 0.01f
                ? bal.strengthTrainingAnimSpeed
                : 5f;
        }

        public static float GetIntervalSeconds()
        {
            var bal = ConfigRegistry.Balance;
            return bal != null && bal.strengthTrainingIntervalSeconds > 0.1f
                ? bal.strengthTrainingIntervalSeconds
                : 10f;
        }

        public static bool TryUnlockGroundFree(string groundId)
        {
            if (Fishing == null) return false;
            RefreshDailyFlags();
            var def = ResolveGroundDef(groundId);
            if (def == null) return false;

            if (IsGroundUnlockedToday(def.groundId))
                return true;

            switch (def.unlockPolicy)
            {
                case StrengthTrainingUnlockPolicy.FreeOncePerDay:
                    MarkGroundUnlockedToday(def.groundId);
                    SaveService.Save();
                    return true;
                case StrengthTrainingUnlockPolicy.ProgressPerDay:
                    return TryRecordGroundProgress(def.groundId, def.claimsRequired, out _);
                default:
                    return false;
            }
        }

        public static bool TryUnlockGroundWithGems(string groundId)
        {
            if (SaveService.Data == null || Fishing == null) return false;
            RefreshDailyFlags();
            var def = ResolveGroundDef(groundId);
            if (def == null || def.unlockPolicy != StrengthTrainingUnlockPolicy.GemOncePerDay)
                return false;

            if (IsGroundUnlockedToday(def.groundId))
                return true;

            var cost = def.gemUnlockCost > 0 ? def.gemUnlockCost : GetTier3GemUnlockCost();
            if (SaveService.Data.gems < cost)
                return false;

            SaveService.Data.gems -= cost;
            MarkGroundUnlockedToday(def.groundId);
            SaveService.Save();
            EconomyDisplayEvents.Raise();
            return true;
        }

        public static bool TryRecordGroundProgress(string groundId, int claimsRequired, out bool unlockedNow)
        {
            unlockedNow = false;
            if (Fishing == null) return false;
            RefreshDailyFlags();

            var id = NormalizeGroundId(groundId);
            if (IsGroundUnlockedToday(id))
            {
                unlockedNow = true;
                return true;
            }

            var required = Mathf.Max(1, claimsRequired);
            var progress = GetGroundProgressInternal(id) + 1;
            SetGroundProgress(id, Mathf.Min(required, progress));

            if (progress >= required)
            {
                MarkGroundUnlockedToday(id);
                SetGroundProgress(id, 0);
                unlockedNow = true;
            }

            SaveService.Save();
            return true;
        }

        public static bool TryUnlockTier2() => TryUnlockGroundFree(LegacyFreeGroundId);

        public static bool TryUnlockTier3WithGems() => TryUnlockGroundWithGems(LegacyPremiumGroundId);

        public static bool TryRecordTier3Progress(out bool unlockedNow)
        {
            var def = ResolveGroundDef(LegacyPremiumGroundId);
            var required = def != null ? def.claimsRequired : GetTier3UnlockRequired();
            return TryRecordGroundProgress(LegacyPremiumGroundId, required, out unlockedNow);
        }

        public static int GetTier3UnlockRequired()
        {
            if (TryGetDatabaseDef(LegacyPremiumGroundId, out var def) && def.claimsRequired > 0)
                return def.claimsRequired;

            return ReadBalanceTier3UnlockRequired();
        }

        public static int GetGroundClaimsRequired(string groundId)
        {
            var def = ResolveGroundDef(groundId);
            return def != null && def.claimsRequired > 0 ? def.claimsRequired : 1;
        }

        public static int GetGroundProgress(string groundId)
        {
            RefreshDailyFlags();
            if (Fishing == null)
                return 0;
            return GetGroundProgressInternal(groundId);
        }

        public static long GetGroundGemUnlockCost(string groundId)
        {
            var def = ResolveGroundDef(groundId);
            if (def != null && def.gemUnlockCost > 0)
                return def.gemUnlockCost;
            return GetTier3GemUnlockCost();
        }

        public static string GetGroundDisplayName(string groundId)
        {
            var def = ResolveGroundDef(groundId);
            return def != null && !string.IsNullOrWhiteSpace(def.displayName)
                ? def.displayName
                : NormalizeGroundId(groundId);
        }

        public static long GetTier3GemUnlockCost()
        {
            if (TryGetDatabaseDef(LegacyPremiumGroundId, out var def) && def.gemUnlockCost > 0)
                return def.gemUnlockCost;

            return ReadBalanceTier3GemUnlockCost();
        }

        public static float AddBaseStrength(float delta)
        {
            if (Fishing == null || delta <= 0f)
                return GetBaseStrength();

            Fishing.baseStrength += delta;
            SaveService.Save();
            BaseStrengthAdded?.Invoke(delta);
            return Fishing.baseStrength;
        }

        static bool IsGroundUnlockedToday(string groundId)
        {
            var id = NormalizeGroundId(groundId);
            if (Fishing.strengthTrainingUnlockedGroundIdsToday != null &&
                Fishing.strengthTrainingUnlockedGroundIdsToday.Contains(id))
                return true;

            return LegacyUnlockFallback(id);
        }

        static void MarkGroundUnlockedToday(string groundId)
        {
            var id = NormalizeGroundId(groundId);
            if (Fishing.strengthTrainingUnlockedGroundIdsToday == null)
                Fishing.strengthTrainingUnlockedGroundIdsToday = new System.Collections.Generic.List<string>();
            if (!Fishing.strengthTrainingUnlockedGroundIdsToday.Contains(id))
                Fishing.strengthTrainingUnlockedGroundIdsToday.Add(id);

            SyncLegacyUnlockFlagsFromGroundId(id, unlocked: true);
        }

        static int GetGroundProgressInternal(string groundId)
        {
            var id = NormalizeGroundId(groundId);
            var list = Fishing.strengthTrainingGroundProgressToday;
            if (list == null) return 0;
            for (var i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.groundId == id)
                    return e.unlockProgress;
            }

            if (id == LegacyPremiumGroundId)
                return Fishing.strengthTrainingTier3Progress;

            return 0;
        }

        static void SetGroundProgress(string groundId, int progress)
        {
            var id = NormalizeGroundId(groundId);
            if (Fishing.strengthTrainingGroundProgressToday == null)
                Fishing.strengthTrainingGroundProgressToday =
                    new System.Collections.Generic.List<FishingModeSaveData.StrengthTrainingGroundProgressEntry>();

            for (var i = 0; i < Fishing.strengthTrainingGroundProgressToday.Count; i++)
            {
                var e = Fishing.strengthTrainingGroundProgressToday[i];
                if (e.groundId != id) continue;
                e.unlockProgress = progress;
                Fishing.strengthTrainingGroundProgressToday[i] = e;
                SyncLegacyProgressFromGroundId(id, progress);
                return;
            }

            Fishing.strengthTrainingGroundProgressToday.Add(new FishingModeSaveData.StrengthTrainingGroundProgressEntry
            {
                groundId = id,
                unlockProgress = progress,
            });
            SyncLegacyProgressFromGroundId(id, progress);
        }

        static bool LegacyUnlockFallback(string groundId)
        {
            if (groundId == LegacyFreeGroundId)
                return Fishing.strengthTrainingTier2UnlockedToday;
            if (groundId == LegacyPremiumGroundId)
                return Fishing.strengthTrainingTier3UnlockedToday;
            return false;
        }

        static void SyncLegacyUnlockFlagsFromGroundId(string groundId, bool unlocked)
        {
            if (!unlocked) return;
            if (groundId == LegacyFreeGroundId)
                Fishing.strengthTrainingTier2UnlockedToday = true;
            else if (groundId == LegacyPremiumGroundId)
                Fishing.strengthTrainingTier3UnlockedToday = true;
        }

        static void SyncLegacyProgressFromGroundId(string groundId, int progress)
        {
            if (groundId == LegacyPremiumGroundId)
                Fishing.strengthTrainingTier3Progress = progress;
        }

        static float GetLegacyTierBonus(StrengthTrainingTier tier)
        {
            var bal = ConfigRegistry.Balance;
            switch (tier)
            {
                case StrengthTrainingTier.FreeDaily:
                    return bal != null ? bal.strengthTrainingTier2BonusPerInterval : 0.5f;
                case StrengthTrainingTier.PremiumDaily:
                    return bal != null ? bal.strengthTrainingTier3BonusPerInterval : 1f;
                default:
                    return bal != null ? bal.strengthTrainingTier1BonusPerInterval : 0.2f;
            }
        }

        static StrengthTrainingGroundDef CreateLegacyFallbackDef(string groundId)
        {
            var tier = LegacyGroundIdToTier(groundId);
            return new StrengthTrainingGroundDef
            {
                groundId = groundId,
                displayName = tier switch
                {
                    StrengthTrainingTier.FreeDaily => "免费训练场",
                    StrengthTrainingTier.PremiumDaily => "高级训练场",
                    _ => "基础训练场",
                },
                bonusPerInterval = GetLegacyTierBonus(tier),
                unlockPolicy = tier switch
                {
                    StrengthTrainingTier.FreeDaily => StrengthTrainingUnlockPolicy.FreeOncePerDay,
                    StrengthTrainingTier.PremiumDaily => StrengthTrainingUnlockPolicy.GemOncePerDay,
                    _ => StrengthTrainingUnlockPolicy.AlwaysOpen,
                },
                gemUnlockCost = tier == StrengthTrainingTier.PremiumDaily
                    ? ReadBalanceTier3GemUnlockCost()
                    : 0,
                claimsRequired = tier == StrengthTrainingTier.PremiumDaily
                    ? ReadBalanceTier3UnlockRequired()
                    : 1,
            };
        }

        static bool TryGetDatabaseDef(string groundId, out StrengthTrainingGroundDef def)
        {
            def = null;
            var db = ConfigRegistry.StrengthTrainingGrounds;
            return db != null && db.TryGet(NormalizeGroundId(groundId), out def) && def != null;
        }

        static int ReadBalanceTier3UnlockRequired()
        {
            var bal = ConfigRegistry.Balance;
            return bal != null && bal.strengthTrainingTier3UnlockRequired > 0
                ? bal.strengthTrainingTier3UnlockRequired
                : Tier3UnlockRequiredDefault;
        }

        static long ReadBalanceTier3GemUnlockCost()
        {
            var bal = ConfigRegistry.Balance;
            return bal != null && bal.strengthTrainingTier3GemUnlockCost > 0
                ? bal.strengthTrainingTier3GemUnlockCost
                : 50L;
        }

        public static string LegacyTierToGroundId(StrengthTrainingTier tier) => tier switch
        {
            StrengthTrainingTier.FreeDaily => LegacyFreeGroundId,
            StrengthTrainingTier.PremiumDaily => LegacyPremiumGroundId,
            _ => LegacyBasicGroundId,
        };

        static StrengthTrainingTier LegacyGroundIdToTier(string groundId) => groundId switch
        {
            LegacyFreeGroundId => StrengthTrainingTier.FreeDaily,
            LegacyPremiumGroundId => StrengthTrainingTier.PremiumDaily,
            _ => StrengthTrainingTier.Basic,
        };

        static string NormalizeGroundId(string groundId) =>
            string.IsNullOrWhiteSpace(groundId) ? LegacyBasicGroundId : groundId.Trim();
    }
}

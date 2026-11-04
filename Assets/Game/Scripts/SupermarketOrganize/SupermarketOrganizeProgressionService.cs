using System;
using Game.Economy;
using Game.Save;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    public static class SupermarketOrganizeProgressionService
    {
        public static event Action<SupermarketSkillId, int> SkillUpgraded;
        public static event Action<SupermarketAttributeId, int> AttributeUpgraded;

        static SupermarketOrganizeProgressionConfig Config =>
            SupermarketOrganizeProgressionConfig.ResolveRuntime();

        static SupermarketOrganizeModeSaveData Save =>
            SaveService.Data?.supermarketOrganize;

        public static int GetSkillLevel(SupermarketSkillId skillId)
        {
            var save = Save;
            if (save == null)
                return 0;

            return skillId switch
            {
                SupermarketSkillId.Scanner => Mathf.Max(0, save.scannerSkillLevel),
                SupermarketSkillId.Magnet => Mathf.Max(0, save.magnetSkillLevel),
                SupermarketSkillId.XRay => Mathf.Max(0, save.xRaySkillLevel),
                SupermarketSkillId.AutoStocker => Mathf.Max(0, save.autoStockerSkillLevel),
                _ => 0,
            };
        }

        public static int GetAttributeLevel(SupermarketAttributeId attributeId)
        {
            var save = Save;
            if (save == null)
                return 0;

            return attributeId switch
            {
                SupermarketAttributeId.PickupRange => Mathf.Max(0, save.pickupRangeLevel),
                SupermarketAttributeId.MoveSpeed => Mathf.Max(0, save.moveSpeedLevel),
                SupermarketAttributeId.HandleCapacity => Mathf.Max(0, save.handleCapacityLevel),
                _ => 0,
            };
        }

        public static int GetSkillMaxLevel(SupermarketSkillId skillId)
        {
            var config = Config;
            if (config == null)
                return 0;

            return skillId switch
            {
                SupermarketSkillId.Scanner => Mathf.Max(0, config.scannerCooldownByLevel.Length - 1),
                SupermarketSkillId.Magnet => Mathf.Max(0, config.magnetCooldownByLevel.Length - 1),
                SupermarketSkillId.XRay => Mathf.Max(0, config.xRayCooldownByLevel.Length - 1),
                SupermarketSkillId.AutoStocker => Mathf.Max(0, config.autoStockerCooldownByLevel.Length - 1),
                _ => 0,
            };
        }

        public static int GetAttributeMaxLevel(SupermarketAttributeId attributeId)
        {
            var config = Config;
            if (config == null)
                return 0;

            return attributeId switch
            {
                SupermarketAttributeId.PickupRange => Mathf.Max(0, config.pickupRangeByLevel.Length - 1),
                SupermarketAttributeId.MoveSpeed => Mathf.Max(0, config.moveSpeedBonusByLevel.Length - 1),
                SupermarketAttributeId.HandleCapacity => Mathf.Max(0, config.handleCapacityByLevel.Length - 1),
                _ => 0,
            };
        }

        public static float GetSkillCooldownSeconds(SupermarketSkillId skillId, int level = -1)
        {
            var config = Config;
            if (config == null)
                return 0f;

            if (level < 0)
                level = GetSkillLevel(skillId);

            return skillId switch
            {
                SupermarketSkillId.Scanner => Sample(config.scannerCooldownByLevel, level, 14f),
                SupermarketSkillId.Magnet => Sample(config.magnetCooldownByLevel, level, 17.5f),
                SupermarketSkillId.XRay => Sample(config.xRayCooldownByLevel, level, 16f),
                SupermarketSkillId.AutoStocker => Sample(config.autoStockerCooldownByLevel, level, 60f),
                _ => 0f,
            };
        }

        public static int GetHandleCapacity(int level = -1)
        {
            var config = Config;
            if (level < 0)
                level = GetAttributeLevel(SupermarketAttributeId.HandleCapacity);

            return config != null
                ? SampleInt(config.handleCapacityByLevel, level, 4)
                : 4;
        }

        public static float GetMoveSpeedBonus(int level = -1)
        {
            var config = Config;
            if (level < 0)
                level = GetAttributeLevel(SupermarketAttributeId.MoveSpeed);

            return config != null
                ? Sample(config.moveSpeedBonusByLevel, level, 0f)
                : 0f;
        }

        public static float GetPickupRange(int level = -1)
        {
            var config = Config;
            if (level < 0)
                level = GetAttributeLevel(SupermarketAttributeId.PickupRange);

            return config != null
                ? Sample(config.pickupRangeByLevel, level, 12f)
                : 12f;
        }

        public static long GetSkillUpgradeCost(SupermarketSkillId skillId)
        {
            var level = GetSkillLevel(skillId);
            if (level >= GetSkillMaxLevel(skillId))
                return 0;

            var config = Config;
            if (config == null)
                return 0;

            return skillId switch
            {
                SupermarketSkillId.Scanner => SampleInt(config.scannerUpgradeCosts, level, 0),
                SupermarketSkillId.Magnet => SampleInt(config.magnetUpgradeCosts, level, 0),
                SupermarketSkillId.XRay => SampleInt(config.xRayUpgradeCosts, level, 0),
                SupermarketSkillId.AutoStocker => SampleInt(config.autoStockerUpgradeCosts, level, 0),
                _ => 0,
            };
        }

        public static long GetAttributeUpgradeCost(SupermarketAttributeId attributeId)
        {
            var level = GetAttributeLevel(attributeId);
            if (level >= GetAttributeMaxLevel(attributeId))
                return 0;

            var config = Config;
            if (config == null)
                return 0;

            return attributeId switch
            {
                SupermarketAttributeId.PickupRange => SampleInt(config.pickupRangeUpgradeCosts, level, 0),
                SupermarketAttributeId.MoveSpeed => SampleInt(config.moveSpeedUpgradeCosts, level, 0),
                SupermarketAttributeId.HandleCapacity => SampleInt(config.handleCapacityUpgradeCosts, level, 0),
                _ => 0,
            };
        }

        public static bool CanAffordSkillUpgrade(SupermarketSkillId skillId) =>
            SaveService.Data != null && SaveService.Data.coins >= GetSkillUpgradeCost(skillId);

        public static bool CanAffordAttributeUpgrade(SupermarketAttributeId attributeId) =>
            SaveService.Data != null && SaveService.Data.coins >= GetAttributeUpgradeCost(attributeId);

        public static bool TryUpgradeSkill(SupermarketSkillId skillId)
        {
            var save = Save;
            if (save == null)
                return false;

            var level = GetSkillLevel(skillId);
            if (level >= GetSkillMaxLevel(skillId))
                return false;

            var cost = GetSkillUpgradeCost(skillId);
            if (cost <= 0 || SaveService.Data.coins < cost)
                return false;

            SaveService.Data.coins -= cost;
            switch (skillId)
            {
                case SupermarketSkillId.Scanner:
                    save.scannerSkillLevel++;
                    break;
                case SupermarketSkillId.Magnet:
                    save.magnetSkillLevel++;
                    break;
                case SupermarketSkillId.XRay:
                    save.xRaySkillLevel++;
                    break;
                case SupermarketSkillId.AutoStocker:
                    save.autoStockerSkillLevel++;
                    break;
            }

            SaveService.Save();
            EconomyDisplayEvents.Raise();
            SkillUpgraded?.Invoke(skillId, GetSkillLevel(skillId));
            return true;
        }

        public static bool TryUpgradeAttribute(SupermarketAttributeId attributeId)
        {
            var save = Save;
            if (save == null)
                return false;

            var level = GetAttributeLevel(attributeId);
            if (level >= GetAttributeMaxLevel(attributeId))
                return false;

            var cost = GetAttributeUpgradeCost(attributeId);
            if (cost <= 0 || SaveService.Data.coins < cost)
                return false;

            SaveService.Data.coins -= cost;
            switch (attributeId)
            {
                case SupermarketAttributeId.PickupRange:
                    save.pickupRangeLevel++;
                    break;
                case SupermarketAttributeId.MoveSpeed:
                    save.moveSpeedLevel++;
                    break;
                case SupermarketAttributeId.HandleCapacity:
                    save.handleCapacityLevel++;
                    break;
            }

            SaveService.Save();
            EconomyDisplayEvents.Raise();
            AttributeUpgraded?.Invoke(attributeId, GetAttributeLevel(attributeId));
            return true;
        }

        public static bool IsSweepRobotActive()
        {
            var save = Save;
            if (save == null || save.sweepRobotActiveUntilUtcTicks <= 0L)
                return false;

            return DateTime.UtcNow.Ticks < save.sweepRobotActiveUntilUtcTicks;
        }

        public static float GetSweepRobotRemainingSeconds()
        {
            if (!IsSweepRobotActive())
                return 0f;

            var remainingTicks = Save.sweepRobotActiveUntilUtcTicks - DateTime.UtcNow.Ticks;
            return Mathf.Max(0f, (float)(remainingTicks / (double)TimeSpan.TicksPerSecond));
        }

        public static void ActivateSweepRobot(float durationSeconds)
        {
            var save = Save;
            if (save == null)
                return;

            var now = DateTime.UtcNow;
            var baseTime = IsSweepRobotActive()
                ? new DateTime(save.sweepRobotActiveUntilUtcTicks, DateTimeKind.Utc)
                : now;

            save.sweepRobotActiveUntilUtcTicks = baseTime
                .AddSeconds(Mathf.Max(0f, durationSeconds))
                .Ticks;
            SaveService.Save();
        }

        static float Sample(float[] values, int level, float fallback)
        {
            if (values == null || values.Length == 0)
                return fallback;

            level = Mathf.Clamp(level, 0, values.Length - 1);
            return values[level];
        }

        static int SampleInt(int[] values, int level, int fallback)
        {
            if (values == null || values.Length == 0)
                return fallback;

            level = Mathf.Clamp(level, 0, values.Length - 1);
            return values[level];
        }
    }
}

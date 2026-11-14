using System;
using UnityEngine;

namespace Game.Save
{
    [Serializable]
    public sealed partial class SupermarketOrganizeModeSaveData
    {
        public string currentSceneId = "supermarket_map_1";
        public int totalRuns;
        public int totalVictories;
        public float bestClearTimeSeconds;

        [Obsolete("Use handleCapacityLevel via attribute system.")]
        public int handleCapacityUpgradeLevel;

        public int pickupRangeLevel;
        public int moveSpeedLevel;
        public int handleCapacityLevel;

        public int scannerSkillLevel;
        public int magnetSkillLevel;
        public int xRaySkillLevel;
        public int autoStockerSkillLevel;

        public long sweepRobotActiveUntilUtcTicks;

        public int stamina = 5;
        public long staminaLastRegenUtcTicks;

        public void MigrateDefaultsIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(currentSceneId))
                currentSceneId = "supermarket_map_1";
            totalRuns = Mathf.Max(0, totalRuns);
            totalVictories = Mathf.Max(0, totalVictories);
            if (bestClearTimeSeconds < 0f)
                bestClearTimeSeconds = 0f;

            pickupRangeLevel = Mathf.Max(0, pickupRangeLevel);
            moveSpeedLevel = Mathf.Max(0, moveSpeedLevel);
            handleCapacityLevel = Mathf.Max(0, handleCapacityLevel);
            scannerSkillLevel = Mathf.Max(0, scannerSkillLevel);
            magnetSkillLevel = Mathf.Max(0, magnetSkillLevel);
            xRaySkillLevel = Mathf.Max(0, xRaySkillLevel);
            autoStockerSkillLevel = Mathf.Max(0, autoStockerSkillLevel);
            stamina = Mathf.Clamp(stamina <= 0 && staminaLastRegenUtcTicks <= 0L ? 5 : stamina, 0, 99);

#pragma warning disable CS0618
            if (handleCapacityLevel <= 0 && handleCapacityUpgradeLevel > 0)
                handleCapacityLevel = Mathf.Clamp(handleCapacityUpgradeLevel, 0, 6);
#pragma warning restore CS0618
        }
    }
}

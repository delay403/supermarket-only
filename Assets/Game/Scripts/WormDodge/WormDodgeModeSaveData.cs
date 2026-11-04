using System;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// 躲避虫子玩法专属存档（仅在 <see cref="Game.Core.GameProductSettings.IsWormDodgeContentEnabled"/> 时读写）。
    /// </summary>
    [Serializable]
    public sealed partial class WormDodgeModeSaveData
    {
        public const int DefaultMaxStamina = 5;

        public string currentWormDodgeSceneId = "worm_dodge_map_1";
        public int bestFloorsReached;
        public int totalRuns;
        public int totalVictories;
        public long totalCoinsEarned;

        public int stamina = DefaultMaxStamina;
        public long staminaLastRegenUtcTicks;
        public float bestClearTimeSeconds;

        /// <summary>融合包体下躲避虫子模式独立经济与养成。</summary>
        public ModeScopedProfile profile = new ModeScopedProfile();

        public void MigrateDefaultsIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(currentWormDodgeSceneId))
                currentWormDodgeSceneId = "worm_dodge_map_1";
            bestFloorsReached = Mathf.Max(0, bestFloorsReached);
            totalRuns = Mathf.Max(0, totalRuns);
            totalVictories = Mathf.Max(0, totalVictories);
            if (totalCoinsEarned < 0L)
                totalCoinsEarned = 0L;
            stamina = Mathf.Clamp(stamina, 0, DefaultMaxStamina);
            if (bestClearTimeSeconds < 0f)
                bestClearTimeSeconds = 0f;
        }
    }
}

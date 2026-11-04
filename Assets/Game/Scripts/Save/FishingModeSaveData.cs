using System;
using Game.Core.Config;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// 钓鱼玩法专属存档（仅在 <see cref="Game.Core.GameProductSettings.IsFishingContentEnabled"/> 时读写）。
    /// </summary>
    [Serializable]
    public sealed partial class FishingModeSaveData
    {
        public string currentFishingSceneId = "fishing_map_1";

        /// <summary>基础力量（力量训练池挂机提升）；&lt;0 表示未初始化，由 MigrateDefaultsIfNeeded 从 GameBalanceConfig 写入。</summary>
        public float baseStrength = -1f;

        /// <summary>力量训练每日解锁刷新日（与 GameDayUtility 对齐）。</summary>
        public string strengthTrainingGameDayId = "";

        public bool strengthTrainingTier2UnlockedToday;
        public bool strengthTrainingTier3UnlockedToday;
        public int strengthTrainingTier3Progress;

        /// <summary>当日已解锁的训练场 groundId（与 StrengthTrainingGroundDatabase 对应）。</summary>
        public System.Collections.Generic.List<string> strengthTrainingUnlockedGroundIdsToday =
            new System.Collections.Generic.List<string>();

        /// <summary>当日免费解锁进度（ProgressPerDay 策略）。</summary>
        public System.Collections.Generic.List<StrengthTrainingGroundProgressEntry> strengthTrainingGroundProgressToday =
            new System.Collections.Generic.List<StrengthTrainingGroundProgressEntry>();

        [Serializable]
        public struct StrengthTrainingGroundProgressEntry
        {
            public string groundId;
            public int unlockProgress;
        }

        /// <summary>背部展示的鱼（图鉴佩戴）。</summary>
        public string equippedBackFishId = "";

        /// <summary>融合包体下钓鱼模式独立经济与养成。</summary>
        public ModeScopedProfile profile = new ModeScopedProfile();

        /// <summary>自动钓鱼通行证结束 UTC Ticks；0 表示未激活。</summary>
        public long autoFishingEndUtcTicks;

        public void MigrateDefaultsIfNeeded()
        {
            var defaultBase = ConfigRegistry.ResolveFishingDefaultBaseStrength();
            if (baseStrength < 0f)
                baseStrength = defaultBase;
            else if (defaultBase > 0f && baseStrength < defaultBase * 0.5f)
                baseStrength = defaultBase;
            baseStrength = Mathf.Max(0f, baseStrength);
            if (equippedBackFishId == null)
                equippedBackFishId = "";
            if (string.IsNullOrEmpty(strengthTrainingGameDayId))
                strengthTrainingGameDayId = "";
            EnsureFishCodexMigrated();
            EnsureDiscountOfferSaveInitialized();
        }
    }
}

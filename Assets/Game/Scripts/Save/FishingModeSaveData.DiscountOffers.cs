using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Save
{
    public sealed partial class FishingModeSaveData
    {
        /// <summary>首次成功钓获 UTC Ticks；0 表示尚未钓获。</summary>
        public long firstSuccessfulCatchUtcTicks;

        public int totalSuccessfulCatchCount;

        public long firstWorldAreaUnlockUtcTicks;
        public string firstWorldAreaUnlockSaveKey;

        public float totalOnlineSeconds;
        public long firstOnlineThresholdUtcTicks;
        public List<OnlineDurationMilestoneEntry> onlineDurationMilestones = new List<OnlineDurationMilestoneEntry>();

        public long firstWingPurchaseUtcTicks;
        public long firstTrailPurchaseUtcTicks;
        public long firstRodPurchaseUtcTicks;
        public long firstPetHatchUtcTicks;

        public List<DiscountOfferStateEntry> discountOfferStates = new List<DiscountOfferStateEntry>();

        public void EnsureDiscountOfferSaveInitialized()
        {
            if (onlineDurationMilestones == null)
                onlineDurationMilestones = new List<OnlineDurationMilestoneEntry>();
            if (discountOfferStates == null)
                discountOfferStates = new List<DiscountOfferStateEntry>();
            if (firstWorldAreaUnlockSaveKey == null)
                firstWorldAreaUnlockSaveKey = string.Empty;

            if (firstOnlineThresholdUtcTicks > 0)
            {
                var hasLegacy = false;
                for (var i = 0; i < onlineDurationMilestones.Count; i++)
                {
                    if (Mathf.Approximately(onlineDurationMilestones[i].thresholdSeconds, 600f))
                    {
                        hasLegacy = true;
                        break;
                    }
                }

                if (!hasLegacy)
                {
                    onlineDurationMilestones.Add(new OnlineDurationMilestoneEntry
                    {
                        thresholdSeconds = 600f,
                        reachedUtcTicks = firstOnlineThresholdUtcTicks,
                    });
                }
            }
        }
    }
}

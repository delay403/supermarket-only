using System;
using System.Collections.Generic;

namespace Game.Save
{
    [Serializable]
    public struct DiscountOfferStateEntry
    {
        public int offerId;
        public bool autoPopupShown;
        public bool popupScheduled;
        public bool purchased;
        public bool expired;
        public long hudEntryEndUtcTicks;
    }

    [Serializable]
    public struct OnlineDurationMilestoneEntry
    {
        public float thresholdSeconds;
        public long reachedUtcTicks;
    }

    /// <summary>限时优惠里程碑字段（各玩法模块各自存档一份）。</summary>
    [Serializable]
    public sealed class DiscountOfferMilestoneSaveData
    {
        public long firstSuccessfulCatchUtcTicks;
        public long firstWorldAreaUnlockUtcTicks;
        public string firstWorldAreaUnlockSaveKey;

        public float totalOnlineSeconds;
        public long firstOnlineThresholdUtcTicks;
        public List<OnlineDurationMilestoneEntry> onlineDurationMilestones = new List<OnlineDurationMilestoneEntry>();

        public long firstWingPurchaseUtcTicks;
        public long firstTrailPurchaseUtcTicks;
        public long firstRodPurchaseUtcTicks;
        public long firstPetHatchUtcTicks;

        public long firstNukeLaunchUtcTicks;
        public long firstFuelProducerUpgradeUtcTicks;

        public List<DiscountOfferStateEntry> discountOfferStates = new List<DiscountOfferStateEntry>();

        public void EnsureInitialized()
        {
            if (onlineDurationMilestones == null)
                onlineDurationMilestones = new List<OnlineDurationMilestoneEntry>();
            if (discountOfferStates == null)
                discountOfferStates = new List<DiscountOfferStateEntry>();
            if (firstWorldAreaUnlockSaveKey == null)
                firstWorldAreaUnlockSaveKey = string.Empty;
        }
    }
}

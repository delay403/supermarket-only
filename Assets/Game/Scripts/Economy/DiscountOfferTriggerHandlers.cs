using System;
using Game.Economy.Config;
using Game.Save;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>单个优惠商品的触发判定；每种 <see cref="DiscountOfferTriggerKind"/> 独立实现。</summary>
    interface IDiscountOfferTriggerHandler
    {
        DiscountOfferTriggerKind Kind { get; }

        bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks);
    }

    static class DiscountOfferTriggerHelpers
    {
        public static bool IsOfferClosed(in DiscountOfferStateEntry state) =>
            state.purchased || state.expired || state.autoPopupShown;

        public static bool MilestoneDelayElapsed(long milestoneUtcTicks, DiscountOfferDef def, long nowUtcTicks)
        {
            var delayTicks = TimeSpan.FromSeconds(Mathf.Max(0f, def.triggerDelaySeconds)).Ticks;
            return nowUtcTicks >= milestoneUtcTicks + delayTicks;
        }
    }

    sealed class DelayAfterFirstSuccessfulCatchTriggerHandler : IDiscountOfferTriggerHandler
    {
        public DiscountOfferTriggerKind Kind => DiscountOfferTriggerKind.DelayAfterFirstSuccessfulCatch;

        public bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks)
        {
            if (DiscountOfferTriggerHelpers.IsOfferClosed(state) || milestones.FirstSuccessfulCatchUtcTicks <= 0)
                return false;

            var delayTicks = TimeSpan.FromSeconds(Mathf.Max(0f, def.popupDelayAfterFirstCatchSeconds)).Ticks;
            return nowUtcTicks >= milestones.FirstSuccessfulCatchUtcTicks + delayTicks;
        }
    }

    sealed class FirstWorldAreaUnlockTriggerHandler : IDiscountOfferTriggerHandler
    {
        public DiscountOfferTriggerKind Kind => DiscountOfferTriggerKind.FirstWorldAreaUnlock;

        public bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks)
        {
            if (DiscountOfferTriggerHelpers.IsOfferClosed(state) || milestones.FirstWorldAreaUnlockUtcTicks <= 0)
                return false;

            if (!string.IsNullOrWhiteSpace(def.triggerWorldAreaSaveKey))
            {
                var expected = def.triggerWorldAreaSaveKey.Trim();
                var actual = milestones.FirstWorldAreaUnlockSaveKey ?? string.Empty;
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    return false;
            }

            return DiscountOfferTriggerHelpers.MilestoneDelayElapsed(
                milestones.FirstWorldAreaUnlockUtcTicks, def, nowUtcTicks);
        }
    }

    sealed class FirstOnlineDurationTriggerHandler : IDiscountOfferTriggerHandler
    {
        public DiscountOfferTriggerKind Kind => DiscountOfferTriggerKind.FirstOnlineDuration;

        public bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks)
        {
            if (DiscountOfferTriggerHelpers.IsOfferClosed(state))
                return false;

            var threshold = def.triggerOnlineSeconds > 0f ? def.triggerOnlineSeconds : 600f;
            if (milestones.TotalOnlineSeconds < threshold)
                return false;

            var milestoneUtcTicks = DiscountOfferSaveBridge.GetOnlineMilestoneUtcTicks(milestones, threshold);
            if (milestoneUtcTicks <= 0)
                return false;

            return DiscountOfferTriggerHelpers.MilestoneDelayElapsed(milestoneUtcTicks, def, nowUtcTicks);
        }
    }

    sealed class FirstWingPurchaseTriggerHandler : IDiscountOfferTriggerHandler
    {
        public DiscountOfferTriggerKind Kind => DiscountOfferTriggerKind.FirstWingPurchase;

        public bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks) =>
            !DiscountOfferTriggerHelpers.IsOfferClosed(state)
            && milestones.FirstWingPurchaseUtcTicks > 0
            && DiscountOfferTriggerHelpers.MilestoneDelayElapsed(milestones.FirstWingPurchaseUtcTicks, def, nowUtcTicks);
    }

    sealed class FirstTrailPurchaseTriggerHandler : IDiscountOfferTriggerHandler
    {
        public DiscountOfferTriggerKind Kind => DiscountOfferTriggerKind.FirstTrailPurchase;

        public bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks) =>
            !DiscountOfferTriggerHelpers.IsOfferClosed(state)
            && milestones.FirstTrailPurchaseUtcTicks > 0
            && DiscountOfferTriggerHelpers.MilestoneDelayElapsed(milestones.FirstTrailPurchaseUtcTicks, def, nowUtcTicks);
    }

    sealed class FirstPetHatchTriggerHandler : IDiscountOfferTriggerHandler
    {
        public DiscountOfferTriggerKind Kind => DiscountOfferTriggerKind.FirstPetHatch;

        public bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks) =>
            !DiscountOfferTriggerHelpers.IsOfferClosed(state)
            && milestones.FirstPetHatchUtcTicks > 0
            && DiscountOfferTriggerHelpers.MilestoneDelayElapsed(milestones.FirstPetHatchUtcTicks, def, nowUtcTicks);
    }

    sealed class FirstRodPurchaseTriggerHandler : IDiscountOfferTriggerHandler
    {
        public DiscountOfferTriggerKind Kind => DiscountOfferTriggerKind.FirstRodPurchase;

        public bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks) =>
            !DiscountOfferTriggerHelpers.IsOfferClosed(state)
            && milestones.FirstRodPurchaseUtcTicks > 0
            && DiscountOfferTriggerHelpers.MilestoneDelayElapsed(milestones.FirstRodPurchaseUtcTicks, def, nowUtcTicks);
    }

    sealed class FirstNukeLaunchTriggerHandler : IDiscountOfferTriggerHandler
    {
        public DiscountOfferTriggerKind Kind => DiscountOfferTriggerKind.FirstNukeLaunch;

        public bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks) =>
            !DiscountOfferTriggerHelpers.IsOfferClosed(state)
            && milestones.FirstNukeLaunchUtcTicks > 0
            && DiscountOfferTriggerHelpers.MilestoneDelayElapsed(milestones.FirstNukeLaunchUtcTicks, def, nowUtcTicks);
    }

    sealed class FirstFuelProducerUpgradeTriggerHandler : IDiscountOfferTriggerHandler
    {
        public DiscountOfferTriggerKind Kind => DiscountOfferTriggerKind.FirstFuelProducerUpgrade;

        public bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks) =>
            !DiscountOfferTriggerHelpers.IsOfferClosed(state)
            && milestones.FirstFuelProducerUpgradeUtcTicks > 0
            && DiscountOfferTriggerHelpers.MilestoneDelayElapsed(milestones.FirstFuelProducerUpgradeUtcTicks, def, nowUtcTicks);
    }

    static class DiscountOfferTriggerRegistry
    {
        static readonly IDiscountOfferTriggerHandler[] Handlers =
        {
            new DelayAfterFirstSuccessfulCatchTriggerHandler(),
            new FirstWorldAreaUnlockTriggerHandler(),
            new FirstOnlineDurationTriggerHandler(),
            new FirstWingPurchaseTriggerHandler(),
            new FirstTrailPurchaseTriggerHandler(),
            new FirstPetHatchTriggerHandler(),
            new FirstRodPurchaseTriggerHandler(),
            new FirstNukeLaunchTriggerHandler(),
            new FirstFuelProducerUpgradeTriggerHandler(),
        };

        public static bool ShouldTrigger(
            DiscountOfferDef def,
            in DiscountOfferMilestoneRef milestones,
            in DiscountOfferStateEntry state,
            long nowUtcTicks)
        {
            var handler = Resolve(def.triggerKind);
            return handler.ShouldTrigger(def, milestones, state, nowUtcTicks);
        }

        static IDiscountOfferTriggerHandler Resolve(DiscountOfferTriggerKind kind)
        {
            for (var i = 0; i < Handlers.Length; i++)
            {
                if (Handlers[i].Kind == kind)
                    return Handlers[i];
            }

            return Handlers[0];
        }
    }
}

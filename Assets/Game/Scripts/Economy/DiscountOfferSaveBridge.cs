using System;
using System.Collections.Generic;
using Game.Core;
using Game.Economy.Config;
using Game.Save;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>限时优惠存档读写桥接：各玩法模块独立里程碑与状态。</summary>
    static class DiscountOfferSaveBridge
    {
        public static bool IsActive => DiscountOfferScopeUtility.TryGetActiveScope(out _);

        public static DiscountOfferProductScope ActiveScope
        {
            get
            {
                return DiscountOfferScopeUtility.TryGetActiveScope(out var scope)
                    ? scope
                    : default;
            }
        }

        public static bool MatchesScope(DiscountOfferDef def) =>
            DiscountOfferScopeUtility.TryGetActiveScope(out var active) && def.productScope == active;

        public static bool MatchesScope(int offerId)
        {
            var db = DiscountOfferService.Database;
            return db != null
                   && db.TryGet(offerId, out var def)
                   && MatchesScope(def);
        }

        public static bool TryGetMilestones(out DiscountOfferMilestoneRef milestones)
        {
            milestones = default;
            if (!DiscountOfferScopeUtility.TryGetActiveScope(out var scope))
                return false;

            var data = SaveService.Data;
            if (data == null)
                return false;

            switch (scope)
            {
                case DiscountOfferProductScope.Fishing:
                    if (!GameProductSettings.IsFishingContentEnabled)
                        return false;
                    data.EnsureFishingSectionInitialized();
                    if (data.fishing == null)
                        return false;
                    data.fishing.EnsureDiscountOfferSaveInitialized();
                    milestones = DiscountOfferMilestoneRef.ForFishing(data.fishing);
                    return true;

                case DiscountOfferProductScope.DestroyCity:
                    if (!GameProductSettings.IsDestroyCityContentEnabled)
                        return false;
                    data.EnsureDestroyCitySectionInitialized();
                    if (data.destroyCity == null)
                        return false;
                    data.destroyCity.EnsureDiscountOfferSaveInitialized();
                    milestones = DiscountOfferMilestoneRef.ForDestroyCity(data.destroyCity);
                    return true;

                case DiscountOfferProductScope.WormDodge:
                    if (!GameProductSettings.IsWormDodgeContentEnabled)
                        return false;
                    data.EnsureWormDodgeSectionInitialized();
                    if (data.wormDodge == null)
                        return false;
                    data.wormDodge.EnsureDiscountOfferSaveInitialized();
                    milestones = DiscountOfferMilestoneRef.ForWormDodge(data.wormDodge);
                    return true;

                case DiscountOfferProductScope.SupermarketOrganize:
                    if (!GameProductSettings.IsSupermarketOrganizeContentEnabled)
                        return false;
                    data.EnsureSupermarketOrganizeSectionInitialized();
                    if (data.supermarketOrganize == null)
                        return false;
                    data.supermarketOrganize.EnsureDiscountOfferSaveInitialized();
                    milestones = DiscountOfferMilestoneRef.ForSupermarketOrganize(data.supermarketOrganize);
                    return true;

                case DiscountOfferProductScope.Tower:
                    if (!GameProductSettings.IsTowerContentEnabled)
                        return false;
                    data.EnsureTowerDiscountOfferSaveInitialized();
                    milestones = DiscountOfferMilestoneRef.ForTower(data);
                    return true;

                default:
                    return false;
            }
        }

        public static long GetOnlineMilestoneUtcTicks(in DiscountOfferMilestoneRef data, float thresholdSeconds)
        {
            if (thresholdSeconds <= 0f)
                return 0;

            var milestones = data.OnlineDurationMilestones;
            if (milestones != null)
            {
                for (var i = 0; i < milestones.Count; i++)
                {
                    if (Mathf.Approximately(milestones[i].thresholdSeconds, thresholdSeconds))
                        return milestones[i].reachedUtcTicks;
                }
            }

            if (data.FirstOnlineThresholdUtcTicks > 0 && Mathf.Approximately(thresholdSeconds, 600f))
                return data.FirstOnlineThresholdUtcTicks;

            return 0;
        }

        public static bool TryRecordOnlineDurationMilestone(
            ref DiscountOfferMilestoneRef data,
            float thresholdSeconds,
            long nowUtcTicks)
        {
            if (thresholdSeconds <= 0f || GetOnlineMilestoneUtcTicks(data, thresholdSeconds) > 0)
                return false;

            var list = data.OnlineDurationMilestones ?? new List<OnlineDurationMilestoneEntry>();
            list.Add(new OnlineDurationMilestoneEntry
            {
                thresholdSeconds = thresholdSeconds,
                reachedUtcTicks = nowUtcTicks,
            });
            data.OnlineDurationMilestones = list;
            return true;
        }
    }

    /// <summary>统一读写各玩法限时优惠里程碑字段。</summary>
    public struct DiscountOfferMilestoneRef
    {
        FishingModeSaveData _fishing;
        DestroyCityModeSaveData _destroyCity;
        WormDodgeModeSaveData _wormDodge;
        SupermarketOrganizeModeSaveData _supermarketOrganize;
        DiscountOfferMilestoneSaveData _tower;

        public static DiscountOfferMilestoneRef ForFishing(FishingModeSaveData fishing) =>
            new DiscountOfferMilestoneRef { _fishing = fishing };

        public static DiscountOfferMilestoneRef ForDestroyCity(DestroyCityModeSaveData destroyCity) =>
            new DiscountOfferMilestoneRef { _destroyCity = destroyCity };

        public static DiscountOfferMilestoneRef ForWormDodge(WormDodgeModeSaveData wormDodge) =>
            new DiscountOfferMilestoneRef { _wormDodge = wormDodge };

        public static DiscountOfferMilestoneRef ForSupermarketOrganize(SupermarketOrganizeModeSaveData supermarket) =>
            new DiscountOfferMilestoneRef { _supermarketOrganize = supermarket };

        public static DiscountOfferMilestoneRef ForTower(PlayerSaveData player) =>
            new DiscountOfferMilestoneRef { _tower = player.towerDiscountOffers };

        DiscountOfferMilestoneSaveData ScopedData =>
            _destroyCity?.discountOffers
            ?? _wormDodge?.discountOffers
            ?? _supermarketOrganize?.discountOffers
            ?? _tower;

        public long FirstSuccessfulCatchUtcTicks
        {
            get => _fishing != null ? _fishing.firstSuccessfulCatchUtcTicks : 0;
            set { if (_fishing != null) _fishing.firstSuccessfulCatchUtcTicks = value; }
        }

        public long FirstWorldAreaUnlockUtcTicks
        {
            get => _fishing != null ? _fishing.firstWorldAreaUnlockUtcTicks : 0;
            set { if (_fishing != null) _fishing.firstWorldAreaUnlockUtcTicks = value; }
        }

        public string FirstWorldAreaUnlockSaveKey
        {
            get => _fishing != null ? _fishing.firstWorldAreaUnlockSaveKey : string.Empty;
            set { if (_fishing != null) _fishing.firstWorldAreaUnlockSaveKey = value; }
        }

        public float TotalOnlineSeconds
        {
            get => _fishing != null ? _fishing.totalOnlineSeconds : ScopedData.totalOnlineSeconds;
            set
            {
                if (_fishing != null) _fishing.totalOnlineSeconds = value;
                else ScopedData.totalOnlineSeconds = value;
            }
        }

        public long FirstOnlineThresholdUtcTicks
        {
            get => _fishing != null ? _fishing.firstOnlineThresholdUtcTicks : ScopedData.firstOnlineThresholdUtcTicks;
            set
            {
                if (_fishing != null) _fishing.firstOnlineThresholdUtcTicks = value;
                else ScopedData.firstOnlineThresholdUtcTicks = value;
            }
        }

        public List<OnlineDurationMilestoneEntry> OnlineDurationMilestones
        {
            get => _fishing != null ? _fishing.onlineDurationMilestones : ScopedData.onlineDurationMilestones;
            set
            {
                if (_fishing != null) _fishing.onlineDurationMilestones = value;
                else ScopedData.onlineDurationMilestones = value;
            }
        }

        public long FirstWingPurchaseUtcTicks
        {
            get => _fishing != null ? _fishing.firstWingPurchaseUtcTicks : ScopedData.firstWingPurchaseUtcTicks;
            set
            {
                if (_fishing != null) _fishing.firstWingPurchaseUtcTicks = value;
                else ScopedData.firstWingPurchaseUtcTicks = value;
            }
        }

        public long FirstTrailPurchaseUtcTicks
        {
            get => _fishing != null ? _fishing.firstTrailPurchaseUtcTicks : ScopedData.firstTrailPurchaseUtcTicks;
            set
            {
                if (_fishing != null) _fishing.firstTrailPurchaseUtcTicks = value;
                else ScopedData.firstTrailPurchaseUtcTicks = value;
            }
        }

        public long FirstRodPurchaseUtcTicks
        {
            get => _fishing != null ? _fishing.firstRodPurchaseUtcTicks : 0;
            set { if (_fishing != null) _fishing.firstRodPurchaseUtcTicks = value; }
        }

        public long FirstPetHatchUtcTicks
        {
            get => _fishing != null ? _fishing.firstPetHatchUtcTicks : ScopedData.firstPetHatchUtcTicks;
            set
            {
                if (_fishing != null) _fishing.firstPetHatchUtcTicks = value;
                else ScopedData.firstPetHatchUtcTicks = value;
            }
        }

        public long FirstNukeLaunchUtcTicks
        {
            get => _destroyCity != null ? _destroyCity.discountOffers.firstNukeLaunchUtcTicks : 0;
            set { if (_destroyCity != null) _destroyCity.discountOffers.firstNukeLaunchUtcTicks = value; }
        }

        public long FirstFuelProducerUpgradeUtcTicks
        {
            get => _destroyCity != null ? _destroyCity.discountOffers.firstFuelProducerUpgradeUtcTicks : 0;
            set { if (_destroyCity != null) _destroyCity.discountOffers.firstFuelProducerUpgradeUtcTicks = value; }
        }

        public List<DiscountOfferStateEntry> DiscountOfferStates
        {
            get => _fishing != null ? _fishing.discountOfferStates : ScopedData.discountOfferStates;
            set
            {
                if (_fishing != null) _fishing.discountOfferStates = value;
                else ScopedData.discountOfferStates = value;
            }
        }

        public int GetOrCreateStateIndex(int offerId)
        {
            var states = DiscountOfferStates ?? new List<DiscountOfferStateEntry>();
            for (var i = 0; i < states.Count; i++)
            {
                if (states[i].offerId == offerId)
                    return i;
            }

            states.Add(new DiscountOfferStateEntry { offerId = offerId });
            DiscountOfferStates = states;
            return states.Count - 1;
        }

        public bool TryGetState(int offerId, out DiscountOfferStateEntry state)
        {
            state = default;
            var states = DiscountOfferStates;
            if (states == null)
                return false;

            for (var i = 0; i < states.Count; i++)
            {
                if (states[i].offerId == offerId)
                {
                    state = states[i];
                    return true;
                }
            }

            return false;
        }

        public void WriteState(int index, DiscountOfferStateEntry state)
        {
            var states = DiscountOfferStates;
            if (states == null || index < 0 || index >= states.Count)
                return;
            states[index] = state;
            DiscountOfferStates = states;
        }
    }
}

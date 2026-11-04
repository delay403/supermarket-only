using System;
using Game.Core;
using Game.Core.Config;
using Game.Economy.Config;
using Game.Save;
using Game.UI.Framework;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>
    /// 限时优惠商品：存档状态、购买（钻石 / 免费解锁）、HUD 入口与弹窗队列。
    /// 触发判定由 <see cref="DiscountOfferEventManager"/> + <see cref="DiscountOfferTriggerRegistry"/> 驱动。
    /// </summary>
    public static class DiscountOfferService
    {
        public static event Action Changed;

        public static DiscountOfferDatabase Database =>
            ConfigRegistry.DiscountOffers ??
            Resources.Load<DiscountOfferDatabase>("DiscountOfferDatabase");

        public static void OnSuccessfulCatch()
        {
            if (!DiscountOfferSaveBridge.IsActive
                || DiscountOfferSaveBridge.ActiveScope != DiscountOfferProductScope.Fishing)
                return;

            if (!DiscountOfferSaveBridge.TryGetMilestones(out _))
                return;

            var fishing = SaveService.Data?.fishing;
            if (fishing == null)
                return;

            fishing.totalSuccessfulCatchCount = Mathf.Max(0, fishing.totalSuccessfulCatchCount) + 1;
            if (fishing.firstSuccessfulCatchUtcTicks <= 0)
                fishing.firstSuccessfulCatchUtcTicks = DateTime.UtcNow.Ticks;

            SaveService.Save();
            EvaluateAllTriggers();
        }

        public static void NotifyWorldAreaUnlocked(string areaSaveKey)
        {
            if (!DiscountOfferSaveBridge.IsActive
                || DiscountOfferSaveBridge.ActiveScope != DiscountOfferProductScope.Fishing)
                return;

            if (!DiscountOfferSaveBridge.TryGetMilestones(out var milestones))
                return;

            if (milestones.FirstWorldAreaUnlockUtcTicks <= 0)
            {
                milestones.FirstWorldAreaUnlockUtcTicks = DateTime.UtcNow.Ticks;
                milestones.FirstWorldAreaUnlockSaveKey = areaSaveKey?.Trim() ?? string.Empty;
                SaveService.Save();
            }

            EvaluateAllTriggers();
        }

        delegate void MilestoneUtcTicksSetter(ref DiscountOfferMilestoneRef milestones, long value);

        static void RecordMilestoneOnce(
            Func<DiscountOfferMilestoneRef, long> getUtcTicks,
            MilestoneUtcTicksSetter setUtcTicks)
        {
            if (!DiscountOfferSaveBridge.IsActive)
                return;

            if (!DiscountOfferSaveBridge.TryGetMilestones(out var milestones))
                return;

            if (getUtcTicks(milestones) > 0)
                return;

            setUtcTicks(ref milestones, DateTime.UtcNow.Ticks);
            SaveService.Save();
            EvaluateAllTriggers();
        }

        public static void NotifyFirstWingPurchased() =>
            RecordMilestoneOnce(
                m => m.FirstWingPurchaseUtcTicks,
                (ref DiscountOfferMilestoneRef m, long v) => m.FirstWingPurchaseUtcTicks = v);

        public static void NotifyFirstTrailPurchased() =>
            RecordMilestoneOnce(
                m => m.FirstTrailPurchaseUtcTicks,
                (ref DiscountOfferMilestoneRef m, long v) => m.FirstTrailPurchaseUtcTicks = v);

        public static void NotifyFirstRodPurchased() =>
            RecordMilestoneOnce(
                m => m.FirstRodPurchaseUtcTicks,
                (ref DiscountOfferMilestoneRef m, long v) => m.FirstRodPurchaseUtcTicks = v);

        public static void NotifyFirstPetHatched() =>
            RecordMilestoneOnce(
                m => m.FirstPetHatchUtcTicks,
                (ref DiscountOfferMilestoneRef m, long v) => m.FirstPetHatchUtcTicks = v);

        public static void NotifyFirstNukeLaunch() =>
            RecordMilestoneOnce(
                m => m.FirstNukeLaunchUtcTicks,
                (ref DiscountOfferMilestoneRef m, long v) => m.FirstNukeLaunchUtcTicks = v);

        public static void NotifyFirstFuelProducerUpgrade() =>
            RecordMilestoneOnce(
                m => m.FirstFuelProducerUpgradeUtcTicks,
                (ref DiscountOfferMilestoneRef m, long v) => m.FirstFuelProducerUpgradeUtcTicks = v);

        public static void AccumulateOnlineTime(float deltaSeconds)
        {
            if (!DiscountOfferSaveBridge.IsActive || deltaSeconds <= 0f)
                return;

            if (!DiscountOfferSaveBridge.TryGetMilestones(out var milestones))
                return;

            milestones.TotalOnlineSeconds = Mathf.Max(0f, milestones.TotalOnlineSeconds) + deltaSeconds;

            var db = Database;
            var now = DateTime.UtcNow.Ticks;
            var changed = false;

            if (db?.offers != null)
            {
                for (var i = 0; i < db.offers.Length; i++)
                {
                    var def = db.offers[i];
                    if (def.offerId <= 0 || !DiscountOfferSaveBridge.MatchesScope(def))
                        continue;
                    if (def.triggerKind != DiscountOfferTriggerKind.FirstOnlineDuration)
                        continue;

                    var threshold = def.triggerOnlineSeconds > 0f ? def.triggerOnlineSeconds : 600f;
                    if (milestones.TotalOnlineSeconds < threshold)
                        continue;

                    if (DiscountOfferSaveBridge.TryRecordOnlineDurationMilestone(ref milestones, threshold, now))
                        changed = true;
                }
            }

            if (changed)
                SaveService.Save();

            EvaluateAllTriggers();
        }

        public static void RestoreHudFromSave()
        {
            TickEntryExpiry();
            Changed?.Invoke();
        }

        /// <summary>按配置逐条评估是否应弹出（每种 trigger 独立处理器）。</summary>
        public static void EvaluateAllTriggers()
        {
            if (!DiscountOfferSaveBridge.IsActive)
                return;

            var db = Database;
            if (db?.offers == null || !DiscountOfferSaveBridge.TryGetMilestones(out var milestones))
                return;

            var now = DateTime.UtcNow.Ticks;
            var changed = false;

            for (var i = 0; i < db.offers.Length; i++)
            {
                var def = db.offers[i];
                if (def.offerId <= 0 || !DiscountOfferSaveBridge.MatchesScope(def))
                    continue;

                var stateIndex = milestones.GetOrCreateStateIndex(def.offerId);
                var states = milestones.DiscountOfferStates;
                var state = states[stateIndex];
                if (state.purchased || state.expired || state.autoPopupShown)
                    continue;

                if (state.popupScheduled)
                {
                    DiscountOfferPopupQueue.Enqueue(def.offerId);
                    continue;
                }

                if (!DiscountOfferTriggerRegistry.ShouldTrigger(def, milestones, state, now))
                    continue;

                state.popupScheduled = true;
                ActivateHudEntry(ref state, def, now);
                milestones.WriteState(stateIndex, state);
                changed = true;
                DiscountOfferPopupQueue.Enqueue(def.offerId);
            }

            TickEntryExpiry(now, ref milestones, ref changed);

            if (changed)
            {
                SaveService.Save();
                Changed?.Invoke();
            }
        }

        public static void MarkAutoPopupShown(int offerId)
        {
            if (!DiscountOfferSaveBridge.TryGetMilestones(out var milestones) || offerId <= 0)
                return;

            var stateIndex = milestones.GetOrCreateStateIndex(offerId);
            var states = milestones.DiscountOfferStates;
            var state = states[stateIndex];
            if (state.autoPopupShown)
                return;

            state.autoPopupShown = true;
            milestones.WriteState(stateIndex, state);
            SaveService.Save();
            Changed?.Invoke();
        }

        public static void TickEntryExpiry()
        {
            var now = DateTime.UtcNow.Ticks;
            if (!DiscountOfferSaveBridge.TryGetMilestones(out var milestones))
                return;

            var changed = false;
            TickEntryExpiry(now, ref milestones, ref changed);
            if (changed)
            {
                SaveService.Save();
                Changed?.Invoke();
            }
        }

        static void TickEntryExpiry(long nowUtcTicks, ref DiscountOfferMilestoneRef milestones, ref bool changed)
        {
            var states = milestones.DiscountOfferStates;
            if (states == null)
                return;

            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state.purchased || state.expired || state.hudEntryEndUtcTicks <= 0)
                    continue;

                if (nowUtcTicks >= state.hudEntryEndUtcTicks)
                {
                    state.expired = true;
                    state.hudEntryEndUtcTicks = 0;
                    milestones.WriteState(i, state);
                    changed = true;
                }
            }
        }

        public static bool IsHudEntryActive(int offerId)
        {
            if (!TryGetState(offerId, out var state))
                return false;

            if (state.purchased || state.expired)
                return false;

            return state.hudEntryEndUtcTicks > DateTime.UtcNow.Ticks;
        }

        public static double GetHudEntryRemainingSeconds(int offerId)
        {
            if (!TryGetState(offerId, out var state) || state.hudEntryEndUtcTicks <= 0)
                return 0d;

            var left = new TimeSpan(state.hudEntryEndUtcTicks - DateTime.UtcNow.Ticks).TotalSeconds;
            return left > 0d ? left : 0d;
        }

        public static bool IsFreePurchase(DiscountOfferDef def) =>
            def.purchaseKind == DiscountOfferPurchaseKind.Free;

        public static bool TryPurchase(int offerId, out string error)
        {
            error = null;
            var db = Database;
            if (db == null || !db.TryGet(offerId, out var def))
            {
                error = "商品不存在";
                return false;
            }

            return def.purchaseKind == DiscountOfferPurchaseKind.Free
                ? TryGrantFreePurchase(offerId, out error)
                : TryPurchaseWithGems(offerId, def, out error);
        }

        static bool TryGrantFreePurchase(int offerId, out string error)
        {
            error = null;
            var db = Database;
            if (db == null || !db.TryGet(offerId, out var def))
            {
                error = "商品不存在";
                return false;
            }

            if (!ValidateOfferAvailable(offerId, out error))
                return false;

            if (!DiscountOfferSaveBridge.TryGetMilestones(out var milestones))
            {
                error = "存档未就绪";
                return false;
            }

            var stateIndex = milestones.GetOrCreateStateIndex(offerId);
            var state = milestones.DiscountOfferStates[stateIndex];
            return TryGrantPurchase(offerId, def, ref state, stateIndex, ref milestones, out error);
        }

        static bool TryPurchaseWithGems(int offerId, DiscountOfferDef def, out string error)
        {
            error = null;
            if (!ValidateOfferAvailable(offerId, out error))
                return false;

            var data = SaveService.Data;
            if (data.gems < def.gemPrice)
            {
                error = "钻石不足";
                return false;
            }

            if (!DiscountOfferSaveBridge.TryGetMilestones(out var milestones))
            {
                error = "存档未就绪";
                return false;
            }

            data.gems -= def.gemPrice;
            var stateIndex = milestones.GetOrCreateStateIndex(offerId);
            var state = milestones.DiscountOfferStates[stateIndex];
            if (!TryGrantPurchase(offerId, def, ref state, stateIndex, ref milestones, out error))
            {
                data.gems += def.gemPrice;
                return false;
            }

            return true;
        }

        static bool ValidateOfferAvailable(int offerId, out string error)
        {
            error = null;
            if (!DiscountOfferSaveBridge.TryGetMilestones(out _))
            {
                error = "存档未就绪";
                return false;
            }

            if (!TryGetState(offerId, out var state))
            {
                error = "存档未就绪";
                return false;
            }

            if (state.purchased)
            {
                error = "已购买";
                return false;
            }

            if (state.expired)
            {
                error = "优惠已过期";
                return false;
            }

            if (!IsHudEntryActive(offerId))
            {
                error = "入口已关闭";
                return false;
            }

            return true;
        }

        static bool TryGrantPurchase(
            int offerId,
            DiscountOfferDef def,
            ref DiscountOfferStateEntry state,
            int stateIndex,
            ref DiscountOfferMilestoneRef milestones,
            out string error)
        {
            error = null;
            if (!DiscountOfferRewardGranter.TryGrantAll(def, out error))
                return false;

            state.purchased = true;
            state.hudEntryEndUtcTicks = 0;
            milestones.WriteState(stateIndex, state);
            SaveService.Save();
            Changed?.Invoke();
            UiCoordinator.RefreshEquipmentVisuals();
            EconomyDisplayEvents.Raise();
            UiCoordinator.RefreshOpenShopPanelsForDiscountRewards(def);
            return true;
        }

        public static void ActivateHudEntryForOffer(int offerId)
        {
            var db = Database;
            if (db == null || !db.TryGet(offerId, out var def))
                return;

            if (!DiscountOfferSaveBridge.TryGetMilestones(out var milestones))
                return;

            var stateIndex = milestones.GetOrCreateStateIndex(offerId);
            var states = milestones.DiscountOfferStates;
            var state = states[stateIndex];
            if (state.purchased || state.expired)
                return;

            ActivateHudEntry(ref state, def, DateTime.UtcNow.Ticks);
            milestones.WriteState(stateIndex, state);
            SaveService.Save();
            Changed?.Invoke();
        }

        static void ActivateHudEntry(ref DiscountOfferStateEntry state, DiscountOfferDef def, long nowUtcTicks)
        {
            var duration = Mathf.Max(1f, def.hudEntryDurationSeconds);
            var end = nowUtcTicks + TimeSpan.FromSeconds(duration).Ticks;
            if (state.hudEntryEndUtcTicks > nowUtcTicks)
                end = Math.Max(end, state.hudEntryEndUtcTicks);

            state.hudEntryEndUtcTicks = end;
        }

        public static bool TryGetState(int offerId, out DiscountOfferStateEntry state)
        {
            state = default;
            if (!DiscountOfferSaveBridge.TryGetMilestones(out var milestones))
                return false;

            return milestones.TryGetState(offerId, out state);
        }
    }
}

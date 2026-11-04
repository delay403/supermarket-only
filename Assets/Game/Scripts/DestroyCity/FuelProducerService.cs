using System;
using System.Collections.Generic;
using Game.Reward;
using Game.Collectibles.Stats;
using Game.Core.Config;
using Game.DestroyCity.Config;
using Game.Economy;
using Game.Save;
using UnityEngine;

namespace Game.DestroyCity
{
    public static class FuelProducerService
    {
        public static event Action<string, float> ProductionApplied;
        public static event Action<string, int> ProducerUpgraded;
        public static event Action<string, int> FuelLeveledUp;
        public static event Action<string> ProducerUnlocked;
        public static event Action FuelInstancesChanged;

        const float MinAutoSaveIntervalSeconds = 0.75f;
        const int MaxAutoProduceStepsPerFrame = 2;

        static DestroyCityModeSaveData Data => SaveService.Data?.destroyCity;
        static DestroyCityStatsAggregator s_statsAggregator;
        static int s_statsCacheFrame = -1;
        static DestroyCityStatsAggregator.FinalDestroyCityStats s_cachedProductionStats;
        static float s_lastSaveTime = float.MinValue;
        static bool s_saveDirty;

        public static FuelProducerDef ResolveDef(string producerId)
        {
            var db = ConfigRegistry.DestroyCityFuelProducers;
            if (db != null && db.TryGet(producerId, out var def))
                return def;
            return CreateFallbackDef(producerId);
        }

        public static bool IsUnlocked(string producerId)
        {
            EnsureProducerEntry(producerId);
            var entry = FindProducer(producerId);
            if (entry == null)
                return false;

            if (entry.Value.unlocked)
                return true;

            var def = ResolveDef(producerId);
            return def != null && def.unlockPolicy == FuelProducerUnlockPolicy.AlwaysOpen;
        }

        public static int GetProducerLevel(string producerId)
        {
            EnsureProducerEntry(producerId);
            var entry = FindProducer(producerId);
            return entry != null ? Mathf.Max(1, entry.Value.level) : 1;
        }

        public static long GetUpgradeCoinCost(string producerId)
        {
            var def = ResolveDef(producerId);
            var level = GetProducerLevel(producerId);
            if (def == null)
                return 0;

            if (level >= def.GetMaxProducerLevel())
                return 0;

            return def.GetUpgradeCostForLevel(level);
        }

        public static bool CanAffordUpgrade(string producerId)
        {
            if (SaveService.Data == null)
                return false;
            return SaveService.Data.coins >= GetUpgradeCoinCost(producerId);
        }

        public static bool TryUpgradeProducer(string producerId)
        {
            if (!IsUnlocked(producerId))
                return false;

            var def = ResolveDef(producerId);
            var level = GetProducerLevel(producerId);
            if (def != null && level >= def.GetMaxProducerLevel())
                return false;

            var cost = GetUpgradeCoinCost(producerId);
            if (cost <= 0 || SaveService.Data.coins < cost)
                return false;

            SaveService.Data.coins -= cost;
            var entry = EnsureProducerEntry(producerId);
            entry.level++;
            WriteProducer(entry);
            DiscountOfferService.NotifyFirstFuelProducerUpgrade();
            SaveService.Save();
            EconomyDisplayEvents.Raise();
            ProducerUpgraded?.Invoke(producerId, entry.level);
            return true;
        }

        public static bool TryUnlockWithCoins(string producerId)
        {
            var def = ResolveDef(producerId);
            if (def == null || IsUnlocked(producerId))
                return IsUnlocked(producerId);

            if (def.unlockPolicy != FuelProducerUnlockPolicy.CoinOnce)
                return false;

            if (SaveService.Data == null || SaveService.Data.coins < def.unlockCoinCost)
                return false;

            SaveService.Data.coins -= def.unlockCoinCost;
            MarkUnlocked(producerId);
            EconomyDisplayEvents.Raise();
            return true;
        }

        public static bool TryUnlockWithGems(string producerId)
        {
            var def = ResolveDef(producerId);
            if (def == null || IsUnlocked(producerId))
                return IsUnlocked(producerId);

            if (def.unlockPolicy != FuelProducerUnlockPolicy.GemOnce)
                return false;

            if (SaveService.Data == null || SaveService.Data.gems < def.unlockGemCost)
                return false;

            SaveService.Data.gems -= def.unlockGemCost;
            MarkUnlocked(producerId);
            EconomyDisplayEvents.Raise();
            return true;
        }

        public static void TryUnlockFree(string producerId, Action<bool> onComplete)
        {
            var def = ResolveDef(producerId);
            if (def == null || IsUnlocked(producerId))
            {
                onComplete?.Invoke(IsUnlocked(producerId));
                return;
            }

            if (def.unlockPolicy != FuelProducerUnlockPolicy.FreeUnlockOnce)
            {
                onComplete?.Invoke(false);
                return;
            }

            FreeRewardGate.Grant(() =>
            {
                MarkUnlocked(producerId);
                onComplete?.Invoke(true);
            });
        }

        public static void UnlockByReward(string producerId)
        {
            if (IsUnlocked(producerId))
                return;

            var def = ResolveDef(producerId);
            if (def == null || def.unlockPolicy != FuelProducerUnlockPolicy.FreeUnlockOnce)
                return;

            MarkUnlocked(producerId);
        }

        public static string GetUnlockConditionText(string producerId)
        {
            var def = ResolveDef(producerId);
            if (def == null || IsUnlocked(producerId))
                return string.Empty;

            switch (def.unlockPolicy)
            {
                case FuelProducerUnlockPolicy.CoinOnce:
                    return $"解锁条件：金币 {FormatUnlockCost(def.unlockCoinCost)}";
                case FuelProducerUnlockPolicy.GemOnce:
                    return $"解锁条件：钻石 {FormatUnlockCost(def.unlockGemCost)}";
                case FuelProducerUnlockPolicy.FreeUnlockOnce:
                    return "解锁条件：免费领取";
                case FuelProducerUnlockPolicy.WorldAreaSaveKey:
                    return string.IsNullOrWhiteSpace(def.unlockSaveKey)
                        ? "解锁条件：探索并解锁指定区域"
                        : $"解锁条件：解锁区域「{def.unlockSaveKey.Trim()}」";
                case FuelProducerUnlockPolicy.AutoUnlockOnEvent:
                    return "解锁条件：达成指定事件后自动解锁";
                default:
                    return string.Empty;
            }
        }

        public static bool CanAffordUnlock(string producerId)
        {
            if (IsUnlocked(producerId))
                return false;

            var def = ResolveDef(producerId);
            if (def == null || SaveService.Data == null)
                return false;

            switch (def.unlockPolicy)
            {
                case FuelProducerUnlockPolicy.CoinOnce:
                    return SaveService.Data.coins >= def.unlockCoinCost;
                case FuelProducerUnlockPolicy.GemOnce:
                    return SaveService.Data.gems >= def.unlockGemCost;
                case FuelProducerUnlockPolicy.FreeUnlockOnce:
                    return true;
                case FuelProducerUnlockPolicy.WorldAreaSaveKey:
                    return !string.IsNullOrWhiteSpace(def.unlockSaveKey)
                        && SaveService.Data.HasUnlockedWorldArea(def.unlockSaveKey);
                case FuelProducerUnlockPolicy.AutoUnlockOnEvent:
                    return false;
                default:
                    return false;
            }
        }

        public static bool TryUnlock(string producerId, Action<bool> onComplete = null)
        {
            if (IsUnlocked(producerId))
            {
                onComplete?.Invoke(true);
                return true;
            }

            var def = ResolveDef(producerId);
            if (def == null)
            {
                onComplete?.Invoke(false);
                return false;
            }

            switch (def.unlockPolicy)
            {
                case FuelProducerUnlockPolicy.CoinOnce:
                    var coinOk = TryUnlockWithCoins(producerId);
                    onComplete?.Invoke(coinOk);
                    return coinOk;
                case FuelProducerUnlockPolicy.GemOnce:
                    var gemOk = TryUnlockWithGems(producerId);
                    onComplete?.Invoke(gemOk);
                    return gemOk;
                case FuelProducerUnlockPolicy.FreeUnlockOnce:
                    TryUnlockFree(producerId, onComplete);
                    return false;
                case FuelProducerUnlockPolicy.WorldAreaSaveKey:
                    RefreshWorldAreaUnlocks();
                    var areaOk = IsUnlocked(producerId);
                    onComplete?.Invoke(areaOk);
                    return areaOk;
                case FuelProducerUnlockPolicy.AutoUnlockOnEvent:
                    TryAutoUnlock(producerId);
                    var autoOk = IsUnlocked(producerId);
                    onComplete?.Invoke(autoOk);
                    return autoOk;
                default:
                    onComplete?.Invoke(false);
                    return false;
            }
        }

        static string FormatUnlockCost(long value)
        {
            if (value >= 1_000_000)
                return (value / 1_000_000f).ToString("0.#") + "M";
            if (value >= 10_000)
                return (value / 1_000f).ToString("0.#") + "K";
            return value.ToString();
        }

        public static void TryAutoUnlock(string producerId)
        {
            var def = ResolveDef(producerId);
            if (def == null || IsUnlocked(producerId))
                return;

            if (def.unlockPolicy != FuelProducerUnlockPolicy.AutoUnlockOnEvent)
                return;

            MarkUnlocked(producerId);
        }

        public static void RefreshWorldAreaUnlocks()
        {
            if (Data?.fuelProducers == null || SaveService.Data == null)
                return;

            var db = ConfigRegistry.DestroyCityFuelProducers;
            if (db == null)
                return;

            foreach (var def in db.producers)
            {
                if (def == null || def.unlockPolicy != FuelProducerUnlockPolicy.WorldAreaSaveKey)
                    continue;
                if (string.IsNullOrWhiteSpace(def.unlockSaveKey))
                    continue;
                if (SaveService.Data.HasUnlockedWorldArea(def.unlockSaveKey))
                    MarkUnlocked(def.producerId);
            }
        }

        public static float ComputeTickXp(string producerId, DestroyCityStatsAggregator.FinalDestroyCityStats stats)
        {
            var def = ResolveDef(producerId);
            if (def == null)
                return 0f;

            var level = GetProducerLevel(producerId);
            var baseXp = def.GetXpPerTickForLevel(level);
            var equipBonus = stats.ComputeEquipmentFuelBonus(baseXp);
            var speed = 1f + stats.totalProductionSpeedMul;
            return Mathf.Max(0f, (baseXp + equipBonus) * speed);
        }

        static DestroyCityStatsAggregator.FinalDestroyCityStats ResolveProductionStats()
        {
            if (s_statsAggregator == null)
                s_statsAggregator = UnityEngine.Object.FindObjectOfType<DestroyCityStatsAggregator>();

            if (s_statsAggregator == null)
                return default;

            if (Time.frameCount == s_statsCacheFrame)
                return s_cachedProductionStats;

            s_statsAggregator.Recompute();
            s_cachedProductionStats = s_statsAggregator.Current;
            s_statsCacheFrame = Time.frameCount;
            return s_cachedProductionStats;
        }

        static void MarkSaveDirty(bool immediate = false)
        {
            s_saveDirty = true;
            if (!immediate)
                return;

            FlushSaveIfDirty(force: true);
        }

        static void FlushSaveIfDirty(bool force = false)
        {
            if (!s_saveDirty)
                return;

            var now = Time.unscaledTime;
            if (!force && now - s_lastSaveTime < MinAutoSaveIntervalSeconds)
                return;

            SaveService.Save();
            s_lastSaveTime = now;
            s_saveDirty = false;
        }

        static int s_lastFlushFrame = -1;

        /// <summary>由场景驱动（如 LateUpdate）定期刷盘，避免自动生产每 tick 写存档。</summary>
        public static void FlushPendingSave()
        {
            if (Time.frameCount == s_lastFlushFrame)
                return;

            s_lastFlushFrame = Time.frameCount;
            FlushSaveIfDirty(force: false);
        }

        public static int AutoProduceStepsPerFrameLimit => MaxAutoProduceStepsPerFrame;

        /// <summary>UI 展示用：等级表配置的单次基础经验（不含装备加成与手动倍率）。</summary>
        public static float GetConfigTickXp(string producerId)
        {
            var def = ResolveDef(producerId);
            if (def == null)
                return 0f;

            return def.GetXpPerTickForLevel(GetProducerLevel(producerId));
        }

        /// <summary>实际生产单次经验（含装备加成；手动生产器另乘点击倍率）。</summary>
        public static float GetDisplayTickXp(string producerId)
        {
            var stats = ResolveProductionStats();
            var xp = ComputeTickXp(producerId, stats);
            var def = ResolveDef(producerId);
            if (def == null || !def.isManual)
                return xp;

            var bal = ConfigRegistry.Balance;
            var tapScale = bal != null && bal.destroyCityManualTapFuelScale > 0f
                ? bal.destroyCityManualTapFuelScale
                : 1f;
            return xp * tapScale;
        }

        public static bool TryManualProduce(string producerId)
        {
            if (!IsUnlocked(producerId))
                return false;

            var def = ResolveDef(producerId);
            if (def == null || !def.isManual)
                return false;

            return ApplyProduction(producerId, isManual: true);
        }

        public static bool TryAutoProduce(string producerId)
        {
            if (!IsUnlocked(producerId))
                return false;

            var def = ResolveDef(producerId);
            if (def == null || def.isManual)
                return false;

            return ApplyProduction(producerId, isManual: false);
        }

        static bool ApplyProduction(string producerId, bool isManual)
        {
            if (Data == null)
                return false;

            var stats = ResolveProductionStats();
            var xp = ComputeTickXp(producerId, stats);
            if (isManual)
            {
                var bal = ConfigRegistry.Balance;
                var tapScale = bal != null && bal.destroyCityManualTapFuelScale > 0f
                    ? bal.destroyCityManualTapFuelScale
                    : 1f;
                xp *= tapScale;
            }

            if (xp <= 0f)
                return false;

            var maxLevelBefore = GetMaxFuelLevelForProducer(producerId);
            AddStoredFuelXp(producerId, xp);
            var instancesChanged = RecomputeProducerFuelInstances(producerId);
            var maxLevelAfter = GetMaxFuelLevelForProducer(producerId);
            MarkSaveDirty(immediate: isManual);
            ProductionApplied?.Invoke(producerId, xp);
            if (instancesChanged)
                FuelInstancesChanged?.Invoke();
            if (maxLevelAfter > maxLevelBefore)
                FuelLeveledUp?.Invoke(producerId, maxLevelAfter);
            return true;
        }

        static int GetMaxFuelLevelForProducer(string producerId)
        {
            EnsureProducerFuelXpInitialized(producerId);
            var entry = FindProducer(producerId);
            var totalXp = entry != null ? Mathf.Max(0f, entry.Value.storedFuelXp) : 0f;
            if (totalXp <= 1e-4f)
                return 0;

            return FuelXpMath.GetMaxLevelForTotalXp(totalXp, out _);
        }

        public static float GetStoredFuelXp(string producerId)
        {
            EnsureProducerFuelXpInitialized(producerId);
            var entry = FindProducer(producerId);
            return entry != null ? Mathf.Max(0f, entry.Value.storedFuelXp) : 0f;
        }

        public static System.Collections.Generic.List<DestroyCityModeSaveData.FuelInstanceSaveEntry> GetInstancesForProducer(string producerId)
        {
            var result = new System.Collections.Generic.List<DestroyCityModeSaveData.FuelInstanceSaveEntry>();
            if (Data?.fuelInstances == null)
                return result;

            for (var i = 0; i < Data.fuelInstances.Count; i++)
            {
                if (Data.fuelInstances[i].producerId == producerId)
                    result.Add(Data.fuelInstances[i]);
            }

            return result;
        }

        public static bool TryRemoveInstance(string instanceId, out DestroyCityModeSaveData.FuelInstanceSaveEntry removed)
        {
            removed = default;
            if (Data?.fuelInstances == null)
                return false;

            for (var i = 0; i < Data.fuelInstances.Count; i++)
            {
                if (Data.fuelInstances[i].instanceId != instanceId)
                    continue;

                removed = Data.fuelInstances[i];
                var producerId = removed.producerId;
                var removedXp = FuelXpMath.GetInstanceTotalXp(removed.level, removed.xp);
                Data.fuelInstances.RemoveAt(i);

                if (!string.IsNullOrWhiteSpace(producerId))
                {
                    AddStoredFuelXp(producerId, -removedXp);
                    RecomputeProducerFuelInstances(producerId);
                }

                MarkSaveDirty(immediate: true);
                FuelInstancesChanged?.Invoke();
                return true;
            }

            return false;
        }

        public static int ResolveMaxFuelSlots(string producerId, int fallback = 4)
        {
            var def = ResolveDef(producerId);
            return def != null ? Mathf.Max(1, def.maxFuelSlots) : Mathf.Max(1, fallback);
        }

        /// <summary>燃料显示/拆分等级上限（配置表 productionCapLevel，与生产器自身等级无关）。</summary>
        public static int GetProductionCapLevel(string producerId)
        {
            var def = ResolveDef(producerId);
            var fuelMax = FuelXpMath.GetMaxLevel();
            return def != null
                ? Mathf.Min(def.productionCapLevel, fuelMax)
                : fuelMax;
        }

        static void EnsureProducerFuelXpInitialized(string producerId)
        {
            var entry = EnsureProducerEntry(producerId);
            if (entry.hasStoredFuelXp)
                return;

            entry.storedFuelXp = SumProducerInstanceTotalXp(producerId);
            entry.hasStoredFuelXp = true;
            WriteProducer(entry);
        }

        static float SumProducerInstanceTotalXp(string producerId)
        {
            if (Data?.fuelInstances == null)
                return 0f;

            var sum = 0f;
            for (var i = 0; i < Data.fuelInstances.Count; i++)
            {
                var inst = Data.fuelInstances[i];
                if (inst.producerId != producerId)
                    continue;
                sum += FuelXpMath.GetInstanceTotalXp(inst.level, inst.xp);
            }

            return sum;
        }

        static void AddStoredFuelXp(string producerId, float delta)
        {
            EnsureProducerFuelXpInitialized(producerId);
            var entry = EnsureProducerEntry(producerId);
            entry.storedFuelXp = Mathf.Max(0f, entry.storedFuelXp + delta);
            entry.hasStoredFuelXp = true;
            WriteProducer(entry);
        }

        public static bool TryGetDisplayInstance(
            string producerId,
            out DestroyCityModeSaveData.FuelInstanceSaveEntry instance) =>
            TryGetProducerDisplayInstance(producerId, out instance);

        /// <summary>根据 storedFuelXp 同步该生产器的单个显示燃料。</summary>
        static bool RecomputeProducerFuelInstances(string producerId)
        {
            if (Data?.fuelInstances == null)
                return false;

            EnsureProducerFuelXpInitialized(producerId);
            var entry = FindProducer(producerId);
            var totalXp = entry != null ? Mathf.Max(0f, entry.Value.storedFuelXp) : 0f;
            var cap = GetProductionCapLevel(producerId);

            TryGetProducerDisplayInstance(producerId, out var previous);
            var existingId = previous.instanceId;
            var hasDisplay = FuelProductionLogic.TryBuildDisplayEntry(
                totalXp,
                producerId,
                cap,
                existingId,
                out var next);

            if (!hasDisplay)
            {
                if (string.IsNullOrWhiteSpace(existingId))
                    return false;

                RemoveProducerInstances(producerId);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(existingId) && DisplayEquivalent(previous, next))
                return false;

            if (!string.IsNullOrWhiteSpace(existingId)
                && previous.level == next.level
                && previous.instanceId == next.instanceId)
            {
                for (var i = 0; i < Data.fuelInstances.Count; i++)
                {
                    if (Data.fuelInstances[i].instanceId != existingId)
                        continue;

                    Data.fuelInstances[i] = next;
                    return false;
                }
            }

            RemoveProducerInstances(producerId);
            Data.fuelInstances.Add(next);
            return true;
        }

        static bool TryGetProducerDisplayInstance(
            string producerId,
            out DestroyCityModeSaveData.FuelInstanceSaveEntry instance)
        {
            instance = default;
            if (Data?.fuelInstances == null)
                return false;

            var stableId = FuelProductionLogic.BuildStableInstanceId(producerId);
            DestroyCityModeSaveData.FuelInstanceSaveEntry fallback = default;
            var hasFallback = false;

            for (var i = 0; i < Data.fuelInstances.Count; i++)
            {
                var inst = Data.fuelInstances[i];
                if (inst.producerId != producerId)
                    continue;

                if (inst.instanceId == stableId)
                {
                    instance = inst;
                    return true;
                }

                if (!hasFallback)
                {
                    fallback = inst;
                    hasFallback = true;
                }
            }

            if (!hasFallback)
                return false;

            instance = fallback;
            return true;
        }

        static void RemoveProducerInstances(string producerId)
        {
            if (Data?.fuelInstances == null)
                return;

            for (var i = Data.fuelInstances.Count - 1; i >= 0; i--)
            {
                if (Data.fuelInstances[i].producerId == producerId)
                    Data.fuelInstances.RemoveAt(i);
            }
        }

        static bool DisplayEquivalent(
            DestroyCityModeSaveData.FuelInstanceSaveEntry previous,
            DestroyCityModeSaveData.FuelInstanceSaveEntry next) =>
            previous.level == next.level
            && Mathf.Abs(previous.xp - next.xp) <= 1e-3f;

        /// <summary>加载后根据燃料总经验重算显示（迁移旧存档多实例）。</summary>
        public static void SyncProducerDisplayFromStoredXp(string producerId)
        {
            if (Data == null)
                return;

            EnsureProducerFuelXpInitialized(producerId);
            if (RecomputeProducerFuelInstances(producerId))
                FuelInstancesChanged?.Invoke();
        }

        static void MarkUnlocked(string producerId)
        {
            var entry = EnsureProducerEntry(producerId);
            entry.unlocked = true;
            WriteProducer(entry);
            MarkSaveDirty(immediate: true);
            ProducerUnlocked?.Invoke(producerId);
        }

        static DestroyCityModeSaveData.FuelProducerSaveEntry EnsureProducerEntry(string producerId)
        {
            var found = FindProducer(producerId);
            if (found != null)
                return found.Value;

            var def = ResolveDef(producerId);
            var entry = new DestroyCityModeSaveData.FuelProducerSaveEntry
            {
                producerId = producerId,
                level = 1,
                unlocked = def != null && def.unlockPolicy == FuelProducerUnlockPolicy.AlwaysOpen,
                storedFuelXp = 0f,
                hasStoredFuelXp = true,
            };
            Data.fuelProducers.Add(entry);
            return entry;
        }

        static DestroyCityModeSaveData.FuelProducerSaveEntry? FindProducer(string producerId)
        {
            if (Data?.fuelProducers == null)
                return null;

            for (var i = 0; i < Data.fuelProducers.Count; i++)
            {
                if (Data.fuelProducers[i].producerId == producerId)
                    return Data.fuelProducers[i];
            }

            return null;
        }

        static void WriteProducer(DestroyCityModeSaveData.FuelProducerSaveEntry entry)
        {
            if (Data?.fuelProducers == null)
                return;

            for (var i = 0; i < Data.fuelProducers.Count; i++)
            {
                if (Data.fuelProducers[i].producerId != entry.producerId)
                    continue;
                Data.fuelProducers[i] = entry;
                return;
            }

            Data.fuelProducers.Add(entry);
        }

        static FuelProducerDef CreateFallbackDef(string producerId)
        {
            var def = ScriptableObject.CreateInstance<FuelProducerDef>();
            def.producerId = producerId;
            def.displayName = producerId;
            def.isManual = producerId == FuelProducerIds.Manual;
            def.autoIntervalSeconds = 1f;
            def.unlockPolicy = def.isManual
                ? FuelProducerUnlockPolicy.AlwaysOpen
                : FuelProducerUnlockPolicy.CoinOnce;
            def.unlockCoinCost = 5000;
            def.baseXpPerTick = 10f;
            return def;
        }
    }
}

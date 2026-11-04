using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Economy;
using Game.Save;
using Game.Core.Config;
using Game.Audio;

namespace Game.Collectibles.Pets
{
    public static class PetHatchService
    {
        public const int PremiumSingleHatchGemCost = 20;

        public static int GetEstimatedSingleHatchCostCoins() => PetHatchProbability.GetSingleHatchCoinCost();

        public static bool TryHatchOnce(out PetDef obtained)
        {
            obtained = null;
            if (SaveService.Data == null) return false;

            var db = ConfigRegistry.Pets;
            if (db == null) return false;

            var pool = BuildPool(db);
            if (pool.Count == 0) return false;

            var cost = EstimateSingleCostPublic(pool);
            if (cost > 0 && SaveService.Data.coins < cost) return false;

            if (!PetService.CanAddToInventory(1)) return false;

            var pet = WeightedPick(pool);
            if (pet == null) return false;

            if (cost > 0)
                SaveService.Data.coins -= cost;

            if (!PetService.TryAdd(pet.petId, 1))
            {
                if (cost > 0) SaveService.Data.coins += cost;
                return false;
            }

            obtained = pet;
            if (cost > 0)
                AudioGameplayFeedback.PlayResourceDeduct();
            SaveService.Save();
            NotifyFirstHatchIfNeeded();
            return true;
        }

        public static bool TryHatchOnceWithGems(out PetDef obtained)
        {
            obtained = null;
            if (SaveService.Data == null) return false;
            if (SaveService.Data.gems < PremiumSingleHatchGemCost) return false;

            var db = ConfigRegistry.Pets;
            if (db == null) return false;

            var pool = BuildPool(db);
            if (pool.Count == 0) return false;

            if (!PetService.CanAddToInventory(1)) return false;

            SaveService.Data.gems -= PremiumSingleHatchGemCost;
            var pet = WeightedPick(pool);
            if (pet == null)
            {
                SaveService.Data.gems += PremiumSingleHatchGemCost;
                return false;
            }

            if (!PetService.TryAdd(pet.petId, 1))
            {
                SaveService.Data.gems += PremiumSingleHatchGemCost;
                return false;
            }

            obtained = pet;
            AudioGameplayFeedback.PlayResourceDeduct();
            SaveService.Save();
            NotifyFirstHatchIfNeeded();
            return true;
        }

        public static bool TryHatchFive(out List<PetDef> obtained)
        {
            obtained = new List<PetDef>();
            if (SaveService.Data == null) return false;

            var db = ConfigRegistry.Pets;
            if (db == null) return false;

            var pool = BuildPool(db);
            if (pool.Count == 0) return false;

            var costOne = EstimateSingleCostPublic(pool);
            var totalCost = (long)costOne * 5L;
            if (SaveService.Data.coins < totalCost) return false;
            if (!PetService.CanAddToInventory(5)) return false;

            SaveService.Data.coins -= totalCost;

            var picked = new PetDef[5];
            for (var i = 0; i < 5; i++)
            {
                picked[i] = WeightedPick(pool);
                if (picked[i] != null) continue;
                SaveService.Data.coins += totalCost;
                return false;
            }

            for (var i = 0; i < 5; i++)
            {
                if (PetService.TryAdd(picked[i].petId, 1)) continue;
                SaveService.Data.coins += totalCost;
                for (var k = 0; k < i; k++)
                    PetService.Remove(picked[k].petId, 1);
                return false;
            }

            obtained.AddRange(picked);
            AudioGameplayFeedback.PlayResourceDeduct();
            SaveService.Save();
            NotifyFirstHatchIfNeeded();
            return true;
        }

        public static bool TryHatchFiveWithGems(out List<PetDef> obtained)
        {
            obtained = new List<PetDef>();
            if (SaveService.Data == null) return false;
            if (SaveService.Data.gems < (long)PremiumSingleHatchGemCost * 5L) return false;
            if (!PetService.CanAddToInventory(5)) return false;

            var db = ConfigRegistry.Pets;
            if (db == null) return false;
            var pool = BuildPool(db);
            if (pool.Count == 0) return false;

            SaveService.Data.gems -= PremiumSingleHatchGemCost * 5;
            var picked = new PetDef[5];
            for (var i = 0; i < 5; i++)
            {
                picked[i] = WeightedPick(pool);
                if (picked[i] != null) continue;
                SaveService.Data.gems += PremiumSingleHatchGemCost * 5;
                return false;
            }

            for (var i = 0; i < 5; i++)
            {
                if (PetService.TryAdd(picked[i].petId, 1)) continue;
                SaveService.Data.gems += PremiumSingleHatchGemCost * 5;
                for (var k = 0; k < i; k++)
                    PetService.Remove(picked[k].petId, 1);
                return false;
            }

            obtained.AddRange(picked);
            AudioGameplayFeedback.PlayResourceDeduct();
            SaveService.Save();
            NotifyFirstHatchIfNeeded();
            return true;
        }

        internal static List<PetDef> BuildPool(PetDatabase db)
        {
            var pool = new List<PetDef>();
            foreach (var p in db.pets)
            {
                if (p == null) continue;
                if (p.hatchWeight <= 0) continue;
                pool.Add(p);
            }
            return pool;
        }

        internal static int EstimateSingleCostPublic(List<PetDef> pool)
        {
            if (pool == null || pool.Count == 0) return 0;
            var sum = 0;
            var n = 0;
            foreach (var p in pool)
            {
                sum += Mathf.Max(0, p.hatchCostCoins);
                n++;
            }
            return Mathf.Max(0, Mathf.RoundToInt((float)sum / Mathf.Max(1, n)));
        }

        private static PetDef WeightedPick(List<PetDef> pool)
        {
            var total = 0;
            foreach (var p in pool) total += Mathf.Max(0, p.hatchWeight);
            if (total <= 0) return null;

            var r = UnityEngine.Random.Range(0, total);
            var acc = 0;
            foreach (var p in pool)
            {
                acc += Mathf.Max(0, p.hatchWeight);
                if (r < acc) return p;
            }
            return pool[pool.Count - 1];
        }

        /// <summary>配置化卡池：单抽。</summary>
        public static bool TryHatchOnce(PetHatchPoolConfig cfg, out PetDef obtained)
        {
            obtained = null;
            if (cfg == null || SaveService.Data == null)
                return false;

            var db = ConfigRegistry.Pets;
            if (!TryBuildWeightedPickList(cfg, db, out var list) || list.Count == 0)
                return false;

            var cost = cfg.singleDrawCost < 0 ? 0 : cfg.singleDrawCost;
            if (!CanSpend(cfg.currency, cost))
                return false;
            if (!PetService.CanAddToInventory(1))
                return false;

            Spend(cfg.currency, cost);
            var pet = WeightedPickFromWeightedList(list);
            if (pet == null || !PetService.TryAdd(pet.petId, 1))
            {
                Refund(cfg.currency, cost);
                return false;
            }

            obtained = pet;
            AudioGameplayFeedback.PlayResourceDeduct();
            SaveService.Save();
            NotifyFirstHatchIfNeeded();
            return true;
        }

        /// <summary>配置化卡池：五连。</summary>
        public static bool TryHatchFive(PetHatchPoolConfig cfg, out List<PetDef> obtained)
        {
            obtained = new List<PetDef>();
            if (cfg == null || SaveService.Data == null)
                return false;

            var db = ConfigRegistry.Pets;
            if (!TryBuildWeightedPickList(cfg, db, out var list) || list.Count == 0)
                return false;

            var totalCost = cfg.GetFiveDrawCost();
            if (totalCost < 0)
                totalCost = 0;
            if (!CanSpend(cfg.currency, totalCost))
                return false;
            if (!PetService.CanAddToInventory(5))
                return false;

            Spend(cfg.currency, totalCost);

            var picked = new PetDef[5];
            for (var i = 0; i < 5; i++)
                picked[i] = WeightedPickFromWeightedList(list);

            for (var i = 0; i < 5; i++)
            {
                if (picked[i] != null)
                    continue;
                Refund(cfg.currency, totalCost);
                return false;
            }

            for (var i = 0; i < 5; i++)
            {
                if (PetService.TryAdd(picked[i].petId, 1))
                    continue;
                Refund(cfg.currency, totalCost);
                for (var k = 0; k < i; k++)
                    PetService.Remove(picked[k].petId, 1);
                return false;
            }

            obtained.AddRange(picked);
            AudioGameplayFeedback.PlayResourceDeduct();
            SaveService.Save();
            NotifyFirstHatchIfNeeded();
            return true;
        }

        static bool TryBuildWeightedPickList(PetHatchPoolConfig cfg, PetDatabase db,
            out List<(PetDef def, int weight)> list)
        {
            list = new List<(PetDef def, int weight)>();
            if (cfg?.entries == null)
                return false;

            foreach (var e in cfg.entries)
            {
                if (e?.pet == null || e.weight <= 0)
                    continue;
                var id = e.pet.petId?.Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                var def = e.pet;
                if (db != null && db.TryGet(id, out var registered) && registered != null)
                    def = registered;

                list.Add((def, e.weight));
            }

            return list.Count > 0;
        }

        static PetDef WeightedPickFromWeightedList(List<(PetDef def, int weight)> list)
        {
            var total = 0;
            foreach (var x in list)
                total += Mathf.Max(0, x.weight);
            if (total <= 0)
                return null;

            var r = UnityEngine.Random.Range(0, total);
            var acc = 0;
            foreach (var x in list)
            {
                acc += Mathf.Max(0, x.weight);
                if (r < acc)
                    return x.def;
            }

            return list[list.Count - 1].def;
        }

        static bool CanSpend(HatchSpendCurrency c, long cost)
        {
            var d = SaveService.Data;
            if (d == null)
                return false;
            return c == HatchSpendCurrency.Gems ? d.gems >= cost : d.coins >= cost;
        }

        static void Spend(HatchSpendCurrency c, long cost)
        {
            var d = SaveService.Data;
            if (d == null)
                return;
            if (c == HatchSpendCurrency.Gems)
                d.gems -= cost;
            else
                d.coins -= cost;
        }

        static void Refund(HatchSpendCurrency c, long cost)
        {
            var d = SaveService.Data;
            if (d == null)
                return;
            if (c == HatchSpendCurrency.Gems)
                d.gems += cost;
            else
                d.coins += cost;
        }

        static void NotifyFirstHatchIfNeeded()
        {
            DiscountOfferService.NotifyFirstPetHatched();
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;
using Game.Core.Config;
using Game.Audio;

namespace Game.Collectibles.Pets
{
    public static class PetService
    {
        public const int EquipSlotCount = PlayerSaveData.PetEquipSlotCount;

        public readonly struct PetInventoryRow
        {
            public readonly string InstanceId;
            public readonly string PetId;
            public readonly PetDef Def;

            public PetInventoryRow(string instanceId, string petId, PetDef def)
            {
                InstanceId = instanceId;
                PetId = petId;
                Def = def;
            }
        }

        public static bool Has(string petId, int count = 1)
        {
            if (SaveService.Data == null) return false;
            return SaveService.Data.GetPetCount(petId ?? string.Empty) >= count;
        }

        public static int GetCount(string petId)
        {
            if (SaveService.Data == null) return 0;
            return SaveService.Data.GetPetCount(petId ?? string.Empty);
        }

        public static void Add(string petId, int count = 1)
        {
            if (SaveService.Data == null) return;
            if (string.IsNullOrWhiteSpace(petId) || count <= 0) return;
            SaveService.Data.AddPet(petId, count);
        }

        public static bool Remove(string petId, int count = 1)
        {
            if (SaveService.Data == null) return false;
            if (string.IsNullOrWhiteSpace(petId) || count <= 0) return false;
            return SaveService.Data.RemovePet(petId, count);
        }

        public static int GetUnlockedEquipSlotCount()
        {
            if (SaveService.Data == null) return PetBackpackEconomy.EquipSlotsInitial;
            EnsureLoadout();
            return Mathf.Clamp(SaveService.Data.petUnlockedEquipSlots,
                PetBackpackEconomy.EquipSlotsInitial, PetBackpackEconomy.EquipSlotsMax);
        }

        public static int GetBackpackCapacity()
        {
            if (SaveService.Data == null) return PetBackpackEconomy.BackpackCapacityInitial;
            EnsureLoadout();
            return Mathf.Clamp(SaveService.Data.petBackpackCapacity,
                PetBackpackEconomy.BackpackCapacityInitial, PetBackpackEconomy.BackpackCapacityMax);
        }

        /// <summary>当前已装备、占用可携带栏的宠物数量（不计空槽）。</summary>
        public static int CountEquippedPetsInUnlockedSlots()
        {
            if (SaveService.Data == null) return 0;
            EnsureLoadout();
            var cap = GetUnlockedEquipSlotCount();
            var n = 0;
            for (var i = 0; i < cap; i++)
            {
                if (!string.IsNullOrWhiteSpace(GetEquippedInstanceId(i))) n++;
            }

            return n;
        }

        public static long GetNextEquipSlotExpandCostCoins()
        {
            var n = GetUnlockedEquipSlotCount();
            if (n >= PetBackpackEconomy.EquipSlotsMax) return 0;
            var idx = n - PetBackpackEconomy.EquipSlotsInitial;
            var bal = ConfigRegistry.Balance;
            if (bal != null && bal.petCarrySlotExpandGoldByTier != null && idx >= 0 &&
                idx < bal.petCarrySlotExpandGoldByTier.Length)
                return bal.petCarrySlotExpandGoldByTier[idx];
            return PetBackpackEconomy.EquipSlotExpandCostCoins;
        }

        public static long GetNextBackpackExpandCostCoins()
        {
            if (SaveService.Data == null) return PetBackpackEconomy.BackpackExpandCostCoins;
            EnsureLoadout();
            var cap = GetBackpackCapacity();
            if (cap >= PetBackpackEconomy.BackpackCapacityMax) return 0;
            var idx = (cap - PetBackpackEconomy.BackpackCapacityInitial) / PetBackpackEconomy.BackpackExpandStep;
            var bal = ConfigRegistry.Balance;
            if (bal != null && bal.petBackpackExpandGoldByTier != null && idx >= 0 &&
                idx < bal.petBackpackExpandGoldByTier.Length)
                return bal.petBackpackExpandGoldByTier[idx];
            return PetBackpackEconomy.BackpackExpandCostCoins;
        }

        public static int GetTotalInventoryPetItemCount()
        {
            if (SaveService.Data?.petInstances == null) return 0;
            return SaveService.Data.petInstances.Count;
        }

        public static bool CanAddToInventory(int count = 1)
        {
            if (count <= 0) return true;
            return GetTotalInventoryPetItemCount() + count <= GetBackpackCapacity();
        }

        public static bool TryAdd(string petId, int count = 1)
        {
            if (SaveService.Data == null) return false;
            if (string.IsNullOrWhiteSpace(petId) || count <= 0) return false;
            if (!CanAddToInventory(count)) return false;
            SaveService.Data.AddPet(petId, count);
            return true;
        }

        public static bool TryExpandEquipSlotSpendCoins()
        {
            if (SaveService.Data == null) return false;
            EnsureLoadout();
            var d = SaveService.Data;
            var n = GetUnlockedEquipSlotCount();
            if (n >= PetBackpackEconomy.EquipSlotsMax) return false;
            var cost = GetNextEquipSlotExpandCostCoins();
            if (d.coins < cost) return false;
            d.coins -= cost;
            d.petUnlockedEquipSlots = n + 1;
            EnsureLoadout();
            AudioGameplayFeedback.PlayResourceDeduct();
            return true;
        }

        public static bool TryExpandBackpackSpendCoins()
        {
            if (SaveService.Data == null) return false;
            EnsureLoadout();
            var d = SaveService.Data;
            if (d.petBackpackCapacity >= PetBackpackEconomy.BackpackCapacityMax) return false;
            var cost = GetNextBackpackExpandCostCoins();
            if (d.coins < cost) return false;
            d.coins -= cost;
            d.petBackpackCapacity = Mathf.Min(
                d.petBackpackCapacity + PetBackpackEconomy.BackpackExpandStep,
                PetBackpackEconomy.BackpackCapacityMax);
            AudioGameplayFeedback.PlayResourceDeduct();
            return true;
        }

        public static void EnsureLoadout()
        {
            SaveService.Data?.EnsurePetLoadoutMigrated();
        }

        /// <summary>装备栏槽位引用（宠物实例 id）。</summary>
        public static string GetEquippedInstanceId(int slot)
        {
            if (SaveService.Data == null) return string.Empty;
            EnsureLoadout();
            if (slot < 0 || slot >= EquipSlotCount) return string.Empty;
            return SaveService.Data.equippedPetIds[slot] ?? string.Empty;
        }

        /// <summary>装备栏槽位对应的宠物配置 id（用于Prefab/属性；与旧 GetEquippedPetId 含义一致）。</summary>
        public static string GetEquippedPetDefId(int slot) => GetEquippedPetId(slot);

        /// <summary>兼容旧调用名：返回槽位内宠物类型 id，而非实例 id。</summary>
        public static string GetEquippedPetId(int slot)
        {
            var instId = GetEquippedInstanceId(slot);
            if (string.IsNullOrWhiteSpace(instId)) return string.Empty;
            if (SaveService.Data != null &&
                SaveService.Data.TryResolveInstanceToPetId(instId, out var pid) &&
                !string.IsNullOrWhiteSpace(pid))
                return pid;
            return string.Empty;
        }

        public static bool IsInstanceEquipped(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) return false;
            for (var i = 0; i < EquipSlotCount; i++)
            {
                if (GetEquippedInstanceId(i) == instanceId) return true;
            }

            return false;
        }

        public static bool TryResolveInstanceToPetId(string instanceId, out string petId)
        {
            petId = null;
            if (SaveService.Data == null) return false;
            return SaveService.Data.TryResolveInstanceToPetId(instanceId, out petId);
        }

        /// <summary>从背包移除指定实例（已装备会先卸下）。钓鱼模式出售用。</summary>
        public static bool TryRemoveInstance(string instanceId)
        {
            if (SaveService.Data == null || string.IsNullOrWhiteSpace(instanceId))
                return false;
            if (!ContainsInstanceId(instanceId))
                return false;
            if (IsInstanceEquipped(instanceId))
                UnequipInstance(instanceId);
            return SaveService.Data.RemovePetInstanceById(instanceId.Trim());
        }

        /// <summary>已弃用按类型统计装备数；判断某实例是否被装备请用 <see cref="IsInstanceEquipped"/>。</summary>
        public static int CountEquippedSlots(string petId)
        {
            if (string.IsNullOrWhiteSpace(petId)) return 0;
            var n = 0;
            for (var i = 0; i < EquipSlotCount; i++)
            {
                if (GetEquippedPetId(i) == petId) n++;
            }

            return n;
        }

        public static bool ValidateEquippedSlots(IReadOnlyList<string> slotInstanceIds)
        {
            if (slotInstanceIds == null || slotInstanceIds.Count != EquipSlotCount) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < EquipSlotCount; i++)
            {
                var v = slotInstanceIds[i];
                if (string.IsNullOrWhiteSpace(v)) continue;
                if (!seen.Add(v)) return false;
                if (!ContainsInstanceId(v)) return false;
            }

            return true;
        }

        static bool ContainsInstanceId(string instanceId)
        {
            if (SaveService.Data?.petInstances == null) return false;
            for (var i = 0; i < SaveService.Data.petInstances.Count; i++)
            {
                if (SaveService.Data.petInstances[i].instanceId == instanceId) return true;
            }

            return false;
        }

        public static bool TryEquipSlot(int slot, string instanceId)
        {
            if (SaveService.Data == null) return false;
            EnsureLoadout();
            if (slot < 0 || slot >= EquipSlotCount) return false;
            if (slot >= GetUnlockedEquipSlotCount()) return false;

            var d = SaveService.Data;
            var trimmed = string.IsNullOrWhiteSpace(instanceId) ? "" : instanceId.Trim();

            var copy = new List<string>(d.equippedPetIds);
            while (copy.Count < EquipSlotCount) copy.Add("");
            copy[slot] = trimmed;

            if (!string.IsNullOrEmpty(trimmed))
            {
                if (!ContainsInstanceId(trimmed)) return false;
                for (var i = 0; i < EquipSlotCount; i++)
                {
                    if (i != slot && copy[i] == trimmed) return false;
                }
            }

            if (!ValidateEquippedSlots(copy)) return false;

            d.equippedPetIds[slot] = trimmed;
            d.SyncLegacyEquippedPetId();
            return true;
        }

        public static void UnequipInstance(string instanceId)
        {
            if (SaveService.Data == null || string.IsNullOrWhiteSpace(instanceId)) return;
            EnsureLoadout();
            var d = SaveService.Data;
            for (var i = 0; i < EquipSlotCount; i++)
            {
                if (d.equippedPetIds[i] == instanceId)
                    d.equippedPetIds[i] = "";
            }

            d.SyncLegacyEquippedPetId();
        }

        public static void ClearPetFromAllSlots(string petId)
        {
            if (SaveService.Data == null || string.IsNullOrWhiteSpace(petId)) return;
            EnsureLoadout();
            for (var i = 0; i < EquipSlotCount; i++)
            {
                if (GetEquippedPetId(i) == petId) SaveService.Data.equippedPetIds[i] = "";
            }

            SaveService.Data.SyncLegacyEquippedPetId();
        }

        public static void UnequipAllPetSlots()
        {
            if (SaveService.Data == null) return;
            EnsureLoadout();
            var d = SaveService.Data;
            for (var i = 0; i < EquipSlotCount; i++)
                d.equippedPetIds[i] = "";
            d.SyncLegacyEquippedPetId();
        }

        public static void Equip(string petId)
        {
            if (SaveService.Data == null) return;
            EnsureLoadout();
            if (string.IsNullOrWhiteSpace(petId))
            {
                UnequipAllPetSlots();
                return;
            }

            var inst = FindFirstUnequippedInstanceId(petId);
            if (inst != null) TryEquipSlot(0, inst);
        }

        public static bool TryEquipFirstUnequippedOfType(int slot, string petId)
        {
            var inst = FindFirstUnequippedInstanceId(petId);
            if (inst == null) return false;
            return TryEquipSlot(slot, inst);
        }

        static string FindFirstUnequippedInstanceId(string petId)
        {
            if (SaveService.Data?.petInstances == null) return null;
            for (var i = 0; i < SaveService.Data.petInstances.Count; i++)
            {
                var e = SaveService.Data.petInstances[i];
                if (e.petId != petId) continue;
                if (IsInstanceEquipped(e.instanceId)) continue;
                return e.instanceId;
            }

            return null;
        }

        public static List<PetDef> GetOwnedSorted()
        {
            var list = new List<PetDef>();
            var db = ConfigRegistry.Pets;
            if (db == null || SaveService.Data == null) return list;
            EnsureLoadout();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (SaveService.Data.petInstances != null)
            {
                for (var i = 0; i < SaveService.Data.petInstances.Count; i++)
                {
                    var pid = SaveService.Data.petInstances[i].petId;
                    if (!seen.Add(pid)) continue;
                    if (db.TryGet(pid, out var def) && def != null) list.Add(def);
                }
            }

            list.Sort(ComparePetsForUI);
            return list;
        }

        public static List<PetInventoryRow> GetOwnedInstancesSorted()
        {
            var rows = new List<PetInventoryRow>();
            var db = ConfigRegistry.Pets;
            if (db == null || SaveService.Data?.petInstances == null) return rows;
            EnsureLoadout();
            for (var i = 0; i < SaveService.Data.petInstances.Count; i++)
            {
                var e = SaveService.Data.petInstances[i];
                PetDef def = null;
                db.TryGet(e.petId, out def);
                rows.Add(new PetInventoryRow(e.instanceId, e.petId, def));
            }

            rows.Sort(CompareInventoryRows);
            return rows;
        }

        static int CompareInventoryRows(PetInventoryRow a, PetInventoryRow b)
        {
            var aEq = IsInstanceEquipped(a.InstanceId);
            var bEq = IsInstanceEquipped(b.InstanceId);
            if (aEq != bEq)
                return aEq ? -1 : 1;

            GetCoinSettlementBonuses(a, out var aMul, out var aAdd);
            GetCoinSettlementBonuses(b, out var bMul, out var bAdd);
            var mulCmp = bMul.CompareTo(aMul);
            if (mulCmp != 0) return mulCmp;
            var addCmp = bAdd.CompareTo(aAdd);
            if (addCmp != 0) return addCmp;

            if (a.Def != null && b.Def != null)
            {
                var c = ComparePetsForUI(a.Def, b.Def);
                if (c != 0) return c;
            }
            else if (a.Def != null) return -1;
            else if (b.Def != null) return 1;

            var pc = string.Compare(a.PetId, b.PetId, StringComparison.Ordinal);
            if (pc != 0) return pc;
            return string.Compare(a.InstanceId, b.InstanceId, StringComparison.Ordinal);
        }

        static void GetCoinSettlementBonuses(PetInventoryRow row, out float coinsMul, out float coinsAdd)
        {
            if (row.Def != null)
            {
                coinsMul = row.Def.modifiers.coinsMul;
                coinsAdd = row.Def.modifiers.coinsAdd;
            }
            else
            {
                coinsMul = 0f;
                coinsAdd = 0f;
            }
        }

        private static int ComparePetsForUI(PetDef a, PetDef b)
        {
            var q = b.quality.CompareTo(a.quality);
            if (q != 0) return q;
            var lvl = b.level.CompareTo(a.level);
            if (lvl != 0) return lvl;
            return string.Compare(a.petId, b.petId, StringComparison.Ordinal);
        }
    }
}

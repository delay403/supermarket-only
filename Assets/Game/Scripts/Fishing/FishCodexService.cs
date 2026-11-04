using System;
using System.Collections.Generic;
using Game.Collectibles;
using Game.Core.Config;
using Game.Fishing.Config;
using Game.Save;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>鱼图鉴单条展示数据（供 UI 层绑定；未解锁时 UI 可自行遮罩名称/图标）。</summary>
    public struct FishCodexEntry
    {
        public string fishId;
        public string displayName;
        public CosmeticQuality quality;
        public long sellPrice;
        public Sprite icon;
        public bool isDiscovered;
        public int catchCount;

        /// <summary>区域图鉴专用：该鱼在本区域鱼池中的权重；全图鉴为 0。</summary>
        public int zonePoolWeight;

        public FishDef def;
        public FishingZoneDef sourceZone;
    }

    /// <summary>
    /// 鱼图鉴：全图鉴 + 当前/指定钓鱼区图鉴；钓获时写入存档。
    /// </summary>
    public static class FishCodexService
    {
        public struct CodexSummary
        {
            public int totalCount;
            public int discoveredCount;
        }

        public static void RecordCatch(FishDef fish)
        {
            if (fish == null || string.IsNullOrWhiteSpace(fish.fishId))
                return;

            if (!Game.Core.GameProductSettings.IsFishingContentEnabled)
                return;

            var data = SaveService.Data?.fishing;
            if (data == null)
                return;

            data.RecordFishCatch(fish.fishId.Trim());
        }

        public static bool IsDiscovered(string fishId)
        {
            var data = SaveService.Data?.fishing;
            return data != null && data.IsFishDiscovered(fishId);
        }

        public static int GetCatchCount(string fishId)
        {
            var data = SaveService.Data?.fishing;
            return data != null ? data.GetFishCatchCount(fishId) : 0;
        }

        /// <summary>全鱼图鉴：FishDatabase 中所有鱼 + 是否已发现。</summary>
        public static List<FishCodexEntry> GetGlobalCodex()
        {
            var list = new List<FishCodexEntry>();
            var fishDb = ConfigRegistry.Fish;
            if (fishDb == null || fishDb.fish == null)
                return list;

            for (var i = 0; i < fishDb.fish.Count; i++)
            {
                var def = fishDb.fish[i];
                if (def == null || string.IsNullOrWhiteSpace(def.fishId))
                    continue;
                list.Add(BuildEntry(def, zoneWeight: 0, sourceZone: null));
            }

            SortEntries(list);
            return list;
        }

        /// <summary>指定钓鱼区图鉴：仅含该区域鱼池中的鱼。</summary>
        public static List<FishCodexEntry> GetZoneCodex(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                return new List<FishCodexEntry>();

            var zoneDb = ConfigRegistry.FishingZones;
            if (zoneDb == null || !zoneDb.TryGet(zoneId.Trim(), out var zone) || zone == null)
                return new List<FishCodexEntry>();

            return GetZoneCodex(zone);
        }

        public static List<FishCodexEntry> GetZoneCodex(FishingZoneDef zone)
        {
            var list = new List<FishCodexEntry>();
            if (zone?.fishPool == null || zone.fishPool.Length == 0)
                return list;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < zone.fishPool.Length; i++)
            {
                var e = zone.fishPool[i];
                var def = FishingZoneFishEntryUtility.ResolveFishDef(e);
                if (def == null)
                    continue;

                var id = def.fishId?.Trim() ?? "";
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                    continue;

                var w = FishingZoneFishEntryUtility.GetConfiguredWeight(e);
                list.Add(BuildEntry(def, w, zone));
            }

            SortEntries(list);
            return list;
        }

        /// <summary>当前玩家所在钓鱼区图鉴（需在钓鱼区内或传入 ActiveZone）。</summary>
        public static List<FishCodexEntry> GetCurrentZoneCodex(FishingZoneDef zoneDef)
        {
            return zoneDef != null ? GetZoneCodex(zoneDef) : new List<FishCodexEntry>();
        }

        public static List<FishCodexEntry> GetCurrentZoneCodex(PlayerFishingController controller)
        {
            return controller != null
                ? GetCurrentZoneCodex(controller.CurrentZoneDef)
                : new List<FishCodexEntry>();
        }

        public static CodexSummary SummarizeCurrentZone(PlayerFishingController controller)
        {
            return SummarizeZone(controller?.CurrentZoneDef);
        }

        public static CodexSummary SummarizeGlobal()
        {
            var entries = GetGlobalCodex();
            var discovered = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].isDiscovered)
                    discovered++;
            }

            return new CodexSummary
            {
                totalCount = entries.Count,
                discoveredCount = discovered,
            };
        }

        public static CodexSummary SummarizeZone(FishingZoneDef zone)
        {
            var entries = GetZoneCodex(zone);
            var discovered = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].isDiscovered)
                    discovered++;
            }

            return new CodexSummary
            {
                totalCount = entries.Count,
                discoveredCount = discovered,
            };
        }

        static FishCodexEntry BuildEntry(FishDef def, int zoneWeight, FishingZoneDef sourceZone)
        {
            var id = def.fishId.Trim();
            var discovered = IsDiscovered(id);
            var count = GetCatchCount(id);
            return new FishCodexEntry
            {
                fishId = id,
                displayName = def.displayName,
                quality = def.quality,
                sellPrice = def.rewardCoins,
                icon = def.icon,
                isDiscovered = discovered,
                catchCount = count,
                zonePoolWeight = zoneWeight,
                def = def,
                sourceZone = sourceZone,
            };
        }

        static void SortEntries(List<FishCodexEntry> list)
        {
            list.Sort((a, b) =>
            {
                var qa = a.def != null ? (int)a.def.quality : 0;
                var qb = b.def != null ? (int)b.def.quality : 0;
                var cmp = qb.CompareTo(qa);
                if (cmp != 0) return cmp;

                var ca = a.sellPrice;
                var cb = b.sellPrice;
                cmp = cb.CompareTo(ca);
                if (cmp != 0) return cmp;

                return string.Compare(a.fishId, b.fishId, StringComparison.Ordinal);
            });
        }
    }
}

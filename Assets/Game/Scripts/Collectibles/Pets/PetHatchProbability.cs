using System.Collections.Generic;
using UnityEngine;
using Game.Core.Config;

namespace Game.Collectibles.Pets
{
    /// <summary>
    /// 「查看概率」面板用：根据 <see cref="PetDef.hatchWeight"/> 展示百分比。
    /// </summary>
    public static class PetHatchProbability
    {
        public readonly struct Row
        {
            public readonly PetDef Def;
            public readonly float Probability01;

            public Row(PetDef def, float p)
            {
                Def = def;
                Probability01 = p;
            }
        }

        public static List<Row> BuildRows(out int totalWeight)
        {
            totalWeight = 0;
            var list = new List<Row>();
            var db = ConfigRegistry.Pets;
            if (db == null || db.pets == null) return list;

            foreach (var p in db.pets)
            {
                if (p == null || p.hatchWeight <= 0) continue;
                totalWeight += Mathf.Max(0, p.hatchWeight);
            }

            foreach (var p in db.pets)
            {
                if (p == null || p.hatchWeight <= 0) continue;
                var w = Mathf.Max(0, p.hatchWeight);
                var pct = totalWeight > 0 ? w / (float)totalWeight : 0f;
                list.Add(new Row(p, pct));
            }

            list.Sort((a, b) =>
            {
                var c = b.Probability01.CompareTo(a.Probability01);
                return c != 0 ? c : string.CompareOrdinal(a.Def.petId, b.Def.petId);
            });

            return list;
        }

        /// <summary>指定卡池配置的权重概率（用于概率弹窗）。</summary>
        public static List<Row> BuildRowsForPool(PetHatchPoolConfig pool, out int totalWeight)
        {
            totalWeight = 0;
            var list = new List<Row>();
            if (pool == null || pool.entries == null)
                return list;

            var db = ConfigRegistry.Pets;

            foreach (var e in pool.entries)
            {
                if (e?.pet == null || e.weight <= 0)
                    continue;
                if (string.IsNullOrWhiteSpace(e.pet.petId))
                    continue;
                totalWeight += Mathf.Max(0, e.weight);
            }

            foreach (var e in pool.entries)
            {
                if (e?.pet == null || e.weight <= 0)
                    continue;
                if (string.IsNullOrWhiteSpace(e.pet.petId))
                    continue;

                var def = e.pet;
                if (db != null && db.TryGet(e.pet.petId.Trim(), out var registered) && registered != null)
                    def = registered;

                var w = Mathf.Max(0, e.weight);
                var pct = totalWeight > 0 ? w / (float)totalWeight : 0f;
                list.Add(new Row(def, pct));
            }

            list.Sort((a, b) =>
            {
                var c = b.Probability01.CompareTo(a.Probability01);
                return c != 0 ? c : string.CompareOrdinal(a.Def.petId, b.Def.petId);
            });

            return list;
        }

        public static int GetSingleHatchCoinCost()
        {
            var db = ConfigRegistry.Pets;
            if (db == null) return 0;
            var pool = PetHatchService.BuildPool(db);
            return PetHatchService.EstimateSingleCostPublic(pool);
        }
    }
}

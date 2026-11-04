using System.Collections.Generic;
using UnityEngine;
using Game.Save;
using Game.Core.Config;

namespace Game.Collectibles.Pets
{
    public static class PetFusionService
    {
        /// <summary>三只合成失败时返还给玩家的材料宠数量。</summary>
        public const int FuseThreeFailureReturnCount = 2;

        /// <summary>一键合成统计：成功时产出的宠物 id 与次数（合并同类）。</summary>
        public readonly struct FusionRewardEntry
        {
            public readonly string PetId;
            public readonly int Count;

            public FusionRewardEntry(string petId, int count)
            {
                PetId = petId;
                Count = count;
            }
        }
        /// <summary>
        /// 三只相同宠物合成下一级；成功率取自材料 <see cref="PetDef.fusionSuccessChance01"/>。
        /// 失败消耗三只，返还两只材料。
        /// </summary>
        public static bool TryFuseThreeToNextLevel(string sourcePetId, out string resultPetId, out bool rollSuccess)
        {
            resultPetId = null;
            rollSuccess = false;
            if (SaveService.Data == null) return false;
            if (string.IsNullOrWhiteSpace(sourcePetId)) return false;
            if (!PetService.Has(sourcePetId, 3)) return false;

            var db = ConfigRegistry.Pets;
            if (db == null || !db.TryGet(sourcePetId, out var source) || source == null) return false;

            if (!TryFindNextLevelDef(source, db, out var target) || target == null)
                return false;

            resultPetId = target.petId;

            PetService.Remove(sourcePetId, 3);

            var chance = Mathf.Clamp01(source.fusionSuccessChance01);
            rollSuccess = Random.value < chance;
            if (rollSuccess)
                PetService.Add(target.petId, 1);
            else
                PetService.Add(sourcePetId, FuseThreeFailureReturnCount);

            return true;
        }

        public static bool CanFuseThreeToNextLevel(string sourcePetId)
        {
            if (!PetService.Has(sourcePetId, 3)) return false;
            var db = ConfigRegistry.Pets;
            return db != null && db.TryGet(sourcePetId, out var source) && source != null &&
                   TryFindNextLevelDef(source, db, out var t) && t != null;
        }

        /// <summary>兼容旧名。</summary>
        public static bool TryFuseTwoToNextLevel(string sourcePetId, out string resultPetId, out bool rollSuccess) =>
            TryFuseThreeToNextLevel(sourcePetId, out resultPetId, out rollSuccess);

        public static bool CanFuseTwoToNextLevel(string sourcePetId) => CanFuseThreeToNextLevel(sourcePetId);

        public static bool TryGetFusionTarget(string sourcePetId, out PetDef target)
        {
            target = null;
            var db = ConfigRegistry.Pets;
            if (db == null || string.IsNullOrWhiteSpace(sourcePetId) || !db.TryGet(sourcePetId, out var source) || source == null)
                return false;
            return TryFindNextLevelDef(source, db, out target);
        }

        /// <summary>一键合成：重复执行直到没有可用的 3 合 1（按配置表顺序尝试）。</summary>
        /// <param name="rewardsMerged">仅统计成功产出，按 petId 合并数量。</param>
        public static int RunOneKeyFuseAll(out int successCount, out int attemptCount,
            out List<FusionRewardEntry> rewardsMerged)
        {
            successCount = 0;
            attemptCount = 0;
            rewardsMerged = new List<FusionRewardEntry>();
            var db = ConfigRegistry.Pets;
            if (db == null || db.pets == null) return 0;

            var acc = new Dictionary<string, int>(System.StringComparer.Ordinal);
            var fusedAny = 0;
            bool progress;
            do
            {
                progress = false;
                foreach (var src in db.pets)
                {
                    if (src == null) continue;
                    while (CanFuseThreeToNextLevel(src.petId))
                    {
                        if (!TryFuseThreeToNextLevel(src.petId, out var resultId, out var ok))
                            break;
                        attemptCount++;
                        if (ok)
                        {
                            successCount++;
                            if (!string.IsNullOrWhiteSpace(resultId))
                            {
                                if (!acc.ContainsKey(resultId)) acc[resultId] = 0;
                                acc[resultId]++;
                            }
                        }

                        fusedAny++;
                        progress = true;
                    }
                }
            } while (progress);

            foreach (var kv in acc)
                rewardsMerged.Add(new FusionRewardEntry(kv.Key, kv.Value));

            rewardsMerged.Sort(SortRewardEntries);

            return fusedAny;
        }

        static int SortRewardEntries(FusionRewardEntry a, FusionRewardEntry b)
        {
            var db = ConfigRegistry.Pets;
            if (db == null) return string.CompareOrdinal(a.PetId, b.PetId);
            db.TryGet(a.PetId, out var da);
            db.TryGet(b.PetId, out var dbef);
            if (da != null && dbef != null)
            {
                var q = dbef.quality.CompareTo(da.quality);
                if (q != 0) return q;
                var l = dbef.level.CompareTo(da.level);
                if (l != 0) return l;
            }

            return string.CompareOrdinal(a.PetId, b.PetId);
        }

        private static bool TryFindNextLevelDef(PetDef source, PetDatabase db, out PetDef target)
        {
            target = null;
            if (source == null || db == null || db.pets == null) return false;

            if (!string.IsNullOrWhiteSpace(source.fusionTargetPetId))
            {
                var tid = source.fusionTargetPetId.Trim();
                if (db.TryGet(tid, out var explicitDef) && explicitDef != null)
                {
                    target = explicitDef;
                    return true;
                }
            }

            PetDef uniqueNextLevel = null;
            var nextLevelCount = 0;
            foreach (var p in db.pets)
            {
                if (p == null) continue;
                if (p.level != source.level + 1) continue;
                uniqueNextLevel = p;
                nextLevelCount++;
                if (nextLevelCount > 1)
                {
                    uniqueNextLevel = null;
                    break;
                }
            }

            if (nextLevelCount == 1 && uniqueNextLevel != null)
            {
                target = uniqueNextLevel;
                return true;
            }

            foreach (var p in db.pets)
            {
                if (p == null) continue;
                if (p.quality != source.quality) continue;
                if (p.level != source.level + 1) continue;
                target = p;
                return true;
            }

            return false;
        }
    }
}

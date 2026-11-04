using System.Text;
using UnityEngine;
using Game.Core;
using Game.Core.Config;
using Game.Collectibles.Pets;
using Game.Collectibles.Wings;
using Game.Collectibles.Trails;
using Game.Collectibles.Characters;
using Game.Save;

namespace Game.Collectibles.Stats
{
    /// <summary>
    /// 将当前地图、已装备宠物/翅膀/拖尾/角色的加成与最终合并属性打印到控制台（与 StatsAggregator 计算口径一致）。
    /// </summary>
    public static class StatBonusDebug
    {
        public static void LogFullBreakdown()
        {
            var bal = ConfigRegistry.Balance;
            var baseClimb = bal != null ? bal.climbSpeedBase : 2f;
            var cfgBaseCoins = bal != null ? bal.glideSettlementBaseCoins : 500f;
            var baseGlideCoins = cfgBaseCoins > 0.01f ? cfgBaseCoins : 500f;
            var cfgCoinDiv = bal != null ? bal.glideSettlementCoinDistanceDivisor : 0f;
            var coinDistDivisor = cfgCoinDiv > 0.01f ? cfgCoinDiv : 1000f;

            var sumClimbAdd = 0f;
            var sumClimbMul = 0f;
            var sumCoinsAdd = 0f;
            var sumCoinsMul = 0f;

            var sb = new StringBuilder(1024);
            sb.AppendLine("[StatBonus] === 基础 ===");
            sb.AppendLine($"  climbSpeedBase={baseClimb:F3}, glideSettlementBaseCoins={baseGlideCoins:F1}, glideSettlementCoinDistanceDivisor={coinDistDivisor:F1}");

            if (SaveService.Data == null)
            {
                sb.AppendLine("[StatBonus] SaveService.Data 为空，跳过装备与地图。");
                Debug.Log(sb.ToString());
                return;
            }

            var mapDb = ConfigRegistry.Maps;
            float mapClimbCorr = 0f, mapCoinCorr = 0f;
            if (mapDb != null && mapDb.TryGet(SaveService.Data.currentMapId, out var map) && map != null)
            {
                mapClimbCorr = map.climbSpeedMapCorrection;
                mapCoinCorr = map.glideCoinsMapCorrection;
                sb.AppendLine($"[StatBonus] === 地图 {SaveService.Data.currentMapId} ===");
                sb.AppendLine($"  climbSpeedMapCorrection={mapClimbCorr:F4}, glideCoinsMapCorrection={mapCoinCorr:F4}");
            }
            else
                sb.AppendLine("[StatBonus] === 地图：无或未找到（地图修正按 0）===");

            var petDb = ConfigRegistry.Pets;
            if (petDb != null)
            {
                PetService.EnsureLoadout();
                var anyPet = false;
                for (var s = 0; s < PetService.EquipSlotCount; s++)
                {
                    var pid = PetService.GetEquippedPetId(s);
                    if (string.IsNullOrWhiteSpace(pid)) continue;
                    if (!petDb.TryGet(pid, out var pet) || pet == null) continue;
                    anyPet = true;
                    sb.AppendLine($"[StatBonus] === 已装备宠物 栏{s + 1}: {pet.displayName} ({pet.petId}) ===");
                    AppendModifiers(sb, pet.modifiers);
                    Accumulate(pet.modifiers, ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
                }

                if (!anyPet)
                    sb.AppendLine("[StatBonus] === 宠物：三栏皆空 ===");
            }
            else
                sb.AppendLine("[StatBonus] === 宠物数据库未加载 ===");

            var wingDb = ConfigRegistry.Wings;
            if (wingDb != null && !string.IsNullOrWhiteSpace(SaveService.Data.equippedWingId) &&
                wingDb.TryGet(SaveService.Data.equippedWingId, out var wing) && wing != null)
            {
                sb.AppendLine($"[StatBonus] === 已装备翅膀 {wing.displayName} ({wing.wingId}) ===");
                AppendModifiers(sb, wing.modifiers);
                Accumulate(wing.modifiers, ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
            }
            else
                sb.AppendLine("[StatBonus] === 翅膀：未装备 ===");

            var trailDb = ConfigRegistry.Trails;
            if (trailDb != null && !string.IsNullOrWhiteSpace(SaveService.Data.equippedTrailId) &&
                trailDb.TryGet(SaveService.Data.equippedTrailId, out var trail) && trail != null)
            {
                sb.AppendLine($"[StatBonus] === 已装备拖尾 {trail.displayName} ({trail.trailId}) ===");
                AppendModifiers(sb, trail.modifiers);
                Accumulate(trail.modifiers, ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
            }
            else
                sb.AppendLine("[StatBonus] === 拖尾：未装备 ===");

            var charDb = ConfigRegistry.Characters;
            if (charDb != null && !string.IsNullOrWhiteSpace(SaveService.Data.equippedCharacterId) &&
                charDb.TryGet(SaveService.Data.equippedCharacterId, out var cha) && cha != null)
            {
                sb.AppendLine($"[StatBonus] === 已装备角色 {cha.displayName} ({cha.characterId}) ===");
                AppendModifiers(sb, cha.modifiers);
                Accumulate(cha.modifiers, ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
            }
            else
                sb.AppendLine("[StatBonus] === 角色：未装备 ===");

            var innerLinear = baseClimb + sumClimbAdd - mapClimbCorr;
            var coreLinear = Mathf.Max(baseClimb, innerLinear);
            var mulBracket = Mathf.Max(0f, 1f + sumClimbMul);
            var climbSpeed = coreLinear * mulBracket;
            var coinBracket = Mathf.Max(0f, 1f + sumCoinsMul + mapCoinCorr);

            sb.AppendLine("[StatBonus] === 合并（与 StatsAggregator 一致）===");
            sb.AppendLine($"  ΣclimbSpeedAdd={sumClimbAdd:F4}, ΣclimbSpeedMul={sumClimbMul:F4}");
            sb.AppendLine($"  core=max(base, base+Σadd-地图修正)={coreLinear:F4}, (1+Σmul)={mulBracket:F4} → climbSpeed={climbSpeed:F4}（再 × 体力系数 × 双倍奖励）");
            sb.AppendLine($"  ΣcoinsAdd={sumCoinsAdd:F4}, ΣcoinsMul={sumCoinsMul:F4}");
            sb.AppendLine($"  (1+ΣcoinsMul+地图金币修正)={coinBracket:F4}（用于 ×(基础+Σadd)×距离÷glideSettlementCoinDistanceDivisor）");

            var agg = UnityObjectFinder.FindFirst<StatsAggregator>();
            if (agg != null)
            {
                agg.Recompute();
                var c = agg.Current;
                sb.AppendLine("[StatBonus] === StatsAggregator 校验 ===");
                sb.AppendLine($"  climbSpeed={c.climbSpeed:F4}, totalCoinsAdd={c.totalCoinsAdd:F4}, totalCoinsMul={c.totalCoinsMul:F4}");
            }
            else
                sb.AppendLine("[StatBonus] 场景中无 StatsAggregator，跳过运行时校验。");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 结算帧打印：与 <see cref="StatsAggregator.Recompute"/> 相同遍历顺序下的金币加成来源（ΣcoinsAdd / ΣcoinsMul）。
        /// 须在 <c>stats.Recompute()</c> 之后、<see cref="Game.Economy.RewardCalculator.CalculateGlideSettlement"/> 之前调用，便于对照公式。
        /// <paramref name="passedSumCoinsAdd"/> / <paramref name="passedSumCoinsMul"/> 为即将传入 RewardCalculator 的养成合计（通常为 StatsAggregator.Current）。
        /// </summary>
        public static void LogSettlementCoinModifierSources(float passedSumCoinsAdd, float passedSumCoinsMul)
        {
            if (!Game.Economy.RewardCalculator.LogGlideCoinSettlement)
                return;

            var sb = new StringBuilder(768);
            sb.AppendLine("[SettlementCoinSources] 滑翔结算金币 · 装备/宠物金币项明细（与 StatsAggregator 合并顺序一致）");

            if (SaveService.Data == null)
            {
                sb.AppendLine("  SaveService.Data 为空，无法枚举。");
                Debug.Log(sb.ToString());
                return;
            }

            var sumAdd = 0f;
            var sumMul = 0f;

            var petDb = ConfigRegistry.Pets;
            if (petDb != null)
            {
                PetService.EnsureLoadout();
                for (var s = 0; s < PetService.EquipSlotCount; s++)
                {
                    var pid = PetService.GetEquippedPetId(s);
                    if (string.IsNullOrWhiteSpace(pid))
                    {
                        sb.AppendLine($"  宠物栏{s + 1}: （空）");
                        continue;
                    }

                    if (!petDb.TryGet(pid, out var pet) || pet == null)
                    {
                        sb.AppendLine($"  宠物栏{s + 1}: id={pid}（未找到定义）");
                        continue;
                    }

                    var m = pet.modifiers;
                    sumAdd += m.coinsAdd;
                    sumMul += m.coinsMul;
                    sb.AppendLine(
                        $"  宠物栏{s + 1}: {pet.displayName} ({pet.petId}) → coinsAdd={m.coinsAdd:F4}, coinsMul={m.coinsMul:F4}");
                }
            }
            else
                sb.AppendLine("  宠物数据库未加载。");

            var wingDb = ConfigRegistry.Wings;
            if (wingDb != null && !string.IsNullOrWhiteSpace(SaveService.Data.equippedWingId) &&
                wingDb.TryGet(SaveService.Data.equippedWingId, out var wing) && wing != null)
            {
                var m = wing.modifiers;
                sumAdd += m.coinsAdd;
                sumMul += m.coinsMul;
                sb.AppendLine($"  翅膀: {wing.displayName} ({wing.wingId}) → coinsAdd={m.coinsAdd:F4}, coinsMul={m.coinsMul:F4}");
            }
            else
                sb.AppendLine("  翅膀: （未装备）");

            var trailDb = ConfigRegistry.Trails;
            if (trailDb != null && !string.IsNullOrWhiteSpace(SaveService.Data.equippedTrailId) &&
                trailDb.TryGet(SaveService.Data.equippedTrailId, out var trail) && trail != null)
            {
                var m = trail.modifiers;
                sumAdd += m.coinsAdd;
                sumMul += m.coinsMul;
                sb.AppendLine($"  拖尾: {trail.displayName} ({trail.trailId}) → coinsAdd={m.coinsAdd:F4}, coinsMul={m.coinsMul:F4}");
            }
            else
                sb.AppendLine("  拖尾: （未装备）");

            var charDb = ConfigRegistry.Characters;
            if (charDb != null && !string.IsNullOrWhiteSpace(SaveService.Data.equippedCharacterId) &&
                charDb.TryGet(SaveService.Data.equippedCharacterId, out var ch) && ch != null)
            {
                var m = ch.modifiers;
                sumAdd += m.coinsAdd;
                sumMul += m.coinsMul;
                sb.AppendLine($"  角色: {ch.displayName} ({ch.characterId}) → coinsAdd={m.coinsAdd:F4}, coinsMul={m.coinsMul:F4}");
            }
            else
                sb.AppendLine("  角色: （未装备）");

            sb.AppendLine($"  上表逐行求和 → ΣcoinsAdd={sumAdd:F4}, ΣcoinsMul={sumMul:F4}");
            const float tol = 0.01f;
            if (Mathf.Abs(sumAdd - passedSumCoinsAdd) > tol || Mathf.Abs(sumMul - passedSumCoinsMul) > tol)
                sb.AppendLine(
                    $"  [WARN] 明细求和与传入 RewardCalculator 不一致：传入 ΣAdd={passedSumCoinsAdd:F4}, ΣMul={passedSumCoinsMul:F4}（请以传入为准追查 StatsAggregator 或场景是否挂载）");
            else
                sb.AppendLine("  [OK] 明细求和与传入 RewardCalculator 一致。");
            Debug.Log(sb.ToString());
        }

        private static void AppendModifiers(StringBuilder sb, StatModifiers m)
        {
            sb.AppendLine($"  climbSpeedAdd={m.climbSpeedAdd:F4}, climbSpeedMul={m.climbSpeedMul:F4}");
            sb.AppendLine($"  coinsMul={m.coinsMul:F4}, coinsAdd={m.coinsAdd:F4}");
        }

        private static void Accumulate(StatModifiers m, ref float sumClimbAdd, ref float sumClimbMul, ref float sumCoinsAdd, ref float sumCoinsMul)
        {
            sumClimbAdd += m.climbSpeedAdd;
            sumClimbMul += m.climbSpeedMul;
            sumCoinsAdd += m.coinsAdd;
            sumCoinsMul += m.coinsMul;
        }
    }
}

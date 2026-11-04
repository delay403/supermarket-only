using System.Text;
using Game.Collectibles.Characters;
using Game.Collectibles.Pets;
using Game.Collectibles.Rods;
using Game.Collectibles.Trails;
using Game.Collectibles.Wings;
using Game.Core;
using Game.Core.Config;
using Game.Save;
using UnityEngine;

namespace Game.Collectibles.Stats
{
    /// <summary>
    /// 打印钓鱼力量各项来源（与 <see cref="FishingStatsAggregator"/> 计算口径一致）。
    /// </summary>
    public static class FishingStatBonusDebug
    {
        public static void LogPowerBreakdown(string context = "Startup")
        {
            if (!GameProductSettings.IsFishingContentEnabled)
                return;

            var configDefaultBase = ConfigRegistry.ResolveFishingDefaultBaseStrength();

            var sumPowerAdd = 0f;
            var sumPowerMul = 0f;
            var sumProgressAdd = 0f;
            var sumCoinMul = 0f;
            var sumTrainMul = 0f;
            var hasSpecialCatch = false;

            var sb = new StringBuilder(1536);
            sb.AppendLine($"[FishingPower] === 钓鱼力量明细 ({context}) ===");
            sb.AppendLine("  公式: totalPower = baseStrength + ΣfishingPowerAdd + baseStrength × ΣfishingPowerMul");
            sb.AppendLine($"  [GameBalanceConfig] fishingDefaultBaseStrength={configDefaultBase:F2}（新档初始值 / 存档未初始化时的兜底，见 Resources/GameBalanceConfig）");
            sb.AppendLine($"  [GameBalanceConfig] fishingFreeStrengthBonus={ConfigRegistry.ResolveFishingFreeStrengthBonus():F2}（一次性加在存档 baseStrength 上）");

            if (SaveService.Data?.fishing == null)
            {
                sb.AppendLine("  [存档] fishing 数据为空，baseStrength 将使用配置默认值。");
                Debug.Log(sb.ToString());
                return;
            }

            var saveBase = SaveService.Data.fishing.baseStrength;
            var resolvedBase = FishingStatsAggregator.ComputeBaseStrength();
            sb.AppendLine($"  [存档] fishing.baseStrength={saveBase:F2}（力量训练 / 免费奖励累加在此字段）");
            sb.AppendLine($"  → 解析后 baseStrength={resolvedBase:F2}");

            AppendPetSection(sb, ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);
            AppendWingSection(sb, ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);
            AppendTrailSection(sb, ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);
            AppendCharacterSection(sb, ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);
            AppendRodSection(sb, ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);

            var mulContribution = FishingStatsAggregator.ComputePowerMulContribution(sumPowerMul, resolvedBase);
            var total = FishingStatsAggregator.ComputeTotalPower(sumPowerAdd, sumPowerMul);

            sb.AppendLine("[FishingPower] === 合并 ===");
            sb.AppendLine($"  ΣfishingPowerAdd={sumPowerAdd:F4}");
            sb.AppendLine($"  ΣfishingPowerMul={sumPowerMul:P1} → base×mul = {mulContribution:F4}");
            sb.AppendLine($"  totalPower={total:F2} (= {resolvedBase:F2} + {sumPowerAdd:F4} + {mulContribution:F4})");
            sb.AppendLine($"  ΣfishingProgressAdd={sumProgressAdd:F4}, ΣfishingCoinMul={sumCoinMul:F4}, ΣfishingTrainSpeedMul={sumTrainMul:F4}, 海洋眷属={(hasSpecialCatch ? "是" : "否")}");

            var agg = UnityObjectFinder.FindFirst<FishingStatsAggregator>();
            if (agg != null)
            {
                agg.Recompute();
                var c = agg.Current;
                sb.AppendLine("[FishingPower] === FishingStatsAggregator 校验 ===");
                sb.AppendLine($"  totalPower={c.totalPower:F2}, totalProgressAdd={c.totalProgressAdd:F4}, totalCoinMul={c.totalCoinMul:F4}");
            }

            sb.AppendLine("[FishingPower] 修改入口: 初始基础力量 → GameBalanceConfig.fishingDefaultBaseStrength + 存档 fishing.baseStrength；装备加成 → 各 WingDef/PetDef 等 modifiers.fishingPowerAdd / fishingPowerMul。");

            Debug.Log(sb.ToString());
        }

        static void AppendPetSection(
            StringBuilder sb,
            ref float sumPowerAdd,
            ref float sumPowerMul,
            ref float sumProgressAdd,
            ref float sumCoinMul,
            ref float sumTrainMul,
            ref bool hasSpecialCatch)
        {
            var petDb = ConfigRegistry.Pets;
            if (petDb == null)
            {
                sb.AppendLine("[FishingPower] 宠物: 数据库未加载");
                return;
            }

            PetService.EnsureLoadout();
            var any = false;
            for (var s = 0; s < PetService.EquipSlotCount; s++)
            {
                var pid = PetService.GetEquippedPetId(s);
                if (string.IsNullOrWhiteSpace(pid))
                    continue;

                any = true;
                if (!petDb.TryGet(pid, out var pet) || pet == null)
                {
                    sb.AppendLine($"[FishingPower] 宠物栏{s + 1}: id={pid}（未找到定义）");
                    continue;
                }

                sb.AppendLine($"[FishingPower] 宠物栏{s + 1}: {pet.displayName} ({pet.petId})");
                AppendFishingModifiers(sb, pet.modifiers, ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);
            }

            if (!any)
                sb.AppendLine("[FishingPower] 宠物: 三栏皆空");
        }

        static void AppendWingSection(
            StringBuilder sb,
            ref float sumPowerAdd,
            ref float sumPowerMul,
            ref float sumProgressAdd,
            ref float sumCoinMul,
            ref float sumTrainMul,
            ref bool hasSpecialCatch)
        {
            var wingId = SaveService.Data?.equippedWingId;
            if (string.IsNullOrWhiteSpace(wingId))
            {
                sb.AppendLine("[FishingPower] 翅膀: 未装备");
                return;
            }

            var wingDb = ConfigRegistry.Wings;
            if (wingDb == null)
            {
                sb.AppendLine($"[FishingPower] 翅膀: equippedWingId={wingId}（WingDatabase 未加载）");
                return;
            }

            if (!wingDb.TryGet(wingId, out var wing) || wing == null)
            {
                sb.AppendLine($"[FishingPower] 翅膀: equippedWingId={wingId}（数据库中未找到，加成不会生效）");
                return;
            }

            sb.AppendLine($"[FishingPower] 翅膀: {wing.displayName} ({wing.wingId})");
            AppendFishingModifiers(sb, wing.modifiers, ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);
        }

        static void AppendTrailSection(
            StringBuilder sb,
            ref float sumPowerAdd,
            ref float sumPowerMul,
            ref float sumProgressAdd,
            ref float sumCoinMul,
            ref float sumTrainMul,
            ref bool hasSpecialCatch)
        {
            var trailId = SaveService.Data?.equippedTrailId;
            if (string.IsNullOrWhiteSpace(trailId))
            {
                sb.AppendLine("[FishingPower] 拖尾: 未装备");
                return;
            }

            var trailDb = ConfigRegistry.Trails;
            if (trailDb == null || !trailDb.TryGet(trailId, out var trail) || trail == null)
            {
                sb.AppendLine($"[FishingPower] 拖尾: equippedTrailId={trailId}（未找到定义）");
                return;
            }

            sb.AppendLine($"[FishingPower] 拖尾: {trail.displayName} ({trail.trailId})");
            AppendFishingModifiers(sb, trail.modifiers, ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);
        }

        static void AppendCharacterSection(
            StringBuilder sb,
            ref float sumPowerAdd,
            ref float sumPowerMul,
            ref float sumProgressAdd,
            ref float sumCoinMul,
            ref float sumTrainMul,
            ref bool hasSpecialCatch)
        {
            var charId = SaveService.Data?.equippedCharacterId;
            if (string.IsNullOrWhiteSpace(charId))
            {
                sb.AppendLine("[FishingPower] 角色: 未装备");
                return;
            }

            var charDb = ConfigRegistry.Characters;
            if (charDb == null || !charDb.TryGet(charId, out var ch) || ch == null)
            {
                sb.AppendLine($"[FishingPower] 角色: equippedCharacterId={charId}（未找到定义）");
                return;
            }

            sb.AppendLine($"[FishingPower] 角色: {ch.displayName} ({ch.characterId})");
            AppendFishingModifiers(sb, ch.modifiers, ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);
        }

        static void AppendRodSection(
            StringBuilder sb,
            ref float sumPowerAdd,
            ref float sumPowerMul,
            ref float sumProgressAdd,
            ref float sumCoinMul,
            ref float sumTrainMul,
            ref bool hasSpecialCatch)
        {
            var rodId = SaveService.Data?.equippedRodId;
            if (string.IsNullOrWhiteSpace(rodId))
            {
                sb.AppendLine("[FishingPower] 钓竿: 未装备");
                return;
            }

            var rodDb = ConfigRegistry.Rods;
            if (rodDb == null || !rodDb.TryGet(rodId, out var rod) || rod == null)
            {
                sb.AppendLine($"[FishingPower] 钓竿: equippedRodId={rodId}（未找到定义）");
                return;
            }

            sb.AppendLine($"[FishingPower] 钓竿: {rod.displayName} ({rod.rodId})");
            AppendFishingModifiers(sb, rod.modifiers, ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);
        }

        static void AppendFishingModifiers(
            StringBuilder sb,
            StatModifiers m,
            ref float sumPowerAdd,
            ref float sumPowerMul,
            ref float sumProgressAdd,
            ref float sumCoinMul,
            ref float sumTrainMul,
            ref bool hasSpecialCatch)
        {
            sb.AppendLine(
                $"    fishingPowerAdd={m.fishingPowerAdd:F4}, fishingPowerMul={m.fishingPowerMul:F4}, " +
                $"fishingProgressAdd={m.fishingProgressAdd:F4}, fishingCoinMul={m.fishingCoinMul:F4}, " +
                $"fishingTrainSpeedMul={m.fishingTrainSpeedMul:F4}, fishingSpecialCatchBonus={m.fishingSpecialCatchBonus:F4}");
            FishingStatsAggregator.AccumulateFishingForDebug(
                m,
                ref sumPowerAdd,
                ref sumPowerMul,
                ref sumProgressAdd,
                ref sumCoinMul,
                ref sumTrainMul,
                ref hasSpecialCatch);
        }
    }
}

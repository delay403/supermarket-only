using UnityEngine;
using Game.Core.Config;
using Game.Save;
using Game.Collectibles.Pets;
using Game.Collectibles.Wings;
using Game.Collectibles.Trails;
using Game.Collectibles.Characters;
using Game.Collectibles.Rods;
using Game.UI.MainHome;

namespace Game.Collectibles.Stats
{
    /// <summary>
    /// 钓鱼玩法专用属性聚合：仅读取 StatModifiers 中的钓鱼字段，忽略爬塔字段。
    /// 仅在钓鱼场景中挂载/使用，不影响 <see cref="StatsAggregator"/>。
    /// </summary>
    public sealed class FishingStatsAggregator : MonoBehaviour
    {
        public struct FinalFishingStats
        {
            /// <summary>
            /// 最终钓鱼力量 = baseStrength + ΣfishingPowerAdd + baseStrength × ΣfishingPowerMul。
            /// </summary>
            public float totalPower;

            public float totalProgressAdd;
            public float totalCoinMul;
            public float totalTrainSpeedMul;
            public float totalSpecialCatchBonus;
        }

        public FinalFishingStats Current { get; private set; }

        void OnEnable() => Recompute();

        public static float ComputeBaseStrength(float? overrideBase = null)
        {
            if (overrideBase.HasValue)
                return Mathf.Max(0f, overrideBase.Value);

            var fishing = SaveService.Data?.fishing;
            if (fishing != null && fishing.baseStrength >= 0f)
                return Mathf.Max(0f, fishing.baseStrength);

            return ConfigRegistry.ResolveFishingDefaultBaseStrength();
        }

        public static float ComputeTotalPower(float sumPowerAdd, float sumPowerMul)
        {
            var baseStr = ComputeBaseStrength();
            var mulContribution = baseStr * sumPowerMul;
            return Mathf.Max(0f, baseStr + sumPowerAdd + mulContribution);
        }

        /// <summary>快速上鱼（ΣfishingPowerMul）对总力量的贡献：baseStrength × ΣfishingPowerMul。</summary>
        public static float ComputePowerMulContribution(float sumPowerMul, float? overrideBase = null)
        {
            return ComputeBaseStrength(overrideBase) * sumPowerMul;
        }

        public void Recompute()
        {
            var sumPowerAdd = 0f;
            var sumPowerMul = 0f;
            var sumProgressAdd = 0f;
            var sumCoinMul = 0f;
            var sumTrainMul = 0f;
            var hasSpecialCatch = false;

            var petDb = ConfigRegistry.Pets;
            if (GameModeRuntimeConfig.AppliesPetBonuses && petDb != null && SaveService.Data != null)
            {
                PetService.EnsureLoadout();
                for (var s = 0; s < PetService.EquipSlotCount; s++)
                {
                    var pid = PetService.GetEquippedPetId(s);
                    if (string.IsNullOrWhiteSpace(pid)) continue;
                    if (!petDb.TryGet(pid, out var pet) || pet == null) continue;
                    AccumulateFishing(GameModeStatUtil.Scale(pet.modifiers), ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);
                }
            }

            var wingDb = ConfigRegistry.Wings;
            if (GameModeRuntimeConfig.AppliesWingBonuses && wingDb != null && SaveService.Data != null &&
                !string.IsNullOrWhiteSpace(SaveService.Data.equippedWingId) &&
                wingDb.TryGet(SaveService.Data.equippedWingId, out var wing) && wing != null)
                AccumulateFishing(GameModeStatUtil.Scale(wing.modifiers), ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);

            var trailDb = ConfigRegistry.Trails;
            if (GameModeRuntimeConfig.AppliesTrailBonuses && trailDb != null && SaveService.Data != null &&
                !string.IsNullOrWhiteSpace(SaveService.Data.equippedTrailId) &&
                trailDb.TryGet(SaveService.Data.equippedTrailId, out var trail) && trail != null)
                AccumulateFishing(GameModeStatUtil.Scale(trail.modifiers), ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);

            var charDb = ConfigRegistry.Characters;
            if (GameModeRuntimeConfig.AppliesCharacterBonuses && charDb != null && SaveService.Data != null &&
                !string.IsNullOrWhiteSpace(SaveService.Data.equippedCharacterId) &&
                charDb.TryGet(SaveService.Data.equippedCharacterId, out var ch) && ch != null)
                AccumulateFishing(GameModeStatUtil.Scale(ch.modifiers), ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);

            var rodDb = ConfigRegistry.Rods;
            if (GameModeRuntimeConfig.AppliesRodBonuses && rodDb != null && SaveService.Data != null &&
                !string.IsNullOrWhiteSpace(SaveService.Data.equippedRodId) &&
                rodDb.TryGet(SaveService.Data.equippedRodId, out var rod) && rod != null)
                AccumulateFishing(GameModeStatUtil.Scale(rod.modifiers), ref sumPowerAdd, ref sumPowerMul, ref sumProgressAdd, ref sumCoinMul, ref sumTrainMul, ref hasSpecialCatch);

            Current = new FinalFishingStats
            {
                totalPower = ComputeTotalPower(sumPowerAdd, sumPowerMul),
                totalProgressAdd = sumProgressAdd,
                totalCoinMul = sumCoinMul,
                totalTrainSpeedMul = sumTrainMul,
                totalSpecialCatchBonus = hasSpecialCatch ? 1f : 0f,
            };
        }

        static void AccumulateFishing(
            StatModifiers m,
            ref float sumPowerAdd,
            ref float sumPowerMul,
            ref float sumProgressAdd,
            ref float sumCoinMul,
            ref float sumTrainMul,
            ref bool hasSpecialCatch)
        {
            AccumulateFishingForDebug(
                m,
                ref sumPowerAdd,
                ref sumPowerMul,
                ref sumProgressAdd,
                ref sumCoinMul,
                ref sumTrainMul,
                ref hasSpecialCatch);
        }

        internal static void AccumulateFishingForDebug(
            StatModifiers m,
            ref float sumPowerAdd,
            ref float sumPowerMul,
            ref float sumProgressAdd,
            ref float sumCoinMul,
            ref float sumTrainMul,
            ref bool hasSpecialCatch)
        {
            sumPowerAdd += m.fishingPowerAdd;
            sumPowerMul += m.fishingPowerMul;
            sumProgressAdd += m.fishingProgressAdd;
            sumCoinMul += m.fishingCoinMul;
            sumTrainMul += m.fishingTrainSpeedMul;
            if (StatModifiersUiFormat.HasSpecialCatchBonus(m.fishingSpecialCatchBonus))
                hasSpecialCatch = true;
        }
    }
}

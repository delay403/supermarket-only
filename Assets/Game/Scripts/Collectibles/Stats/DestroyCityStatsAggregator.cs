using UnityEngine;
using Game.Core.Config;
using Game.Save;
using Game.Collectibles.Pets;
using Game.Collectibles.Wings;
using Game.Collectibles.Trails;
using Game.Collectibles.Characters;
using Game.UI.MainHome;

namespace Game.Collectibles.Stats
{
    /// <summary>
    /// 摧毁城市玩法专用属性聚合：复用 <see cref="StatModifiers"/> 中的钓鱼字段语义，
    /// 映射为燃料经验产出加成（fishingPowerAdd/Mul → 燃料加成，fishingTrainSpeedMul → 生产速度，fishingCoinMul → 摧毁金币）。
    /// </summary>
    public sealed class DestroyCityStatsAggregator : MonoBehaviour
    {
        public struct FinalDestroyCityStats
        {
            /// <summary>玩家基础燃料产出率（养成属性，与装备无关）。</summary>
            public float baseFuelRate;

            /// <summary>装备 flat 加成（Σ fishingPowerAdd）。</summary>
            public float equipmentFuelAdd;

            /// <summary>装备比例加成（Σ fishingPowerMul，作用于单次基础经验）。</summary>
            public float equipmentFuelMul;

            public float totalProductionSpeedMul;
            public float totalDestroyCoinMul;

            public float ComputeEquipmentFuelBonus(float producerBaseXp) =>
                Mathf.Max(0f, equipmentFuelAdd + producerBaseXp * equipmentFuelMul);
        }

        public FinalDestroyCityStats Current { get; private set; }

        void OnEnable() => Recompute();

        public static float ComputeBaseFuelRate(float? overrideBase = null)
        {
            if (overrideBase.HasValue)
                return Mathf.Max(0f, overrideBase.Value);

            var dc = SaveService.Data?.destroyCity;
            if (dc != null && dc.baseFuelRate >= 0f)
                return Mathf.Max(0f, dc.baseFuelRate);

            return ConfigRegistry.ResolveDestroyCityDefaultBaseFuelRate();
        }

        public static float ComputeEquipmentFuelBonus(float producerBaseXp, float sumFuelAdd, float sumFuelMul)
        {
            return Mathf.Max(0f, sumFuelAdd + producerBaseXp * sumFuelMul);
        }

        public void Recompute()
        {
            var sumFuelAdd = 0f;
            var sumFuelMul = 0f;
            var sumProdMul = 0f;
            var sumCoinMul = 0f;

            var petDb = ConfigRegistry.Pets;
            if (GameModeRuntimeConfig.AppliesPetBonuses && petDb != null && SaveService.Data != null)
            {
                PetService.EnsureLoadout();
                for (var s = 0; s < PetService.EquipSlotCount; s++)
                {
                    var pid = PetService.GetEquippedPetId(s);
                    if (string.IsNullOrWhiteSpace(pid)) continue;
                    if (!petDb.TryGet(pid, out var pet) || pet == null) continue;
                    AccumulateDestroyCity(GameModeStatUtil.Scale(pet.modifiers), ref sumFuelAdd, ref sumFuelMul, ref sumProdMul, ref sumCoinMul);
                }
            }

            var wingDb = ConfigRegistry.Wings;
            if (GameModeRuntimeConfig.AppliesWingBonuses && wingDb != null && SaveService.Data != null &&
                !string.IsNullOrWhiteSpace(SaveService.Data.equippedWingId) &&
                wingDb.TryGet(SaveService.Data.equippedWingId, out var wing) && wing != null)
                AccumulateDestroyCity(GameModeStatUtil.Scale(wing.modifiers), ref sumFuelAdd, ref sumFuelMul, ref sumProdMul, ref sumCoinMul);

            var trailDb = ConfigRegistry.Trails;
            if (GameModeRuntimeConfig.AppliesTrailBonuses && trailDb != null && SaveService.Data != null &&
                !string.IsNullOrWhiteSpace(SaveService.Data.equippedTrailId) &&
                trailDb.TryGet(SaveService.Data.equippedTrailId, out var trail) && trail != null)
                AccumulateDestroyCity(GameModeStatUtil.Scale(trail.modifiers), ref sumFuelAdd, ref sumFuelMul, ref sumProdMul, ref sumCoinMul);

            var charDb = ConfigRegistry.Characters;
            if (GameModeRuntimeConfig.AppliesCharacterBonuses && charDb != null && SaveService.Data != null &&
                !string.IsNullOrWhiteSpace(SaveService.Data.equippedCharacterId) &&
                charDb.TryGet(SaveService.Data.equippedCharacterId, out var ch) && ch != null)
                AccumulateDestroyCity(GameModeStatUtil.Scale(ch.modifiers), ref sumFuelAdd, ref sumFuelMul, ref sumProdMul, ref sumCoinMul);

            Current = new FinalDestroyCityStats
            {
                baseFuelRate = ComputeBaseFuelRate(),
                equipmentFuelAdd = sumFuelAdd,
                equipmentFuelMul = sumFuelMul,
                totalProductionSpeedMul = sumProdMul,
                totalDestroyCoinMul = sumCoinMul,
            };
        }

        internal static void AccumulateDestroyCity(
            StatModifiers m,
            ref float sumFuelAdd,
            ref float sumFuelMul,
            ref float sumProdMul,
            ref float sumCoinMul)
        {
            sumFuelAdd += m.fishingPowerAdd;
            sumFuelMul += m.fishingPowerMul;
            sumProdMul += m.fishingTrainSpeedMul;
            sumCoinMul += m.fishingCoinMul;
        }
    }
}

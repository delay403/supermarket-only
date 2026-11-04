using Game.Collectibles.Characters;
using Game.Collectibles.Pets;
using Game.Collectibles.Trails;
using Game.Collectibles.Wings;
using Game.Core.Config;
using Game.Save;
using Game.UI.MainHome;
using UnityEngine;

namespace Game.Collectibles.Stats
{
    /// <summary>
    /// 躲避虫子玩法专用属性聚合：读取装备 <see cref="StatModifiers"/> 中的虫子字段。
    /// </summary>
    public sealed class WormDodgeStatsAggregator : MonoBehaviour
    {
        public struct FinalWormDodgeStats
        {
            public float totalMoveSpeedAdd;
            public float totalMoveSpeedMul;
            public float totalCoinMul;

            public float ApplyToMoveSpeed(float baseSpeed) =>
                Mathf.Max(0f, (baseSpeed + totalMoveSpeedAdd) * (1f + totalMoveSpeedMul));

            public long ApplyCoinBonus(long baseCoins)
            {
                if (baseCoins <= 0L)
                    return baseCoins;

                return (long)Mathf.Max(0L, Mathf.RoundToInt(baseCoins * (1f + totalCoinMul)));
            }
        }

        public static event System.Action Changed;

        public FinalWormDodgeStats Current { get; private set; }

        void OnEnable() => Recompute();

        public void Recompute()
        {
            Current = Compute();
            Changed?.Invoke();
        }

        public static FinalWormDodgeStats Compute()
        {
            var sumMoveAdd = 0f;
            var sumMoveMul = 0f;
            var sumCoinMul = 0f;

            var petDb = ConfigRegistry.Pets;
            if (GameModeRuntimeConfig.AppliesPetBonuses && petDb != null && SaveService.Data != null)
            {
                PetService.EnsureLoadout();
                for (var s = 0; s < PetService.EquipSlotCount; s++)
                {
                    var pid = PetService.GetEquippedPetId(s);
                    if (string.IsNullOrWhiteSpace(pid))
                        continue;

                    if (!petDb.TryGet(pid, out var pet) || pet == null)
                        continue;

                    var mods = StatModifiersModeResolver.ResolveForActiveModule(pet.modifiers);
                    Accumulate(GameModeStatUtil.Scale(mods), ref sumMoveAdd, ref sumMoveMul, ref sumCoinMul);
                }
            }

            var wingDb = ConfigRegistry.Wings;
            if (GameModeRuntimeConfig.AppliesWingBonuses && wingDb != null && SaveService.Data != null &&
                !string.IsNullOrWhiteSpace(SaveService.Data.equippedWingId) &&
                wingDb.TryGet(SaveService.Data.equippedWingId, out var wing) && wing != null)
            {
                var mods = StatModifiersModeResolver.ResolveForActiveModule(wing.modifiers);
                Accumulate(GameModeStatUtil.Scale(mods), ref sumMoveAdd, ref sumMoveMul, ref sumCoinMul);
            }

            var trailDb = ConfigRegistry.Trails;
            if (GameModeRuntimeConfig.AppliesTrailBonuses && trailDb != null && SaveService.Data != null &&
                !string.IsNullOrWhiteSpace(SaveService.Data.equippedTrailId) &&
                trailDb.TryGet(SaveService.Data.equippedTrailId, out var trail) && trail != null)
            {
                var mods = StatModifiersModeResolver.ResolveForActiveModule(trail.modifiers);
                Accumulate(GameModeStatUtil.Scale(mods), ref sumMoveAdd, ref sumMoveMul, ref sumCoinMul);
            }

            var charDb = ConfigRegistry.Characters;
            if (GameModeRuntimeConfig.AppliesCharacterBonuses && charDb != null && SaveService.Data != null &&
                !string.IsNullOrWhiteSpace(SaveService.Data.equippedCharacterId) &&
                charDb.TryGet(SaveService.Data.equippedCharacterId, out var ch) && ch != null)
            {
                var mods = StatModifiersModeResolver.ResolveForActiveModule(ch.modifiers);
                Accumulate(GameModeStatUtil.Scale(mods), ref sumMoveAdd, ref sumMoveMul, ref sumCoinMul);
            }

            return new FinalWormDodgeStats
            {
                totalMoveSpeedAdd = sumMoveAdd,
                totalMoveSpeedMul = sumMoveMul,
                totalCoinMul = sumCoinMul,
            };
        }

        static void Accumulate(
            StatModifiers m,
            ref float sumMoveAdd,
            ref float sumMoveMul,
            ref float sumCoinMul)
        {
            sumMoveAdd += m.wormDodgeMoveSpeedAdd;
            sumMoveMul += m.wormDodgeMoveSpeedMul;
            sumCoinMul += m.wormDodgeCoinMul;
        }
    }
}

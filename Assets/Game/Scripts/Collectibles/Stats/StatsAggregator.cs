using UnityEngine;
using Game.Core.Config;
using Game.Debugging;
using Game.Save;
using Game.Collectibles.Pets;
using Game.Collectibles.Wings;
using Game.Collectibles.Trails;
using Game.Features.Treadmill;
using Game.UI.MainHome;

namespace Game.Collectibles.Stats
{
    public sealed class StatsAggregator : MonoBehaviour
    {
        public struct FinalStats
        {
            /// <summary>
            /// 本地图爬塔速度（不含奖励倍率；不含体力耗尽倍率）=
            /// max(climbSpeedBase, climbSpeedBase + ΣclimbSpeedAdd - 地图修正) × max(0, 1 + ΣclimbSpeedMul)。
            /// 运行时再 × 体力系数、× 双倍奖励(2)。
            /// </summary>
            public float climbSpeed;

            /// <summary>已装备养成：coinsAdd 之和（用于滑翔结算）。</summary>
            public float totalCoinsAdd;
            /// <summary>已装备养成：coinsMul 之和（用于滑翔结算）。</summary>
            public float totalCoinsMul;
        }

        public FinalStats Current { get; private set; }

        private void Update()
        {
            Recompute();
        }

        /// <summary>
        /// 配置基础爬塔速度 + 跑步机永久加成（线性相加；不含地图修正、装备与 ΣclimbSpeedMul）。
        /// </summary>
        public static float ComputeBaseAndTreadmillClimbSpeed(float? treadmillPermanentOverride = null)
        {
            var bal = ConfigRegistry.Balance;
            var baseClimb = bal != null ? bal.climbSpeedBase : 2f;
            var treadmill = treadmillPermanentOverride ?? TreadmillService.GetPermanentClimbSpeedBonus();
            return Mathf.Max(0f, baseClimb + treadmill);
        }

        public void Recompute()
        {
            var bal = ConfigRegistry.Balance;
            var baseClimb = bal != null ? bal.climbSpeedBase : 2f;

            var sumClimbAdd = 0f;
            var sumClimbMul = 0f;
            var sumCoinsAdd = 0f;
            var sumCoinsMul = 0f;

            var dbg = EquipmentStatDebugState.ShouldApplyOverrides;

            var petDb = ConfigRegistry.Pets;
            if (GameModeRuntimeConfig.AppliesPetBonuses && petDb != null && SaveService.Data != null)
            {
                PetService.EnsureLoadout();
                for (var s = 0; s < PetService.EquipSlotCount; s++)
                {
                    var pid = PetService.GetEquippedPetId(s);
                    if (string.IsNullOrWhiteSpace(pid)) continue;
                    if (!petDb.TryGet(pid, out var pet) || pet == null) continue;
                    if (dbg)
                    {
                        var m = pet.modifiers;
                        Accumulate(new StatModifiers
                            {
                                climbSpeedAdd = m.climbSpeedAdd,
                                climbSpeedMul = m.climbSpeedMul,
                                coinsAdd = EquipmentStatDebugState.GetPetCoinsAdd(s),
                                coinsMul = m.coinsMul,
                            },
                            ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
                    }
                    else
                        Accumulate(GameModeStatUtil.Scale(pet.modifiers), ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
                }
            }

            var wingDb = ConfigRegistry.Wings;
            if (GameModeRuntimeConfig.AppliesWingBonuses && wingDb != null && SaveService.Data != null && !string.IsNullOrWhiteSpace(SaveService.Data.equippedWingId) &&
                wingDb.TryGet(SaveService.Data.equippedWingId, out var wing) && wing != null)
            {
                if (dbg)
                {
                    var m = wing.modifiers;
                    Accumulate(new StatModifiers
                        {
                            climbSpeedAdd = m.climbSpeedAdd,
                            climbSpeedMul = EquipmentStatDebugState.WingClimbSpeedMul,
                            coinsAdd = m.coinsAdd,
                            coinsMul = m.coinsMul,
                        },
                        ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
                }
                else
                    Accumulate(GameModeStatUtil.Scale(wing.modifiers), ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
            }

            var trailDb = ConfigRegistry.Trails;
            if (GameModeRuntimeConfig.AppliesTrailBonuses && trailDb != null && SaveService.Data != null && !string.IsNullOrWhiteSpace(SaveService.Data.equippedTrailId) &&
                trailDb.TryGet(SaveService.Data.equippedTrailId, out var trail) && trail != null)
            {
                if (dbg)
                {
                    var m = trail.modifiers;
                    Accumulate(new StatModifiers
                        {
                            climbSpeedAdd = m.climbSpeedAdd,
                            climbSpeedMul = m.climbSpeedMul,
                            coinsAdd = m.coinsAdd,
                            coinsMul = EquipmentStatDebugState.TrailCoinsMul,
                        },
                        ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
                }
                else
                    Accumulate(GameModeStatUtil.Scale(trail.modifiers), ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
            }

            var charDb = ConfigRegistry.Characters;
            if (GameModeRuntimeConfig.AppliesCharacterBonuses && charDb != null && SaveService.Data != null && !string.IsNullOrWhiteSpace(SaveService.Data.equippedCharacterId) &&
                charDb.TryGet(SaveService.Data.equippedCharacterId, out var ch) && ch != null)
            {
                if (dbg)
                {
                    var m = ch.modifiers;
                    Accumulate(new StatModifiers
                        {
                            climbSpeedAdd = m.climbSpeedAdd,
                            climbSpeedMul = EquipmentStatDebugState.CharacterClimbSpeedMul,
                            coinsAdd = m.coinsAdd,
                            coinsMul = EquipmentStatDebugState.CharacterCoinsMul,
                        },
                        ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
                }
                else
                    Accumulate(GameModeStatUtil.Scale(ch.modifiers), ref sumClimbAdd, ref sumClimbMul, ref sumCoinsAdd, ref sumCoinsMul);
            }

            sumClimbAdd += TreadmillService.GetPermanentClimbSpeedBonus();

            var mapCorrection = 0f;
            var mapDb = ConfigRegistry.Maps;
            if (mapDb != null && SaveService.Data != null && mapDb.TryGet(SaveService.Data.currentMapId, out var map) && map != null)
                mapCorrection = map.climbSpeedMapCorrection;

            var innerLinear = baseClimb + sumClimbAdd - mapCorrection;
            var coreLinear = Mathf.Max(baseClimb, innerLinear);
            var mulBracket = Mathf.Max(0f, 1f + sumClimbMul);
            var climbSpeed = coreLinear * mulBracket;

            Current = new FinalStats
            {
                climbSpeed = Mathf.Max(0f, climbSpeed),
                totalCoinsAdd = sumCoinsAdd,
                totalCoinsMul = sumCoinsMul,
            };
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

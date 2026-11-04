using Game.Core;
using UnityEngine;

namespace Game.Collectibles.Stats
{
    /// <summary>将 SO 上的 <see cref="StatModifiers"/> 解析为当前玩法实际生效/展示的字段。</summary>
    public static class StatModifiersModeResolver
    {
        const float Eps = 1e-5f;

        public static StatModifiers ResolveForActiveModule(StatModifiers raw)
        {
            return ResolveForModule(raw, ResolveTargetModule());
        }

        public static StatModifiers ResolveForModule(StatModifiers raw, GameplayModule module) => module switch
        {
            GameplayModule.WormDodge => ResolveWormDodge(raw),
            GameplayModule.DestroyCity => ResolveDestroyCity(raw),
            GameplayModule.Fishing => raw,
            GameplayModule.Tower => raw,
            _ => raw,
        };

        static GameplayModule ResolveTargetModule()
        {
            if (GameplayModuleContext.IsFusionProduct())
                return GameplayModuleContext.ActiveModule;

            if (GameProductSettings.IsDestroyCityContentEnabled)
                return GameplayModule.DestroyCity;
            if (GameProductSettings.IsFishingContentEnabled)
                return GameplayModule.Fishing;
            if (GameProductSettings.IsWormDodgeContentEnabled)
                return GameplayModule.WormDodge;
            return GameplayModule.Tower;
        }

        static StatModifiers ResolveWormDodge(StatModifiers m) => new StatModifiers
        {
            wormDodgeMoveSpeedAdd = Pick(m.wormDodgeMoveSpeedAdd, m.climbSpeedAdd),
            wormDodgeMoveSpeedMul = Pick(m.wormDodgeMoveSpeedMul, m.climbSpeedMul),
            wormDodgeCoinMul = Pick(m.wormDodgeCoinMul, m.coinsMul),
        };

        static StatModifiers ResolveDestroyCity(StatModifiers m) => new StatModifiers
        {
            fishingPowerAdd = Pick(m.fishingPowerAdd, m.climbSpeedAdd),
            fishingPowerMul = Pick(m.fishingPowerMul, m.climbSpeedMul),
            fishingTrainSpeedMul = m.fishingTrainSpeedMul,
            fishingCoinMul = Pick(m.fishingCoinMul, m.coinsMul),
            fishingSpecialCatchBonus = m.fishingSpecialCatchBonus,
        };

        static float Pick(float primary, float fallback) =>
            Mathf.Abs(primary) > Eps ? primary : fallback;
    }
}

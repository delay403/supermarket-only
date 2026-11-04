using Game.UI.MainHome;
using UnityEngine;

namespace Game.Collectibles.Stats
{
    static class GameModeStatUtil
    {
        public static StatModifiers Scale(StatModifiers m)
        {
            var mul = GameModeRuntimeConfig.StatMultiplier;
            if (Mathf.Approximately(mul, 1f))
                return m;

            return new StatModifiers
            {
                climbSpeedAdd = m.climbSpeedAdd * mul,
                climbSpeedMul = m.climbSpeedMul * mul,
                coinsMul = m.coinsMul * mul,
                coinsAdd = m.coinsAdd * mul,
                fishingPowerAdd = m.fishingPowerAdd * mul,
                fishingPowerMul = m.fishingPowerMul * mul,
                fishingProgressAdd = m.fishingProgressAdd * mul,
                fishingCoinMul = m.fishingCoinMul * mul,
                fishingTrainSpeedMul = m.fishingTrainSpeedMul * mul,
                fishingSpecialCatchBonus = m.fishingSpecialCatchBonus * mul,
                wormDodgeMoveSpeedAdd = m.wormDodgeMoveSpeedAdd * mul,
                wormDodgeMoveSpeedMul = m.wormDodgeMoveSpeedMul * mul,
                wormDodgeCoinMul = m.wormDodgeCoinMul * mul,
            };
        }
    }
}

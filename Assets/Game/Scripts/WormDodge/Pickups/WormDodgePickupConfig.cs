using UnityEngine;

namespace Game.WormDodge.Pickups
{
    [CreateAssetMenu(menuName = "Game/Worm Dodge/Pickup Config", fileName = "WormDodgePickupConfig")]
    public sealed class WormDodgePickupConfig : ScriptableObject
    {
        [Header("Coin Pickup")]
        [Tooltip("金币道具随机数量下限（含）。")]
        public long coinAmountMin = 25L;

        [Tooltip("金币道具随机数量上限（含）。")]
        public long coinAmountMax = 75L;

        public long RollCoinAmount()
        {
            var min = coinAmountMin < 0L ? 0L : coinAmountMin;
            var max = coinAmountMax < min ? min : coinAmountMax;
            if (max <= min)
                return min;

            var range = max - min + 1L;
            return min + (long)(Random.value * range);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            coinAmountMin = System.Math.Max(0L, coinAmountMin);
            coinAmountMax = System.Math.Max(coinAmountMin, coinAmountMax);
        }
#endif
    }
}

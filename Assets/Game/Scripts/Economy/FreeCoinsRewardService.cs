using Game.Core.Config;
using Game.Economy.Config;
using UnityEngine;

namespace Game.Economy
{
    public static class FreeCoinsRewardService
    {
        public static long ResolveFreeCoins() =>
            ModeRewardCoinResolver.ResolveForActiveModule();
    }
}

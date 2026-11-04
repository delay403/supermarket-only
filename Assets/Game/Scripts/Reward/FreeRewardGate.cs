using System;

namespace Game.Reward
{
    /// <summary>
    /// 游戏内奖励入口：直接发放奖励。
    /// </summary>
    public static class FreeRewardGate
    {
        public static void Grant(Action onRewardGranted)
        {
            onRewardGranted?.Invoke();
        }
    }
}

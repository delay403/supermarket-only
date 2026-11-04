namespace Game.DestroyCity.Config
{
    public enum FuelProducerUnlockPolicy
    {
        /// <summary>初始可用（手动生产器）。</summary>
        AlwaysOpen = 0,

        /// <summary>消耗金币一次性解锁（见 unlockCoinCost）。</summary>
        CoinOnce = 1,

        /// <summary>消耗钻石一次性解锁。</summary>
        GemOnce = 2,

        /// <summary>免费解锁。</summary>
        FreeUnlockOnce = 3,

        /// <summary>与 <see cref="FuelProducerDef.unlockSaveKey"/> 关联的世界区域解锁。</summary>
        WorldAreaSaveKey = 4,

        /// <summary>游戏内某时机自动解锁（由脚本调用 FuelProducerService.TryAutoUnlock）。</summary>
        AutoUnlockOnEvent = 5,
    }
}

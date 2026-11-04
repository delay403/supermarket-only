namespace Game.Economy.Config
{
    /// <summary>每个优惠商品在 <see cref="DiscountOfferEventManager"/> 中按类型分发到独立触发处理器。</summary>
    public enum DiscountOfferTriggerKind
    {
        /// <summary>首次成功钓获后，再等待 <see cref="DiscountOfferDef.popupDelayAfterFirstCatchSeconds"/> 秒弹出。</summary>
        DelayAfterFirstSuccessfulCatch = 0,

        /// <summary>首次解锁指定世界区域（见 <see cref="DiscountOfferDef.triggerWorldAreaSaveKey"/>）。</summary>
        FirstWorldAreaUnlock = 1,

        /// <summary>累计在线满 10 分钟（见 <see cref="DiscountOfferDef.triggerOnlineSeconds"/>）。</summary>
        FirstOnlineDuration = 2,

        /// <summary>首次购买翅膀。</summary>
        FirstWingPurchase = 3,

        /// <summary>首次购买拖尾。</summary>
        FirstTrailPurchase = 4,

        /// <summary>首次孵蛋成功。</summary>
        FirstPetHatch = 5,

        /// <summary>首次购买钓竿。</summary>
        FirstRodPurchase = 6,

        /// <summary>首次发射火箭（摧毁城市）。</summary>
        FirstNukeLaunch = 7,

        /// <summary>首次升级燃料生产器（摧毁城市）。</summary>
        FirstFuelProducerUpgrade = 8,
    }
}

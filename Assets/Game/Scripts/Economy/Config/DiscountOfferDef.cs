using System;
using UnityEngine;

namespace Game.Economy.Config
{
    [Serializable]
    public struct DiscountOfferDef
    {
        [Tooltip("优惠商品唯一编号。")]
        public int offerId;

        public string title;

        [TextArea(2, 4)]
        public string description;

        public DiscountOfferPurchaseKind purchaseKind;

        [Tooltip("钻石购买时的价格。")]
        public long gemPrice;

        public DiscountOfferTriggerKind triggerKind;

        [Tooltip("所属玩法模块；仅在该模块激活时才会弹出与显示 HUD 入口。")]
        public DiscountOfferProductScope productScope;

        [Tooltip("购买后获得的奖励列表（金币、钻石、翅膀、钓竿、宠物、拖尾等）。")]
        public DiscountOfferRewardEntry[] rewards;

        [Tooltip("弹窗关闭后 HUD 入口按钮剩余秒数；到期后按钮消失且该商品不再出现。")]
        public float hudEntryDurationSeconds;

        [Tooltip("首次钓获成功后多少秒自动弹出（仅 DelayAfterFirstSuccessfulCatch）。")]
        public float popupDelayAfterFirstCatchSeconds;

        [Tooltip("里程碑达成后再延迟多少秒弹出（其它触发类型）。")]
        public float triggerDelaySeconds;

        [Tooltip("FirstWorldAreaUnlock：须匹配的 UnlockWorldArea saveKey；留空则任意首次区域解锁。")]
        public string triggerWorldAreaSaveKey;

        [Tooltip("FirstOnlineDuration：累计在线秒数阈值，默认 600（10 分钟）。")]
        public float triggerOnlineSeconds;
    }
}

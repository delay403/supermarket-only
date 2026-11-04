using System;

namespace Game.UI.Framework
{
    /// <summary>通用激励视频弹窗参数（由 <see cref="FreeCurrencyOfferPanel"/> / Free 预制体处理）。</summary>
    public sealed class RewardDialogPayload
    {
        public string Title = "立即领取";
        public string Description = "";
        public Action<bool> Completion;
    }
}

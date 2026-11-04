using System;

namespace Game.Economy
{
    /// <summary>
    /// 当 <see cref="Game.Save.SaveService.Data"/> 的金币/钻石/奖牌数值变化且 HUD 已同步时触发，
    /// 供仍打开的商店、孵化等界面刷新价格文案与按钮，不必各处手动调用刷新。
    /// </summary>
    public static class EconomyDisplayEvents
    {
        public static event Action Changed;

        public static void Raise()
        {
            Changed?.Invoke();
        }
    }
}

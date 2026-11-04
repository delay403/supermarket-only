using Game.Save;
using Game.UI.Framework;

namespace Game.SupermarketOrganize
{
    /// <summary>超市收纳局内金币奖励发放。</summary>
    public static class SupermarketOrganizeEconomyRewards
    {
        public static void GrantCoins(long amount)
        {
            if (amount <= 0 || SaveService.Data == null)
                return;

            var hud = UiCoordinator.Hud;
            if (hud != null)
                hud.AddCoinsDisplay(amount);
            else
            {
                ModeSaveAccess.AddCoins(amount);
                SaveService.Save();
            }
        }
    }
}

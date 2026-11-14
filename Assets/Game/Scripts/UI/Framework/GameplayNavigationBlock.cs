namespace Game.UI.Framework
{
    public static class GameplayNavigationBlock
    {
        public static bool ShouldDenyOpeningPanel(UiPanelId id) => false;
        public static bool ShouldBlockOpeningFreeCurrencyFromHud() => false;
        public static void ShowShopNavigationBlockedHint() { }
    }
}

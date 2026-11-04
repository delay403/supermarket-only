namespace Game.UI.Framework
{
    public enum CoinsHubTab
    {
        FreeCoins = 0,
        DiamondExchange = 1,
    }

    public sealed class CoinsHubOpenPayload
    {
        public CoinsHubTab InitialTab = CoinsHubTab.FreeCoins;
    }
}

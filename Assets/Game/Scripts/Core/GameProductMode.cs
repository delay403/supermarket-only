namespace Game.Core
{
    public enum GameProductMode
    {
        SupermarketOrganizeOnly = 5,
    }

    [System.Flags]
    public enum GameplayModule
    {
        None = 0,
        SupermarketOrganize = 1 << 4,
    }
}

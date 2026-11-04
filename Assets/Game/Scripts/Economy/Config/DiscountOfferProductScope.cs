namespace Game.Economy.Config
{
    /// <summary>限时优惠商品所属玩法；与 <see cref="Game.Core.GameplayModule"/> 一一对应。</summary>
    public enum DiscountOfferProductScope
    {
        Fishing = 0,
        DestroyCity = 1,
        WormDodge = 2,
        SupermarketOrganize = 3,
        Tower = 4,
    }
}

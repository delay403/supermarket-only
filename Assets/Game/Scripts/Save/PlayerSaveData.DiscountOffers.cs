namespace Game.Save
{
    public sealed partial class PlayerSaveData
    {
        public DiscountOfferMilestoneSaveData towerDiscountOffers = new DiscountOfferMilestoneSaveData();

        public void EnsureTowerDiscountOfferSaveInitialized()
        {
            towerDiscountOffers ??= new DiscountOfferMilestoneSaveData();
            towerDiscountOffers.EnsureInitialized();
        }
    }
}

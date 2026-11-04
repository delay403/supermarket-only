namespace Game.Save
{
    public sealed partial class WormDodgeModeSaveData
    {
        public DiscountOfferMilestoneSaveData discountOffers = new DiscountOfferMilestoneSaveData();

        public void EnsureDiscountOfferSaveInitialized()
        {
            discountOffers ??= new DiscountOfferMilestoneSaveData();
            discountOffers.EnsureInitialized();
        }
    }
}

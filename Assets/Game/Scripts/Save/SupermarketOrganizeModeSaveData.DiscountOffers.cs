namespace Game.Save
{
    public sealed partial class SupermarketOrganizeModeSaveData
    {
        public DiscountOfferMilestoneSaveData discountOffers = new DiscountOfferMilestoneSaveData();

        public void EnsureDiscountOfferSaveInitialized()
        {
            discountOffers ??= new DiscountOfferMilestoneSaveData();
            discountOffers.EnsureInitialized();
        }
    }
}

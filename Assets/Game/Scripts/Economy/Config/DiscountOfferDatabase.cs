using UnityEngine;

namespace Game.Economy.Config
{
    [CreateAssetMenu(fileName = "DiscountOfferDatabase", menuName = "Game/Economy/Discount Offer Database")]
    public sealed class DiscountOfferDatabase : ScriptableObject
    {
        public DiscountOfferDef[] offers;

        public bool TryGet(int offerId, out DiscountOfferDef def)
        {
            def = default;
            if (offers == null || offerId <= 0)
                return false;

            for (var i = 0; i < offers.Length; i++)
            {
                if (offers[i].offerId == offerId)
                {
                    def = offers[i];
                    return true;
                }
            }

            return false;
        }
    }
}

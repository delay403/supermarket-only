using UnityEngine;

namespace Game.Economy.Config
{
    [CreateAssetMenu(fileName = "DiamondCoinsExchangeDatabase", menuName = "Game/Economy/Diamond Coins Exchange Database")]
    public sealed class DiamondCoinsExchangeDatabase : ScriptableObject
    {
        public DiamondCoinsExchangeTierDef[] tiers;

        public bool TryGet(int tierId, out DiamondCoinsExchangeTierDef def)
        {
            def = default;
            if (tiers == null || tierId <= 0)
                return false;

            for (var i = 0; i < tiers.Length; i++)
            {
                if (tiers[i].tierId == tierId)
                {
                    def = tiers[i];
                    return true;
                }
            }

            return false;
        }
    }
}

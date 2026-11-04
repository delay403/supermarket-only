using System.Collections.Generic;
using Game.Collectibles.Rods;
using Game.Save;

namespace Game.UI.ViewModels
{
    public sealed class RodShopRowViewModel
    {
        public string Id;
        public string DisplayName;
        public int Level;
        public int PriceCoins;
        public bool Owned;
        public bool UnlockedForPurchase;
    }

    public sealed class RodShopScreenViewModel
    {
        public List<RodShopRowViewModel> Rows = new List<RodShopRowViewModel>();
        public long Coins;
        public string EquippedId;

        public static RodShopScreenViewModel Build()
        {
            var vm = new RodShopScreenViewModel
            {
                Coins = SaveService.Data?.coins ?? 0,
                EquippedId = SaveService.Data?.equippedRodId,
            };

            foreach (var rod in RodService.GetAllSorted())
            {
                if (rod == null) continue;
                vm.Rows.Add(new RodShopRowViewModel
                {
                    Id = rod.rodId,
                    DisplayName = rod.displayName,
                    Level = rod.level,
                    PriceCoins = rod.priceCoins,
                    Owned = RodService.IsOwned(rod.rodId),
                    UnlockedForPurchase = RodService.IsUnlockedForPurchase(rod),
                });
            }

            return vm;
        }
    }
}

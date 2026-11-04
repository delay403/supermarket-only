using System.Collections.Generic;
using System.Text;
using Game.Collectibles.Trails;
using Game.Collectibles.Wings;
using Game.Save;

namespace Game.UI.ViewModels
{
    public enum CosmeticKind
    {
        Wing = 0,
        Trail = 1,
    }

    public sealed class CosmeticShopRowViewModel
    {
        public CosmeticKind Kind;
        public string Id;
        public string DisplayName;
        public int Level;
        public int PriceCoins;
        public bool Owned;
        public bool UnlockedForPurchase;
    }

    public sealed class CosmeticShopScreenViewModel
    {
        public CosmeticKind Kind;
        public string Header;
        public List<CosmeticShopRowViewModel> Rows = new List<CosmeticShopRowViewModel>();
        public long Coins;
        public string EquippedId;

        public static CosmeticShopScreenViewModel BuildWings()
        {
            var vm = new CosmeticShopScreenViewModel
            {
                Kind = CosmeticKind.Wing,
                Header = "Wing Shop:",
                Coins = SaveService.Data?.coins ?? 0,
                EquippedId = SaveService.Data?.equippedWingId,
            };
            foreach (var w in WingService.GetAllSorted())
            {
                if (w == null) continue;
                vm.Rows.Add(new CosmeticShopRowViewModel
                {
                    Kind = CosmeticKind.Wing,
                    Id = w.wingId,
                    DisplayName = w.displayName,
                    Level = w.level,
                    PriceCoins = w.priceCoins,
                    Owned = WingService.IsOwned(w.wingId),
                    UnlockedForPurchase = WingService.IsUnlockedForPurchase(w),
                });
            }
            return vm;
        }

        public static CosmeticShopScreenViewModel BuildTrails()
        {
            var vm = new CosmeticShopScreenViewModel
            {
                Kind = CosmeticKind.Trail,
                Header = "Trail Shop:",
                Coins = SaveService.Data?.coins ?? 0,
                EquippedId = SaveService.Data?.equippedTrailId,
            };
            foreach (var t in TrailService.GetAllSorted())
            {
                if (t == null) continue;
                vm.Rows.Add(new CosmeticShopRowViewModel
                {
                    Kind = CosmeticKind.Trail,
                    Id = t.trailId,
                    DisplayName = t.displayName,
                    Level = t.level,
                    PriceCoins = t.priceCoins,
                    Owned = TrailService.IsOwned(t.trailId),
                    UnlockedForPurchase = TrailService.IsUnlockedForPurchase(t),
                });
            }
            return vm;
        }

        public string ToListText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Header);
            foreach (var r in Rows)
            {
                sb.AppendLine($"- Lv{r.Level} {r.DisplayName} ({r.Id}) price:{r.PriceCoins} owned:{r.Owned} unlocked:{r.UnlockedForPurchase}");
            }
            sb.AppendLine();
            sb.AppendLine($"Coins: {Coins}");
            sb.AppendLine($"Equipped: {EquippedId ?? "(none)"}");
            return sb.ToString();
        }
    }
}

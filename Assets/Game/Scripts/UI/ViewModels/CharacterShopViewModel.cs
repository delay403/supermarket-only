using System.Collections.Generic;
using System.Text;
using Game.Collectibles.Characters;
using Game.Save;

namespace Game.UI.ViewModels
{
    public sealed class CharacterShopRowViewModel
    {
        public string Id;
        public string DisplayName;
        public int Level;
        public int PriceAmount;
        public CharacterCurrencyType PriceCurrency;
        public bool Owned;
        public bool UnlockedForPurchase;
        public int RequiredUnlockedLevel;
    }

    public sealed class CharacterShopScreenViewModel
    {
        public List<CharacterShopRowViewModel> Rows = new List<CharacterShopRowViewModel>();
        public long Coins;
        public long Gems;
        public string EquippedId;

        public static CharacterShopScreenViewModel Build()
        {
            var vm = new CharacterShopScreenViewModel
            {
                Coins = SaveService.Data?.coins ?? 0,
                Gems = SaveService.Data?.gems ?? 0,
                EquippedId = SaveService.Data?.equippedCharacterId,
            };

            foreach (var c in CharacterService.GetAllSorted())
            {
                if (c == null) continue;
                vm.Rows.Add(new CharacterShopRowViewModel
                {
                    Id = c.characterId,
                    DisplayName = c.displayName,
                    Level = c.level,
                    PriceAmount = c.priceAmount,
                    PriceCurrency = c.priceCurrency,
                    Owned = CharacterService.IsOwned(c.characterId),
                    UnlockedForPurchase = CharacterService.IsUnlockedForPurchase(c),
                    RequiredUnlockedLevel = c.requiredUnlockedLevel,
                });
            }

            return vm;
        }

        public string ToDebugText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Character Shop");
            foreach (var r in Rows)
            {
                sb.AppendLine($"- Lv{r.Level} {r.DisplayName} ({r.Id}) {r.PriceCurrency}:{r.PriceAmount} owned:{r.Owned} gateOk:{r.UnlockedForPurchase}");
            }

            sb.AppendLine($"Coins:{Coins} Gems:{Gems} Equipped:{EquippedId ?? "(none)"}");
            return sb.ToString();
        }
    }
}

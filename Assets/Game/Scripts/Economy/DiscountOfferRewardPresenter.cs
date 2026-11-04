using Game.Collectibles;
using Game.Core.Config;
using Game.Economy.Config;
using UnityEngine;

namespace Game.Economy
{
    public static class DiscountOfferRewardPresenter
    {
        public static readonly CosmeticQuality CurrencyDefaultQuality = CosmeticQuality.Epic;

        public static string ResolveLabel(DiscountOfferRewardEntry reward)
        {
            switch (reward.kind)
            {
                case DiscountOfferRewardKind.Coins:
                    return $"金币 x{EconomyDisplayFormat.Format(reward.amount)}";

                case DiscountOfferRewardKind.Gems:
                    return $"钻石 x{EconomyDisplayFormat.Format(reward.amount)}";

                case DiscountOfferRewardKind.Wing:
                    return ResolveWingName(reward.itemId);

                case DiscountOfferRewardKind.Rod:
                    return ResolveRodName(reward.itemId);

                case DiscountOfferRewardKind.Trail:
                    return ResolveTrailName(reward.itemId);

                case DiscountOfferRewardKind.Pet:
                    var count = Mathf.Max(1, (int)reward.amount);
                    return count > 1 ? $"{ResolvePetName(reward.itemId)} x{count}" : ResolvePetName(reward.itemId);

                default:
                    return reward.itemId ?? string.Empty;
            }
        }

        public static CosmeticQuality ResolveQuality(DiscountOfferRewardEntry reward)
        {
            switch (reward.kind)
            {
                case DiscountOfferRewardKind.Coins:
                case DiscountOfferRewardKind.Gems:
                    return CurrencyDefaultQuality;

                case DiscountOfferRewardKind.Wing:
                    return ResolveWingQuality(reward.itemId);

                case DiscountOfferRewardKind.Rod:
                    return ResolveRodQuality(reward.itemId);

                case DiscountOfferRewardKind.Trail:
                    return ResolveTrailQuality(reward.itemId);

                case DiscountOfferRewardKind.Pet:
                    return ResolvePetQuality(reward.itemId);

                default:
                    return CosmeticQuality.Common;
            }
        }

        public static Sprite ResolveIcon(DiscountOfferRewardEntry reward, Sprite coinIcon = null, Sprite gemIcon = null)
        {
            switch (reward.kind)
            {
                case DiscountOfferRewardKind.Coins:
                    return coinIcon;
                case DiscountOfferRewardKind.Gems:
                    return gemIcon;
                case DiscountOfferRewardKind.Wing:
                    return TryWingIcon(reward.itemId);
                case DiscountOfferRewardKind.Rod:
                    return TryRodIcon(reward.itemId);
                case DiscountOfferRewardKind.Trail:
                    return TryTrailIcon(reward.itemId);
                case DiscountOfferRewardKind.Pet:
                    return TryPetIcon(reward.itemId);
                default:
                    return null;
            }
        }

        public static Sprite ResolveFrame(DiscountOfferRewardEntry reward, QualityVisualLibrary library)
        {
            var quality = ResolveQuality(reward);
            return library != null ? library.ResolveSlotBackground(quality) : null;
        }

        static CosmeticQuality ResolveWingQuality(string id)
        {
            var db = ConfigRegistry.Wings;
            return db != null && db.TryGet(id, out var def) && def != null
                ? def.quality
                : CosmeticQuality.Common;
        }

        static CosmeticQuality ResolveRodQuality(string id)
        {
            var db = ConfigRegistry.Rods;
            return db != null && db.TryGet(id, out var def) && def != null
                ? def.quality
                : CosmeticQuality.Common;
        }

        static CosmeticQuality ResolveTrailQuality(string id)
        {
            var db = ConfigRegistry.Trails;
            return db != null && db.TryGet(id, out var def) && def != null
                ? def.quality
                : CosmeticQuality.Common;
        }

        static CosmeticQuality ResolvePetQuality(string id)
        {
            var db = ConfigRegistry.Pets;
            if (db != null && db.TryGet(id, out var def) && def != null)
                return (CosmeticQuality)Mathf.Clamp((int)def.quality, 0, (int)CosmeticQuality.Mythic);

            return CosmeticQuality.Common;
        }

        static string ResolveWingName(string id)
        {
            var db = ConfigRegistry.Wings;
            return db != null && db.TryGet(id, out var def) && def != null
                ? def.displayName ?? id
                : id ?? "翅膀";
        }

        static string ResolveRodName(string id)
        {
            var db = ConfigRegistry.Rods;
            return db != null && db.TryGet(id, out var def) && def != null
                ? def.displayName ?? id
                : id ?? "钓竿";
        }

        static string ResolveTrailName(string id)
        {
            var db = ConfigRegistry.Trails;
            return db != null && db.TryGet(id, out var def) && def != null
                ? def.displayName ?? id
                : id ?? "拖尾";
        }

        static string ResolvePetName(string id)
        {
            var db = ConfigRegistry.Pets;
            return db != null && db.TryGet(id, out var def) && def != null
                ? def.displayName ?? id
                : id ?? "宠物";
        }

        static Sprite TryWingIcon(string id)
        {
            var db = ConfigRegistry.Wings;
            return db != null && db.TryGet(id, out var def) && def != null ? def.shopIcon : null;
        }

        static Sprite TryRodIcon(string id)
        {
            var db = ConfigRegistry.Rods;
            return db != null && db.TryGet(id, out var def) && def != null ? def.shopIcon : null;
        }

        static Sprite TryTrailIcon(string id)
        {
            var db = ConfigRegistry.Trails;
            return db != null && db.TryGet(id, out var def) && def != null ? def.shopIcon : null;
        }

        static Sprite TryPetIcon(string id)
        {
            var db = ConfigRegistry.Pets;
            return db != null && db.TryGet(id, out var def) && def != null ? def.inventoryIcon : null;
        }
    }
}

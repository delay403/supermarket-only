using Game.Collectibles.Pets;
using Game.Core.Config;
using Game.DailySignIn;
using UnityEngine;

namespace Game.UI
{
    /// <summary>签到 / 转盘等奖励行的图标与文案解析。</summary>
    public static class RewardPresentation
    {
        public static string FormatRewardLine(RewardDef r)
        {
            switch (r.type)
            {
                case RewardType.Coins:
                    return $"金币 x{r.amount}";
                case RewardType.Gems:
                    return $"钻石 x{r.amount}";
                case RewardType.Medals:
                    return $"奖牌 x{r.amount}";
                case RewardType.Pet:
                    return FormatNamedId("宠物", r.itemId, r.amount);
                case RewardType.Wing:
                    return FormatNamedId("翅膀", r.itemId, r.amount);
                case RewardType.Trail:
                    return FormatNamedId("拖尾", r.itemId, r.amount);
                case RewardType.Character:
                    return FormatNamedId("角色", r.itemId, r.amount);
                default:
                    return r.type.ToString();
            }
        }

        static string FormatNamedId(string prefix, string itemId, long amount)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
                return $"{prefix} x{Mathf.Max(1, (int)amount)}";
            return prefix;
        }

        public static Sprite TryResolveIcon(RewardDef r)
        {
            switch (r.type)
            {
                case RewardType.Pet:
                    if (!string.IsNullOrWhiteSpace(r.itemId) && ConfigRegistry.Pets != null &&
                        ConfigRegistry.Pets.TryGet(r.itemId.Trim(), out var pet) && pet != null)
                        return pet.inventoryIcon;
                    break;
                case RewardType.Wing:
                    if (!string.IsNullOrWhiteSpace(r.itemId) && ConfigRegistry.Wings != null &&
                        ConfigRegistry.Wings.TryGet(r.itemId.Trim(), out var wing) && wing != null)
                        return wing.shopIcon;
                    break;
                case RewardType.Trail:
                    if (!string.IsNullOrWhiteSpace(r.itemId) && ConfigRegistry.Trails != null &&
                        ConfigRegistry.Trails.TryGet(r.itemId.Trim(), out var trail) && trail != null)
                        return trail.shopIcon;
                    break;
                case RewardType.Character:
                    if (!string.IsNullOrWhiteSpace(r.itemId) && ConfigRegistry.Characters != null &&
                        ConfigRegistry.Characters.TryGet(r.itemId.Trim(), out var ch) && ch != null)
                        return ch.portraitIcon;
                    break;
            }

            return null;
        }
    }
}

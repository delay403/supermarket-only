using Game.Economy.Config;
using Game.Save;
using UnityEngine;

namespace Game.Economy
{
    public static class DiscountOfferRewardGranter
    {
        public static DiscountOfferRewardEntry[] ResolveRewards(DiscountOfferDef def)
        {
            if (def.rewards != null && def.rewards.Length > 0)
                return def.rewards;

            return System.Array.Empty<DiscountOfferRewardEntry>();
        }

        public static bool TryGrantAll(DiscountOfferDef def, out string error)
        {
            error = null;
            var data = SaveService.Data;
            if (data == null)
            {
                error = "存档未就绪";
                return false;
            }

            var rewards = ResolveRewards(def);
            if (rewards.Length == 0)
            {
                error = "无奖励配置";
                return false;
            }

            for (var i = 0; i < rewards.Length; i++)
            {
                if (!TryGrantOne(data, rewards[i], out error))
                    return false;
            }

            return true;
        }

        static bool TryGrantOne(PlayerSaveData data, DiscountOfferRewardEntry reward, out string error)
        {
            error = null;
            switch (reward.kind)
            {
                case DiscountOfferRewardKind.Coins:
                    if (reward.amount > 0)
                        data.coins += reward.amount;
                    return true;

                case DiscountOfferRewardKind.Gems:
                    if (reward.amount > 0)
                        data.gems += reward.amount;
                    return true;

                case DiscountOfferRewardKind.Wing:
                    if (string.IsNullOrWhiteSpace(reward.itemId))
                    {
                        error = "翅膀 id 为空";
                        return false;
                    }

                    if (data.HasWing(reward.itemId))
                    {
                        error = "已拥有该翅膀";
                        return false;
                    }

                    data.AddWing(reward.itemId.Trim());
                    return true;

                case DiscountOfferRewardKind.Rod:
                    if (string.IsNullOrWhiteSpace(reward.itemId))
                    {
                        error = "钓竿 id 为空";
                        return false;
                    }

                    if (data.HasRod(reward.itemId))
                    {
                        error = "已拥有该钓竿";
                        return false;
                    }

                    data.AddRod(reward.itemId.Trim());
                    return true;

                case DiscountOfferRewardKind.Trail:
                    if (string.IsNullOrWhiteSpace(reward.itemId))
                    {
                        error = "拖尾 id 为空";
                        return false;
                    }

                    if (data.HasTrail(reward.itemId))
                    {
                        error = "已拥有该拖尾";
                        return false;
                    }

                    data.AddTrail(reward.itemId.Trim());
                    return true;

                case DiscountOfferRewardKind.Pet:
                    if (string.IsNullOrWhiteSpace(reward.itemId))
                    {
                        error = "宠物 id 为空";
                        return false;
                    }

                    var count = Mathf.Max(1, (int)reward.amount);
                    data.AddPet(reward.itemId.Trim(), count);
                    return true;

                default:
                    error = "未知奖励类型";
                    return false;
            }
        }
    }
}

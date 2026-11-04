using Game.Collectibles.Characters;
using Game.Collectibles.Pets;
using Game.Core.Config;
using Game.Save;
using UnityEngine;

namespace Game.DailySignIn
{
    /// <summary>统一发放 <see cref="RewardDef"/>（签到、转盘等共用）。</summary>
    public static class RewardGrant
    {
        public static void Apply(RewardDef reward)
        {
            if (SaveService.Data == null) return;
            switch (reward.type)
            {
                case RewardType.Coins:
                    SaveService.Data.coins += reward.amount;
                    break;
                case RewardType.Gems:
                    SaveService.Data.gems += reward.amount;
                    break;
                case RewardType.Medals:
                    SaveService.Data.AddMedals(reward.amount);
                    break;
                case RewardType.Pet:
                    if (!string.IsNullOrWhiteSpace(reward.itemId))
                        PetService.Add(reward.itemId, Mathf.Max(1, (int)reward.amount));
                    break;
                case RewardType.Wing:
                    if (!string.IsNullOrWhiteSpace(reward.itemId))
                        SaveService.Data.AddWing(reward.itemId);
                    break;
                case RewardType.Trail:
                    if (!string.IsNullOrWhiteSpace(reward.itemId))
                        SaveService.Data.AddTrail(reward.itemId);
                    break;
                case RewardType.Character:
                    GrantCharacter(reward.itemId);
                    break;
                default:
                    Debug.LogWarning($"RewardGrant: unknown reward type {reward.type}");
                    break;
            }
        }

        static void GrantCharacter(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return;
            var id = characterId.Trim();
            if (SaveService.Data.HasCharacter(id))
                return;

            var db = ConfigRegistry.Characters;
            var catalogStars = 1;
            if (db != null && db.TryGet(id, out var def) && def != null)
                catalogStars = Mathf.Clamp(def.catalogStars, 1, 3);

            SaveService.Data.UpsertOwnedCharacter(id, catalogStars);
        }
    }
}

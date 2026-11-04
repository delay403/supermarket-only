using System;
using System.Collections.Generic;
using Game.Core.Content;
using UnityEngine;

namespace Game.SupermarketOrganize.Config
{
    [Serializable]
    public sealed class SupermarketOrganizeDifficultyEntryDef
    {
        public string id = "grocery";
        public Sprite previewImage;
        [Min(1)]
        public int staminaCost = 1;
        public string startButtonLabel = "开始挑战";
        public string shelfCountFormat = "货架：{0}个";
        public string itemCountFormat = "商品：{0}个";
        public float entryAnimationSeconds = 3f;
        public int displayOrder;
    }

    [CreateAssetMenu(
        menuName = "Game/Supermarket Organize/Home Config",
        fileName = "SupermarketOrganizeHomeConfig")]
    public sealed class SupermarketOrganizeHomeConfig : ScriptableObject
    {
        [Tooltip("为 false 时禁用体力消耗、恢复与相关 UI（逻辑保留）。")]
        public bool staminaEnabled = false;

        public int maxStamina = 5;
        public int staminaRegenIntervalMinutes = 30;
        public int staminaReward = 5;
        public string staminaTextFormat = "{0}";
        public SupermarketOrganizeDifficultyEntryDef[] difficulties = Array.Empty<SupermarketOrganizeDifficultyEntryDef>();

        public static SupermarketOrganizeHomeConfig ResolveRuntime()
        {
            var loaded = GameContentService.Resolve<SupermarketOrganizeHomeConfig>();
            if (loaded != null)
                return loaded;

            var fallback = CreateInstance<SupermarketOrganizeHomeConfig>();
            fallback.hideFlags = HideFlags.HideAndDontSave;
            fallback.difficulties = BuildDefaultDifficulties();
            return fallback;
        }

        public IReadOnlyList<SupermarketOrganizeDifficultyEntryDef> GetSortedDifficulties()
        {
            if (difficulties == null || difficulties.Length == 0)
                difficulties = BuildDefaultDifficulties();

            var list = new List<SupermarketOrganizeDifficultyEntryDef>(difficulties);
            list.Sort((a, b) => a.displayOrder.CompareTo(b.displayOrder));
            return list;
        }

        public bool TryGetEntry(string id, out SupermarketOrganizeDifficultyEntryDef entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var list = GetSortedDifficulties();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].id == id)
                {
                    entry = list[i];
                    return true;
                }
            }

            return false;
        }

        public static SupermarketOrganizeDifficultyEntryDef[] BuildDefaultDifficulties() => new[]
        {
            new SupermarketOrganizeDifficultyEntryDef
            {
                id = "grocery",
                staminaCost = 1,
                startButtonLabel = "开始挑战",
                shelfCountFormat = "货架：{0}个",
                itemCountFormat = "商品：{0}个",
                entryAnimationSeconds = 3f,
                displayOrder = 0,
            },
            new SupermarketOrganizeDifficultyEntryDef
            {
                id = "supermarket",
                staminaCost = 1,
                startButtonLabel = "开始挑战",
                shelfCountFormat = "货架：{0}个",
                itemCountFormat = "商品：{0}个",
                entryAnimationSeconds = 3.5f,
                displayOrder = 1,
            },
        };
    }
}

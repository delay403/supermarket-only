using System;
using System.Collections.Generic;
using Game.Core.Content;
using UnityEngine;

namespace Game.SupermarketOrganize.Config
{
    public enum SupermarketOrganizeUpgradeEntryKind
    {
        Skill = 0,
        Attribute = 1,
    }

    [Serializable]
    public sealed class SupermarketOrganizeUpgradeEntryDef
    {
        public SupermarketOrganizeUpgradeEntryKind kind = SupermarketOrganizeUpgradeEntryKind.Skill;
        public SupermarketSkillId skillId = SupermarketSkillId.Scanner;
        public SupermarketAttributeId attributeId = SupermarketAttributeId.HandleCapacity;
        [Tooltip("留空则使用默认名称。")]
        public string displayNameOverride;
        [Tooltip("留空则使用默认描述。")]
        [TextArea(2, 5)]
        public string descriptionOverride;
        public Sprite icon;
        [Tooltip("留空则使用 UpgradeUiConfig 中对应分类（技能/属性）的默认底图。")]
        public Sprite background;
        public int displayOrder;
    }

    [CreateAssetMenu(
        menuName = "Game/Supermarket Organize/Upgrade UI Config",
        fileName = "SupermarketOrganizeUpgradeUiConfig")]
    public sealed class SupermarketOrganizeUpgradeUiConfig : ScriptableObject
    {
        [Header("左侧小图标底图")]
        [Tooltip("「能力」区技能格子的默认底图。")]
        public Sprite skillIconBackground;
        [Tooltip("「强化道具」区属性格子的默认底图。")]
        public Sprite attributeIconBackground;

        public SupermarketOrganizeUpgradeEntryDef[] entries = Array.Empty<SupermarketOrganizeUpgradeEntryDef>();

        public Sprite ResolveCellBackground(SupermarketOrganizeUpgradeEntryDef entry)
        {
            if (entry == null)
                return null;

            if (entry.background != null)
                return entry.background;

            return entry.kind switch
            {
                SupermarketOrganizeUpgradeEntryKind.Skill => skillIconBackground,
                SupermarketOrganizeUpgradeEntryKind.Attribute => attributeIconBackground,
                _ => null,
            };
        }

        public static SupermarketOrganizeUpgradeUiConfig ResolveRuntime()
        {
            var loaded = GameContentService.Resolve<SupermarketOrganizeUpgradeUiConfig>();
            if (loaded != null)
                return loaded;

            var fallback = CreateInstance<SupermarketOrganizeUpgradeUiConfig>();
            fallback.hideFlags = HideFlags.HideAndDontSave;
            fallback.entries = BuildDefaultEntries();
            return fallback;
        }

        public IReadOnlyList<SupermarketOrganizeUpgradeEntryDef> GetSortedEntries()
        {
            if (entries == null || entries.Length == 0)
                entries = BuildDefaultEntries();

            var list = new List<SupermarketOrganizeUpgradeEntryDef>(entries);
            list.Sort((a, b) => a.displayOrder.CompareTo(b.displayOrder));
            return list;
        }

        public static SupermarketOrganizeUpgradeEntryDef[] BuildDefaultEntries() => new[]
        {
            Skill(SupermarketSkillId.Scanner, 0),
            Skill(SupermarketSkillId.Magnet, 1),
            Skill(SupermarketSkillId.XRay, 2),
            Skill(SupermarketSkillId.AutoStocker, 3),
            Attribute(SupermarketAttributeId.HandleCapacity, 10),
            Attribute(SupermarketAttributeId.MoveSpeed, 11),
            Attribute(SupermarketAttributeId.PickupRange, 12),
        };

        static SupermarketOrganizeUpgradeEntryDef Skill(SupermarketSkillId id, int order) => new()
        {
            kind = SupermarketOrganizeUpgradeEntryKind.Skill,
            skillId = id,
            displayOrder = order,
        };

        static SupermarketOrganizeUpgradeEntryDef Attribute(SupermarketAttributeId id, int order) => new()
        {
            kind = SupermarketOrganizeUpgradeEntryKind.Attribute,
            attributeId = id,
            displayOrder = order,
        };
    }
}

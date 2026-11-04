using Game.Economy;
using Game.SupermarketOrganize.Config;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>升级面板右侧详情区。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeUpgradeDetailUi : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text effectText;
        [SerializeField] private Text costText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Text upgradeButtonLabel;
        [SerializeField] private GameObject maxLevelBadgeRoot;
        [SerializeField] private SupermarketOrganizeUpgradeLevelProgressUi levelProgressUi;

        SupermarketOrganizeUpgradeEntryDef _entry;

        void Awake() => upgradeButton?.onClick.AddListener(OnUpgradeClicked);

        void OnDestroy() => upgradeButton?.onClick.RemoveListener(OnUpgradeClicked);

        public void Bind(SupermarketOrganizeUpgradeEntryDef entry)
        {
            _entry = entry;
            Refresh();
        }

        public void Refresh()
        {
            if (_entry == null)
                return;

            var level = GetCurrentLevel();
            var maxLevel = GetMaxLevel();
            var maxed = level >= maxLevel;
            var cost = GetUpgradeCost();

            if (iconImage != null)
            {
                if (_entry.icon != null)
                {
                    iconImage.sprite = _entry.icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = new Color(1f, 1f, 1f, 0.2f);
                }

                iconImage.preserveAspect = true;
            }

            if (nameText != null)
                nameText.text = ResolveDisplayName();

            if (levelText != null)
                levelText.text = $"等级：{level}/{maxLevel}";

            if (descriptionText != null)
                descriptionText.text = ResolveDescription();

            if (effectText != null)
                effectText.text = ResolveEffect(level);

            RefreshLevelProgress(level, maxLevel);

            if (costText != null)
                costText.text = maxed ? "已满级" : $"X{cost}";

            if (maxLevelBadgeRoot != null)
                maxLevelBadgeRoot.SetActive(maxed);

            if (upgradeButton != null)
                upgradeButton.interactable = !maxed;

            if (upgradeButtonLabel != null)
            {
                if (maxed)
                    upgradeButtonLabel.text = "已满级";
                else if (level <= 0)
                    upgradeButtonLabel.text = "解锁";
                else
                    upgradeButtonLabel.text = "升级";
            }
        }

        void RefreshLevelProgress(int level, int maxLevel)
        {
            levelProgressUi?.Refresh(level, maxLevel);
        }

        void OnUpgradeClicked()
        {
            if (_entry == null)
                return;

            var cost = GetUpgradeCost();
            if (cost <= 0)
                return;

            switch (_entry.kind)
            {
                case SupermarketOrganizeUpgradeEntryKind.Skill:
                    if (!SupermarketOrganizeProgressionService.CanAffordSkillUpgrade(_entry.skillId))
                    {
                        UiRouter.OpenCoinsHub();
                        return;
                    }

                    SupermarketOrganizeProgressionService.TryUpgradeSkill(_entry.skillId);
                    break;

                case SupermarketOrganizeUpgradeEntryKind.Attribute:
                    if (!SupermarketOrganizeProgressionService.CanAffordAttributeUpgrade(_entry.attributeId))
                    {
                        UiRouter.OpenCoinsHub();
                        return;
                    }

                    SupermarketOrganizeProgressionService.TryUpgradeAttribute(_entry.attributeId);
                    break;
            }
        }

        string ResolveDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(_entry.displayNameOverride))
                return _entry.displayNameOverride;

            return _entry.kind switch
            {
                SupermarketOrganizeUpgradeEntryKind.Skill =>
                    SupermarketOrganizeProgressionDisplayNames.GetSkillName(_entry.skillId),
                SupermarketOrganizeUpgradeEntryKind.Attribute =>
                    SupermarketOrganizeProgressionDisplayNames.GetAttributeName(_entry.attributeId),
                _ => string.Empty,
            };
        }

        string ResolveDescription()
        {
            if (!string.IsNullOrWhiteSpace(_entry.descriptionOverride))
                return _entry.descriptionOverride;

            return _entry.kind switch
            {
                SupermarketOrganizeUpgradeEntryKind.Skill =>
                    SupermarketOrganizeProgressionDisplayNames.GetSkillDescription(_entry.skillId),
                SupermarketOrganizeUpgradeEntryKind.Attribute =>
                    SupermarketOrganizeProgressionDisplayNames.GetAttributeDescription(_entry.attributeId),
                _ => string.Empty,
            };
        }

        string ResolveEffect(int level) => _entry.kind switch
        {
            SupermarketOrganizeUpgradeEntryKind.Skill =>
                SupermarketOrganizeProgressionDisplayNames.FormatSkillEffect(_entry.skillId, level),
            SupermarketOrganizeUpgradeEntryKind.Attribute =>
                SupermarketOrganizeProgressionDisplayNames.FormatAttributeEffect(_entry.attributeId, level),
            _ => string.Empty,
        };

        int GetCurrentLevel() => _entry.kind switch
        {
            SupermarketOrganizeUpgradeEntryKind.Skill =>
                SupermarketOrganizeProgressionService.GetSkillLevel(_entry.skillId),
            SupermarketOrganizeUpgradeEntryKind.Attribute =>
                SupermarketOrganizeProgressionService.GetAttributeLevel(_entry.attributeId),
            _ => 0,
        };

        int GetMaxLevel() => _entry.kind switch
        {
            SupermarketOrganizeUpgradeEntryKind.Skill =>
                SupermarketOrganizeProgressionService.GetSkillMaxLevel(_entry.skillId),
            SupermarketOrganizeUpgradeEntryKind.Attribute =>
                SupermarketOrganizeProgressionService.GetAttributeMaxLevel(_entry.attributeId),
            _ => 0,
        };

        long GetUpgradeCost() => _entry.kind switch
        {
            SupermarketOrganizeUpgradeEntryKind.Skill =>
                SupermarketOrganizeProgressionService.GetSkillUpgradeCost(_entry.skillId),
            SupermarketOrganizeUpgradeEntryKind.Attribute =>
                SupermarketOrganizeProgressionService.GetAttributeUpgradeCost(_entry.attributeId),
            _ => 0,
        };
    }
}

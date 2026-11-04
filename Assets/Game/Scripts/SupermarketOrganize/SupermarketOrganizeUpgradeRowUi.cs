using System;
using DG.Tweening;
using Game.SupermarketOrganize.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>升级面板左侧技能/属性图标格。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeUpgradeRowUi : MonoBehaviour
    {
        [SerializeField] private Button cellButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text levelText;
        [SerializeField] private Text nameText;
        [SerializeField] private GameObject maxLevelBadgeRoot;
        [SerializeField] private RectTransform selectionFrame;

        SupermarketOrganizeUpgradeEntryDef _entry;
        Sprite _cellBackground;
        Tweener _pulseTween;

        public SupermarketOrganizeUpgradeEntryDef Entry => _entry;

        void OnDestroy() => _pulseTween?.Kill();

        public void SetupClick(Action<SupermarketOrganizeUpgradeRowUi> onClick)
        {
            if (cellButton == null)
                cellButton = GetComponent<Button>();
            cellButton.onClick.RemoveAllListeners();
            cellButton.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void Bind(SupermarketOrganizeUpgradeEntryDef entry, Sprite cellBackground = null)
        {
            _entry = entry;
            _cellBackground = cellBackground;
            Refresh();
        }

        public void ApplySelection(bool selected, bool playPulse)
        {
            if (selectionFrame != null)
            {
                selectionFrame.gameObject.SetActive(selected);
                if (selected && playPulse)
                    PlaySelectionPulse();
            }
        }

        public void Refresh()
        {
            if (_entry == null)
                return;

            var level = GetCurrentLevel();
            var maxLevel = GetMaxLevel();
            var maxed = level >= maxLevel;

            if (backgroundImage != null)
            {
                var background = ResolveBackground();
                if (background != null)
                {
                    backgroundImage.sprite = background;
                    backgroundImage.color = Color.white;
                }
            }

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

            if (levelText != null)
                levelText.text = $"LV.{level}";

            if (nameText != null)
                nameText.text = ResolveDisplayName();

            if (maxLevelBadgeRoot != null)
                maxLevelBadgeRoot.SetActive(maxed);
        }

        public void PlaySelectionPulse()
        {
            if (selectionFrame == null)
                return;

            _pulseTween?.Kill();
            selectionFrame.localScale = Vector3.one;
            _pulseTween = selectionFrame
                .DOScale(1.12f, 0.12f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo);
        }

        Sprite ResolveBackground()
        {
            if (_entry?.background != null)
                return _entry.background;

            return _cellBackground;
        }

        string ResolveDisplayName()
        {
            if (_entry == null)
                return string.Empty;

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

        int GetCurrentLevel() => _entry == null
            ? 0
            : _entry.kind switch
            {
                SupermarketOrganizeUpgradeEntryKind.Skill =>
                    SupermarketOrganizeProgressionService.GetSkillLevel(_entry.skillId),
                SupermarketOrganizeUpgradeEntryKind.Attribute =>
                    SupermarketOrganizeProgressionService.GetAttributeLevel(_entry.attributeId),
                _ => 0,
            };

        int GetMaxLevel() => _entry == null
            ? 0
            : _entry.kind switch
            {
                SupermarketOrganizeUpgradeEntryKind.Skill =>
                    SupermarketOrganizeProgressionService.GetSkillMaxLevel(_entry.skillId),
                SupermarketOrganizeUpgradeEntryKind.Attribute =>
                    SupermarketOrganizeProgressionService.GetAttributeMaxLevel(_entry.attributeId),
                _ => 0,
            };
    }
}

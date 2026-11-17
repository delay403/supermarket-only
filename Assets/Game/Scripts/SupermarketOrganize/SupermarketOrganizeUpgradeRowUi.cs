using System;
using Game.Audio;
using Game.SupermarketOrganize.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>局外商店里的一张道具或永久强化卡。</summary>
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
        [SerializeField] private Text effectText;
        [SerializeField] private Text cooldownText;
        [SerializeField] private GameObject costRoot;
        [SerializeField] private Text costText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Text upgradeButtonLabel;

        const float CardWidth = 200f;
        const float CardHeight = 418f;

        SupermarketOrganizeUpgradeEntryDef _entry;
        Sprite _cellBackground;
        Font _font;
        Sprite _buyButtonSprite;
        Sprite _coinIconSprite;

        public SupermarketOrganizeUpgradeEntryDef Entry => _entry;

        void Awake()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }

        void OnDestroy()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        }

        public static SupermarketOrganizeUpgradeRowUi CreateShopCard(
            RectTransform parent,
            Font font,
            Sprite buyButtonSprite,
            Sprite coinIconSprite)
        {
            var root = new GameObject("ShopCard", typeof(RectTransform));
            root.layer = parent.gameObject.layer;
            var rt = root.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CardWidth, CardHeight);

            var layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = CardWidth;
            layout.preferredHeight = CardHeight;
            layout.minWidth = CardWidth;
            layout.minHeight = CardHeight;

            var card = root.AddComponent<SupermarketOrganizeUpgradeRowUi>();
            card._font = font;
            card._buyButtonSprite = buyButtonSprite;
            card._coinIconSprite = coinIconSprite;
            card.BuildShopCardVisuals();
            card.upgradeButton.onClick.AddListener(card.OnUpgradeClicked);
            return card;
        }

        public void SetupClick(Action<SupermarketOrganizeUpgradeRowUi> onClick)
        {
            if (cellButton == null)
                cellButton = GetComponent<Button>();
            if (cellButton == null)
                return;

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
            if (selectionFrame == null)
                return;

            selectionFrame.gameObject.SetActive(selected);
        }

        public void PlaySelectionPulse()
        {
        }

        public void Refresh()
        {
            if (_entry == null)
                return;

            var level = GetCurrentLevel();
            var maxLevel = GetMaxLevel();
            var maxed = level >= maxLevel;
            var nextLevel = Mathf.Min(level + 1, maxLevel);
            var cost = GetUpgradeCost();
            var canAfford = !maxed && cost > 0 && CanAfford();

            if (backgroundImage != null)
            {
                var background = ResolveBackground();
                if (background != null)
                {
                    backgroundImage.sprite = background;
                    backgroundImage.color = Color.white;
                    backgroundImage.preserveAspect = true;
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

            if (nameText != null)
                nameText.text = ResolveDisplayName();

            if (levelText != null)
                levelText.text = $"等级：{level}/{maxLevel}";

            if (effectText != null)
            {
                effectText.gameObject.SetActive(!maxed);
                if (!maxed)
                    effectText.text = $"下一级：{ResolveNextEffect(nextLevel)}";
            }

            if (cooldownText != null)
            {
                var showCooldown = !maxed && _entry.kind == SupermarketOrganizeUpgradeEntryKind.Skill;
                cooldownText.gameObject.SetActive(showCooldown);
                if (showCooldown)
                    cooldownText.text = $"冷却：{ResolveNextCooldown(nextLevel)}";
            }

            if (costRoot != null)
                costRoot.SetActive(!maxed);
            else if (costText != null)
                costText.gameObject.SetActive(!maxed);

            if (costText != null && !maxed)
            {
                costText.text = $"X{cost}";
                costText.color = canAfford ? Color.white : new Color(1f, 0.45f, 0.45f, 1f);
            }

            if (maxLevelBadgeRoot != null)
                maxLevelBadgeRoot.SetActive(maxed);

            if (upgradeButton != null)
                upgradeButton.interactable = !maxed;

            if (upgradeButtonLabel != null)
            {
                if (maxed)
                    upgradeButtonLabel.text = "已满级";
                else if (level <= 0)
                    upgradeButtonLabel.text = "购买";
                else
                    upgradeButtonLabel.text = "升级";
            }
        }

        void OnUpgradeClicked()
        {
            if (_entry == null)
                return;

            AudioGameplayFeedback.PlayUiButton();
            var cost = GetUpgradeCost();
            if (cost <= 0)
                return;

            if (!CanAfford())
                return;

            switch (_entry.kind)
            {
                case SupermarketOrganizeUpgradeEntryKind.Skill:
                    SupermarketOrganizeProgressionService.TryUpgradeSkill(_entry.skillId);
                    break;
                case SupermarketOrganizeUpgradeEntryKind.Attribute:
                    SupermarketOrganizeProgressionService.TryUpgradeAttribute(_entry.attributeId);
                    break;
            }
        }

        void BuildShopCardVisuals()
        {
            backgroundImage = AddImage(transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            backgroundImage.preserveAspect = true;

            iconImage = AddImage(transform, "Icon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(88f, 88f), false);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            nameText = AddLabel(transform, "Name", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(180f, 32f), 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            levelText = AddLabel(transform, "Level", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -148f), new Vector2(180f, 24f), 18, FontStyle.Normal, TextAnchor.MiddleCenter);
            effectText = AddLabel(transform, "Effect", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(176f, 48f), 16, FontStyle.Normal, TextAnchor.UpperCenter);
            cooldownText = AddLabel(transform, "Cooldown", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(176f, 24f), 16, FontStyle.Normal, TextAnchor.MiddleCenter);

            var costRt = CreateRect("CostRow", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 86f), new Vector2(140f, 28f));
            costRoot = costRt.gameObject;
            var coin = AddImage(costRt, "CoinIcon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(28f, 28f), false);
            coin.preserveAspect = true;
            coin.raycastTarget = false;
            if (_coinIconSprite != null)
                coin.sprite = _coinIconSprite;
            costText = AddLabel(costRt, "Cost", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            var costTextRt = costText.rectTransform;
            costTextRt.offsetMin = new Vector2(36f, 0f);
            costTextRt.offsetMax = new Vector2(-4f, 0f);

            var buttonRt = CreateRect("BuyButton", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(140f, 46f));
            var buttonImage = buttonRt.gameObject.AddComponent<Image>();
            buttonImage.sprite = _buyButtonSprite;
            buttonImage.type = _buyButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            buttonImage.color = Color.white;
            upgradeButton = buttonRt.gameObject.AddComponent<Button>();
            upgradeButton.targetGraphic = buttonImage;
            upgradeButtonLabel = AddLabel(buttonRt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            upgradeButtonLabel.text = "购买";
        }

        Image AddImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, bool stretch)
        {
            var rt = stretch
                ? CreateRect(name, parent, anchorMin, anchorMax, pos, size)
                : CreateRect(name, parent, anchorMin, anchorMax, pos, size);
            if (stretch)
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            return rt.gameObject.AddComponent<Image>();
        }

        Text AddLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, int fontSize, FontStyle style, TextAnchor alignment)
        {
            var rt = CreateRect(name, parent, anchorMin, anchorMax, pos, size);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            var outline = rt.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }

            return rt;
        }

        Sprite ResolveBackground()
        {
            if (_cellBackground != null)
                return _cellBackground;
            return _entry?.background;
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

        string ResolveNextEffect(int nextLevel) => _entry.kind switch
        {
            SupermarketOrganizeUpgradeEntryKind.Skill =>
                SupermarketOrganizeProgressionDisplayNames.FormatSkillNextEffect(_entry.skillId, nextLevel),
            SupermarketOrganizeUpgradeEntryKind.Attribute =>
                SupermarketOrganizeProgressionDisplayNames.FormatAttributeEffect(_entry.attributeId, nextLevel),
            _ => string.Empty,
        };

        string ResolveNextCooldown(int nextLevel) =>
            SupermarketOrganizeProgressionDisplayNames.FormatSkillNextCooldown(_entry.skillId, nextLevel);

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

        bool CanAfford() => _entry.kind switch
        {
            SupermarketOrganizeUpgradeEntryKind.Skill =>
                SupermarketOrganizeProgressionService.CanAffordSkillUpgrade(_entry.skillId),
            SupermarketOrganizeUpgradeEntryKind.Attribute =>
                SupermarketOrganizeProgressionService.CanAffordAttributeUpgrade(_entry.attributeId),
            _ => false,
        };
    }
}

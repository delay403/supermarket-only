using System;
using DG.Tweening;
using Game.Collectibles;
using Game.Core.Config;
using Game.Collectibles.Characters;
using Game.Collectibles.Stats;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>角色卡片：锁、价格币种图标、选中描边缩放、星级、属性摘要（口径同翅膀/宠物）。</summary>
    public sealed class CharacterItemView : MonoBehaviour
    {
        [SerializeField] private Button cellButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text attrText;
        [SerializeField] private GameObject lockRoot;
        [SerializeField] private GameObject prizeRoot;
        [SerializeField] private Image prizeCurrencyIcon;
        [SerializeField] private Text prizeAmountText;
        [SerializeField] private GameObject[] starObjects = new GameObject[5];
        [SerializeField] private GameObject equippedBannerRoot;
        [SerializeField] private Text equippedBannerText;
        [SerializeField] private RectTransform selectionFrame;

        private CharacterShopRowViewModel _row;
        private Tweener _pulseTween;

        public CharacterShopRowViewModel Row => _row;

        private void Reset()
        {
            cellButton = GetComponentInChildren<Button>();
        }

        private void OnDestroy()
        {
            _pulseTween?.Kill();
        }

        public void SetupClick(Action<CharacterItemView> onClick)
        {
            if (cellButton == null) cellButton = GetComponent<Button>();
            cellButton.onClick.RemoveAllListeners();
            cellButton.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void Apply(
            CharacterShopRowViewModel row,
            string equippedCharacterId,
            bool selected,
            CosmeticQuality quality,
            Sprite slotBackground,
            Sprite portrait,
            Sprite coinIconSprite,
            Sprite gemIconSprite,
            string attrLine,
            int stars1to5,
            bool playSelectPulse)
        {
            _row = row;
            if (row == null) return;

            bool owned = row.Owned;
            bool locked = !owned;
            bool equipped =
                owned && string.Equals(equippedCharacterId, row.Id, StringComparison.Ordinal);

            var lib = ConfigRegistry.QualityVisuals;

            if (nameText != null)
                nameText.text = row.DisplayName ?? "";

            if (attrText != null)
                attrText.text = attrLine ?? "";

            if (backgroundImage != null)
            {
                Sprite bg = slotBackground;
                bg ??= lib != null ? lib.ResolveSlotBackground(quality) : null;
                if (bg != null)
                {
                    backgroundImage.sprite = bg;
                    backgroundImage.color = Color.white;
                }
                else
                {
                    backgroundImage.sprite = null;
                    backgroundImage.color = new Color(0.12f, 0.12f, 0.18f, 0.94f);
                }
            }

            if (portraitImage != null)
            {
                if (portrait != null)
                {
                    portraitImage.sprite = portrait;
                    portraitImage.color = Color.white;
                }
                else
                {
                    portraitImage.sprite = null;
                    portraitImage.color = new Color(1f, 1f, 1f, 0.12f);
                }

                // portraitImage.preserveAspect = true;
            }

            if (lockRoot != null)
                lockRoot.SetActive(locked);

            if (prizeRoot != null)
            {
                prizeRoot.SetActive(locked);
                if (locked)
                {
                    if (prizeCurrencyIcon != null)
                    {
                        var spr = row.PriceCurrency == CharacterCurrencyType.Gems ? gemIconSprite : coinIconSprite;
                        prizeCurrencyIcon.sprite = spr;
                        prizeCurrencyIcon.enabled = spr != null;
                    }

                    if (prizeAmountText != null)
                        prizeAmountText.text = $"x{row.PriceAmount}";
                }
            }

            ApplyStars(Mathf.Clamp(stars1to5, 0, 5));

            if (equippedBannerRoot != null)
                equippedBannerRoot.SetActive(equipped);

            if (equippedBannerText != null)
                equippedBannerText.text = "使用中";

            if (selectionFrame != null)
            {
                selectionFrame.gameObject.SetActive(selected);
                if (selected && playSelectPulse)
                    PlaySelectionPulse();
            }
        }

        static string BuildAttrLine(string characterId)
        {
            if (ConfigRegistry.Characters != null &&
                ConfigRegistry.Characters.TryGet(characterId, out var def) && def != null)
                return StatModifiersUiFormat.FormatItemLine(def.modifiers);
            return StatModifiersUiFormat.NoBonusText;
        }

        public static string ResolveAttrLine(string characterId)
        {
            return BuildAttrLine(characterId);
        }

        void ApplyStars(int count)
        {
            if (starObjects == null) return;
            for (var i = 0; i < starObjects.Length; i++)
            {
                var go = starObjects[i];
                if (go == null) continue;
                go.SetActive(i < count);
            }
        }

        public void PlaySelectionPulse()
        {
            if (selectionFrame == null) return;
            _pulseTween?.Kill();
            selectionFrame.localScale = Vector3.one;
            _pulseTween = selectionFrame
                .DOScale(1.12f, 0.12f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo);
        }
    }
}

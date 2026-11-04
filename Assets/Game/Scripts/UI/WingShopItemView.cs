using System;
using DG.Tweening;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 翅膀商店左侧格子：未拥有显示锁；已拥有（含优惠/赠送等途径）可装备。
    /// 购买资格（UnlockedForPurchase）只影响能否点「购买」，不影响已拥有物品的使用。
    /// </summary>
    public sealed class WingShopItemView : MonoBehaviour
    {
        [SerializeField] private Button cellButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text attrText;
        [SerializeField] private GameObject lockRoot;
        [SerializeField] private GameObject equippedBannerRoot;
        [SerializeField] private Text equippedBannerText;
        [SerializeField] private RectTransform selectionFrame;

        private CosmeticShopRowViewModel _row;
        private Tweener _pulseTween;

        public CosmeticShopRowViewModel Row => _row;

        private void Reset()
        {
            cellButton = GetComponentInChildren<Button>();
        }

        private void OnDestroy()
        {
            _pulseTween?.Kill();
        }

        public void SetupClick(Action<WingShopItemView> onClick)
        {
            if (cellButton == null) cellButton = GetComponent<Button>();
            cellButton.onClick.RemoveAllListeners();
            cellButton.onClick.AddListener(() => onClick?.Invoke(this));
        }

        /// <summary>不设颜色：底图、图标仅用 Sprite（品质底图请在 SO 配置）。</summary>
        public void Apply(
            CosmeticShopRowViewModel row,
            string equippedWingId,
            bool selected,
            Sprite slotBackground,
            Sprite itemIcon,
            string attrLine,
            bool playSelectPulse)
        {
            _row = row;
            if (row == null) return;

            bool owned = row.Owned;
            bool lockedInShop = !owned;
            bool equipped = owned && string.Equals(equippedWingId, row.Id, StringComparison.Ordinal);

            if (attrText != null)
                attrText.text = attrLine ?? "";

            if (backgroundImage != null)
            {
                if (slotBackground != null)
                {
                    backgroundImage.sprite = slotBackground;
                    backgroundImage.color = Color.white;
                }
                else
                {
                    backgroundImage.sprite = null;
                    backgroundImage.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
                }
            }

            if (iconImage != null)
            {
                if (itemIcon != null)
                {
                    iconImage.sprite = itemIcon;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = new Color(1f, 1f, 1f, 0.15f);
                }
                iconImage.preserveAspect = true;
            }

            if (lockRoot != null)
                lockRoot.SetActive(lockedInShop);

            if (equippedBannerRoot != null)
                equippedBannerRoot.SetActive(equipped);

            if (equippedBannerText != null)
                equippedBannerText.text = "携带中";

            if (selectionFrame != null)
            {
                selectionFrame.gameObject.SetActive(selected);
                if (selected && playSelectPulse)
                    PlaySelectionPulse();
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

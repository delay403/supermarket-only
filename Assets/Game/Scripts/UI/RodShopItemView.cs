using System;
using DG.Tweening;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 钓竿商店左侧格子：未购买显示锁；选中脉冲与 <see cref="WingShopItemView"/> 同源。
    /// </summary>
    public sealed class RodShopItemView : MonoBehaviour
    {
        [SerializeField] private Button cellButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text attrText;
        [SerializeField] private GameObject lockRoot;
        [SerializeField] private GameObject equippedBannerRoot;
        [SerializeField] private Text equippedBannerText;
        [SerializeField] private RectTransform selectionFrame;

        RodShopRowViewModel _row;
        Tweener _pulseTween;

        public RodShopRowViewModel Row => _row;

        void Reset()
        {
            cellButton = GetComponentInChildren<Button>();
        }

        void OnDestroy()
        {
            _pulseTween?.Kill();
        }

        public void SetupClick(Action<RodShopItemView> onClick)
        {
            if (cellButton == null) cellButton = GetComponent<Button>();
            cellButton.onClick.RemoveAllListeners();
            cellButton.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void Apply(
            RodShopRowViewModel row,
            string equippedRodId,
            bool selected,
            Sprite slotBackground,
            Sprite itemIcon,
            string attrLine,
            bool playSelectPulse)
        {
            _row = row;
            if (row == null) return;

            var owned = row.Owned;
            var equipped = owned && string.Equals(equippedRodId, row.Id, StringComparison.Ordinal);

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
                lockRoot.SetActive(!owned);

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

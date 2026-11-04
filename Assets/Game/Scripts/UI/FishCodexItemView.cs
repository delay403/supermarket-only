using System;
using DG.Tweening;
using Game.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 鱼图鉴左侧格子：品质底图、锁、选中脉冲（与 <see cref="WingShopItemView"/> 同源）。
    /// </summary>
    public sealed class FishCodexItemView : MonoBehaviour
    {
        [SerializeField] private Button cellButton;
        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text probabilityText;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private RectTransform selectionFrame;

        FishCodexEntry _entry;
        Tweener _pulseTween;

        void Reset()
        {
            cellButton = GetComponentInChildren<Button>();
        }

        void OnDestroy()
        {
            _pulseTween?.Kill();
        }

        public void SetupClick(Action<FishCodexItemView> onClick)
        {
            if (cellButton == null) cellButton = GetComponent<Button>();
            cellButton.onClick.RemoveAllListeners();
            cellButton.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void Apply(
            FishCodexEntry entry,
            bool selected,
            Sprite slotBackground,
            Sprite itemIcon,
            float totalPoolWeight,
            bool playSelectPulse)
        {
            _entry = entry;
            var discovered = entry.isDiscovered;

            if (frameImage != null)
            {
                if (slotBackground != null)
                {
                    frameImage.sprite = slotBackground;
                    frameImage.color = Color.white;
                }
                else
                {
                    frameImage.sprite = null;
                    frameImage.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
                }
            }

            if (lockedOverlay != null)
                lockedOverlay.SetActive(!discovered);

            if (iconImage != null)
            {
                if (discovered && itemIcon != null)
                {
                    iconImage.gameObject.SetActive(true);
                    iconImage.sprite = itemIcon;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = new Color(1f, 1f, 1f, 0.15f);
                    iconImage.gameObject.SetActive(discovered);
                }

                iconImage.preserveAspect = true;
            }

            if (nameText != null)
                nameText.text = discovered ? entry.displayName : "???";

            if (probabilityText != null)
            {
                if (discovered && entry.zonePoolWeight > 0 && totalPoolWeight > 0)
                    probabilityText.text = $"{100f * entry.zonePoolWeight / totalPoolWeight:0.#}%";
                else
                    probabilityText.text = "";
            }

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

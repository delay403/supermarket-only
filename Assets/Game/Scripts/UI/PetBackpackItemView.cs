using System;
using DG.Tweening;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class PetBackpackItemView : MonoBehaviour
    {
        [SerializeField] private Button cellButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text countLabel;
        [SerializeField] private Text attrText;
        [SerializeField] private GameObject equippedBannerRoot;
        [SerializeField] private Text equippedBannerText;
        [SerializeField] private RectTransform selectionFrame;

        private PetRowViewModel _row;
        private Tweener _pulseTween;

        public PetRowViewModel Row => _row;

        public void ConfigureRuntime(Button btn, Image bg, Image icon, Text count, Text attr, GameObject eqBanner, Text eqTxt, RectTransform sel)
        {
            cellButton = btn;
            backgroundImage = bg;
            iconImage = icon;
            countLabel = count;
            attrText = attr;
            equippedBannerRoot = eqBanner;
            equippedBannerText = eqTxt;
            selectionFrame = sel;
        }

        private void OnDestroy()
        {
            _pulseTween?.Kill();
        }

        public void SetupClick(Action<PetBackpackItemView> onClick)
        {
            if (cellButton == null) cellButton = GetComponent<Button>();
            cellButton.onClick.RemoveAllListeners();
            cellButton.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void Apply(
            PetRowViewModel row,
            bool selected,
            bool anyEquipped,
            Sprite slotBg,
            Sprite itemIcon,
            string attrLine,
            bool playSelectPulse)
        {
            _row = row;
            if (row == null) return;

            if (backgroundImage != null)
            {
                if (slotBg != null)
                {
                    backgroundImage.sprite = slotBg;
                    backgroundImage.color = Color.white;
                }
                else
                {
                    backgroundImage.sprite = null;
                    backgroundImage.color = new Color(0.14f, 0.22f, 0.36f, 0.95f);
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
                    iconImage.color = new Color(1f, 1f, 1f, 0.12f);
                }
                iconImage.preserveAspect = true;
            }

            if (countLabel != null)
                countLabel.gameObject.SetActive(false);

            if (attrText != null)
                attrText.text = attrLine ?? "";

            if (equippedBannerRoot != null)
                equippedBannerRoot.SetActive(anyEquipped);
            if (equippedBannerText != null)
                equippedBannerText.text = "携带中";

            if (selectionFrame != null)
            {
                selectionFrame.gameObject.SetActive(selected);
                if (selected && playSelectPulse)
                    PlaySelectionPulse();
            }
        }

        void PlaySelectionPulse()
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

using System;
using DG.Tweening;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>地图卡片：图标、名称、锁与奖牌花费、选中框、当前地图角标（无星级）。</summary>
    public sealed class MapItemView : MonoBehaviour
    {
        [SerializeField] private Button cellButton;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Text nameText;
        [SerializeField] private GameObject lockRoot;
        [SerializeField] private GameObject prizeRoot;
        [SerializeField] private Image prizeCurrencyIcon;
        [SerializeField] private Text prizeAmountText;
        [SerializeField] private GameObject currentMapBannerRoot;
        [SerializeField] private Text currentMapBannerText;
        [SerializeField] private RectTransform selectionFrame;
        [SerializeField] private GameObject selectedCheckRoot;

        private MapRowViewModel _row;
        private Tweener _pulseTween;

        public MapRowViewModel Row => _row;

        private void Reset()
        {
            cellButton = GetComponentInChildren<Button>();
        }

        private void OnDestroy()
        {
            _pulseTween?.Kill();
        }

        public void SetupClick(Action<MapItemView> onClick)
        {
            if (cellButton == null) cellButton = GetComponent<Button>();
            cellButton.onClick.RemoveAllListeners();
            cellButton.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void Apply(
            MapRowViewModel row,
            bool selected,
            bool isCurrentMap,
            Sprite medalIconSprite,
            bool playSelectPulse)
        {
            _row = row;
            if (row == null) return;

            bool unlocked = row.Unlocked;
            bool locked = !unlocked;

            if (nameText != null)
                nameText.text = row.DisplayName ?? row.MapId ?? "";

            if (portraitImage != null)
            {
                if (row.Icon != null)
                {
                    portraitImage.sprite = row.Icon;
                    portraitImage.color = Color.white;
                }
                else
                {
                    portraitImage.sprite = null;
                    portraitImage.color = new Color(1f, 1f, 1f, 0.12f);
                }

                portraitImage.preserveAspect = true;
            }

            if (lockRoot != null)
                lockRoot.SetActive(locked);

            if (prizeRoot != null)
            {
                prizeRoot.SetActive(locked);
                if (locked)
                {
                    var cost = Mathf.Max(0, row.MedalsRequiredToUnlock);
                    if (prizeCurrencyIcon != null)
                    {
                        prizeCurrencyIcon.sprite = medalIconSprite;
                        prizeCurrencyIcon.enabled = medalIconSprite != null;
                    }

                    if (prizeAmountText != null)
                        prizeAmountText.text = cost <= 0 ? "免费" : $"x{cost}";
                }
            }

            if (currentMapBannerRoot != null)
                currentMapBannerRoot.SetActive(isCurrentMap && unlocked);

            if (currentMapBannerText != null)
                currentMapBannerText.text = "当前地图";

            if (selectionFrame != null)
            {
                selectionFrame.gameObject.SetActive(selected);
                if (selected && playSelectPulse)
                    PlaySelectionPulse();
            }

            if (selectedCheckRoot != null)
                selectedCheckRoot.SetActive(selected);
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

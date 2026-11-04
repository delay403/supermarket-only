using System;
using Game.SupermarketOrganize.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>主页难度详情弹窗。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeHomeDetailPopup : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject maskRoot;
        [SerializeField] private RectTransform popupRect;
        [SerializeField] private Image previewImage;
        [SerializeField] private Text shelfCountText;
        [SerializeField] private Text itemCountText;
        [SerializeField] private Text staminaCostText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Text startButtonLabel;

        SupermarketOrganizeDifficultyEntryDef _entry;
        int _shelfCount;
        int _itemCount;
        Action _onClose;
        Action<SupermarketOrganizeDifficultyEntryDef> _onStart;

        public Button StartButton => startButton;
        public bool IsVisible => root != null && root.activeSelf;

        void Awake()
        {
            startButton?.onClick.AddListener(OnStartClicked);
            closeButton?.onClick.AddListener(OnCloseClicked);
            ResolveOverlayRoots();
        }

        void ResolveOverlayRoots()
        {
            if (root == null)
                return;

            if (maskRoot == null)
            {
                var maskTransform = root.transform.Find("Mask");
                if (maskTransform != null)
                    maskRoot = maskTransform.gameObject;
            }

            if (popupRect == null)
            {
                var popupTransform = root.transform.Find("Popup");
                if (popupTransform != null)
                    popupRect = popupTransform as RectTransform;
            }
        }

        public void Bind(
            Action<SupermarketOrganizeDifficultyEntryDef> onStart,
            Action onClose = null)
        {
            _onStart = onStart;
            _onClose = onClose;
        }

        public void Show(
            SupermarketOrganizeDifficultyEntryDef entry,
            RectTransform anchorButton,
            Vector2 popupOffset,
            int shelfCount,
            int itemCount)
        {
            _entry = entry;
            _shelfCount = shelfCount;
            _itemCount = itemCount;
            ResolveOverlayRoots();
            if (root != null)
                root.SetActive(true);
            if (maskRoot != null)
                maskRoot.SetActive(true);
            if (popupRect != null)
                popupRect.gameObject.SetActive(true);

            if (popupRect != null && anchorButton != null)
            {
                var parent = popupRect.parent as RectTransform;
                if (parent != null)
                {
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                        parent,
                        anchorButton);
                    popupRect.anchoredPosition = (Vector2)bounds.center + popupOffset;
                }
            }

            Refresh();
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }

        /// <summary>仅隐藏遮罩和弹窗内容，保留 Root 激活以便播放进场角色动画。</summary>
        public void HideOverlayKeepRoot()
        {
            ResolveOverlayRoots();
            if (maskRoot != null)
                maskRoot.SetActive(false);
            if (popupRect != null)
                popupRect.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (_entry == null)
                return;

            if (previewImage != null)
            {
                if (_entry.previewImage != null)
                {
                    previewImage.sprite = _entry.previewImage;
                    previewImage.color = Color.white;
                    previewImage.gameObject.SetActive(true);
                }
                else
                {
                    previewImage.gameObject.SetActive(false);
                }

                previewImage.preserveAspect = true;
            }

            if (shelfCountText != null)
                shelfCountText.text = FormatCount(_entry.shelfCountFormat, _shelfCount);

            if (itemCountText != null)
                itemCountText.text = FormatCount(_entry.itemCountFormat, _itemCount);

            if (staminaCostText != null)
            {
                var staminaEnabled = SupermarketOrganizeStaminaService.IsEnabled;
                staminaCostText.transform.parent?.gameObject.SetActive(staminaEnabled);
                if (staminaEnabled)
                    staminaCostText.text = $"-{Mathf.Max(1, _entry.staminaCost)}";
            }

            if (startButtonLabel != null)
                startButtonLabel.text = string.IsNullOrWhiteSpace(_entry.startButtonLabel)
                    ? "开始挑战"
                    : _entry.startButtonLabel;

            if (startButton != null)
            {
                var cost = Mathf.Max(1, _entry.staminaCost);
                startButton.interactable = !SupermarketOrganizeStaminaService.IsEnabled
                                           || SupermarketOrganizeStaminaService.Current >= cost;
            }
        }

        static string FormatCount(string format, int count)
        {
            if (string.IsNullOrWhiteSpace(format))
                return count.ToString();

            try
            {
                return string.Format(format, count);
            }
            catch (FormatException)
            {
                return count.ToString();
            }
        }

        void OnStartClicked()
        {
            if (_entry == null)
                return;

            _onStart?.Invoke(_entry);
        }

        void OnCloseClicked() => _onClose?.Invoke();
    }
}

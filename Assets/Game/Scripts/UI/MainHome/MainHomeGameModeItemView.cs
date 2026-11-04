using System;
using Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.MainHome
{
    /// <summary>主页右侧副模式卡片：缩略图 + 标题 + 在线人数 + 锁定遮罩；卡片本体为 Button。</summary>
    public sealed class MainHomeGameModeItemView : MonoBehaviour
    {
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text playerCountText;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private Button clickButton;

        MainHomeGameModeEntry _entry;
        Action<MainHomeGameModeEntry> _onClick;
        Action _onMoreGamesClick;

        void Awake()
        {
            ResolveReferences();
            clickButton?.onClick.AddListener(OnClicked);
        }

        void OnDestroy()
        {
            clickButton?.onClick.RemoveListener(OnClicked);
        }

        void ResolveReferences()
        {
            if (clickButton == null)
                clickButton = GetComponent<Button>();

            if (thumbnailImage == null)
            {
                thumbnailImage = GetComponent<Image>();
                if (thumbnailImage == null && clickButton != null)
                    thumbnailImage = clickButton.targetGraphic as Image;
            }

            if (lockOverlay == null)
            {
                var lockedRoot = transform.Find("LockedRoot");
                if (lockedRoot != null)
                    lockOverlay = lockedRoot.gameObject;
            }

            if (titleText == null)
            {
                var title = transform.Find("TitleText");
                if (title != null)
                    titleText = title.GetComponent<Text>();
            }

            if (playerCountText == null)
            {
                var count = transform.Find("PlayerCountText");
                if (count != null)
                    playerCountText = count.GetComponent<Text>();
            }
        }

        public void Bind(MainHomeGameModeEntry entry, bool unlocked, Action<MainHomeGameModeEntry> onClick)
        {
            _entry = entry;
            _onClick = onClick;
            _onMoreGamesClick = null;

            var sprite = entry?.ResolveSecondarySprite();
            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = sprite;
                thumbnailImage.enabled = sprite != null;
                thumbnailImage.preserveAspect = true;
                thumbnailImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.18f);
            }

            if (titleText != null)
                titleText.text = entry?.displayName ?? string.Empty;

            if (playerCountText != null)
                playerCountText.text = MainHomeGameModeEntry.ResolveDisplayPlayerCount(entry);

            if (lockOverlay != null)
                lockOverlay.SetActive(entry != null && !unlocked);

            if (clickButton != null)
                clickButton.interactable = entry != null;
        }

        /// <summary>绑定「更多游戏」占位卡片（无模式数据、无锁定）。</summary>
        public void BindAsMoreGames(Sprite thumbnail, string title, Action onClick)
        {
            _entry = null;
            _onClick = null;
            _onMoreGamesClick = onClick;

            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = thumbnail;
                thumbnailImage.enabled = thumbnail != null;
                thumbnailImage.preserveAspect = true;
                thumbnailImage.color = thumbnail != null ? Color.white : new Color(1f, 1f, 1f, 0.18f);
            }

            if (titleText != null)
                titleText.text = title ?? "更多游戏";

            if (playerCountText != null)
                playerCountText.text = string.Empty;

            if (lockOverlay != null)
                lockOverlay.SetActive(false);

            if (clickButton != null)
                clickButton.interactable = onClick != null;
        }

        void OnClicked()
        {
            if (_onMoreGamesClick != null)
            {
                AudioGameplayFeedback.PlayUiButton();
                _onMoreGamesClick.Invoke();
                return;
            }

            if (_entry == null)
                return;

            AudioGameplayFeedback.PlayUiButton();
            _onClick?.Invoke(_entry);
        }
    }
}


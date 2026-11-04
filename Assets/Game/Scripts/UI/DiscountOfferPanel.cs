using System.Collections.Generic;
using Game.Reward;
using Game.Audio;
using Game.Economy;
using Game.Economy.Config;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 限时优惠商品弹窗；须在场景中注册为 <see cref="UiPanelId.DiscountOffer"/>。
    /// </summary>
    public sealed class DiscountOfferPanel : MonoBehaviour, IUiPanelController
    {
        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Button closeButton;
        public Button purchaseButton;
        public Text titleText;
        public Text descriptionText;
        public Text priceText;
        [Tooltip("免费解锁时在价格旁显示；留空则尝试从 purchaseButton 子节点查找 Icon。")]
        public Image purchaseFreeIcon;
        public Text rewardText;
        public RectTransform rewardItemListRoot;
        public DiscountOfferRewardItemView rewardItemPrefab;

        [Header("Animation")]
        public float slideDuration = 0.38f;
        public float settleDuration = 0.28f;
        public float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        int _offerId;
        DiscountOfferDef _def;
        readonly List<DiscountOfferRewardItemView> _rewardItems = new List<DiscountOfferRewardItemView>();

        public UiPanelId PanelId => UiPanelId.DiscountOffer;

        void Awake()
        {
            maskCloseButton?.onClick.AddListener(OnDismissClicked);
            closeButton?.onClick.AddListener(OnDismissClicked);
            purchaseButton?.onClick.AddListener(OnPurchaseClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable()
        {
            UiCoordinator.Register(this);
            DiscountOfferPopupQueue.NotifyPanelRegistered();
        }

        void OnDisable() => UiCoordinator.Unregister(this);

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null)
        {
            _offerId = payload?.DiscountOffer?.OfferId ?? 0;
            if (_offerId <= 0 || !DiscountOfferService.Database.TryGet(_offerId, out _def))
            {
                Debug.LogWarning($"{nameof(DiscountOfferPanel)}: 无效 offerId={_offerId}");
                DiscountOfferPopupQueue.NotifyOpenFailed(_offerId);
                return;
            }

            if (!DiscountOfferSaveBridge.MatchesScope(_def))
            {
                Debug.LogWarning($"{nameof(DiscountOfferPanel)}: offerId={_offerId} 不属于当前玩法模块");
                DiscountOfferPopupQueue.NotifyOpenFailed(_offerId);
                return;
            }

            DiscountOfferService.MarkAutoPopupShown(_offerId);
            RefreshUi(_def);
            slideHost.PlayOpen();
        }

        public void Close() => Hide();

        void RefreshUi(DiscountOfferDef def)
        {
            if (titleText != null)
                titleText.text = string.IsNullOrWhiteSpace(def.title) ? "限时优惠" : def.title;
            if (descriptionText != null)
                descriptionText.text = def.description ?? string.Empty;

            RefreshPurchaseLabel(def);
            RefreshRewardList(def);
        }

        void RefreshPurchaseLabel(DiscountOfferDef def)
        {
            if (priceText == null)
                return;

            if (DiscountOfferService.IsFreePurchase(def))
            {
                RefreshPurchaseFreeIcon(show: true);
                priceText.text = "免费获得";
                return;
            }

            RefreshPurchaseFreeIcon(show: false);
            priceText.text = $"仅需 {EconomyDisplayFormat.Format(def.gemPrice)} 钻石";
        }

        void RefreshPurchaseFreeIcon(bool show)
        {
            var icon = ResolvePurchaseFreeIcon();
            if (icon == null)
                return;

            icon.gameObject.SetActive(show);
            if (!show)
                return;
        }

        Image ResolvePurchaseFreeIcon()
        {
            if (purchaseFreeIcon != null)
                return purchaseFreeIcon;

            if (purchaseButton == null)
                return null;

            var iconTr = purchaseButton.transform.Find("Icon");
            if (iconTr != null)
                purchaseFreeIcon = iconTr.GetComponent<Image>();

            return purchaseFreeIcon;
        }

        void RefreshRewardList(DiscountOfferDef def)
        {
            ClearRewardItems();
            var rewards = DiscountOfferRewardGranter.ResolveRewards(def);
            if (rewardText != null)
            {
                rewardText.text = rewards.Length == 1
                    ? DiscountOfferRewardPresenter.ResolveLabel(rewards[0])
                    : $"共 {rewards.Length} 项奖励";
            }

            if (rewardItemListRoot == null || rewardItemPrefab == null)
            {
                AppendRewardSummaryLines(rewards);
                return;
            }

            for (var i = 0; i < rewards.Length; i++)
            {
                var item = Instantiate(rewardItemPrefab, rewardItemListRoot);
                item.gameObject.SetActive(true);
                item.Bind(rewards[i]);
                _rewardItems.Add(item);
            }
        }

        void AppendRewardSummaryLines(DiscountOfferRewardEntry[] rewards)
        {
            if (descriptionText == null || rewards == null || rewards.Length == 0)
                return;

            var baseText = descriptionText.text ?? string.Empty;
            var sb = new System.Text.StringBuilder(baseText);
            if (!string.IsNullOrEmpty(baseText))
                sb.AppendLine();

            sb.AppendLine("包含奖励：");
            for (var i = 0; i < rewards.Length; i++)
                sb.AppendLine("· " + DiscountOfferRewardPresenter.ResolveLabel(rewards[i]));

            descriptionText.text = sb.ToString();
        }

        void ClearRewardItems()
        {
            for (var i = 0; i < _rewardItems.Count; i++)
            {
                if (_rewardItems[i] != null)
                    Destroy(_rewardItems[i].gameObject);
            }

            _rewardItems.Clear();
        }

        void OnPurchaseClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_offerId <= 0)
                return;

            if (DiscountOfferService.IsFreePurchase(_def))
            {
                OnFreePurchaseClicked();
                return;
            }

            if (!DiscountOfferService.TryPurchase(_offerId, out var error))
            {
                AudioGameplayFeedback.PlayUiError();
                if (error == "钻石不足")
                    UiRouter.OpenFreeDiamondsOffer();
                else if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning($"[DiscountOffer] {error}");
                return;
            }

            CompletePurchase();
        }

        void OnFreePurchaseClicked()
        {
            if (!DiscountOfferService.TryPurchase(_offerId, out var error))
            {
                AudioGameplayFeedback.PlayUiError();
                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning($"[DiscountOffer] {error}");
                return;
            }

            CompletePurchase();
        }

        void CompletePurchase()
        {
            AudioGameplayFeedback.PlayResourceCollect();
            UiCoordinator.Hud?.RefreshEconomyDisplay();
            Hide();
        }

        void OnDismissClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            Hide();
        }

        void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;

            slideHost.RequestClose(() =>
            {
                _offerId = 0;
                _def = default;
                ClearRewardItems();
                DiscountOfferPopupQueue.NotifyPanelClosed();
            });
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
            _offerId = 0;
            _def = default;
            ClearRewardItems();
        }

        void EnsureSlideHost()
        {
            UiSlidingPanelHost.EnsureOn(
                this,
                ref slideHost,
                root,
                mainPanel,
                UiSlidingPanelHost.SlideDirection.FromBottom,
                slideDuration,
                settleDuration,
                overshootDownPx);
        }
    }
}

using System.Collections.Generic;
using Game.Reward;
using Game.Audio;
using Game.Economy;
using Game.Economy.Config;
using Game.Save;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 金币中心：同一页面同时展示领取金币与钻石兑换金币（场景内手动引用 <see cref="exchangeTierItems"/>）。
    /// </summary>
    public sealed class CoinsHubPanel : MonoBehaviour, IUiPanelController
    {
        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Button closeButton;
        public Text coinsPreviewText;
        public Button claimButton;
        [Tooltip("按显示顺序引用场景内手动创建的兑换格（与 DiamondCoinsExchangeDatabase.tiers 一一对应）。")]
        public DiamondCoinsExchangeTierItemView[] exchangeTierItems;

        [Header("Animation")]
        public float slideDuration = 0.38f;
        public float settleDuration = 0.28f;
        public float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        public UiPanelId PanelId => UiPanelId.CoinsHub;

        void Awake()
        {
            maskCloseButton?.onClick.AddListener(OnDismissClicked);
            closeButton?.onClick.AddListener(OnDismissClicked);
            claimButton?.onClick.AddListener(OnClaimClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null)
        {
            RefreshAll();
            slideHost.PlayOpen();
        }

        public void Close() => Hide();

        void RefreshAll()
        {
            RefreshRewardSection();
            RefreshExchangeSection();
        }

        void RefreshRewardSection()
        {
            var coins = FreeCoinsRewardService.ResolveFreeCoins();
            if (coinsPreviewText != null)
                coinsPreviewText.text = $"{coins}";
        }

        void RefreshExchangeSection()
        {
            if (exchangeTierItems == null || exchangeTierItems.Length == 0)
                return;

            var db = DiamondCoinsExchangeService.Database;
            if (db?.tiers == null)
                return;

            var validTiers = new List<DiamondCoinsExchangeTierDef>();
            for (var i = 0; i < db.tiers.Length; i++)
            {
                var tier = db.tiers[i];
                if (tier.tierId <= 0 || tier.gemCost <= 0)
                    continue;

                validTiers.Add(tier);
            }

            for (var i = 0; i < exchangeTierItems.Length; i++)
            {
                var view = exchangeTierItems[i];
                if (view == null)
                    continue;

                if (i < validTiers.Count)
                {
                    view.gameObject.SetActive(true);
                    view.Bind(this, validTiers[i]);
                }
                else
                {
                    view.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>兑换格点击：钻石不足时打开领取钻石页。</summary>
        public void RequestExchangeTier(int tierId)
        {
            AudioGameplayFeedback.PlayUiButton();
            if (DiamondCoinsExchangeService.TryExchangeTier(tierId, out _, out var insufficientGems))
            {
                AudioGameplayFeedback.PlayResourceCollect();
                UiCoordinator.Hud?.RefreshEconomyDisplay();
                RefreshExchangeSection();
                return;
            }

            AudioGameplayFeedback.PlayUiError();
            if (insufficientGems)
                UiRouter.OpenFreeDiamondsOffer();
        }

        void OnClaimClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            FreeRewardGate.Grant(() =>
            {
                var d = SaveService.Data;
                if (d == null)
                    return;

                d.coins += FreeCoinsRewardService.ResolveFreeCoins();
                SaveService.Save();
                AudioGameplayFeedback.PlayResourceCollect();
                UiCoordinator.Hud?.RefreshEconomyDisplay();
                RefreshRewardSection();
            });
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
            slideHost.RequestClose(null);
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
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

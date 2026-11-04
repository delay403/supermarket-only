using Game.Reward;
using Game.Audio;
using Game.SupermarketOrganize.Config;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>超市收纳体力补充面板：领取体力。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeStaminaPanel : MonoBehaviour, IUiPanelController
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform mainPanel;
        [SerializeField] private Button maskCloseButton;
        [SerializeField] private Button returnHomeButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text rewardAmountText;
        [SerializeField] private Text claimButtonLabel;

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.38f;
        [SerializeField] private float settleDuration = 0.28f;
        [SerializeField] private float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        SupermarketOrganizeHomeConfig _homeConfig;

        public UiPanelId PanelId => UiPanelId.SupermarketOrganizeStamina;

        void Awake()
        {
            maskCloseButton?.onClick.AddListener(OnReturnHomeClicked);
            returnHomeButton?.onClick.AddListener(OnReturnHomeClicked);
            claimButton?.onClick.AddListener(OnClaimClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable()
        {
            UiCoordinator.Register(this);
            SupermarketOrganizeStaminaService.Changed += RefreshAll;
        }

        void OnDisable()
        {
            SupermarketOrganizeStaminaService.Changed -= RefreshAll;
            UiCoordinator.Unregister(this);
        }

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null)
        {
            _homeConfig = SupermarketOrganizeHomeConfig.ResolveRuntime();
            RefreshAll();
            Show();
        }

        public void Close() => Hide();

        public void RefreshAll()
        {
            _homeConfig ??= SupermarketOrganizeHomeConfig.ResolveRuntime();
            SupermarketOrganizeStaminaService.RefreshRegen();

            if (titleText != null)
                titleText.text = "补充体力";

            if (descriptionText != null)
                descriptionText.text = "可立即获得";

            var reward = SupermarketOrganizeStaminaService.FreeStaminaReward;
            if (rewardAmountText != null)
                rewardAmountText.text = $"x{reward}";

            if (claimButtonLabel != null)
                claimButtonLabel.text = "继续游戏";

            var full = SupermarketOrganizeStaminaService.IsFull;
            if (claimButton != null)
                claimButton.interactable = !full;
        }

        void OnClaimClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (SupermarketOrganizeStaminaService.IsFull)
            {
                AudioGameplayFeedback.PlayUiError();
                RefreshAll();
                return;
            }

            FreeRewardGate.Grant(() =>
            {
                if (SupermarketOrganizeStaminaService.TryGrantStamina())
                {
                    AudioGameplayFeedback.PlayResourceCollect();
                    Hide();
                }
                else
                {
                    AudioGameplayFeedback.PlayUiError();
                }

                RefreshAll();
            });
        }

        void OnReturnHomeClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            Hide();
        }

        void Show()
        {
            EnsureSlideHost();
            slideHost.PlayOpen();
        }

        void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;

            slideHost.RequestClose();
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

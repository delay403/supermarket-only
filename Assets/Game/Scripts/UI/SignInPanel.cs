using Game.Audio;
using Game.DailySignIn;
using Game.Save;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// UGUI 七日签到：绑定 7 个 <see cref="SignInSlotView"/>；滑动壳由 <see cref="UiSlidingPanelHost"/> 统一管理。
    /// </summary>
    public sealed class SignInPanel : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.SignIn;

        [SerializeField] private UiSlidingPanelHost slideHost;
        public GameObject root;
        public RectTransform mainPanel;

        [Tooltip("长度 7，按 Day 1～7 绑定；可留空仅用下方 Legacy UI。")]
        public SignInSlotView[] daySlots = new SignInSlotView[7];

        [Header("Legacy fallback")]
        public Text infoText;
        public Button claimButton;
        public Button closeButton;

        [Header("Animation")]
        public bool playSlideAnimation = true;

        void Awake()
        {
            EnsureSlideHost();
            if (claimButton != null)
                claimButton.onClick.AddListener(OnClaimClicked);
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (daySlots != null)
            {
                for (var i = 0; i < daySlots.Length; i++)
                    daySlots[i]?.BindClaim(OnClaimClicked);
            }

            slideHost.HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        public void Open(UiOpenPayload payload = null) => Show();

        public void Close() => Hide();

        public void Show()
        {
            EnsureSlideHost();
            Refresh();
            if (playSlideAnimation)
                slideHost.PlayOpen();
            else
            {
                if (!slideHost.IsShown)
                    slideHost.PlayOpen();
                slideHost.KillTweens();
            }
        }

        public void Hide()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;

            if (!playSlideAnimation)
            {
                slideHost.HideImmediate();
                return;
            }

            slideHost.RequestClose();
        }

        void EnsureSlideHost()
        {
            UiSlidingPanelHost.EnsureOn(
                this,
                ref slideHost,
                root,
                mainPanel,
                UiSlidingPanelHost.SlideDirection.FromRight);
        }

        void OnClaimClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (SignInService.TryClaimToday(out _))
            {
                AudioGameplayFeedback.PlayResourceCollect();
                SaveService.Save();
                UiCoordinator.Hud?.RefreshEconomyDisplay();
                UiCoordinator.RefreshEquipmentVisuals();
                Game.Economy.EconomyDisplayEvents.Raise();
                Refresh();
            }
            else
                AudioGameplayFeedback.PlayUiError();
        }

        public void Refresh()
        {
            var vm = SignInScreenViewModel.Build();

            var useSlots = daySlots != null && daySlots.Length > 0;
            for (var i = 0; useSlots && i < daySlots.Length; i++)
            {
                var slot = daySlots[i];
                if (slot == null) continue;
                if (i < vm.Slots.Count)
                    slot.Apply(vm.Slots[i]);
            }

            if (infoText != null)
                infoText.text = vm.ToInfoText();

            if (claimButton != null)
                claimButton.interactable = vm.CanClaimToday;
        }
    }
}

using Game.Reward;
using Game.Audio;
using Game.Features.OnlineReward;
using Game.Save;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 在线奖励弹窗：状态文案（富文本时长）+ 奖励图标/数量 + 领取 / 双倍领取。
    /// </summary>
    public sealed class OnlineRewardPanel : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.OnlineReward;

        [SerializeField] private UiSlidingPanelHost slideHost;
        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Button closeButton;

        [Header("状态与奖励展示")]
        [Tooltip("如：已在线 <color=#7CFF3A>5分钟</color> 可领取奖励")]
        public Text statusText;

        public Image rewardIconImage;
        public Text rewardAmountText;

        [Header("领取")]
        public Button claimButton;
        public Button doubleClaimButton;

        [Tooltip("时长富文本颜色（含 #）。")]
        [SerializeField] private string timeRichColorHex = "#7CFF3A";

        [Header("Animation")]
        public bool playSlideAnimation = true;

        float _nextUiRefresh;
        bool _claimBusy;
        Coroutine _clearBusyCo;

        void Awake()
        {
            EnsureSlideHost();
            if (maskCloseButton != null)
                maskCloseButton.onClick.AddListener(Hide);
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
            if (claimButton != null)
                claimButton.onClick.AddListener(OnClaimClicked);
            if (doubleClaimButton != null)
                doubleClaimButton.onClick.AddListener(OnDoubleClaimClicked);

            if (statusText != null)
                statusText.supportRichText = true;

            slideHost.HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        void Update()
        {
            if (slideHost == null || !slideHost.IsShown)
                return;

            if (Time.unscaledTime < _nextUiRefresh)
                return;

            _nextUiRefresh = Time.unscaledTime + 0.5f;
            Refresh();
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

        public void Open(UiOpenPayload payload = null) => Show();

        public void Close() => Hide();

        public void Show()
        {
            EnsureSlideHost();
            _claimBusy = false;
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
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;

            AudioGameplayFeedback.PlayUiButton();
            if (!playSlideAnimation)
            {
                slideHost.HideImmediate();
                return;
            }

            slideHost.RequestClose();
        }

        void OnClaimClicked()
        {
            if (_claimBusy)
                return;

            AudioGameplayFeedback.PlayUiButton();
            TryClaimInternal(multiplier: 1);
        }

        void OnDoubleClaimClicked()
        {
            if (_claimBusy)
                return;

            AudioGameplayFeedback.PlayUiButton();
            if (!OnlineRewardService.HasClaimableReward())
            {
                AudioGameplayFeedback.PlayUiError();
                Refresh();
                return;
            }

            _claimBusy = true;
            SetClaimButtonsInteractable(false);

            if (_clearBusyCo != null)
                StopCoroutine(_clearBusyCo);
            _clearBusyCo = StartCoroutine(CoClearBusyIfNoCallback());

            FreeRewardGate.Grant(() =>
            {
                if (this == null)
                    return;

                EnsureSlideHost();
                if (slideHost != null && slideHost.IsShown && root != null && root.activeInHierarchy)
                    TryClaimInternal(multiplier: 2);

                _claimBusy = false;
                if (_clearBusyCo != null)
                {
                    StopCoroutine(_clearBusyCo);
                    _clearBusyCo = null;
                }

                Refresh();
            });
        }

        System.Collections.IEnumerator CoClearBusyIfNoCallback()
        {
            yield return null;
            if (!_claimBusy)
            {
                _clearBusyCo = null;
                yield break;
            }

            var wait = 0f;
            while (_claimBusy && wait < 120f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_claimBusy)
            {
                _claimBusy = false;
                Refresh();
            }

            _clearBusyCo = null;
        }

        void TryClaimInternal(int multiplier)
        {
            if (OnlineRewardService.TryClaimNext(out _, out _, multiplier))
            {
                AudioGameplayFeedback.PlayResourceCollect();
                SaveService.Save();
                UiCoordinator.Hud?.RefreshEconomyDisplay();
                UiCoordinator.RefreshEquipmentVisuals();
                Game.Economy.EconomyDisplayEvents.Raise();
                Refresh();
            }
            else
            {
                AudioGameplayFeedback.PlayUiError();
                Refresh();
            }
        }

        public void Refresh()
        {
            var vm = OnlineRewardScreenViewModel.Build(timeRichColorHex);

            if (statusText != null)
                statusText.text = vm.StatusRichText;

            if (rewardIconImage != null)
            {
                rewardIconImage.sprite = vm.CurrentRewardIcon;
                rewardIconImage.enabled = vm.CurrentRewardIcon != null && !vm.AllClaimed;
                rewardIconImage.gameObject.SetActive(!vm.AllClaimed && vm.CurrentTierIndex >= 0);
            }

            if (rewardAmountText != null)
            {
                rewardAmountText.gameObject.SetActive(!vm.AllClaimed && vm.CurrentTierIndex >= 0);
                rewardAmountText.text = vm.CurrentTierIndex >= 0 ? $"x{vm.CurrentRewardAmount}" : string.Empty;
            }

            SetClaimButtonsInteractable(!_claimBusy && vm.HasClaimable);
        }

        void SetClaimButtonsInteractable(bool interactable)
        {
            if (claimButton != null)
                claimButton.interactable = interactable;
            if (doubleClaimButton != null)
                doubleClaimButton.interactable = interactable;
        }
    }
}

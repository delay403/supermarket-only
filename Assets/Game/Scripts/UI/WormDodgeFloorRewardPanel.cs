using Game.Reward;
using Game.Audio;
using Game.UI.Framework;
using Game.UI.ViewModels;
using Game.WormDodge;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>躲避虫子楼层奖励弹窗。</summary>
    public sealed class WormDodgeFloorRewardPanel : MonoBehaviour, IUiPanelController
    {
        public GameObject root;
        public RectTransform mainPanel;
        public Text titleText;
        public Text bodyText;
        public Text rewardText;
        public Button claimButton;
        public Button doubleClaimButton;

        [Header("Animation")]
        public float slideDuration = 0.38f;
        public float settleDuration = 0.28f;
        public float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        WormDodgeFloorRewardViewModel _vm;
        bool _claimed;

        public UiPanelId PanelId => UiPanelId.WormDodgeFloorReward;

        void Awake()
        {
            claimButton?.onClick.AddListener(OnClaimClicked);
            doubleClaimButton?.onClick.AddListener(OnDoubleClaimClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null)
        {
            _vm = payload?.WormDodgeFloorReward;
            _claimed = false;
            if (_vm == null)
            {
                Debug.LogWarning($"{nameof(WormDodgeFloorRewardPanel)}: 缺少 FloorReward payload。");
                return;
            }

            if (titleText != null)
                titleText.text = "楼层奖励";

            if (bodyText != null)
                bodyText.text = $"您已到达 {_vm.FloorIndex} 层，请领取专属奖励！";

            if (rewardText != null)
                rewardText.text = $"x{_vm.CoinReward}";

            AudioGameplayFeedback.PlayUiFloatPopup();
            Show();
        }

        public void Close() => Hide();

        void OnClaimClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_claimed || _vm == null)
            {
                Hide();
                return;
            }

            _claimed = true;
            WormDodgeDirector.Instance?.GrantFloorPopupReward(_vm.CoinReward, doubled: false);
            AudioGameplayFeedback.PlayResourceCollect();
            Hide();
        }

        void OnDoubleClaimClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_claimed || _vm == null)
            {
                Hide();
                return;
            }

            var director = WormDodgeDirector.Instance;
            FreeRewardGate.Grant(() =>
            {
                EnsureSlideHost();
                if (!slideHost.IsShown || _vm == null || _claimed)
                    return;

                if (director == null)
                {
                    AudioGameplayFeedback.PlayUiError();
                    return;
                }

                _claimed = true;
                director.GrantFloorPopupReward(_vm.CoinReward, doubled: true);
                AudioGameplayFeedback.PlayResourceCollect();
                Hide();
            });
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
            slideHost.RequestClose(() =>
            {
                _vm = null;
                _claimed = false;
            });
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
            _vm = null;
            _claimed = false;
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

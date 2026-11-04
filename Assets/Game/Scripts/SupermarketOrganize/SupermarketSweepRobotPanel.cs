using Game.Audio;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>扫地机器人独立面板：继续游戏 / 续时 + 剩余时间。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketSweepRobotPanel : MonoBehaviour, IUiPanelController
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform mainPanel;
        [SerializeField] private Button maskCloseButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text remainingTimeText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private Text claimButtonLabel;

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.38f;
        [SerializeField] private float settleDuration = 0.28f;
        [SerializeField] private float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        SupermarketSweepRobotController _sweepRobot;
        float _nextUiTick;

        public UiPanelId PanelId => UiPanelId.SupermarketOrganizeSweepRobot;

        void Awake()
        {
            maskCloseButton?.onClick.AddListener(OnContinueClicked);
            closeButton?.onClick.AddListener(OnContinueClicked);
            continueButton?.onClick.AddListener(OnContinueClicked);
            claimButton?.onClick.AddListener(OnClaimClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        void OnDestroy() => slideHost?.KillTweens();

        void Update()
        {
            if (!IsOpen() || Time.unscaledTime < _nextUiTick)
                return;

            _nextUiTick = Time.unscaledTime + 0.25f;
            RefreshRemainingTime();
        }

        public void Open(UiOpenPayload payload = null)
        {
            BindSweepRobot();
            RefreshAll();
            Show();
        }

        public void Close() => Hide();

        void BindSweepRobot()
        {
            if (_sweepRobot != null)
                return;

            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            _sweepRobot = director?.SweepRobot;
        }

        public void RefreshAll()
        {
            var active = SupermarketOrganizeProgressionService.IsSweepRobotActive();
            if (titleText != null)
                titleText.text = "[清理机器人]";

            if (descriptionText != null)
            {
                descriptionText.text = active
                    ? "扫地机器人正在工作，每 15 秒将一件物品放入货架。"
                    : "该道具存在 5 分钟，每 15 秒将一件物品放入货架！";
            }

            if (claimButtonLabel != null)
                claimButtonLabel.text = active ? "续时" : "立即领取";

            RefreshRemainingTime();
        }

        void RefreshRemainingTime()
        {
            if (remainingTimeText == null)
                return;

            var active = SupermarketOrganizeProgressionService.IsSweepRobotActive();
            if (!active)
            {
                remainingTimeText.text = "剩余时间：未激活";
                return;
            }

            var remaining = SupermarketOrganizeProgressionService.GetSweepRobotRemainingSeconds();
            remainingTimeText.text = $"剩余时间：{FormatDuration(remaining)}";
        }

        static string FormatDuration(float seconds)
        {
            if (seconds <= 0f)
                return "0:00";

            var total = Mathf.CeilToInt(seconds);
            var minutes = total / 60;
            var secs = total % 60;
            return $"{minutes}:{secs:00}";
        }

        void OnClaimClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            BindSweepRobot();
            _sweepRobot?.TryUnlockFree();
            RefreshAll();
        }

        void OnContinueClicked()
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

        bool IsOpen() => slideHost != null && slideHost.IsShown;

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

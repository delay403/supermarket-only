using System;
using Game.Reward;
using Game.Audio;
using Game.UI.Framework;
using Game.UI.ViewModels;
using Game.WormDodge;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>躲避虫子胜负结算（同一面板，按 ViewModel 切换文案与按钮）。</summary>
    public sealed class WormDodgeResultPanel : MonoBehaviour, IUiPanelController
    {
        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Text titleText;
        public Text statusText;
        public Text floorText;
        public Text bestFloorText;
        public Text clearTimeText;
        public Text bestClearTimeText;
        public Text coinsText;
        public Button homeButton;
        public Button playAgainButton;
        public Button doubleRewardButton;
        public Button reviveButton;
        public GameObject reviveButtonRoot;

        [Header("Animation")]
        public float slideDuration = 0.38f;
        public float settleDuration = 0.28f;
        public float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        WormDodgeResultViewModel _vm;

        public UiPanelId PanelId => UiPanelId.WormDodgeResult;

        void Awake()
        {
            maskCloseButton?.onClick.AddListener(OnHomeClicked);
            homeButton?.onClick.AddListener(OnHomeClicked);
            playAgainButton?.onClick.AddListener(OnPlayAgainClicked);
            doubleRewardButton?.onClick.AddListener(OnDoubleRewardClicked);
            reviveButton?.onClick.AddListener(OnReviveClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null)
        {
            _vm = payload?.WormDodgeResult;
            if (_vm == null)
            {
                Debug.LogWarning($"{nameof(WormDodgeResultPanel)}: 缺少 WormDodgeResult payload。");
                return;
            }

            BindTexts(_vm);
            BindButtons(_vm);
            AudioGameplayFeedback.PlayPanelOpen();
            Show();
        }

        public void Close() => Hide();

        void BindTexts(WormDodgeResultViewModel vm)
        {
            if (titleText != null)
                titleText.text = vm.IsVictory ? "游戏胜利" : "游戏结束";

            if (statusText != null)
            {
                statusText.text = vm.IsVictory
                    ? "成功到达终点！"
                    : vm.EndReason switch
                    {
                        WormDodgeEndReason.Caught => "被虫子吃掉了！",
                        WormDodgeEndReason.Eliminated => "掉入危险区域！",
                        _ => "本局结束"
                    };
            }

            if (floorText != null)
                floorText.text = $"当前到达层数：{vm.FloorsReached} 层";

            if (bestFloorText != null)
                bestFloorText.text = $"历史最高层数：{vm.BestFloorsRecord} 层";

            if (clearTimeText != null)
                clearTimeText.text = vm.IsVictory
                    ? $"本局通关时间：{FormatTime(vm.RunClearTimeSeconds)}"
                    : string.Empty;

            if (bestClearTimeText != null)
            {
                bestClearTimeText.text = vm.BestClearTimeSeconds > 0f
                    ? $"历史最快通关：{FormatTime(vm.BestClearTimeSeconds)}"
                    : "历史最快通关：暂无记录";
            }

            if (coinsText != null)
                coinsText.text = $"x{vm.CoinsEarned}";
        }

        void BindButtons(WormDodgeResultViewModel vm)
        {
            var showRevive = !vm.IsVictory && vm.CanRevive;
            if (reviveButtonRoot != null)
                reviveButtonRoot.SetActive(showRevive);
            else if (reviveButton != null)
                reviveButton.gameObject.SetActive(showRevive);

            var canDouble = vm.CoinsEarned > 0 && !vm.DoubleRewardClaimed;
            if (doubleRewardButton != null)
                doubleRewardButton.interactable = canDouble;
        }

        void OnHomeClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            Hide();
            if (WormDodgeDirector.Instance == null)
                return;

            if (WormDodgeDirector.Instance.IsDefeatPending)
                WormDodgeDirector.Instance.FinalizeRunAndReturnHome();
            else
                WormDodgeDirector.Instance.ShowHome();
        }

        void OnPlayAgainClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            Hide();
            var director = WormDodgeDirector.Instance;
            if (director == null)
                return;

            if (!director.TryPlayAgainFromResult())
            {
                AudioGameplayFeedback.PlayUiError();
                director.ShowHome();
            }
        }

        void OnDoubleRewardClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_vm == null || _vm.DoubleRewardClaimed || _vm.CoinsEarned <= 0L)
                return;

            var director = WormDodgeDirector.Instance;
            if (director == null)
                return;

            FreeRewardGate.Grant(() =>
            {
                EnsureSlideHost();
                if (!slideHost.IsShown || _vm == null)
                    return;

                if (!director.TryApplyDoubleRunReward())
                {
                    AudioGameplayFeedback.PlayUiError();
                    return;
                }

                AudioGameplayFeedback.PlayResourceCollect();
                _vm.DoubleRewardClaimed = true;
                if (doubleRewardButton != null)
                    doubleRewardButton.interactable = false;
            });
        }

        void OnReviveClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_vm == null || !_vm.CanRevive)
                return;

            var director = WormDodgeDirector.Instance;
            if (director == null)
                return;

            FreeRewardGate.Grant(() =>
            {
                EnsureSlideHost();
                if (!slideHost.IsShown || _vm == null)
                    return;

                if (!director.TryRevivePlayer())
                {
                    AudioGameplayFeedback.PlayUiError();
                    return;
                }

                Hide();
            });
        }

        static string FormatTime(float seconds)
        {
            if (seconds <= 0f)
                return "00:00";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1d
                ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
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
            slideHost.RequestClose(() => _vm = null);
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
            _vm = null;
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

using Game.Reward;
using Game.Audio;
using Game.Economy;
using Game.Player;
using Game.Save;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// UGUI 滑行结算：Mask 仅遮挡、不可用于关闭；主面板自顶向下弹入；继续 / Home / 双倍金币后回出生地。
    /// </summary>
    public sealed class ResultPanel : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.Result;

        [Header("Bindings")]
        public GameObject root;
        public RectTransform mainPanel;
        public Image maskImage;
        public Button closeHomeButton;
        public Button continueButton;
        public Button doubleRewardButton;

        [Header("Texts")]
        public Text titleText;
        public Text glideDistanceText;
        public Text recordDistanceText;
        public Text towerHeightText;
        public Text coinsText;
        public Text medalLineText;
        [Tooltip("登顶奖章整行（图标+文字）的根物体。不指定时使用 medalLineText 的父物体。")]
        public GameObject medalRowRoot;
        public Text gemsLineText;
        [Tooltip("本局钻石奖励整行（图标+文字）的根物体。不指定时使用 gemsLineText 的父物体。")]
        public GameObject gemsRowRoot;

        [Header("Animation")]
        public float slideDuration = 0.38f;
        public float settleDuration = 0.28f;
        public float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        SettlementViewModel _model;

        void Awake()
        {
            if (closeHomeButton != null)
                closeHomeButton.onClick.AddListener(OnContinueOrHomeClicked);
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueOrHomeClicked);
            if (doubleRewardButton != null)
                doubleRewardButton.onClick.AddListener(OnDoubleRewardClicked);

            if (maskImage != null)
                maskImage.raycastTarget = true;

            EnsureSlideHost();
            HideImmediate();
        }

        void OnDestroy() => slideHost?.KillTweens();

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        public void Open(UiOpenPayload payload = null)
        {
            _model = payload?.Settlement;
            if (_model == null)
            {
                Debug.LogWarning("ResultPanel: 无 SettlementViewModel。");
                return;
            }

            EnsureSlideHost();
            BindTexts(_model);
            AudioGameplayFeedback.PlayPanelOpen();
            slideHost.PlayOpen();
        }

        public void Close() => Hide();

        public void Show(float height, float glideDistanceFromTower, Rewards rewards)
        {
            var record = SaveService.Data != null
                ? Mathf.Max(SaveService.Data.bestGlideDistanceMeters, glideDistanceFromTower)
                : glideDistanceFromTower;
            Open(new UiOpenPayload
            {
                Settlement = SettlementViewModel.FromRun(height, glideDistanceFromTower, record, rewards, 0),
            });
        }

        void BindTexts(SettlementViewModel vm)
        {
            if (titleText != null)
                titleText.text = vm.Title;
            if (glideDistanceText != null)
                glideDistanceText.text = $"滑翔距离：{vm.GlideDistanceFromTowerMeters:0}米";
            if (recordDistanceText != null)
                recordDistanceText.text = $"（最远距离：{vm.RecordGlideDistanceMeters:0}米）";
            if (towerHeightText != null)
                towerHeightText.text = $"爬塔高度：{vm.HeightMeters:0}米";
            if (coinsText != null)
                coinsText.text = $"x{vm.CoinsReward}";

            ApplyMedalRow(vm);
            ApplyGemsRow(vm);
        }

        /// <summary>
        /// 无奖章时隐藏整行根物体（含图标）；有奖章时显示并刷新数量。
        /// </summary>
        void ApplyMedalRow(SettlementViewModel vm)
        {
            var count = vm.TowerClearMedalCount;
            var row = medalRowRoot;
            if (row == null && medalLineText != null)
            {
                var p = medalLineText.transform.parent;
                row = p != null ? p.gameObject : medalLineText.gameObject;
            }

            if (row != null)
            {
                if (count > 0)
                {
                    row.SetActive(true);
                    if (medalLineText != null)
                        medalLineText.text = $"x{count}";
                }
                else
                    row.SetActive(false);
                return;
            }

            if (medalLineText == null)
                return;
            if (count > 0)
            {
                medalLineText.gameObject.SetActive(true);
                medalLineText.text = $"x{count}";
            }
            else
                medalLineText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 无钻石时隐藏整行；有钻石时显示「钻石 x数量」（与奖章行相同规则）。
        /// </summary>
        void ApplyGemsRow(SettlementViewModel vm)
        {
            var amount = vm.GemsReward;
            var row = gemsRowRoot;
            if (row == null && gemsLineText != null)
            {
                var p = gemsLineText.transform.parent;
                row = p != null ? p.gameObject : gemsLineText.gameObject;
            }

            if (row != null)
            {
                if (amount > 0)
                {
                    row.SetActive(true);
                    if (gemsLineText != null)
                        gemsLineText.text = $"x{amount}";
                }
                else
                    row.SetActive(false);
                return;
            }

            if (gemsLineText == null)
                return;
            if (amount > 0)
            {
                gemsLineText.gameObject.SetActive(true);
                gemsLineText.text = $"x{amount}";
            }
            else
                gemsLineText.gameObject.SetActive(false);
        }

        public void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;
            slideHost.RequestClose(() => _model = null);
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
            _model = null;
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

        void OnContinueOrHomeClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            FinishAndRespawn();
        }

        void OnDoubleRewardClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_model == null || SaveService.Data == null || !_model.HasDoubleReward)
            {
                FinishAndRespawn();
                return;
            }

            var extra = _model.BuildDoubleRewardExtra();
            FreeRewardGate.Grant(() =>
            {
                RewardApplier.Apply(extra);
                SaveService.Save();
                UiCoordinator.Hud?.RefreshEconomyDisplay();
                FinishAndRespawn();
            });
        }

        void FinishAndRespawn()
        {
            Hide();
            RespawnService.RespawnPlayerToActivePoint();
        }
    }
}

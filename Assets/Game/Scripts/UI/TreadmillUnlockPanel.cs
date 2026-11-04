using Game.Reward;
using Game.Audio;
using Game.Economy;
using Game.Features.Treadmill;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 跑步机面板：解锁今日使用权（免费/钻石按配置显示），或确认开始锻炼。
    /// 须在场景中注册为 <see cref="UiPanelId.TreadmillUnlock"/>。
    /// </summary>
    public sealed class TreadmillUnlockPanel : MonoBehaviour, IUiPanelController
    {
        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Button closeButton;
        public Button claimButton;
        public Button gemUnlockButton;
        public Button confirmStartButton;
        public Image treadmillIconImage;
        public Text titleText;
        public Text descriptionText;
        public Text gemButtonLabel;
        public Text confirmButtonLabel;
        public Text unlockProgressText;

        [Header("档位默认图标（Zone 未指定 Panel Icon 时使用）")]
        public Sprite basicTierIcon;
        public Sprite freeDailyTierIcon;
        public Sprite premiumDailyTierIcon;

        [Header("Animation")]
        public float slideDuration = 0.38f;
        public float settleDuration = 0.28f;
        public float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        TreadmillUnlockOpenPayload _payload;

        public UiPanelId PanelId => UiPanelId.TreadmillUnlock;

        void Awake()
        {
            maskCloseButton?.onClick.AddListener(OnDismissClicked);
            closeButton?.onClick.AddListener(OnDismissClicked);
            claimButton?.onClick.AddListener(OnClaimClicked);
            gemUnlockButton?.onClick.AddListener(OnGemUnlockClicked);
            confirmStartButton?.onClick.AddListener(OnConfirmStartClicked);

            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null)
        {
            _payload = payload?.TreadmillUnlock;
            if (_payload == null)
            {
                Debug.LogWarning($"{nameof(TreadmillUnlockPanel)}: 缺少 TreadmillUnlock payload。");
                return;
            }

            RefreshUi();
            Show();
        }

        public void Close() => Hide();

        void RefreshUi()
        {
            var tier = _payload?.Tier ?? TreadmillTier.FreeDaily;
            var isConfirm = _payload?.Mode == TreadmillPanelMode.ConfirmStart;

            if (titleText != null)
            {
                titleText.text = isConfirm
                    ? TreadmillService.GetTierDisplayName(tier)
                    : tier == TreadmillTier.PremiumDaily
                        ? "解锁高级跑步机"
                        : "解锁进阶跑步机";
            }

            if (descriptionText != null)
            {
                var bonus = TreadmillService.GetBonusPerInterval(tier);
                var interval = TreadmillService.GetIntervalSeconds();
                var suffix = isConfirm ? "确认后开始锻炼" : "解锁后今日可用";
                descriptionText.text =
                    $"每 {interval:0.#} 秒永久速度 +{bonus:0.###} m/s\n{suffix}";
            }

            if (confirmButtonLabel != null)
                confirmButtonLabel.text = "确认开始";

            RefreshTreadmillIcon(tier);
            RefreshActionButtons(tier, isConfirm);
            RefreshProgressText(tier, isConfirm);
        }

        void RefreshTreadmillIcon(TreadmillTier tier)
        {
            if (treadmillIconImage == null)
                return;

            var sprite = _payload?.Icon;
            if (sprite == null)
                sprite = ResolveTierFallbackIcon(tier);

            if (sprite != null)
            {
                treadmillIconImage.sprite = sprite;
                treadmillIconImage.enabled = true;
                treadmillIconImage.gameObject.SetActive(true);
            }
            else
            {
                treadmillIconImage.enabled = false;
                treadmillIconImage.gameObject.SetActive(false);
            }
        }

        Sprite ResolveTierFallbackIcon(TreadmillTier tier) => tier switch
        {
            TreadmillTier.FreeDaily => freeDailyTierIcon,
            TreadmillTier.PremiumDaily => premiumDailyTierIcon,
            _ => basicTierIcon,
        };

        void RefreshActionButtons(TreadmillTier tier, bool isConfirm)
        {
            if (confirmStartButton != null)
                confirmStartButton.gameObject.SetActive(isConfirm);

            if (!isConfirm)
            {
                var showClaim = _payload != null
                             && _payload.AllowFreeUnlock
                             && tier != TreadmillTier.Basic;
                var showGem = _payload != null
                              && _payload.AllowGemUnlock
                              && tier == TreadmillTier.PremiumDaily;

                if (claimButton != null)
                    claimButton.gameObject.SetActive(showClaim);
                if (gemUnlockButton != null)
                {
                    gemUnlockButton.gameObject.SetActive(showGem);
                    if (showGem && gemButtonLabel != null)
                    {
                        var cost = TreadmillService.GetTier3GemUnlockCost();
                        gemButtonLabel.text = $"钻石 x{cost}";
                    }
                }
            }
            else
            {
                if (claimButton != null)
                    claimButton.gameObject.SetActive(false);
                if (gemUnlockButton != null)
                    gemUnlockButton.gameObject.SetActive(false);
            }
        }

        void RefreshProgressText(TreadmillTier tier, bool isConfirm)
        {
            if (unlockProgressText == null)
                return;

            if (isConfirm)
            {
                unlockProgressText.text = string.Empty;
                return;
            }

            if (tier == TreadmillTier.PremiumDaily && _payload != null && _payload.AllowFreeUnlock)
            {
                var required = TreadmillService.GetTier3UnlockRequired();
                var progress = TreadmillService.GetTier3Progress();
                unlockProgressText.text = $"解锁进度 {progress}/{required}";
            }
            else if (tier == TreadmillTier.FreeDaily && _payload != null && _payload.AllowFreeUnlock)
            {
                unlockProgressText.text = "免费解锁今日使用权";
            }
            else
            {
                unlockProgressText.text = string.Empty;
            }
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
            slideHost.RequestClose(() => _payload = null);
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
            _payload = null;
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

        void OnDismissClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            Hide();
        }

        void OnConfirmStartClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_payload == null || _payload.Mode != TreadmillPanelMode.ConfirmStart)
                return;

            CompleteStartRequest();
        }

        void OnClaimClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_payload == null || _payload.Mode != TreadmillPanelMode.Unlock)
                return;

            FreeRewardGate.Grant(() =>
            {
                EnsureSlideHost();
                if (!slideHost.IsShown || _payload == null)
                    return;

                if (_payload.Tier == TreadmillTier.FreeDaily)
                {
                    if (!TreadmillService.TryUnlockTier2())
                    {
                        AudioGameplayFeedback.PlayUiError();
                        return;
                    }
                }
                else if (_payload.Tier == TreadmillTier.PremiumDaily)
                {
                    if (!TreadmillService.TryRecordTier3Progress(out var unlockedNow))
                    {
                        AudioGameplayFeedback.PlayUiError();
                        return;
                    }

                    RefreshProgressText(_payload.Tier, isConfirm: false);
                    if (!unlockedNow)
                        return;
                }
                else
                {
                    return;
                }

                CompleteStartRequest();
            });
        }

        void OnGemUnlockClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_payload == null
                || _payload.Mode != TreadmillPanelMode.Unlock
                || _payload.Tier != TreadmillTier.PremiumDaily)
                return;

            if (!TreadmillService.TryUnlockTier3WithGems())
            {
                AudioGameplayFeedback.PlayUiError();
                return;
            }

            EconomyDisplayEvents.Raise();
            CompleteStartRequest();
        }

        void CompleteStartRequest()
        {
            var cb = _payload?.OnStartRequested;
            Hide();
            cb?.Invoke();
        }
    }
}

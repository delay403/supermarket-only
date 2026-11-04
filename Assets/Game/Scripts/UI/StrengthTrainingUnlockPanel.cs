using Game.Reward;
using Game.Audio;
using Game.Economy;
using Game.Features.StrengthTraining;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 力量训练场面板：解锁今日使用权（免费/钻石按配置显示），或确认开始训练。
    /// 须在场景中注册为 <see cref="UiPanelId.StrengthTrainingUnlock"/>。
    /// </summary>
    public sealed class StrengthTrainingUnlockPanel : MonoBehaviour, IUiPanelController
    {
        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Button closeButton;
        public Button claimButton;
        public Button gemUnlockButton;
        public Button confirmStartButton;
        public Image groundIconImage;
        public Image claimButtonIcon;
        public Text titleText;
        public Text descriptionText;
        public Text gemButtonLabel;
        public Text confirmButtonLabel;
        public Text unlockProgressText;

        [Header("默认图标")]
        public Sprite defaultGroundIcon;
        public Sprite defaultClaimIcon;

        [Header("Animation")]
        public float slideDuration = 0.38f;
        public float settleDuration = 0.28f;
        public float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        StrengthTrainingUnlockOpenPayload _payload;

        public UiPanelId PanelId => UiPanelId.StrengthTrainingUnlock;

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
            _payload = payload?.StrengthTrainingUnlock;
            if (_payload == null)
            {
                Debug.LogWarning($"{nameof(StrengthTrainingUnlockPanel)}: 缺少 StrengthTrainingUnlock payload。");
                return;
            }

            RefreshUi();
            Show();
        }

        public void Close() => Hide();

        void RefreshUi()
        {
            var groundId = _payload?.GroundId ?? string.Empty;
            var def = StrengthTrainingService.ResolveGroundDef(groundId);
            var isConfirm = _payload?.Mode == StrengthTrainingPanelMode.ConfirmStart;

            if (titleText != null)
            {
                var displayName = StrengthTrainingService.GetGroundDisplayName(groundId);
                titleText.text = isConfirm ? displayName : $"{displayName}";
            }

            if (descriptionText != null)
            {
                var bonus = StrengthTrainingService.GetBonusPerInterval(groundId);
                var interval = StrengthTrainingService.GetIntervalSeconds();
                descriptionText.text =
                    $"每 {interval:0.#} 秒力量 +{bonus:0.###}";
            }

            if (confirmButtonLabel != null)
                confirmButtonLabel.text = "确认开始";

            RefreshGroundIcon(groundId, def);
            RefreshActionButtons(def, isConfirm);
            RefreshProgressText(groundId, def, isConfirm);
            RefreshClaimButtonIcon(isConfirm);
        }

        void RefreshGroundIcon(string groundId, StrengthTrainingGroundDef def)
        {
            if (groundIconImage == null)
                return;

            var sprite = _payload?.Icon;
            if (sprite == null && def != null)
                sprite = def.panelIcon;
            if (sprite == null)
                sprite = defaultGroundIcon;

            if (sprite != null)
            {
                groundIconImage.sprite = sprite;
                groundIconImage.enabled = true;
                groundIconImage.gameObject.SetActive(true);
            }
            else
            {
                groundIconImage.enabled = false;
                groundIconImage.gameObject.SetActive(false);
            }
        }

        void RefreshClaimButtonIcon(bool isConfirm)
        {
            if (claimButtonIcon == null)
                return;

            var show = !isConfirm && _payload != null && _payload.AllowFreeUnlock;
            claimButtonIcon.gameObject.SetActive(show);
            if (!show)
                return;

            if (defaultClaimIcon != null)
            {
                claimButtonIcon.sprite = defaultClaimIcon;
                claimButtonIcon.enabled = true;
            }
        }

        void RefreshActionButtons(StrengthTrainingGroundDef def, bool isConfirm)
        {
            if (confirmStartButton != null)
                confirmStartButton.gameObject.SetActive(isConfirm);

            if (!isConfirm)
            {
                var policy = def?.unlockPolicy ?? StrengthTrainingUnlockPolicy.AlwaysOpen;
                var showClaim = _payload != null
                             && _payload.AllowFreeUnlock
                             && policy is StrengthTrainingUnlockPolicy.FreeOncePerDay
                                 or StrengthTrainingUnlockPolicy.ProgressPerDay;
                var showGem = _payload != null
                              && _payload.AllowGemUnlock
                              && policy == StrengthTrainingUnlockPolicy.GemOncePerDay;

                if (claimButton != null)
                    claimButton.gameObject.SetActive(showClaim);
                if (gemUnlockButton != null)
                {
                    gemUnlockButton.gameObject.SetActive(showGem);
                    if (showGem && gemButtonLabel != null)
                    {
                        var cost = StrengthTrainingService.GetGroundGemUnlockCost(_payload.GroundId);
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

        void RefreshProgressText(string groundId, StrengthTrainingGroundDef def, bool isConfirm)
        {
            if (unlockProgressText == null)
                return;

            if (isConfirm)
            {
                unlockProgressText.text = string.Empty;
                return;
            }

            if (_payload == null || !_payload.AllowFreeUnlock || def == null)
            {
                unlockProgressText.text = string.Empty;
                return;
            }

            switch (def.unlockPolicy)
            {
                case StrengthTrainingUnlockPolicy.FreeOncePerDay:
                    unlockProgressText.text = "免费解锁今日使用权";
                    break;
                case StrengthTrainingUnlockPolicy.ProgressPerDay:
                {
                    var required = StrengthTrainingService.GetGroundClaimsRequired(groundId);
                    var progress = StrengthTrainingService.GetGroundProgress(groundId);
                    unlockProgressText.text = $"解锁进度 {progress}/{required}";
                    break;
                }
                default:
                    unlockProgressText.text = string.Empty;
                    break;
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
            if (_payload == null || _payload.Mode != StrengthTrainingPanelMode.ConfirmStart)
                return;

            CompleteStartRequest();
        }

        void OnClaimClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_payload == null || _payload.Mode != StrengthTrainingPanelMode.Unlock)
                return;

            var groundId = _payload.GroundId;
            var def = StrengthTrainingService.ResolveGroundDef(groundId);
            if (def == null)
                return;

            FreeRewardGate.Grant(() =>
            {
                EnsureSlideHost();
                if (!slideHost.IsShown || _payload == null)
                    return;

                switch (def.unlockPolicy)
                {
                    case StrengthTrainingUnlockPolicy.FreeOncePerDay:
                        if (!StrengthTrainingService.TryUnlockGroundFree(groundId))
                        {
                            AudioGameplayFeedback.PlayUiError();
                            return;
                        }
                        break;
                    case StrengthTrainingUnlockPolicy.ProgressPerDay:
                        if (!StrengthTrainingService.TryRecordGroundProgress(
                                groundId, def.claimsRequired, out var unlockedNow))
                        {
                            AudioGameplayFeedback.PlayUiError();
                            return;
                        }

                        RefreshProgressText(groundId, def, isConfirm: false);
                        if (!unlockedNow)
                            return;
                        break;
                    default:
                        return;
                }

                CompleteStartRequest();
            });
        }

        void OnGemUnlockClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_payload == null || _payload.Mode != StrengthTrainingPanelMode.Unlock)
                return;

            if (!StrengthTrainingService.TryUnlockGroundWithGems(_payload.GroundId))
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

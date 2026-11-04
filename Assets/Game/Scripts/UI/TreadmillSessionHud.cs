using DG.Tweening;
using Game.Audio;
using Game.Core;
using Game.Features.Treadmill;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 跑步机锻炼进行中 HUD：时长、累计提升、进度条与领取按钮（循环 Pause 原生由 PlayerTreadmillController 驱动）。
    /// 挂到场景 Canvas 下，绑定 <see cref="PlayerTreadmillController"/> 后自动显隐。
    /// </summary>
    public sealed class TreadmillSessionHud : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text elapsedTimeText;
        [SerializeField] private Text speedGainText;
        [SerializeField] private Image intervalProgressFill;
        [SerializeField] private Text speedBoostPopupText;
        [SerializeField] private Button instantRewardButton;

        [Header("Interval tick FX")]
        [SerializeField] private float speedGainPunchScale = 0.12f;
        [SerializeField] private float speedGainPunchDuration = 0.32f;
        [SerializeField] private int speedGainPunchVibrato = 6;

        [Header("Speed boost popup (fade up / fade out)")]
        [SerializeField] private float popupRiseInPx = 28f;
        [SerializeField] private float popupRiseOutPx = 36f;
        [SerializeField] private float popupFadeInDuration = 0.22f;
        [SerializeField] private float popupHoldDuration = 0.55f;
        [SerializeField] private float popupFadeOutDuration = 0.38f;

        PlayerTreadmillController _controller;
        float _lastSessionRunningGain;
        bool _popupRestCached;
        Vector2 _popupRestAnchoredPos;
        CanvasGroup _popupCanvasGroup;
        Sequence _popupSequence;
        Tween _speedGainPunchTween;

        void Awake()
        {
            if (panelRoot == null)
                panelRoot = gameObject;

            instantRewardButton?.onClick.AddListener(OnInstantRewardClicked);
            CachePopupBaseline();
        }

        void OnDestroy()
        {
            KillFxTweens();
        }

        void Start() => SetVisible(false);

        void Update()
        {
            if (_controller == null)
                _controller = UnityObjectFinder.FindFirst<PlayerTreadmillController>();

            if (_controller == null || !_controller.IsSessionActive)
            {
                if (_lastSessionRunningGain != 0f)
                    ResetSessionState();
                SetVisible(false);
                return;
            }

            SetVisible(true);

            var elapsed = _controller.SessionElapsedSeconds;

            if (elapsedTimeText != null)
                elapsedTimeText.text = "时长： " + FormatDuration(elapsed);

            if (speedGainText != null)
                speedGainText.text = $"本次提升：+{_controller.SessionBonusGain:0.###} m/s";

            if (intervalProgressFill != null)
                intervalProgressFill.fillAmount = _controller.SessionIntervalProgress01;

            var runningGain = _controller.SessionRunningBonusGain;
            if (runningGain > _lastSessionRunningGain + 0.000001f)
            {
                var tickBonus = runningGain - _lastSessionRunningGain;
                OnIntervalTick(tickBonus);
            }

            _lastSessionRunningGain = runningGain;
        }

        void OnIntervalTick(float tickBonus)
        {
            PlaySpeedGainPunch();
            PlaySpeedBoostPopup(tickBonus);
        }

        void PlaySpeedGainPunch()
        {
            if (speedGainText == null)
                return;

            var rt = speedGainText.rectTransform;
            _speedGainPunchTween?.Kill();
            rt.localScale = Vector3.one;
            _speedGainPunchTween = rt.DOPunchScale(
                    Vector3.one * speedGainPunchScale,
                    speedGainPunchDuration,
                    speedGainPunchVibrato)
                .OnKill(() =>
                {
                    if (rt != null)
                        rt.localScale = Vector3.one;
                });
        }

        void PlaySpeedBoostPopup(float tickBonus)
        {
            if (speedBoostPopupText == null)
                return;

            CachePopupBaseline();

            var rt = speedBoostPopupText.rectTransform;
            speedBoostPopupText.text = $" +{tickBonus:0.###} ↑↑↑";
            speedBoostPopupText.gameObject.SetActive(true);

            _popupSequence?.Kill();

            var startPos = _popupRestAnchoredPos + Vector2.down * popupRiseInPx;
            var endPos = _popupRestAnchoredPos + Vector2.up * popupRiseOutPx;
            rt.anchoredPosition = startPos;

            if (_popupCanvasGroup != null)
                _popupCanvasGroup.alpha = 0f;
            else
                SetTextAlpha(speedBoostPopupText, 0f);

            _popupSequence = DOTween.Sequence();
            _popupSequence.Append(rt.DOAnchorPos(_popupRestAnchoredPos, popupFadeInDuration).SetEase(Ease.OutQuad));
            if (_popupCanvasGroup != null)
                _popupSequence.Join(_popupCanvasGroup.DOFade(1f, popupFadeInDuration));
            else
                _popupSequence.Join(DOTween.To(() => 0f, a => SetTextAlpha(speedBoostPopupText, a), 1f, popupFadeInDuration));

            if (popupHoldDuration > 0.001f)
                _popupSequence.AppendInterval(popupHoldDuration);

            _popupSequence.Append(rt.DOAnchorPos(endPos, popupFadeOutDuration).SetEase(Ease.InQuad));
            if (_popupCanvasGroup != null)
                _popupSequence.Join(_popupCanvasGroup.DOFade(0f, popupFadeOutDuration));
            else
                _popupSequence.Join(DOTween.To(() => 1f, a => SetTextAlpha(speedBoostPopupText, a), 0f, popupFadeOutDuration));

            _popupSequence.OnComplete(() =>
            {
                if (speedBoostPopupText != null)
                    speedBoostPopupText.gameObject.SetActive(false);
                rt.anchoredPosition = _popupRestAnchoredPos;
                if (_popupCanvasGroup != null)
                    _popupCanvasGroup.alpha = 0f;
            });
        }

        void CachePopupBaseline()
        {
            if (speedBoostPopupText == null)
                return;

            var rt = speedBoostPopupText.rectTransform;
            if (!_popupRestCached)
            {
                _popupRestAnchoredPos = rt.anchoredPosition;
                _popupRestCached = true;
            }

            _popupCanvasGroup = speedBoostPopupText.GetComponent<CanvasGroup>();
            if (_popupCanvasGroup == null)
                _popupCanvasGroup = speedBoostPopupText.gameObject.AddComponent<CanvasGroup>();

            _popupCanvasGroup.alpha = 0f;
            _popupCanvasGroup.blocksRaycasts = false;
            _popupCanvasGroup.interactable = false;
            speedBoostPopupText.gameObject.SetActive(false);
        }

        static void SetTextAlpha(Text text, float alpha)
        {
            if (text == null)
                return;
            var c = text.color;
            c.a = alpha;
            text.color = c;
        }

        void ResetSessionState()
        {
            _lastSessionRunningGain = 0f;
            if (intervalProgressFill != null)
                intervalProgressFill.fillAmount = 0f;
            KillFxTweens();
        }

        void KillFxTweens()
        {
            _popupSequence?.Kill();
            _popupSequence = null;
            _speedGainPunchTween?.Kill();
            _speedGainPunchTween = null;

            if (speedGainText != null)
                speedGainText.rectTransform.localScale = Vector3.one;

            if (speedBoostPopupText != null)
            {
                speedBoostPopupText.rectTransform.anchoredPosition = _popupRestAnchoredPos;
                speedBoostPopupText.gameObject.SetActive(false);
                if (_popupCanvasGroup != null)
                    _popupCanvasGroup.alpha = 0f;
            }
        }

        void OnInstantRewardClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_controller == null || !_controller.IsSessionActive)
                return;

            UiRouter.OpenTreadmillInstantRewardOffer(_controller.ActiveTier);
        }

        static string FormatDuration(float seconds)
        {
            if (seconds < 0f)
                seconds = 0f;
            var mins = (int)(seconds / 60f);
            var secs = (int)(seconds % 60f);
            return $"{mins:D2}:{secs:D2}";
        }

        void SetVisible(bool visible)
        {
            if (panelRoot != null && panelRoot.activeSelf != visible)
                panelRoot.SetActive(visible);
        }
    }
}

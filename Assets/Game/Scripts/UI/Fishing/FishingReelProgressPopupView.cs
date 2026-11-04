using System.Collections;
using Game.Audio;
using Game.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Fishing
{
    public enum HudFloatPopupMotion
    {
        RiseAndFade = 0,
        SlideRightHold = 1,
    }

    /// <summary>
    /// HUD 飘字：上浮淡出 / 右移停留（力量累加时不继续右移，重置存在时间并恢复透明度）。
    /// </summary>
    public sealed class FishingReelProgressPopupView : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private HudFloatPopupMotion motion = HudFloatPopupMotion.RiseAndFade;
        [SerializeField] private float lifetimeSeconds = 1.1f;
        [SerializeField] private float floatDistance = 48f;
        [SerializeField] private float maxRightOffset = 120f;
        [SerializeField] private float slideDurationRatio = 0.35f;

        RectTransform _rt;
        CanvasGroup _canvasGroup;
        Coroutine _routine;
        Vector2 _startPos;
        float _maxX;
        bool _atRightLimit;
        float _totalDuration;
        float _elapsed;
        Color _labelBaseColor = Color.white;

        void Awake()
        {
            _rt = transform as RectTransform;
            if (label == null)
                label = GetComponentInChildren<Text>(true);
            if (label != null)
                _labelBaseColor = label.color;
        }

        public void Configure(HudFloatPopupMotion popupMotion, float rightLimitOffset = 120f)
        {
            motion = popupMotion;
            maxRightOffset = rightLimitOffset;
        }

        public void ShowDelta(float delta) => ShowDelta(delta, mergeUpdate: false);

        public void ShowDelta(float delta, bool mergeUpdate)
        {
            if (label != null)
            {
                var n = Mathf.Max(0, Mathf.RoundToInt(delta));
                label.text = n >= 100_000
                    ? $"+{EconomyDisplayFormat.Format(n)}"
                    : $"+{n}";
            }

            if (mergeUpdate && _routine != null)
            {
                AudioGameplayFeedback.PlayUiFloatPopup(merged: true);
                ResetLifetime();
                if (motion == HudFloatPopupMotion.RiseAndFade && _rt != null)
                    _rt.anchoredPosition = _startPos;
                return;
            }

            AudioGameplayFeedback.PlayUiFloatPopup(merged: false);

            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(CoAnimate());
        }

        void ResetLifetime()
        {
            _totalDuration = Mathf.Max(0.05f, lifetimeSeconds);
            _elapsed = 0f;
            ResetVisualAlpha();
        }

        void ResetVisualAlpha()
        {
            EnsureCanvasGroup();
            _canvasGroup.alpha = 1f;

            if (label == null)
                return;

            var c = _labelBaseColor;
            c.a = 1f;
            label.color = c;
        }

        void EnsureCanvasGroup()
        {
            if (_canvasGroup != null)
                return;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        IEnumerator CoAnimate()
        {
            if (_rt == null)
            {
                Destroy(gameObject);
                yield break;
            }

            _startPos = _rt.anchoredPosition;
            _maxX = _startPos.x + maxRightOffset;
            _atRightLimit = false;
            _totalDuration = Mathf.Max(0.05f, lifetimeSeconds);
            _elapsed = 0f;
            ResetVisualAlpha();

            var slideDur = _totalDuration * Mathf.Clamp01(slideDurationRatio);

            while (_elapsed < _totalDuration)
            {
                _elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(_elapsed / _totalDuration);
                slideDur = _totalDuration * Mathf.Clamp01(slideDurationRatio);

                switch (motion)
                {
                    case HudFloatPopupMotion.SlideRightHold:
                    {
                        var slideT = slideDur <= 0.0001f ? 1f : Mathf.Clamp01(_elapsed / slideDur);
                        var desiredX = Mathf.Lerp(_startPos.x, _maxX, slideT);
                        if (_rt.anchoredPosition.x >= _maxX - 0.5f)
                        {
                            _atRightLimit = true;
                            desiredX = _maxX;
                        }

                        _rt.anchoredPosition = new Vector2(
                            _atRightLimit ? _maxX : desiredX,
                            _startPos.y);
                        break;
                    }
                    default:
                        _rt.anchoredPosition = Vector2.Lerp(
                            _startPos,
                            _startPos + new Vector2(0f, floatDistance),
                            t);
                        break;
                }

                _canvasGroup.alpha = 1f - t;
                yield return null;
            }

            _routine = null;
            Destroy(gameObject);
        }
    }
}

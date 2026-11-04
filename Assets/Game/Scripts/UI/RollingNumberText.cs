using DG.Tweening;
using Game.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 数字快速滚动（金币/钻石/力量等 HUD 文本）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RollingNumberText : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private float minDuration = 0.22f;
        [SerializeField] private float maxDuration = 1.1f;
        [SerializeField] private int maxSteps = 28;

        long _displayValue;
        long _animateTarget;
        Tweener _tween;

        void Awake()
        {
            if (label == null)
                label = GetComponent<Text>();
        }

        void OnDestroy() => _tween?.Kill();

        public long DisplayValue => _displayValue;

        public void SetImmediate(long value)
        {
            _tween?.Kill();
            _animateTarget = value;
            _displayValue = value;
            ApplyText(_displayValue);
        }

        public void AddDelta(long delta, bool immediate = false)
        {
            if (delta == 0)
                return;

            // 扣款必须立刻对齐存档，避免顶栏仍显示旧余额导致误判可购买。
            if (delta < 0)
                immediate = true;

            AnimateTo(_displayValue + delta, immediate);
        }

        public void AnimateTo(long target, bool forceImmediate = false)
        {
            if (label == null)
                return;

            // 余额下降时不播放滚动，防止 HUD 短暂高于实际金币。
            if (!forceImmediate && target < _displayValue)
                forceImmediate = true;

            if (forceImmediate || target == _displayValue)
            {
                SetImmediate(target);
                return;
            }

            if (_tween != null && _tween.IsActive() && _animateTarget == target)
                return;

            _animateTarget = target;

            var from = _displayValue;
            var delta = System.Math.Abs(target - from);
            if (delta <= 0)
            {
                SetImmediate(target);
                return;
            }

            var step = ComputeStep(delta);
            var steps = Mathf.Max(1, Mathf.CeilToInt(delta / (float)step));
            steps = Mathf.Min(steps, maxSteps);
            var duration = Mathf.Clamp(steps * 0.035f, minDuration, maxDuration);

            _tween?.Kill();
            var shownSteps = 0;
            _tween = DOTween.To(
                    () => 0f,
                    t =>
                    {
                        var nextStep = Mathf.Min(steps, Mathf.FloorToInt(t * steps));
                        if (nextStep == shownSteps && t < 1f - 0.0001f)
                            return;
                        shownSteps = nextStep;
                        var v = target > from
                            ? from + nextStep * step
                            : from - nextStep * step;
                        if (nextStep >= steps || t >= 1f - 0.0001f)
                            v = target;
                        else if (target > from)
                            v = System.Math.Min(v, target);
                        else
                            v = System.Math.Max(v, target);
                        _displayValue = v;
                        ApplyText(_displayValue);
                    },
                    1f,
                    duration)
                .SetEase(Ease.Linear)
                .OnComplete(() => SetImmediate(target));
        }

        static long ComputeStep(long delta)
        {
            if (delta < 20) return 1;
            if (delta < 200) return 10;
            if (delta < 2_000) return 100;
            if (delta < 20_000) return 1_000;
            if (delta < 200_000) return 10_000;
            return System.Math.Max(1L, delta / 24);
        }

        void ApplyText(long value)
        {
            if (label != null)
                label.text = EconomyDisplayFormat.Format(value);
        }
    }
}

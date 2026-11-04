using System;
using DG.Tweening;
using UnityEngine;

namespace Game.UI.Framework
{
    /// <summary>
    /// 面板滑入/弹回滑出动画（关闭比打开更快）。
    /// </summary>
    public static class UiPanelSlideAnim
    {
        public const float DefaultCloseSpeedMultiplier = 1.55f;

        static Sequence NewSequence(RectTransform rt) =>
            DOTween.Sequence().SetLink(rt.gameObject);

        public static Sequence PlayOpenFromLeft(
            RectTransform rt,
            Vector2 rest,
            float slideDuration,
            float settleDuration,
            float overshootLeftPx,
            float openSlideInMultiplier,
            ref Sequence seqSlot)
        {
            seqSlot?.Kill();
            rt.DOKill();

            var w = Screen.width;
            rt.anchoredPosition = rest + Vector2.right * (w * openSlideInMultiplier + 400f);

            seqSlot = NewSequence(rt);
            seqSlot.Append(rt.DOAnchorPos(rest + Vector2.left * overshootLeftPx, slideDuration).SetEase(Ease.OutCubic));
            seqSlot.Append(rt.DOAnchorPos(rest, settleDuration).SetEase(Ease.OutBack, 1.15f));
            return seqSlot;
        }

        public static Sequence PlayCloseFromLeft(
            RectTransform rt,
            Vector2 rest,
            float slideDuration,
            float settleDuration,
            float overshootLeftPx,
            float openSlideInMultiplier,
            Action onComplete,
            ref Sequence seqSlot,
            float closeSpeedMultiplier = DefaultCloseSpeedMultiplier)
        {
            seqSlot?.Kill();
            rt.DOKill();

            var slide = slideDuration / closeSpeedMultiplier;
            var settle = settleDuration / closeSpeedMultiplier;
            var w = Screen.width;
            var offscreen = rest + Vector2.right * (w * openSlideInMultiplier + 400f);

            seqSlot = NewSequence(rt);
            seqSlot.Append(rt.DOAnchorPos(rest + Vector2.left * overshootLeftPx, settle).SetEase(Ease.OutQuad));
            seqSlot.Append(rt.DOAnchorPos(offscreen, slide).SetEase(Ease.InCubic));
            seqSlot.OnComplete(() => onComplete?.Invoke());
            return seqSlot;
        }

        public static Sequence PlayOpenFromRight(
            RectTransform rt,
            Vector2 rest,
            float slideDuration,
            float settleDuration,
            float overshootRightPx,
            ref Sequence seqSlot)
        {
            seqSlot?.Kill();
            rt.DOKill();

            var w = Screen.width;
            rt.anchoredPosition = rest + Vector2.left * (w * 0.55f + 400f);

            seqSlot = NewSequence(rt);
            seqSlot.Append(rt.DOAnchorPos(rest + Vector2.right * overshootRightPx, slideDuration).SetEase(Ease.OutCubic));
            seqSlot.Append(rt.DOAnchorPos(rest, settleDuration).SetEase(Ease.OutBack, 1.15f));
            return seqSlot;
        }

        public static Sequence PlayCloseFromRight(
            RectTransform rt,
            Vector2 rest,
            float slideDuration,
            float settleDuration,
            float overshootRightPx,
            Action onComplete,
            ref Sequence seqSlot,
            float closeSpeedMultiplier = DefaultCloseSpeedMultiplier)
        {
            seqSlot?.Kill();
            rt.DOKill();

            var slide = slideDuration / closeSpeedMultiplier;
            var settle = settleDuration / closeSpeedMultiplier;
            var w = Screen.width;
            var offscreen = rest + Vector2.left * (w * 0.55f + 400f);

            seqSlot = NewSequence(rt);
            seqSlot.Append(rt.DOAnchorPos(rest + Vector2.right * overshootRightPx, settle).SetEase(Ease.OutQuad));
            seqSlot.Append(rt.DOAnchorPos(offscreen, slide).SetEase(Ease.InCubic));
            seqSlot.OnComplete(() => onComplete?.Invoke());
            return seqSlot;
        }

        public static Sequence PlayOpenFromBottom(
            RectTransform rt,
            Vector2 rest,
            float slideDuration,
            float settleDuration,
            float overshootDownPx,
            ref Sequence seqSlot)
        {
            seqSlot?.Kill();
            rt.DOKill();

            var h = Screen.height;
            rt.anchoredPosition = rest + Vector2.up * (h * 0.45f + 300f);

            seqSlot = NewSequence(rt);
            seqSlot.Append(rt.DOAnchorPos(rest + Vector2.down * overshootDownPx, slideDuration).SetEase(Ease.OutCubic));
            seqSlot.Append(rt.DOAnchorPos(rest, settleDuration).SetEase(Ease.OutBack, 1.15f));
            return seqSlot;
        }

        public static Sequence PlayCloseFromBottom(
            RectTransform rt,
            Vector2 rest,
            float slideDuration,
            float settleDuration,
            float overshootDownPx,
            Action onComplete,
            ref Sequence seqSlot,
            float closeSpeedMultiplier = DefaultCloseSpeedMultiplier)
        {
            seqSlot?.Kill();
            rt.DOKill();

            var slide = slideDuration / closeSpeedMultiplier;
            var settle = settleDuration / closeSpeedMultiplier;
            var h = Screen.height;
            var offscreen = rest + Vector2.up * (h * 0.45f + 300f);

            seqSlot = NewSequence(rt);
            seqSlot.Append(rt.DOAnchorPos(rest + Vector2.down * overshootDownPx, settle).SetEase(Ease.OutQuad));
            seqSlot.Append(rt.DOAnchorPos(offscreen, slide).SetEase(Ease.InCubic));
            seqSlot.OnComplete(() => onComplete?.Invoke());
            return seqSlot;
        }
    }
}

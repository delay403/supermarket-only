using System.Collections;
using UnityEngine;

namespace Game.UI.Util
{
    /// <summary>UGUI 弹力缩放：0→peak→rest 或 rest→peak→rest。</summary>
    public static class UguiBounceScaleTween
    {
        public static Coroutine Play(
            MonoBehaviour host,
            RectTransform target,
            float restScale,
            float peakScale,
            float upDuration = 0.12f,
            float downDuration = 0.1f,
            Coroutine running = null)
        {
            if (host == null || target == null)
                return null;

            if (running != null)
                host.StopCoroutine(running);

            return host.StartCoroutine(BounceRoutine(target, restScale, peakScale, upDuration, downDuration));
        }

        public static Coroutine PlayFromZero(
            MonoBehaviour host,
            RectTransform target,
            float restScale,
            float peakScale,
            float upDuration = 0.16f,
            float downDuration = 0.12f,
            Coroutine running = null)
        {
            if (host == null || target == null)
                return null;

            if (running != null)
                host.StopCoroutine(running);

            target.localScale = Vector3.zero;
            return host.StartCoroutine(BounceFromZeroRoutine(target, restScale, peakScale, upDuration, downDuration));
        }

        static IEnumerator BounceRoutine(
            RectTransform target,
            float restScale,
            float peakScale,
            float upDuration,
            float downDuration)
        {
            var start = target.localScale.x;
            yield return LerpScale(target, start, peakScale, upDuration);
            yield return LerpScale(target, peakScale, restScale, downDuration);
            target.localScale = new Vector3(restScale, restScale, 1f);
        }

        static IEnumerator BounceFromZeroRoutine(
            RectTransform target,
            float restScale,
            float peakScale,
            float upDuration,
            float downDuration)
        {
            yield return LerpScale(target, 0f, peakScale, upDuration);
            yield return LerpScale(target, peakScale, restScale, downDuration);
            target.localScale = new Vector3(restScale, restScale, 1f);
        }

        static IEnumerator LerpScale(RectTransform target, float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                target.localScale = new Vector3(to, to, 1f);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = 1f - (1f - t) * (1f - t);
                var s = Mathf.Lerp(from, to, t);
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            target.localScale = new Vector3(to, to, 1f);
        }
    }
}

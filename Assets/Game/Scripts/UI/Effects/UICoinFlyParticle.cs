using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Effects
{
    /// <summary>单枚飞行金币 UI。</summary>
    [DisallowMultipleComponent]
    public sealed class UICoinFlyParticle : MonoBehaviour
    {
        RectTransform _rect;
        Image _image;
        Action<UICoinFlyParticle> _onFinished;

        public void EnsureImage(Sprite sprite)
        {
            _rect ??= transform as RectTransform;
            if (_rect == null)
                _rect = gameObject.AddComponent<RectTransform>();

            _image ??= GetComponent<Image>();
            if (_image == null)
                _image = gameObject.AddComponent<Image>();

            _image.raycastTarget = false;
            if (sprite != null)
                _image.sprite = sprite;
        }

        public void Play(
            RectTransform parent,
            Vector2 startLocal,
            Vector2 endLocal,
            float arcSign,
            float arcStrength,
            float duration,
            Vector2 size,
            Vector2 scaleRange,
            AnimationCurve moveCurve,
            AnimationCurve arcCurve,
            float? collectSfxMoveProgress,
            Action onCollectSfx,
            Action<UICoinFlyParticle> onFinished)
        {
            EnsureImage(_image != null ? _image.sprite : null);
            _onFinished = onFinished;

            transform.SetParent(parent, false);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = size;
            _rect.anchoredPosition = startLocal;
            _rect.localScale = Vector3.one * scaleRange.x;
            gameObject.SetActive(true);

            StopAllCoroutines();
            StartCoroutine(CoFly(
                startLocal,
                endLocal,
                arcSign,
                arcStrength,
                duration,
                scaleRange,
                moveCurve,
                arcCurve,
                collectSfxMoveProgress,
                onCollectSfx));
        }

        IEnumerator CoFly(
            Vector2 start,
            Vector2 end,
            float arcSign,
            float arcStrength,
            float duration,
            Vector2 scaleRange,
            AnimationCurve moveCurve,
            AnimationCurve arcCurve,
            float? collectSfxMoveProgress,
            Action onCollectSfx)
        {
            duration = Mathf.Max(0.05f, duration);
            var delta = end - start;
            var arcNormal = delta.sqrMagnitude > 0.0001f
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.right;

            var elapsed = 0f;
            var collectSfxTriggered = false;
            var collectSfxAt = collectSfxMoveProgress.HasValue
                ? Mathf.Clamp01(collectSfxMoveProgress.Value)
                : 1f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var moveT = moveCurve != null && moveCurve.length > 0
                    ? moveCurve.Evaluate(t)
                    : 1f - (1f - t) * (1f - t);

                var pos = Vector2.Lerp(start, end, moveT);
                var arcT = arcCurve != null && arcCurve.length > 0
                    ? arcCurve.Evaluate(t)
                    : Mathf.Sin(t * Mathf.PI);
                pos += arcNormal * (arcSign * arcStrength * arcT);

                _rect.anchoredPosition = pos;
                _rect.localScale = Vector3.one * Mathf.Lerp(scaleRange.x, scaleRange.y, moveT);

                if (!collectSfxTriggered && onCollectSfx != null && t >= collectSfxAt)
                {
                    collectSfxTriggered = true;
                    onCollectSfx.Invoke();
                }

                yield return null;
            }

            _rect.anchoredPosition = end;
            gameObject.SetActive(false);
            _onFinished?.Invoke(this);
        }
    }
}

using System;
using System.Collections;
using DG.Tweening;
using Game.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Controls
{
    /// <summary>
    /// 自定义左右滑动开关：底图与滑块图两套 Sprite，点击切换并播放 UI 点击音效。
    /// On：手柄靠轨道的局部 +X；Off：靠 -X（请把轨道与手柄的 RectTransform 的 pivot 都设在水平中心）。
    /// 建议挂在可命中区域（如整条轨道）的 <see cref="Image"/> 上；滑块图若单独为 <see cref="Image"/>，请关闭其 <c>raycastTarget</c> 以免拦点击。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomSlidingToggle : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform track;
        [SerializeField] private RectTransform handle;
        [SerializeField] private Image trackImage;
        [SerializeField] private Image handleImage;

        [Header("Sprites · On")]
        [SerializeField] private Sprite trackSpriteOn;
        [SerializeField] private Sprite handleSpriteOn;

        [Header("Sprites · Off")]
        [SerializeField] private Sprite trackSpriteOff;
        [SerializeField] private Sprite handleSpriteOff;

        [SerializeField] private float horizontalPadding = 8f;
        [SerializeField] private float slideDuration = 0.22f;

        [Tooltip("首帧或未布局时 track.rect.width 常为 0，会导致行程为 0；用此像素值作为手柄半边行程下限。")]
        [SerializeField] private float fallbackHalfTravelPixels = 36f;

        [SerializeField] private bool isOn = true;

        public bool IsOn => isOn;

        public event Action<bool> ValueChanged;

        Tween _handleTween;

        void Awake()
        {
            ApplySprites();
            SnapHandle(false);
        }

        void OnEnable() => StartCoroutine(CoSnapAfterLayout());

        void OnDestroy() => _handleTween?.Kill();

        IEnumerator CoSnapAfterLayout()
        {
            yield return null;
            EnsureLayoutForMeasure();
            ApplySprites();
            SnapHandle(false);
        }

        public void SetValue(bool on, bool notify = true, bool animate = true)
        {
            if (isOn == on)
            {
                ApplySprites();
                SnapHandle(false);
                return;
            }

            isOn = on;
            ApplySprites();
            MoveHandle(animate);

            if (notify)
                ValueChanged?.Invoke(isOn);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            AudioGameplayFeedback.PlayUiButton();
            SetValue(!isOn, true, true);
        }

        void ApplySprites()
        {
            if (trackImage != null && trackSpriteOn != null && trackSpriteOff != null)
                trackImage.sprite = isOn ? trackSpriteOn : trackSpriteOff;

            if (handleImage != null && handleSpriteOn != null && handleSpriteOff != null)
                handleImage.sprite = isOn ? handleSpriteOn : handleSpriteOff;
        }

        void EnsureLayoutForMeasure()
        {
            Canvas.ForceUpdateCanvases();
            if (track != null)
            {
                var p = track.parent as RectTransform;
                if (p != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(p);
                LayoutRebuilder.ForceRebuildLayoutImmediate(track);
            }
            if (handle != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(handle);
        }

        void SnapHandle(bool refreshLayout)
        {
            if (track == null || handle == null)
                return;

            if (refreshLayout)
                EnsureLayoutForMeasure();

            var x = HandleTargetX();
            var p = handle.anchoredPosition;
            p.x = x;
            handle.anchoredPosition = p;
        }

        void MoveHandle(bool animate)
        {
            if (track == null || handle == null)
                return;

            EnsureLayoutForMeasure();

            var targetX = HandleTargetX();
            _handleTween?.Kill();

            if (!animate)
            {
                SnapHandle(false);
                return;
            }

            _handleTween = handle.DOAnchorPosX(targetX, slideDuration).SetEase(Ease.OutCubic);
        }

        float HandleTargetX()
        {
            var w = track != null ? Mathf.Max(0f, track.rect.width) : 0f;
            var hw = handle != null ? Mathf.Max(0f, handle.rect.width) : 0f;
            var halfRange = w * 0.5f - hw * 0.5f - horizontalPadding;

            if (halfRange < 1f)
                halfRange = Mathf.Max(1f, fallbackHalfTravelPixels);

            return isOn ? halfRange : -halfRange;
        }
    }
}

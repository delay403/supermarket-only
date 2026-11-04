using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 按钮/可点击 UI 的按压反馈：同步缩放脉冲 + Z 轴旋转抖动（Ease Out，结束后复位）。
    /// 动效进行中重复触发会被忽略。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JuicyUiPressFeedback : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private bool playOnButtonClick = true;
        [SerializeField] private float duration = 0.32f;
        [SerializeField] private float scaleMin = 0.95f;
        [SerializeField] private float scalePeak = 1.05f;
        [SerializeField] private float maxRotationDegrees = 10f;
        [SerializeField] private float referenceWidth = 120f;
        [SerializeField] private float minWidthForCalc = 48f;
        [SerializeField] private float minRotationRatio = 0.35f;

        Sequence _sequence;
        bool _playing;
        Vector3 _baseScale = Vector3.one;
        float _baseRotationZ;

        void Awake()
        {
            if (target == null)
                target = transform as RectTransform;

            CacheBaseTransform();
            if (playOnButtonClick)
            {
                var button = GetComponent<Button>();
                if (button != null)
                    button.onClick.AddListener(PlayPress);
            }
        }

        void OnDisable()
        {
            _sequence?.Kill();
            _sequence = null;
            _playing = false;
            RestoreBaseTransform();
        }

        void CacheBaseTransform()
        {
            if (target == null)
                return;

            _baseScale = target.localScale;
            _baseRotationZ = target.localEulerAngles.z;
        }

        public void PlayPress()
        {
            if (_playing || target == null)
                return;

            _playing = true;
            _sequence?.Kill();
            RestoreBaseTransform();

            var width = Mathf.Max(minWidthForCalc, target.rect.width);
            var lengthRatio = Mathf.Clamp(referenceWidth / width, minRotationRatio, 1f);
            var rotationTarget = maxRotationDegrees * lengthRatio * (Random.value < 0.5f ? -1f : 1f);

            var total = Mathf.Max(0.08f, duration);
            var scaleSeq = DOTween.Sequence();
            scaleSeq.Append(target.DOScale(_baseScale * scaleMin, total * 0.25f).SetEase(Ease.OutQuad));
            scaleSeq.Append(target.DOScale(_baseScale * scalePeak, total * 0.35f).SetEase(Ease.OutQuad));
            scaleSeq.Append(target.DOScale(_baseScale, total * 0.4f).SetEase(Ease.OutBack, 1.1f));

            var rotSeq = DOTween.Sequence();
            rotSeq.Append(target.DOLocalRotate(new Vector3(0f, 0f, _baseRotationZ + rotationTarget), total * 0.5f)
                .SetEase(Ease.OutQuad));
            rotSeq.Append(target.DOLocalRotate(new Vector3(0f, 0f, _baseRotationZ), total * 0.5f).SetEase(Ease.OutQuad));

            _sequence = DOTween.Sequence()
                .SetLink(gameObject)
                .Join(scaleSeq)
                .Join(rotSeq)
                .OnComplete(() =>
                {
                    _playing = false;
                    RestoreBaseTransform();
                });
        }

        void RestoreBaseTransform()
        {
            if (target == null)
                return;

            target.localScale = _baseScale;
            target.localRotation = Quaternion.Euler(0f, 0f, _baseRotationZ);
        }

        public static void EnsureOn(Button button)
        {
            if (button == null)
                return;

            if (button.GetComponent<JuicyUiPressFeedback>() == null)
                button.gameObject.AddComponent<JuicyUiPressFeedback>();
        }
    }
}

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Leaderboard
{
    /// <summary>
    /// 排行榜前三名展示槽（头像、名称、主分数文案）；皇冠等装饰由Prefab摆放。
    /// </summary>
    public sealed class LeaderboardTopSlotView : MonoBehaviour
    {
        [SerializeField] private RectTransform animRoot;
        [SerializeField] private GameObject avatarRoot;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text valueText;

        Vector2 _baseAnchoredPos;
        Tween _moveTween;

        void Awake()
        {
            var rt = animRoot != null ? animRoot : transform as RectTransform;
            if (rt != null)
                _baseAnchoredPos = rt.anchoredPosition;
        }

        void OnDestroy()
        {
            _moveTween?.Kill();
        }

        public void Bind(bool visible, string displayName, string valueFormatted, Sprite portrait, bool showPortrait)
        {
            gameObject.SetActive(visible);
            if (!visible)
                return;

            if (nameText != null)
                nameText.text = displayName ?? "";

            if (valueText != null)
                valueText.text = valueFormatted ?? "";

            var show = showPortrait && portrait != null && avatarImage != null;
            // if (avatarRoot != null)
            //     avatarRoot.SetActive(show);

            // if (avatarImage != null)
            // {
            //     avatarImage.sprite = portrait;
            //     avatarImage.enabled = show;
            // }
        }

        /// <summary>排名上升时轻微上移回弹。</summary>
        public void PlayRankUpFx(float punchUpPx = 22f, float upDur = 0.14f, float downDur = 0.22f)
        {
            var rt = animRoot != null ? animRoot : transform as RectTransform;
            if (rt == null)
                return;

            _moveTween?.Kill();
            _baseAnchoredPos = rt.anchoredPosition;
            _moveTween = DOTween.Sequence()
                .Append(rt.DOAnchorPos(_baseAnchoredPos + Vector2.up * punchUpPx, upDur).SetEase(Ease.OutQuad))
                .Append(rt.DOAnchorPos(_baseAnchoredPos, downDur).SetEase(Ease.OutQuad));
        }
    }
}

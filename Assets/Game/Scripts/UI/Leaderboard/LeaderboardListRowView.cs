using DG.Tweening;
using Game.Leaderboard;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Leaderboard
{
    /// <summary>
    /// 排行榜第 4～9 名单行：位图名次序号 + 名称 + 分数；金币榜可选金币图标。
    /// </summary>
    public sealed class LeaderboardListRowView : MonoBehaviour
    {
        [SerializeField] private RectTransform animRoot;
        [SerializeField] private RectTransform rankDigitStrip;
        [SerializeField] private GameObject avatarRoot;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text valueText;
        [SerializeField] private GameObject coinIconRoot;

        Image[] _rankDigits;
        Vector2 _baseAnchoredPos;
        Tween _moveTween;

        void Awake()
        {
            _rankDigits = BitmapDigitStripBinder.CacheDigitImages(rankDigitStrip);
            var rt = animRoot != null ? animRoot : transform as RectTransform;
            if (rt != null)
                _baseAnchoredPos = rt.anchoredPosition;
        }

        void OnDestroy()
        {
            _moveTween?.Kill();
        }

        public void Bind(
            LeaderboardKind kind,
            Sprite[] digitSprites,
            int rankOneBased,
            string displayName,
            string valueFormatted,
            Sprite portrait,
            bool showPortrait)
        {
            gameObject.SetActive(true);

            BitmapDigitStripBinder.Bind(_rankDigits, digitSprites, rankOneBased);

            if (nameText != null)
                nameText.text = displayName ?? "";

            if (valueText != null)
                valueText.text = valueFormatted ?? "";

            if (coinIconRoot != null)
                coinIconRoot.SetActive(kind == LeaderboardKind.Coins);

            var show = showPortrait && portrait != null && avatarImage != null;
            if (avatarRoot != null)
                avatarRoot.SetActive(show);

            if (avatarImage != null)
            {
                avatarImage.sprite = portrait;
                avatarImage.enabled = show;
            }
        }

        public void HideRow()
        {
            gameObject.SetActive(false);
        }

        public void PlayRankUpFx(float punchUpPx = 18f, float upDur = 0.12f, float downDur = 0.2f)
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

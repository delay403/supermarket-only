using UnityEngine;

namespace Game.UI.Fishing
{
    /// <summary>
    /// 力量增加飘字：上浮淡出；1 秒内多次增加合并为单次显示。
    /// </summary>
    public sealed class StrengthGainFloater : MonoBehaviour
    {
        [SerializeField] private FishingReelProgressPopupView popupPrefab;
        [SerializeField] private RectTransform spawnAnchor;
        [SerializeField] private float maxRightOffset = 120f;
        [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 8f);
        [SerializeField] private float spawnRandomY = 12f;
        [SerializeField] private float mergeWindowSeconds = 1f;

        float _pendingAmount;
        float _mergeDeadline;
        FishingReelProgressPopupView _activePopup;

        void Awake()
        {
            if (spawnAnchor == null)
                spawnAnchor = transform as RectTransform;
        }

        public void SetSpawnAnchor(RectTransform anchor)
        {
            if (anchor != null)
                spawnAnchor = anchor;
        }

        public void AddGain(float delta, RectTransform anchorOverride = null)
        {
            var anchor = ResolveAnchor(anchorOverride);
            if (popupPrefab == null || anchor == null || delta <= 0.0001f)
            {
                if (popupPrefab == null)
                    Debug.LogWarning("[StrengthGainFloater] 未绑定 popupPrefab，无法显示力量飘字。", this);
                else if (anchor == null)
                    Debug.LogWarning("[StrengthGainFloater] 未绑定 spawnAnchor，无法显示力量飘字。", this);
                return;
            }

            var now = Time.unscaledTime;
            if (_activePopup != null && now <= _mergeDeadline)
            {
                _pendingAmount += delta;
                _activePopup.ShowDelta(_pendingAmount, mergeUpdate: true);
                _mergeDeadline = now + mergeWindowSeconds;
                return;
            }

            _pendingAmount = delta;
            _mergeDeadline = now + mergeWindowSeconds;
            _activePopup = Instantiate(popupPrefab, anchor);
            _activePopup.Configure(HudFloatPopupMotion.RiseAndFade, maxRightOffset);

            var rt = _activePopup.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(
                    spawnOffset.x,
                    spawnOffset.y + Random.Range(-spawnRandomY, spawnRandomY));
                rt.localScale = Vector3.one;
            }

            _activePopup.ShowDelta(_pendingAmount, mergeUpdate: false);
        }

        RectTransform ResolveAnchor(RectTransform anchorOverride)
        {
            if (anchorOverride != null)
                return anchorOverride;
            if (spawnAnchor != null)
                return spawnAnchor;
            return transform as RectTransform;
        }
    }
}

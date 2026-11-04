using UnityEngine;

namespace Game.UI.Fishing
{
    /// <summary>
    /// 在钓鱼进度条附近随机位置生成收杆 +进度 飘字（使用预制体）。
    /// </summary>
    public sealed class FishingReelProgressFloater : MonoBehaviour
    {
        [SerializeField] private FishingReelProgressPopupView popupPrefab;
        [SerializeField] private RectTransform spawnAnchor;
        [SerializeField] private Vector2 randomOffsetMin = new Vector2(-120f, 20f);
        [SerializeField] private Vector2 randomOffsetMax = new Vector2(120f, 80f);

        public void SpawnProgressDelta(float delta)
        {
            if (popupPrefab == null || spawnAnchor == null || delta <= 0.0001f)
                return;

            var inst = Instantiate(popupPrefab, spawnAnchor);
            var rt = inst.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(
                    Random.Range(randomOffsetMin.x, randomOffsetMax.x),
                    Random.Range(randomOffsetMin.y, randomOffsetMax.y));
                rt.localScale = Vector3.one;
            }

            inst.ShowDelta(delta);
        }

        /// <summary>销毁锚点下所有收杆飘字（钓鱼 UI 关闭/重开时避免叠加显示）。</summary>
        public void ClearAll()
        {
            if (spawnAnchor == null)
                return;

            for (var i = spawnAnchor.childCount - 1; i >= 0; i--)
            {
                var child = spawnAnchor.GetChild(i);
                if (child != null && child.GetComponent<FishingReelProgressPopupView>() != null)
                    Destroy(child.gameObject);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public static class ShopUiScroll
    {
        /// <summary>
        /// 将纵向或横向 ScrollRect 滚到指定商品索引。
        /// 若 Content 上带纵向滚动的 <see cref="GridLayoutGroup"/>，则按<strong>行</strong>对齐：
        /// 行号 <c>index / 列数</c>，行距 <c>cellSize.y + spacing.y</c>（与 Unity 网格行间距一致）；
        /// 否则回退为沿滚动轴按索引均匀分布（旧逻辑）。
        /// </summary>
        /// <param name="gridColumns">固定每行列数；0 表示从 Grid 约束或 Content 宽度自动推断。</param>
        public static void ScrollToItemIndex(ScrollRect scrollRect, int index, int totalCount, int gridColumns = 0)
        {
            if (scrollRect == null || totalCount <= 0 || index < 0 || index >= totalCount)
                return;

            // 仅重建当前 Scroll 的 Content，避免 Canvas.ForceUpdateCanvases 触发全 Canvas 重算（Profiler 中 GC/耗时大户）。
            EnsureContentLayoutReady(scrollRect);

            if (totalCount == 1)
            {
                if (scrollRect.vertical)
                    scrollRect.verticalNormalizedPosition = 1f;
                if (scrollRect.horizontal)
                    scrollRect.horizontalNormalizedPosition = 0f;
                return;
            }

            var content = scrollRect.content;
            var grid = content != null ? content.GetComponent<GridLayoutGroup>() : null;

            if (scrollRect.vertical && grid != null)
            {
                ScrollVerticalGridToRow(scrollRect, grid, index, totalCount, gridColumns);
                return;
            }

            var t = index / (float)(totalCount - 1);
            if (scrollRect.vertical)
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(1f - t);
            if (scrollRect.horizontal)
                scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(t);
        }

        static void EnsureContentLayoutReady(ScrollRect scrollRect)
        {
            var content = scrollRect.content;
            if (content == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        static void ScrollVerticalGridToRow(ScrollRect scrollRect, GridLayoutGroup grid, int index, int totalCount,
            int gridColumns)
        {
            int cols = ResolveGridColumns(grid, scrollRect.content, totalCount, gridColumns);
            int row = index / cols;
            float rowStep = grid.cellSize.y + grid.spacing.y;
            float rowTop = grid.padding.top + row * rowStep;

            var content = scrollRect.content;
            var viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;

            float contentH = content.rect.height;
            float viewportH = viewport != null ? viewport.rect.height : 0f;

            if (contentH <= viewportH + 0.01f)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                if (scrollRect.horizontal)
                    scrollRect.horizontalNormalizedPosition = 0f;
                return;
            }

            float scrollable = Mathf.Max(0.0001f, contentH - viewportH);
            float normalized = 1f - rowTop / scrollable;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);

            if (scrollRect.horizontal)
                scrollRect.horizontalNormalizedPosition = 0f;
        }

        static int ResolveGridColumns(GridLayoutGroup grid, RectTransform content, int totalCount, int fallback)
        {
            if (fallback > 0)
                return Mathf.Max(1, fallback);

            switch (grid.constraint)
            {
                case GridLayoutGroup.Constraint.FixedColumnCount:
                    return Mathf.Max(1, grid.constraintCount);
                case GridLayoutGroup.Constraint.FixedRowCount:
                {
                    int rows = Mathf.Max(1, grid.constraintCount);
                    return Mathf.Max(1, Mathf.CeilToInt(totalCount / (float)rows));
                }
                default:
                    return EstimateFlexibleColumns(grid, content);
            }
        }

        static int EstimateFlexibleColumns(GridLayoutGroup grid, RectTransform content)
        {
            float inner = content.rect.width - grid.padding.horizontal;
            float stepX = grid.cellSize.x + grid.spacing.x;
            if (stepX <= 0.01f)
                return 1;
            int n = Mathf.FloorToInt((inner + grid.spacing.x) / stepX);
            return Mathf.Max(1, n);
        }
    }
}

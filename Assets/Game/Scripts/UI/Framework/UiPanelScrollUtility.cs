using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Framework
{
    /// <summary>
    /// 面板关闭时禁用 <see cref="ScrollRect"/>，避免隐藏 UI 仍跑 LateUpdate（Profiler 常见 3ms+ / 50KB GC）。
    /// </summary>
    public static class UiPanelScrollUtility
    {
        public static void SetScrollRectsActive(GameObject panelRoot, bool active)
        {
            if (panelRoot == null)
                return;

            var scrolls = panelRoot.GetComponentsInChildren<ScrollRect>(true);
            for (var i = 0; i < scrolls.Length; i++)
            {
                var scroll = scrolls[i];
                if (scroll != null)
                    scroll.enabled = active;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>
    /// 升级详情等级进度：Slider 表示当前等级，竖线预制体按上限等分放置。
    /// 竖线位于 1/max、2/max … (max-1)/max；已达成的竖线隐藏。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeUpgradeLevelProgressUi : MonoBehaviour
    {
        [SerializeField] private Slider levelSlider;
        [SerializeField] private RectTransform tickContainer;
        [SerializeField] private RectTransform tickPrefab;
        [SerializeField] private float tickWidth = 2f;
        [Tooltip("竖线上下内边距（像素）；0 表示与容器同高。")]
        [SerializeField] private float tickTopInset;
        [SerializeField] private float tickBottomInset;

        readonly List<RectTransform> _ticks = new List<RectTransform>();

        void Reset()
        {
            levelSlider = GetComponentInChildren<Slider>();
            if (tickContainer == null && levelSlider != null)
                tickContainer = levelSlider.fillRect != null
                    ? levelSlider.fillRect.parent as RectTransform
                    : levelSlider.GetComponent<RectTransform>();
        }

        void Awake()
        {
            if (tickContainer == null && levelSlider != null)
                tickContainer = levelSlider.fillRect != null
                    ? levelSlider.fillRect.parent as RectTransform
                    : levelSlider.GetComponent<RectTransform>();

            ConfigureSlider();
        }

        void ConfigureSlider()
        {
            if (levelSlider == null)
                return;

            levelSlider.minValue = 0f;
            levelSlider.maxValue = 1f;
            levelSlider.interactable = false;
        }

        public void Refresh(int currentLevel, int maxLevel)
        {
            if (maxLevel <= 0)
            {
                SetSliderValue(0f);
                HideAllTicks();
                return;
            }

            var clampedLevel = Mathf.Clamp(currentLevel, 0, maxLevel);
            SetSliderValue((float)clampedLevel / maxLevel);
            EnsureTicks(maxLevel);
            LayoutTicks(maxLevel);
            RefreshTickVisibility(clampedLevel, maxLevel);
        }

        void SetSliderValue(float normalized)
        {
            if (levelSlider == null)
                return;

            levelSlider.value = Mathf.Clamp01(normalized);
        }

        void EnsureTicks(int maxLevel)
        {
            if (tickContainer == null || tickPrefab == null || maxLevel <= 1)
            {
                HideAllTicks();
                return;
            }

            var required = maxLevel - 1;
            while (_ticks.Count < required)
            {
                var tick = Instantiate(tickPrefab, tickContainer);
                tick.gameObject.SetActive(true);
                _ticks.Add(tick);
            }

            for (var i = 0; i < _ticks.Count; i++)
            {
                var visible = i < required;
                if (_ticks[i] != null)
                    _ticks[i].gameObject.SetActive(visible);
            }
        }

        void LayoutTicks(int maxLevel)
        {
            var width = Mathf.Max(2f, tickWidth);
            for (var i = 0; i < _ticks.Count; i++)
            {
                var tick = _ticks[i];
                if (tick == null || i >= maxLevel - 1)
                    continue;

                var ratio = (i + 1f) / maxLevel;
                // 垂直拉伸 + 固定宽度；忽略父级 Layout 对宽度的压缩。
                tick.anchorMin = new Vector2(ratio, 0f);
                tick.anchorMax = new Vector2(ratio, 1f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.anchoredPosition = Vector2.zero;
                tick.offsetMin = new Vector2(0f, tickBottomInset);
                tick.offsetMax = new Vector2(0f, -tickTopInset);
                tick.localScale = Vector3.one;
                tick.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

                var layoutElement = tick.GetComponent<LayoutElement>();
                if (layoutElement == null)
                    layoutElement = tick.gameObject.AddComponent<LayoutElement>();

                layoutElement.ignoreLayout = true;
                layoutElement.minWidth = width;
                layoutElement.preferredWidth = width;
                layoutElement.flexibleWidth = 0f;
            }
        }

        void RefreshTickVisibility(int currentLevel, int maxLevel)
        {
            for (var i = 0; i < _ticks.Count; i++)
            {
                if (_ticks[i] == null)
                    continue;

                if (i >= maxLevel - 1)
                {
                    _ticks[i].gameObject.SetActive(false);
                    continue;
                }

                // 竖线 i 位于 (i+1)/max；当前等级已超过该刻度则隐藏。
                var passed = currentLevel > i + 1;
                _ticks[i].gameObject.SetActive(!passed);
            }
        }

        void HideAllTicks()
        {
            for (var i = 0; i < _ticks.Count; i++)
            {
                if (_ticks[i] != null)
                    _ticks[i].gameObject.SetActive(false);
            }
        }
    }
}

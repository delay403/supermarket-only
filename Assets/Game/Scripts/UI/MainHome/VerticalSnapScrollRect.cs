using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.MainHome
{
    /// <summary>
    /// 垂直 ScrollRect：拖拽结束或惯性接近停止时，吸附到距视口中心最近的子项。
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class VerticalSnapScrollRect : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private float snapDuration = 0.1f;
        [SerializeField] private float velocitySnapThreshold = 120f;
        [SerializeField] private float centerTolerance = 8f;

        readonly List<RectTransform> _items = new List<RectTransform>();
        Coroutine _snapRoutine;
        bool _dragging;
        bool _settled = true;
        int _selectedIndex = -1;

        public event Action<int> SelectionChanged;

        public int SelectedIndex => _selectedIndex;

        void Awake()
        {
            if (scrollRect == null)
                scrollRect = GetComponent<ScrollRect>();
            if (viewport == null && scrollRect != null)
                viewport = scrollRect.viewport;
            if (content == null && scrollRect != null)
                content = scrollRect.content;
        }

        void OnDisable()
        {
            if (_snapRoutine != null)
            {
                StopCoroutine(_snapRoutine);
                _snapRoutine = null;
            }
        }

        void LateUpdate()
        {
            if (_dragging || _snapRoutine != null || _settled || scrollRect == null)
                return;

            if (scrollRect.velocity.sqrMagnitude > velocitySnapThreshold * velocitySnapThreshold)
                return;

            BeginSnapToNearest(true);
        }

        public void RebuildItems()
        {
            _items.Clear();
            if (content == null)
                return;

            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;
                if (child.GetComponent<LayoutElement>() != null && child.GetComponent<MainHomeGameModeItemView>() == null)
                    continue;
                if (child.GetComponent<MainHomeGameModeItemView>() == null)
                    continue;
                _items.Add(child);
            }
        }

        public void SnapToIndex(int index, bool immediate)
        {
            RebuildItems();
            if (_items.Count == 0)
                return;

            index = Mathf.Clamp(index, 0, _items.Count - 1);

            if (immediate)
            {
                if (_snapRoutine != null)
                {
                    StopCoroutine(_snapRoutine);
                    _snapRoutine = null;
                }

                PrepareLayoutForSnap();
                ApplySnapOffset(_items[index]);
                SetSelectedIndex(index, false);
                scrollRect.velocity = Vector2.zero;
                _settled = true;
                return;
            }

            _settled = false;
            BeginSnapToIndex(index);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _settled = false;
            if (_snapRoutine != null)
            {
                StopCoroutine(_snapRoutine);
                _snapRoutine = null;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            _settled = false;
            BeginSnapToNearest(true);
        }

        void BeginSnapToNearest(bool fromUserDrag)
        {
            RebuildItems();
            if (_items.Count == 0)
                return;

            var nearest = FindNearestIndex();
            if (_settled && _selectedIndex == nearest && IsItemCentered(_items[nearest]))
                return;

            BeginSnapToIndex(nearest, fromUserDrag);
        }

        void BeginSnapToIndex(int index, bool fromUserDrag = true)
        {
            if (_snapRoutine != null)
            {
                StopCoroutine(_snapRoutine);
                _snapRoutine = null;
            }

            _snapRoutine = StartCoroutine(SnapRoutine(index, fromUserDrag));
        }

        IEnumerator SnapRoutine(int index, bool fromUserDrag)
        {
            RebuildItems();
            if (_items.Count == 0)
                yield break;

            index = Mathf.Clamp(index, 0, _items.Count - 1);
            var target = _items[index];
            scrollRect.velocity = Vector2.zero;
            PrepareLayoutForSnap();

            var start = content.anchoredPosition;
            ApplySnapOffset(target);
            var end = content.anchoredPosition;
            content.anchoredPosition = start;

            if (snapDuration <= 0f)
            {
                content.anchoredPosition = end;
            }
            else
            {
                var elapsed = 0f;
                while (elapsed < snapDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / snapDuration);
                    t = 1f - (1f - t) * (1f - t);
                    content.anchoredPosition = Vector2.Lerp(start, end, t);
                    yield return null;
                }

                content.anchoredPosition = end;
            }

            PrepareLayoutForSnap();
            ApplySnapOffset(target);
            scrollRect.velocity = Vector2.zero;
            SetSelectedIndex(index, fromUserDrag);
            _snapRoutine = null;
            _settled = true;
        }

        void PrepareLayoutForSnap()
        {
            if (content == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        void ApplySnapOffset(RectTransform item)
        {
            if (viewport == null || content == null || item == null)
                return;

            PrepareLayoutForSnap();

            var itemCenter = item.TransformPoint(item.rect.center);
            var viewportCenter = viewport.TransformPoint(viewport.rect.center);
            var deltaWorld = viewportCenter - itemCenter;
            var deltaLocal = viewport.InverseTransformVector(deltaWorld);
            var deltaY = deltaLocal.y;
            if (Mathf.Abs(deltaY) <= centerTolerance * 0.25f)
                return;

            var pos = content.anchoredPosition;
            pos.y += deltaY;
            content.anchoredPosition = pos;
        }

        int FindNearestIndex()
        {
            if (_items.Count == 0)
                return 0;

            var viewportCenter = viewport.TransformPoint(viewport.rect.center);
            var bestIndex = 0;
            var bestDist = float.MaxValue;
            for (var i = 0; i < _items.Count; i++)
            {
                var itemCenter = _items[i].TransformPoint(_items[i].rect.center);
                var dist = Mathf.Abs(itemCenter.y - viewportCenter.y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        void SetSelectedIndex(int index, bool notify)
        {
            if (_selectedIndex == index)
                return;

            _selectedIndex = index;
            if (notify)
                SelectionChanged?.Invoke(_selectedIndex);
        }

        bool IsItemCentered(RectTransform item)
        {
            if (item == null || viewport == null)
                return false;

            var itemCenter = item.TransformPoint(item.rect.center);
            var viewportCenter = viewport.TransformPoint(viewport.rect.center);
            return Mathf.Abs(itemCenter.y - viewportCenter.y) <= centerTolerance;
        }
    }
}

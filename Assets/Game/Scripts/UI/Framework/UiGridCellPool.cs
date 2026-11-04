using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Framework
{
    /// <summary>
    /// UGUI 列表格子池：复用 Cell，避免每次打开面板 Destroy/Instantiate 造成卡顿与 GC。
    /// </summary>
    public sealed class UiGridCellPool<T> where T : Component
    {
        public const int DefaultBatchPerFrame = 4;

        readonly Transform _parent;
        readonly T _prefab;
        readonly List<T> _inactive = new List<T>();

        public UiGridCellPool(Transform parent, T prefab)
        {
            _parent = parent;
            _prefab = prefab;
        }

        public void EnsureActiveCount(int count, List<T> active)
        {
            if (active == null)
                return;

            Shrink(count, active);

            var layout = _parent != null ? _parent.GetComponent<LayoutGroup>() : null;
            var layoutWasEnabled = layout != null && layout.enabled;
            if (layout != null)
                layout.enabled = false;

            while (active.Count < count)
                active.Add(Acquire());

            for (var i = 0; i < active.Count; i++)
            {
                var cell = active[i];
                if (cell != null && !cell.gameObject.activeSelf)
                    cell.gameObject.SetActive(true);
            }

            RestoreLayout(layout, layoutWasEnabled);
        }

        public IEnumerator EnsureActiveCountBatched(int count, List<T> active, int perFrame = DefaultBatchPerFrame)
        {
            if (active == null)
                yield break;

            if (perFrame <= 0)
                perFrame = DefaultBatchPerFrame;

            Shrink(count, active);

            var layout = _parent != null ? _parent.GetComponent<LayoutGroup>() : null;
            var layoutWasEnabled = layout != null && layout.enabled;
            if (layout != null)
                layout.enabled = false;

            while (active.Count < count)
            {
                var added = 0;
                while (active.Count < count && added < perFrame)
                {
                    active.Add(Acquire());
                    added++;
                }

                if (active.Count < count)
                    yield return null;
            }

            for (var i = 0; i < active.Count; i++)
            {
                var cell = active[i];
                if (cell != null && !cell.gameObject.activeSelf)
                    cell.gameObject.SetActive(true);
            }

            RestoreLayout(layout, layoutWasEnabled);
        }

        void Shrink(int count, List<T> active)
        {
            while (active.Count > count)
            {
                var last = active.Count - 1;
                Release(active[last]);
                active.RemoveAt(last);
            }
        }

        T Acquire()
        {
            T cell;
            if (_inactive.Count > 0)
            {
                var last = _inactive.Count - 1;
                cell = _inactive[last];
                _inactive.RemoveAt(last);
            }
            else
            {
                cell = Object.Instantiate(_prefab, _parent);
            }

            if (cell != null)
                cell.gameObject.SetActive(true);
            return cell;
        }

        void Release(T cell)
        {
            if (cell == null)
                return;
            cell.gameObject.SetActive(false);
            _inactive.Add(cell);
        }

        static void RestoreLayout(LayoutGroup layout, bool layoutWasEnabled)
        {
            if (layout == null)
                return;

            layout.enabled = layoutWasEnabled || true;
            if (layout.transform is RectTransform rt)
                LayoutRebuilder.MarkLayoutForRebuild(rt);
        }

        public void DisposeAll(List<T> active)
        {
            if (active != null)
            {
                for (var i = 0; i < active.Count; i++)
                {
                    var cell = active[i];
                    if (cell != null)
                        Object.Destroy(cell.gameObject);
                }

                active.Clear();
            }

            for (var i = 0; i < _inactive.Count; i++)
            {
                var cell = _inactive[i];
                if (cell != null)
                    Object.Destroy(cell.gameObject);
            }

            _inactive.Clear();
        }
    }
}

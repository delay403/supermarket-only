using System.Collections.Generic;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>
    /// 单个难度的场景内容：地图根节点 + 货架列表。
    /// 货架数量与商品数量由 <see cref="SupermarketContainerZone"/> 配置推导。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeDifficultySetup : MonoBehaviour
    {
        [SerializeField] private string difficultyId;
        [SerializeField] private GameObject[] mapRoots;
        [SerializeField] private SupermarketContainerZone[] shelves;
        [SerializeField] private bool autoCollectShelvesFromMapRoots = true;

        [Tooltip("本难度实际启用的货架比例（1 = 全部；0.2 = 只保留 20% 货架与商品，用于快速调整难度）。")]
        [SerializeField, Range(0.05f, 1f)] private float activeShelfRatio = 1f;

        readonly HashSet<GameObject> _resolvedContentRoots = new HashSet<GameObject>();

        public string DifficultyId => string.IsNullOrWhiteSpace(difficultyId)
            ? string.Empty
            : difficultyId.Trim();

        public void ResolveReferences()
        {
            if (autoCollectShelvesFromMapRoots && mapRoots != null && mapRoots.Length > 0)
            {
                var collected = new List<SupermarketContainerZone>();
                for (var i = 0; i < mapRoots.Length; i++)
                {
                    var root = mapRoots[i];
                    if (root == null)
                        continue;

                    root.GetComponentsInChildren(true, collected);
                }

                if (collected.Count > 0)
                    shelves = collected.ToArray();
            }

            if (shelves == null || shelves.Length == 0)
                shelves = GetComponentsInChildren<SupermarketContainerZone>(true);
        }

        public void SetContentVisible(bool visible)
        {
            if (!visible)
                HideAllTips();

            if (mapRoots != null && mapRoots.Length > 0)
            {
                for (var i = 0; i < mapRoots.Length; i++)
                {
                    if (mapRoots[i] != null)
                        mapRoots[i].SetActive(visible);
                }
            }
            else
            {
                ResolveContentRoots();
                foreach (var root in _resolvedContentRoots)
                {
                    if (root != null)
                        root.SetActive(visible);
                }
            }

            if (visible)
                ApplyActiveShelfVisibility();

            gameObject.SetActive(visible);
        }

        /// <summary>按 <see cref="activeShelfRatio"/> 只显示参与本难度的货架，其余隐藏以免误触/误瞄。</summary>
        void ApplyActiveShelfVisibility()
        {
            ResolveReferences();
            if (shelves == null)
                return;

            var activeCount = ResolveActiveShelfCount();
            for (var i = 0; i < shelves.Length; i++)
            {
                var shelf = shelves[i];
                if (shelf != null)
                    shelf.gameObject.SetActive(i < activeCount);
            }
        }

        int ResolveActiveShelfCount()
        {
            if (shelves == null || shelves.Length == 0)
                return 0;

            var ratio = Mathf.Clamp01(activeShelfRatio);
            if (ratio >= 0.999f)
                return shelves.Length;

            return Mathf.Clamp(Mathf.CeilToInt(shelves.Length * ratio), 1, shelves.Length);
        }

        void ResolveContentRoots()
        {
            _resolvedContentRoots.Clear();
            ResolveReferences();
            if (shelves == null)
                return;

            for (var i = 0; i < shelves.Length; i++)
            {
                var root = ResolveShelfContentRoot(shelves[i]);
                if (root != null)
                    _resolvedContentRoots.Add(root);
            }
        }

        static GameObject ResolveShelfContentRoot(SupermarketContainerZone zone)
        {
            if (zone == null)
                return null;

            Transform containerRoot = null;
            var current = zone.transform;
            while (current != null)
            {
                if (current.name.StartsWith("Containers"))
                    containerRoot = current;

                current = current.parent;
            }

            return containerRoot != null ? containerRoot.gameObject : zone.gameObject;
        }

        void HideAllTips()
        {
            ResolveReferences();
            if (shelves == null)
                return;

            for (var i = 0; i < shelves.Length; i++)
                shelves[i]?.SetTipVisible(false);
        }

        public void ComputeCounts(out int shelfCount, out int itemCount)
        {
            var active = GetShelves();
            shelfCount = 0;
            itemCount = 0;

            for (var i = 0; i < active.Count; i++)
            {
                var zone = active[i];
                if (zone == null)
                    continue;

                shelfCount++;
                itemCount += zone.Capacity;
            }
        }

        /// <summary>本难度实际启用的货架（受 <see cref="activeShelfRatio"/> 裁剪）。</summary>
        public IReadOnlyList<SupermarketContainerZone> GetShelves()
        {
            ResolveReferences();
            if (shelves == null || shelves.Length == 0)
                return System.Array.Empty<SupermarketContainerZone>();

            var activeCount = ResolveActiveShelfCount();
            if (activeCount >= shelves.Length)
                return shelves;

            var result = new SupermarketContainerZone[activeCount];
            System.Array.Copy(shelves, result, activeCount);
            return result;
        }
    }
}

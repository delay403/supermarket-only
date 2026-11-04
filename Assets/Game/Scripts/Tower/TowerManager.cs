using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Core.Config;
using Game.Maps;
using Game.Save;

namespace Game.Tower
{
    public sealed class TowerManager : MonoBehaviour
    {
        private const int MidSegmentsPerGroup = 2;

        [Header("Prefabs (optional, otherwise primitives)")]
        public GameObject towerBasePrefab;
        [Tooltip("Legacy single mid prefab. Used when Tower Mid Prefabs is empty.")]
        public GameObject towerMidPrefab;
        [Tooltip("Colored mid segments in order. Every 5 consecutive segments share one prefab (1–5 → [0], 6–10 → [1], …). Cycles after the last entry. At summit, a partial group (<5) is kept as-is.")]
        public GameObject[] towerMidPrefabs;
        [Tooltip("有限塔高时铺在最高一格（替换最后一个中间段）。可在预制体上挂 TowerSummitChestZone（Trigger + 箱盖）。")]
        public GameObject towerTopPrefab;

        [Header("Placement")]
        public Transform towerRoot;
        public float baseY = 0f;

        [Header("Height")]
        [Tooltip("旧逻辑保留字段：塔是否无限延伸改由当前地图 MapDef.maxTowerHeight（>0 为有限顶）与 maxHeightOverride 决定。")]
        public bool infiniteHeight = true;
        [Tooltip("Optional: force a finite height limit (0 = use map config).")]
        public float maxHeightOverride = 0f;

        private readonly Dictionary<int, GameObject> _activeMids = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Queue<GameObject>> _midPoolsBySlot = new Dictionary<int, Queue<GameObject>>();
        private readonly Dictionary<GameObject, int> _midInstanceSlot = new Dictionary<GameObject, int>();

        private GameObject _baseInstance;
        private GameObject _topInstance;

        private float _segmentHeight;
        private float _visibleBack;
        private float _visibleForward;
        private int _poolCount;
        private float _maxHeight;
        private int _summitSegmentIndex = 1;

        /// <summary>本地图会话内：玩家已在塔顶通过 HUD「跳跃」完成过一次登顶滑翔（同地图重生不重置；换地图或读档刷新配置时重置）。为 false 时在塔顶禁止自动进入滑翔/空翻。</summary>
        public bool SummitGlideCompletedForSession { get; private set; }

        private void Awake()
        {
            if (towerRoot == null) towerRoot = transform;

            var bal = ConfigRegistry.Balance;
            _segmentHeight = bal != null ? bal.segmentHeight : 10f;
            _visibleBack = bal != null ? bal.visibleRangeBack : 40f;
            _visibleForward = bal != null ? bal.visibleRangeForward : 60f;
            _poolCount = bal != null ? bal.pooledSegmentCount : 40;

            RefreshMaxHeightFromSave();

            EnsureBase();
            WarmPool();
        }

        void Start() => RefreshFromCurrentMapConfig();

        /// <summary>
        /// 切换当前地图配置但仍在同一场景时调用（或直接依赖存档刷新塔上限）。
        /// </summary>
        public void RefreshFromCurrentMapConfig()
        {
            RefreshMaxHeightFromSave();
            UpdateTopIfNeeded();
            ResetSummitChestZonesOnTop();
        }

        void ResetSummitChestZonesOnTop()
        {
            if (_topInstance == null)
                return;
            var zones = _topInstance.GetComponentsInChildren<TowerSummitChestZone>(true);
            for (var i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null)
                    zones[i].ResetForNewMapConfiguration();
            }
        }

        void RefreshMaxHeightFromSave()
        {
            _maxHeight = 0f;
            SummitGlideCompletedForSession = false;
            var mapDb = ConfigRegistry.Maps;
            if (SaveService.Data == null)
                return;

            if (mapDb == null)
            {
                Debug.LogWarning(
                    $"{nameof(TowerManager)}: ConfigRegistry.Maps 为空，无法应用地图塔高；请在场景中放置并配置 {nameof(GameBootstrapper)} 或 Resources/{nameof(MapDatabase)}。");
                return;
            }

            var mapId = SaveService.Data.currentMapId ?? string.Empty;
            if (!mapDb.TryGet(mapId, out var map) || map == null)
            {
                Debug.LogWarning(
                    $"{nameof(TowerManager)}: 存档 currentMapId='{mapId}' 在 MapDatabase 中无条目，塔高度视为无限（攀爬不会封顶）。请检查 MapDef.mapId 是否与存档一致。");
                return;
            }

            _maxHeight = Mathf.Max(0f, map.maxTowerHeight);
            if (maxHeightOverride > 0f)
                _maxHeight = maxHeightOverride;

            RecomputeSummitSegmentIndex();
        }

        /// <summary>由塔顶宝箱 HUD「跳跃」调用：允许之后在塔顶自动/正常进入滑翔。</summary>
        public void MarkSummitGlideCompletedForSession()
        {
            SummitGlideCompletedForSession = true;
        }

        void RecomputeSummitSegmentIndex()
        {
            if (_maxHeight <= 0f)
            {
                _summitSegmentIndex = 1;
                return;
            }

            _summitSegmentIndex = Mathf.Max(1, Mathf.FloorToInt(_maxHeight / Mathf.Max(1e-4f, _segmentHeight)));
        }

        public float BaseY => baseY;
        public float SegmentHeight => _segmentHeight;
        public float MaxHeight => _maxHeight;
        public int ActiveMidCount => _activeMids.Count;
        public int PooledMidCount
        {
            get
            {
                var count = 0;
                foreach (var pool in _midPoolsBySlot.Values)
                    count += pool.Count;
                return count;
            }
        }

        private void EnsureBase()
        {
            if (_baseInstance != null) return;
            _baseInstance = InstantiateOrPrimitive(towerBasePrefab, PrimitiveType.Cylinder);
            _baseInstance.name = "TowerBase";
            _baseInstance.transform.SetParent(towerRoot, false);
            _baseInstance.transform.localPosition = new Vector3(0f, baseY, 0f);
        }

        private int MidPrefabSlotCount =>
            towerMidPrefabs != null && towerMidPrefabs.Length > 0 ? towerMidPrefabs.Length : 1;

        private int GetMidPrefabSlotForSegment(int segmentIndex)
        {
            var groupIndex = (segmentIndex - 1) / MidSegmentsPerGroup;
            return groupIndex % MidPrefabSlotCount;
        }

        private GameObject GetMidPrefabForSlot(int slot)
        {
            if (towerMidPrefabs != null && towerMidPrefabs.Length > 0)
                return towerMidPrefabs[Mathf.Clamp(slot, 0, towerMidPrefabs.Length - 1)];
            return towerMidPrefab;
        }

        private Queue<GameObject> GetMidPoolForSlot(int slot)
        {
            if (!_midPoolsBySlot.TryGetValue(slot, out var pool))
            {
                pool = new Queue<GameObject>();
                _midPoolsBySlot[slot] = pool;
            }

            return pool;
        }

        private void WarmPool()
        {
            var slotCount = MidPrefabSlotCount;
            var perSlot = Mathf.Max(1, _poolCount / slotCount);
            for (var slot = 0; slot < slotCount; slot++)
            {
                var pool = GetMidPoolForSlot(slot);
                for (var i = 0; i < perSlot; i++)
                {
                    var go = CreateMidInstance(slot);
                    go.transform.SetParent(towerRoot, false);
                    go.SetActive(false);
                    pool.Enqueue(go);
                }
            }
        }

        private GameObject CreateMidInstance(int slot)
        {
            var go = InstantiateOrPrimitive(GetMidPrefabForSlot(slot), PrimitiveType.Cylinder);
            _midInstanceSlot[go] = slot;
            return go;
        }

        private GameObject GetMidFromPool(int slot)
        {
            var pool = GetMidPoolForSlot(slot);
            if (pool.Count > 0)
            {
                var go = pool.Dequeue();
                go.SetActive(true);
                return go;
            }

            return CreateMidInstance(slot);
        }

        private void ReturnMidToPool(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            if (!_midInstanceSlot.TryGetValue(go, out var slot))
                slot = 0;

            var pool = GetMidPoolForSlot(slot);
            var maxPerSlot = Mathf.Max(1, _poolCount / MidPrefabSlotCount);
            if (pool.Count < maxPerSlot)
                pool.Enqueue(go);
            else
            {
                _midInstanceSlot.Remove(go);
                Destroy(go);
            }
        }

        private static GameObject InstantiateOrPrimitive(GameObject prefab, PrimitiveType primitiveType)
        {
            if (prefab != null) return Instantiate(prefab);
            var go = GameObject.CreatePrimitive(primitiveType);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            return go;
        }

        public void UpdateVisibility(float playerY)
        {
            var minY = playerY - _visibleBack;
            var maxY = playerY + _visibleForward;

            // Segment index starts after base: 1..N at y = baseY + index*segmentHeight
            var minIndex = Mathf.FloorToInt((minY - baseY) / _segmentHeight);
            var maxIndex = Mathf.CeilToInt((maxY - baseY) / _segmentHeight);
            if (minIndex < 1) minIndex = 1;
            if (maxIndex < 1) maxIndex = 1;

            RecomputeSummitSegmentIndex();

            // 有限高度：中间段只铺到「顶段」前一根；最上一格用 towerTopPrefab（与中间段同网格位置逻辑）。
            if (_maxHeight > 0f)
            {
                var maxMidIndex = _summitSegmentIndex - 1;
                var cap = maxMidIndex < 1 ? 0 : maxMidIndex;
                maxIndex = Mathf.Min(maxIndex, cap);
            }

            // Deactivate out of window
            var toRemove = ListPool<int>.Get();
            foreach (var kv in _activeMids)
            {
                var idx = kv.Key;
                if (idx < minIndex || idx > maxIndex)
                    toRemove.Add(idx);
            }
            foreach (var idx in toRemove)
            {
                ReturnMidToPool(_activeMids[idx]);
                _activeMids.Remove(idx);
            }
            ListPool<int>.Release(toRemove);

            // Activate needed
            for (var idx = minIndex; idx <= maxIndex; idx++)
            {
                if (_activeMids.ContainsKey(idx)) continue;
                var prefabSlot = GetMidPrefabSlotForSegment(idx);
                var mid = GetMidFromPool(prefabSlot);
                mid.name = $"TowerMid_{idx}";
                mid.transform.SetParent(towerRoot, false);
                mid.transform.localPosition = new Vector3(0f, baseY + idx * _segmentHeight, 0f);
                _activeMids[idx] = mid;
            }

            UpdateTopIfNeeded();
        }

        private void UpdateTopIfNeeded()
        {
            if (_maxHeight <= 0f)
            {
                if (_topInstance != null)
                {
                    Destroy(_topInstance);
                    _topInstance = null;
                }
                return;
            }

            if (_topInstance == null)
            {
                _topInstance = InstantiateOrPrimitive(towerTopPrefab, PrimitiveType.Cylinder);
                _topInstance.name = "TowerTop";
                _topInstance.transform.SetParent(towerRoot, false);
            }

            // 与最后一段中间节对齐：占用「下一格」索引位置（替换原本最后一节中间段）。
            _topInstance.transform.localPosition = new Vector3(0f, baseY + _summitSegmentIndex * _segmentHeight, 0f);
        }
    }

    internal static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new Stack<List<T>>();
        public static List<T> Get() => Pool.Count > 0 ? Pool.Pop() : new List<T>();
        public static void Release(List<T> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}


using System.Collections.Generic;
using UnityEngine;
using Game.Core.Config;

namespace Game.Glide
{
    /// <summary>
    /// Endless Z-axis runway segments with pooling (tower-style).
    /// </summary>
    public sealed class RunwayManager : MonoBehaviour
    {
        [Header("Prefabs (optional, otherwise primitives)")]
        public GameObject runwaySegmentPrefab;

        [Header("Placement")]
        public Transform runwayRoot;
        public float baseZ = 0f;
        [Tooltip("World-space X position of runway center. If 0, uses runwayRoot.position.x.")]
        public float centerXOverride = 0f;

        public float CenterXWorld => Mathf.Abs(centerXOverride) > 0.0001f ? centerXOverride : (runwayRoot != null ? runwayRoot.position.x : 0f);

        private readonly Dictionary<int, GameObject> _active = new Dictionary<int, GameObject>();
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();

        private float _segmentLength;
        private float _visibleBack;
        private float _visibleForward;
        private int _poolCount;
        private Vector3 _segmentLocalScaleDefault = Vector3.one;

        private Transform _impactSegment;
        private Vector3 _impactBaseScale;
        private float _impactElapsed;
        private float _impactDuration;
        private float _impactSquash;

        private void Awake()
        {
            if (runwayRoot == null) runwayRoot = transform;

            if (runwaySegmentPrefab != null)
                _segmentLocalScaleDefault = runwaySegmentPrefab.transform.localScale;

            var bal = ConfigRegistry.Balance;
            _segmentLength = bal != null ? bal.runwaySegmentLength : 12f;
            _visibleBack = bal != null ? bal.runwayVisibleRangeBack : 60f;
            _visibleForward = bal != null ? bal.runwayVisibleRangeForward : 120f;
            _poolCount = bal != null ? bal.runwayPooledSegmentCount : 60;

            WarmPool();
        }

        private void Update()
        {
            if (_impactSegment == null)
                return;

            _impactElapsed += Time.deltaTime;
            var u = Mathf.Clamp01(_impactElapsed / Mathf.Max(0.001f, _impactDuration));
            var wave = Mathf.Sin(u * Mathf.PI);
            var yMul = 1f - _impactSquash * wave;
            var xzMul = 1f + _impactSquash * 0.55f * wave;
            _impactSegment.localScale = new Vector3(
                _impactBaseScale.x * xzMul,
                _impactBaseScale.y * yMul,
                _impactBaseScale.z * xzMul);

            if (u >= 1f)
            {
                _impactSegment.localScale = _impactBaseScale;
                _impactSegment = null;
            }
        }

        /// <summary>角色落地砸击：对落点附近的跑道分段做短暂压扁/外扩形变（流星坠地感）。</summary>
        public void PlayLandingImpact(float worldZ, float skidSpeedMps)
        {
            var intensity = Mathf.Clamp01(skidSpeedMps / 28f);
            if (intensity < 0.04f || _segmentLength <= 0.01f)
                return;

            if (_impactSegment != null)
            {
                _impactSegment.localScale = _impactBaseScale;
                _impactSegment = null;
            }

            var idx = Mathf.FloorToInt((worldZ - baseZ) / _segmentLength);
            if (idx < 0 || !_active.TryGetValue(idx, out var seg) || seg == null)
                return;

            _impactSegment = seg.transform;
            _impactBaseScale = _impactSegment.localScale;
            _impactElapsed = 0f;
            _impactDuration = Mathf.Lerp(0.14f, 0.28f, intensity);
            _impactSquash = Mathf.Lerp(0.06f, 0.14f, intensity);
        }

        private void WarmPool()
        {
            for (var i = 0; i < _poolCount; i++)
            {
                var go = InstantiateOrPrimitive(runwaySegmentPrefab, PrimitiveType.Cube);
                go.name = "RunwaySeg_Pooled";
                go.transform.SetParent(runwayRoot, false);
                go.SetActive(false);
                _pool.Enqueue(go);
            }
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0)
            {
                var go = _pool.Dequeue();
                go.SetActive(true);
                return go;
            }
            return InstantiateOrPrimitive(runwaySegmentPrefab, PrimitiveType.Cube);
        }

        private void ReturnToPool(GameObject go)
        {
            if (go == null) return;
            if (_impactSegment == go.transform)
            {
                go.transform.localScale = _impactBaseScale;
                _impactSegment = null;
            }

            go.transform.localScale = _segmentLocalScaleDefault;
            go.SetActive(false);
            if (_pool.Count < _poolCount) _pool.Enqueue(go);
            else Destroy(go);
        }

        private static GameObject InstantiateOrPrimitive(GameObject prefab, PrimitiveType primitiveType)
        {
            if (prefab != null) return Instantiate(prefab);
            var go = GameObject.CreatePrimitive(primitiveType);
            return go;
        }

        public void UpdateVisibility(float playerZ)
        {
            var minZ = playerZ - _visibleBack;
            var maxZ = playerZ + _visibleForward;

            var minIndex = Mathf.FloorToInt((minZ - baseZ) / _segmentLength);
            var maxIndex = Mathf.CeilToInt((maxZ - baseZ) / _segmentLength);
            // Only show segments after runwayRoot/baseZ (no negative/backward runway).
            if (minIndex < 0) minIndex = 0;
            if (maxIndex < 0) maxIndex = 0;

            var toRemove = ListPool<int>.Get();
            foreach (var kv in _active)
            {
                var idx = kv.Key;
                if (idx < minIndex || idx > maxIndex)
                    toRemove.Add(idx);
            }
            foreach (var idx in toRemove)
            {
                ReturnToPool(_active[idx]);
                _active.Remove(idx);
            }
            ListPool<int>.Release(toRemove);

            for (var idx = minIndex; idx <= maxIndex; idx++)
            {
                if (_active.ContainsKey(idx)) continue;
                var seg = GetFromPool();
                seg.name = $"RunwaySeg_{idx}";
                seg.transform.SetParent(runwayRoot, false);
                seg.transform.localPosition = new Vector3(0f, 0f, baseZ + idx * _segmentLength);
                seg.transform.localScale = _segmentLocalScaleDefault;
                _active[idx] = seg;
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
}


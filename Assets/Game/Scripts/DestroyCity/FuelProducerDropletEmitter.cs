using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>燃料生产器液滴发射与对象池。</summary>
    [DisallowMultipleComponent]
    public sealed class FuelProducerDropletEmitter : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private FuelProductionDroplet dropletPrefab;
        [SerializeField] private int poolSize = 8;
        [SerializeField] private float moveSpeed = 4f;
        [Tooltip("命中燃料时落在目标点上方偏移（米）。")]
        [SerializeField] private float targetHeightOffset = 0.15f;

        readonly Queue<FuelProductionDroplet> _pool = new Queue<FuelProductionDroplet>();
        Transform _poolRoot;
        bool _initialized;

        public Transform SpawnPoint
        {
            get => spawnPoint;
            set => spawnPoint = value;
        }

        void Awake()
        {
            WarmPool();
        }

        /// <summary>向每个燃料实例各发射一滴液滴，抵达后按 instanceId 回调（避免视图被销毁后访问悬空引用）。</summary>
        public void EmitToFuelTargets(
            IReadOnlyList<FuelInstanceView> fuelViews,
            Func<FuelInstanceView, Vector3> resolveTargetPosition,
            Action<string> onDropletArrived = null)
        {
            if (dropletPrefab == null || fuelViews == null || fuelViews.Count == 0)
                return;

            WarmPool();
            var origin = spawnPoint != null ? spawnPoint.position : transform.position;

            for (var i = 0; i < fuelViews.Count; i++)
            {
                var view = fuelViews[i];
                if (view == null)
                    continue;

                var instanceId = view.InstanceId;
                var target = (resolveTargetPosition != null
                    ? resolveTargetPosition(view)
                    : view.transform.position) + Vector3.up * targetHeightOffset;

                var droplet = Rent();
                droplet.LaunchToTarget(
                    origin,
                    target,
                    moveSpeed,
                    () =>
                    {
                        if (!string.IsNullOrWhiteSpace(instanceId))
                            onDropletArrived?.Invoke(instanceId);
                    },
                    Return);
            }
        }

        void WarmPool()
        {
            if (_initialized || dropletPrefab == null)
                return;

            _initialized = true;
            if (_poolRoot == null)
            {
                var rootGo = new GameObject("DropletPool");
                rootGo.transform.SetParent(transform, false);
                _poolRoot = rootGo.transform;
            }

            var count = Mathf.Max(1, poolSize);
            for (var i = _pool.Count; i < count; i++)
                CreatePooledInstance();
        }

        FuelProductionDroplet Rent()
        {
            if (_pool.Count == 0)
                CreatePooledInstance();
            return _pool.Dequeue();
        }

        void CreatePooledInstance()
        {
            var inst = Instantiate(dropletPrefab, _poolRoot);
            inst.gameObject.SetActive(false);
            _pool.Enqueue(inst);
        }

        void Return(FuelProductionDroplet droplet)
        {
            if (droplet == null)
                return;
            droplet.gameObject.SetActive(false);
            _pool.Enqueue(droplet);
        }
    }
}

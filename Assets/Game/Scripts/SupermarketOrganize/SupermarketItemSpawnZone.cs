using System.Collections.Generic;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>
    /// 物品散落区域（可放置多个拼接成不规则范围）。
    /// 在 XZ 平面按本地半尺寸随机采样，支持通过 Transform 旋转/缩放调整形状。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketItemSpawnZone : MonoBehaviour
    {
        [SerializeField] private Vector3 localHalfExtents = new Vector3(3f, 0f, 3f);
        [SerializeField] private float groundLocalY;

        public Vector3 LocalHalfExtents => new Vector3(
            Mathf.Max(0.05f, localHalfExtents.x),
            localHalfExtents.y,
            Mathf.Max(0.05f, localHalfExtents.z));

        public float SampleWeight
        {
            get
            {
                var scale = transform.lossyScale;
                return LocalHalfExtents.x * Mathf.Abs(scale.x)
                       * LocalHalfExtents.z * Mathf.Abs(scale.z)
                       * 4f;
            }
        }

        public bool TrySampleWorldPoint(System.Random rng, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (rng == null)
                return false;

            var half = LocalHalfExtents;
            var localX = ((float)rng.NextDouble() * 2f - 1f) * half.x;
            var localZ = ((float)rng.NextDouble() * 2f - 1f) * half.z;
            var sampled = transform.TransformPoint(new Vector3(localX, groundLocalY, localZ));
            // 仅使用 XZ；高度由 Spawner 贴地射线统一计算，避免区域 Transform 高度不同。
            worldPoint = new Vector3(sampled.x, sampled.y, sampled.z);
            return true;
        }

        public bool TrySampleWorldXZ(System.Random rng, out Vector3 worldXZ)
        {
            if (!TrySampleWorldPoint(rng, out var worldPoint))
            {
                worldXZ = default;
                return false;
            }

            worldXZ = new Vector3(worldPoint.x, 0f, worldPoint.z);
            return true;
        }

        public static bool TrySampleFromZones(
            IReadOnlyList<SupermarketItemSpawnZone> zones,
            System.Random rng,
            out Vector3 worldPoint)
        {
            worldPoint = default;
            if (zones == null || zones.Count == 0 || rng == null)
                return false;

            var totalWeight = 0f;
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null || !zone.isActiveAndEnabled)
                    continue;

                totalWeight += zone.SampleWeight;
            }

            if (totalWeight <= 1e-4f)
                return false;

            var roll = (float)rng.NextDouble() * totalWeight;
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null || !zone.isActiveAndEnabled)
                    continue;

                roll -= zone.SampleWeight;
                if (roll > 0f)
                    continue;

                return zone.TrySampleWorldPoint(rng, out worldPoint);
            }

            for (var i = zones.Count - 1; i >= 0; i--)
            {
                var zone = zones[i];
                if (zone == null || !zone.isActiveAndEnabled)
                    continue;

                return zone.TrySampleWorldPoint(rng, out worldPoint);
            }

            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.85f);
            var matrix = transform.localToWorldMatrix;
            Gizmos.matrix = matrix;
            Gizmos.DrawWireCube(new Vector3(0f, groundLocalY, 0f), LocalHalfExtents * 2f);

            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.12f);
            Gizmos.DrawCube(new Vector3(0f, groundLocalY, 0f), LocalHalfExtents * 2f);
        }
    }
}

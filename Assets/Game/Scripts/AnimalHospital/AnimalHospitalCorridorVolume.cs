using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 走廊子区域：多个体积可拼接成不规则走廊范围（对齐超市收纳物品生成区）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalCorridorVolume : MonoBehaviour
    {
        [SerializeField] private Vector3 localHalfExtents = new Vector3(2f, 0f, 4f);
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
            worldPoint = new Vector3(sampled.x, sampled.y, sampled.z);
            return true;
        }

        void OnDrawGizmos()
        {
            AnimalHospitalZoneGizmoUtility.DrawLocalXZVolume(
                transform,
                new Vector3(0f, groundLocalY, 0f),
                LocalHalfExtents,
                new Color(0.95f, 0.55f, 0.15f, 0.35f),
                new Color(0.95f, 0.55f, 0.15f, 0.06f));
        }

        void OnDrawGizmosSelected()
        {
            AnimalHospitalZoneGizmoUtility.DrawLocalXZVolume(
                transform,
                new Vector3(0f, groundLocalY, 0f),
                LocalHalfExtents,
                new Color(0.95f, 0.55f, 0.15f, 0.95f),
                new Color(0.95f, 0.55f, 0.15f, 0.18f));
        }
    }
}

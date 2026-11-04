using System.Collections.Generic;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>走廊区域：粘液/黑影随机刷新；支持多个子体积拼接不规则范围。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalCorridorZone : MonoBehaviour
    {
        [SerializeField] private AnimalHospitalCorridorVolume[] volumes = System.Array.Empty<AnimalHospitalCorridorVolume>();
        [SerializeField] private Transform[] spawnPoints = System.Array.Empty<Transform>();
        [SerializeField] private Collider boundsCollider;

        public IReadOnlyList<AnimalHospitalCorridorVolume> Volumes => volumes;

        public Vector3 GetRandomPoint()
        {
            if (TrySampleFromVolumes(volumes, out var volumePoint))
                return AnimalHospitalNavMeshUtility.SampleOrKeep(volumePoint, 4f);

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var t = spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (t != null)
                    return AnimalHospitalNavMeshUtility.SampleOrKeep(t.position, 4f);
            }

            if (boundsCollider != null)
            {
                var b = boundsCollider.bounds;
                var raw = new Vector3(
                    Random.Range(b.min.x, b.max.x),
                    b.center.y,
                    Random.Range(b.min.z, b.max.z));
                return AnimalHospitalNavMeshUtility.SampleOrKeep(raw, 6f);
            }

            return AnimalHospitalNavMeshUtility.SampleOrKeep(transform.position, 6f);
        }

        public static bool TrySampleFromVolumes(
            IReadOnlyList<AnimalHospitalCorridorVolume> zoneVolumes,
            out Vector3 worldPoint)
        {
            worldPoint = default;
            if (zoneVolumes == null || zoneVolumes.Count == 0)
                return false;

            var totalWeight = 0f;
            for (var i = 0; i < zoneVolumes.Count; i++)
            {
                var volume = zoneVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                totalWeight += volume.SampleWeight;
            }

            if (totalWeight <= 1e-4f)
                return false;

            var rng = new System.Random();
            var roll = (float)rng.NextDouble() * totalWeight;
            for (var i = 0; i < zoneVolumes.Count; i++)
            {
                var volume = zoneVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                roll -= volume.SampleWeight;
                if (roll > 0f)
                    continue;

                return volume.TrySampleWorldPoint(rng, out worldPoint);
            }

            for (var i = zoneVolumes.Count - 1; i >= 0; i--)
            {
                var volume = zoneVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                return volume.TrySampleWorldPoint(rng, out worldPoint);
            }

            return false;
        }

        void OnValidate()
        {
            if (volumes == null || volumes.Length == 0)
                volumes = GetComponentsInChildren<AnimalHospitalCorridorVolume>(true);
        }

        void OnDrawGizmos()
        {
            if (spawnPoints != null)
            {
                for (var i = 0; i < spawnPoints.Length; i++)
                {
                    AnimalHospitalZoneGizmoUtility.DrawPoint(
                        spawnPoints[i],
                        new Color(0.95f, 0.55f, 0.15f, 0.45f),
                        0.16f);
                }
            }

            if (boundsCollider != null)
            {
                AnimalHospitalZoneGizmoUtility.DrawWorldBounds(
                    boundsCollider.bounds,
                    new Color(0.95f, 0.55f, 0.15f, 0.35f),
                    new Color(0.95f, 0.55f, 0.15f, 0.05f));
            }
        }

        void OnDrawGizmosSelected()
        {
            if (spawnPoints != null)
            {
                for (var i = 0; i < spawnPoints.Length; i++)
                {
                    AnimalHospitalZoneGizmoUtility.DrawPoint(
                        spawnPoints[i],
                        new Color(0.95f, 0.55f, 0.15f, 0.95f),
                        0.2f);
                }
            }

            if (boundsCollider != null)
            {
                AnimalHospitalZoneGizmoUtility.DrawWorldBounds(
                    boundsCollider.bounds,
                    new Color(0.95f, 0.55f, 0.15f, 0.95f),
                    new Color(0.95f, 0.55f, 0.15f, 0.15f));
            }
        }
    }
}

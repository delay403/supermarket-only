using System.Collections.Generic;
using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// 根据楼层判定区的世界高度自动聚类并分配层号（1 = 最低平台）。
    /// </summary>
    public static class WormFloorHeightIndex
    {
        public const float DefaultHeightTolerance = 4f;

        public static float SampleZoneWorldHeight(Component zone)
        {
            if (zone == null)
                return 0f;

            var col = zone.GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                var center = box.transform.TransformPoint(box.center);
                var halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
                return center.y - halfExtents.y;
            }

            if (col != null)
                return col.bounds.min.y;

            return zone.transform.position.y;
        }

        public static int ResolveFloorIndex(
            float worldHeight,
            IReadOnlyList<float> bandHeightsAscending,
            float tolerance = DefaultHeightTolerance)
        {
            if (bandHeightsAscending == null || bandHeightsAscending.Count == 0)
                return 1;

            tolerance = Mathf.Max(0.5f, tolerance);

            var bestIndex = 1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < bandHeightsAscending.Count; i++)
            {
                var distance = Mathf.Abs(worldHeight - bandHeightsAscending[i]);
                if (distance > tolerance || distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestIndex = i + 1;
            }

            if (bestDistance <= tolerance)
                return bestIndex;

            if (worldHeight < bandHeightsAscending[0] - tolerance)
                return 1;

            if (worldHeight > bandHeightsAscending[bandHeightsAscending.Count - 1] + tolerance)
                return bandHeightsAscending.Count;

            bestIndex = 1;
            bestDistance = float.MaxValue;
            for (var i = 0; i < bandHeightsAscending.Count; i++)
            {
                var distance = Mathf.Abs(worldHeight - bandHeightsAscending[i]);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestIndex = i + 1;
            }

            return bestIndex;
        }

        public static List<float> BuildBandHeightsAscending(
            IReadOnlyList<float> sampleHeights,
            float tolerance = DefaultHeightTolerance)
        {
            var bands = new List<float>();
            if (sampleHeights == null || sampleHeights.Count == 0)
                return bands;

            tolerance = Mathf.Max(0.5f, tolerance);

            var sorted = new List<float>(sampleHeights.Count);
            for (var i = 0; i < sampleHeights.Count; i++)
                sorted.Add(sampleHeights[i]);
            sorted.Sort();

            for (var i = 0; i < sorted.Count; i++)
            {
                var height = sorted[i];
                var merged = false;
                for (var b = 0; b < bands.Count; b++)
                {
                    if (Mathf.Abs(height - bands[b]) > tolerance)
                        continue;

                    bands[b] = (bands[b] + height) * 0.5f;
                    merged = true;
                    break;
                }

                if (!merged)
                    bands.Add(height);
            }

            bands.Sort();
            return bands;
        }

#if UNITY_EDITOR
        public static int ResolveFloorIndexInEditor(Component zone, float tolerance = DefaultHeightTolerance)
        {
            if (zone == null)
                return 1;

            var heights = CollectAllFloorZoneHeightsInEditor();
            var bands = BuildBandHeightsAscending(heights, tolerance);
            return ResolveFloorIndex(SampleZoneWorldHeight(zone), bands, tolerance);
        }

        public static int ResolveRewardFloorIndexInEditor(Component zone, float tolerance = DefaultHeightTolerance) =>
            ResolveFloorIndexInEditor(zone, tolerance);

        static List<float> CollectAllFloorZoneHeightsInEditor()
        {
            var heights = new List<float>();

#if UNITY_2023_1_OR_NEWER
            var settlementZones = Object.FindObjectsByType<WormFloorSettlementZone>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var rewardZones = Object.FindObjectsByType<WormDodgeFloorRewardZone>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
#else
            var settlementZones = Object.FindObjectsOfType<WormFloorSettlementZone>(true);
            var rewardZones = Object.FindObjectsOfType<WormDodgeFloorRewardZone>(true);
#endif
            for (var i = 0; i < settlementZones.Length; i++)
            {
                if (settlementZones[i] != null)
                    heights.Add(SampleZoneWorldHeight(settlementZones[i]));
            }

            for (var i = 0; i < rewardZones.Length; i++)
            {
                if (rewardZones[i] != null)
                    heights.Add(SampleZoneWorldHeight(rewardZones[i]));
            }

            return heights;
        }
#endif
    }
}

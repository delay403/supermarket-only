using System.Collections.Generic;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 求鱼线与水面（<see cref="WaterEdgeLayerName"/> trigger 或水平面 Y）的首次接触点。
    /// </summary>
    public static class FishingWaterSurfaceContact
    {
        public const string WaterEdgeLayerName = "WaterEdge";

        const float PlaneEpsilon = 0.02f;

        static int _waterEdgeLayerMask = int.MinValue;

        public static int WaterEdgeLayerMask
        {
            get
            {
                if (_waterEdgeLayerMask == int.MinValue)
                {
                    var layer = LayerMask.NameToLayer(WaterEdgeLayerName);
                    _waterEdgeLayerMask = layer >= 0 ? 1 << layer : 0;
                }

                return _waterEdgeLayerMask;
            }
        }

        public static bool TryGetPolylineContact(
            IReadOnlyList<Vector3> linePoints,
            float waterSurfaceY,
            out Vector3 contactWorld)
        {
            contactWorld = default;
            if (linePoints == null || linePoints.Count < 2)
                return false;

            for (var i = 0; i < linePoints.Count - 1; i++)
            {
                if (TryGetSegmentContact(linePoints[i], linePoints[i + 1], waterSurfaceY, out contactWorld))
                    return true;
            }

            return false;
        }

        public static bool TryGetSegmentContact(
            Vector3 fromWorld,
            Vector3 toWorld,
            float waterSurfaceY,
            out Vector3 contactWorld)
        {
            contactWorld = default;

            var mask = WaterEdgeLayerMask;
            if (mask != 0 && Physics.Linecast(
                    fromWorld,
                    toWorld,
                    out var hit,
                    mask,
                    QueryTriggerInteraction.Collide))
            {
                contactWorld = hit.point;
                return true;
            }

            if (TrySegmentPlaneYIntersection(fromWorld, toWorld, waterSurfaceY, out contactWorld))
                return true;

            return false;
        }

        static bool TrySegmentPlaneYIntersection(Vector3 a, Vector3 b, float planeY, out Vector3 hit)
        {
            hit = default;

            var dy = b.y - a.y;
            if (Mathf.Abs(dy) < 1e-5f)
            {
                if (Mathf.Abs(a.y - planeY) <= PlaneEpsilon)
                {
                    hit = new Vector3(a.x, planeY, a.z);
                    return true;
                }

                return false;
            }

            var crossesSurface = (a.y >= planeY - PlaneEpsilon && b.y <= planeY + PlaneEpsilon)
                                 || (a.y <= planeY + PlaneEpsilon && b.y >= planeY - PlaneEpsilon);
            if (!crossesSurface)
                return false;

            var t = (planeY - a.y) / dy;
            if (t < -0.001f || t > 1.001f)
                return false;

            hit = Vector3.Lerp(a, b, Mathf.Clamp01(t));
            hit.y = planeY;
            return true;
        }
    }
}

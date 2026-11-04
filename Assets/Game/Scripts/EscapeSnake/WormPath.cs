using System;
using UnityEngine;

namespace Game.EscapeSnake
{
    /// <summary>
    /// 多节虫专用路径：由若干路点组成，支持弧长采样与任意 3D 直线段（斜坡、台阶等）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormPath : MonoBehaviour
    {
        public enum PathMode
        {
            Once,
            Loop,
            PingPong
        }

        [SerializeField] private Transform[] waypoints;
        [SerializeField] private PathMode pathMode = PathMode.PingPong;
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.85f, 0.35f, 0.9f);
        [SerializeField] private float gizmoNodeRadius = 0.25f;

        float[] _segmentLengths;
        float[] _cumulativeDistances;
        float _totalLength;
        bool _cacheDirty = true;

        public PathMode Mode => pathMode;
        public int WaypointCount => waypoints != null ? waypoints.Length : 0;
        public float TotalLength
        {
            get
            {
                RebuildCacheIfNeeded();
                return _totalLength;
            }
        }

        public Vector3 GetWaypointWorld(int index)
        {
            if (waypoints == null || waypoints.Length == 0)
                return transform.position;

            index = WrapIndex(index);
            var wp = waypoints[index];
            return wp != null ? wp.position : transform.position;
        }

        public float GetCumulativeDistance(int waypointIndex)
        {
            RebuildCacheIfNeeded();
            if (_cumulativeDistances == null || _cumulativeDistances.Length == 0)
                return 0f;

            waypointIndex = Mathf.Clamp(waypointIndex, 0, _cumulativeDistances.Length - 1);
            return _cumulativeDistances[waypointIndex];
        }

        /// <summary>求离世界坐标最近的路点索引。</summary>
        public int FindNearestWaypointIndex(Vector3 worldPos)
        {
            if (waypoints == null || waypoints.Length == 0)
                return 0;

            var best = 0;
            var bestDist = float.MaxValue;
            for (var i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                    continue;

                var d = (waypoints[i].position - worldPos).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }

        public bool TryGetSegmentDirection(int fromIndex, int directionSign, out Vector3 unitDir)
        {
            unitDir = Vector3.forward;
            if (waypoints == null || waypoints.Length < 2)
                return false;

            var toIndex = fromIndex + directionSign;
            if (pathMode == PathMode.Loop)
                toIndex = WrapIndex(toIndex);
            else if (toIndex < 0 || toIndex >= waypoints.Length)
                return false;

            var from = GetWaypointWorld(fromIndex);
            var to = GetWaypointWorld(toIndex);
            var delta = to - from;
            if (delta.sqrMagnitude < 1e-6f)
                return false;

            unitDir = delta.normalized;
            return true;
        }

        /// <summary>按弧长采样路径位置与切线方向（distance=0 为起点路点）。</summary>
        public Vector3 SampleAtDistance(float distance, out Vector3 tangent)
        {
            RebuildCacheIfNeeded();
            tangent = Vector3.forward;

            if (waypoints == null || waypoints.Length == 0)
                return transform.position;

            if (waypoints.Length == 1)
                return GetWaypointWorld(0);

            if (_totalLength < 1e-5f)
                return GetWaypointWorld(0);

            distance = MapDistanceForMode(distance);

            var remaining = distance;
            var segmentCount = GetForwardSegmentCount();
            for (var i = 0; i < segmentCount; i++)
            {
                var segLen = _segmentLengths[i];
                if (segLen < 1e-6f)
                    continue;

                if (remaining <= segLen)
                {
                    var t = remaining / segLen;
                    var a = GetWaypointWorld(i);
                    var b = GetWaypointWorld(i + 1);
                    tangent = (b - a).normalized;
                    return Vector3.Lerp(a, b, t);
                }

                remaining -= segLen;
            }

            var last = GetWaypointWorld(waypoints.Length - 1);
            if (segmentCount > 0)
                tangent = (last - GetWaypointWorld(waypoints.Length - 2)).normalized;
            return last;
        }

        public Vector3 GetTangentAtDistance(float distance)
        {
            SampleAtDistance(distance, out var tangent);
            return tangent;
        }

        /// <summary>求世界坐标在路径折线上的最近弧长位置，用于追目标结束后回到原路径。</summary>
        public float FindClosestDistance(Vector3 worldPos)
        {
            RebuildCacheIfNeeded();
            if (waypoints == null || waypoints.Length < 2 || _totalLength < 1e-5f)
                return 0f;

            var bestSqr = float.MaxValue;
            var bestDistance = 0f;
            var accumulated = 0f;
            var segmentCount = GetForwardSegmentCount();

            for (var i = 0; i < segmentCount; i++)
            {
                var a = GetWaypointWorld(i);
                var b = GetWaypointWorld(i + 1);
                var segLen = _segmentLengths[i];
                if (segLen < 1e-6f)
                    continue;

                var closest = ClosestPointOnSegment(worldPos, a, b, out var t);
                var sqr = (closest - worldPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestDistance = accumulated + t * segLen;
                }

                accumulated += segLen;
            }

            return MapDistanceForMode(bestDistance);
        }

        /// <summary>在离世界坐标最近的路径弧长与先前进度之间取合理恢复点，避免明显倒退。</summary>
        public float GetResumeDistance(Vector3 worldPos, float previousDistance)
        {
            var closest = FindClosestDistance(worldPos);
            if (pathMode == PathMode.Loop)
                return closest;

            return Mathf.Clamp(Mathf.Max(closest, previousDistance * 0.85f), 0f, _totalLength);
        }

        static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b, out float t)
        {
            var ab = b - a;
            var lenSqr = ab.sqrMagnitude;
            if (lenSqr < 1e-8f)
            {
                t = 0f;
                return a;
            }

            t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lenSqr);
            return a + ab * t;
        }

        public int WrapIndex(int index)
        {
            var count = WaypointCount;
            if (count <= 0)
                return 0;

            if (pathMode == PathMode.Once)
                return Mathf.Clamp(index, 0, count - 1);

            if (pathMode == PathMode.Loop)
            {
                index %= count;
                if (index < 0)
                    index += count;
                return index;
            }

            var period = Mathf.Max(1, count * 2 - 2);
            index %= period;
            if (index < 0)
                index += period;
            if (index >= count)
                index = period - index;
            return index;
        }

        public int AdvanceIndex(int current, int directionSign, out int newDirectionSign)
        {
            newDirectionSign = directionSign;
            var count = WaypointCount;
            if (count <= 1)
                return current;

            var next = current + directionSign;
            if (pathMode == PathMode.Once)
            {
                if (next >= count)
                {
                    newDirectionSign = 0;
                    return count - 1;
                }

                if (next < 0)
                {
                    newDirectionSign = 0;
                    return 0;
                }

                return next;
            }

            if (pathMode == PathMode.Loop)
                return WrapIndex(next);

            if (next >= count)
            {
                newDirectionSign = -1;
                return count - 2;
            }

            if (next < 0)
            {
                newDirectionSign = 1;
                return 1;
            }

            return next;
        }

        void OnValidate() => _cacheDirty = true;

        void RebuildCacheIfNeeded()
        {
            if (!_cacheDirty && _segmentLengths != null)
                return;

            _cacheDirty = false;
            var count = WaypointCount;
            if (count <= 1)
            {
                _segmentLengths = Array.Empty<float>();
                _cumulativeDistances = count > 0 ? new[] { 0f } : Array.Empty<float>();
                _totalLength = 0f;
                return;
            }

            var segCount = GetForwardSegmentCount();
            _segmentLengths = new float[segCount];
            _cumulativeDistances = new float[count];
            _cumulativeDistances[0] = 0f;
            _totalLength = 0f;

            for (var i = 0; i < segCount; i++)
            {
                var len = Vector3.Distance(GetWaypointWorld(i), GetWaypointWorld(i + 1));
                _segmentLengths[i] = len;
                _totalLength += len;
                if (i + 1 < count)
                    _cumulativeDistances[i + 1] = _cumulativeDistances[i] + len;
            }
        }

        int GetForwardSegmentCount()
        {
            var count = WaypointCount;
            if (count <= 1)
                return 0;
            if (pathMode == PathMode.Loop)
                return count;
            return count - 1;
        }

        float MapDistanceForMode(float distance)
        {
            if (_totalLength < 1e-5f)
                return 0f;

            if (pathMode == PathMode.Loop)
            {
                distance %= _totalLength;
                if (distance < 0f)
                    distance += _totalLength;
                return distance;
            }

            return Mathf.Clamp(distance, 0f, _totalLength);
        }

        void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Length == 0)
                return;

            Gizmos.color = gizmoColor;
            for (var i = 0; i < waypoints.Length; i++)
            {
                var a = GetWaypointWorld(i);
                Gizmos.DrawSphere(a, gizmoNodeRadius);
                if (i + 1 < waypoints.Length)
                {
                    var b = GetWaypointWorld(i + 1);
                    Gizmos.DrawLine(a, b);
                }
                else if (pathMode == PathMode.Loop && waypoints.Length > 1)
                {
                    var b = GetWaypointWorld(0);
                    Gizmos.DrawLine(a, b);
                }
            }
        }
    }

    public readonly struct WormTurnDelta
    {
        public readonly float PitchX;
        public readonly float YawZ;

        public WormTurnDelta(float pitchX, float yawZ)
        {
            PitchX = pitchX;
            YawZ = yawZ;
        }

        public bool IsNearlyZero =>
            Mathf.Abs(PitchX) < 0.01f && Mathf.Abs(YawZ) < 0.01f;
    }

    public static class WormPathMath
    {
        const float AxisEpsilon = 0.01f;

        public static Vector3 SnapToAxis(Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-6f)
                return Vector3.forward;

            dir.Normalize();
            var ax = Mathf.Abs(dir.x);
            var ay = Mathf.Abs(dir.y);
            var az = Mathf.Abs(dir.z);
            if (ax >= ay && ax >= az)
                return dir.x >= 0f ? Vector3.right : Vector3.left;
            if (ay >= ax && ay >= az)
                return dir.y >= 0f ? Vector3.up : Vector3.down;
            return dir.z >= 0f ? Vector3.forward : Vector3.back;
        }

        /// <summary>
        /// 计算两段 3D 路径切线之间的关节转角（局部 X 俯仰、局部 Z 偏航）。
        /// 支持斜坡等非轴对齐直线段之间的 90° 转弯。
        /// </summary>
        public static bool TryGetTurnDelta(Vector3 fromDir, Vector3 toDir, float maxAngle, out WormTurnDelta delta)
        {
            delta = default;
            if (fromDir.sqrMagnitude < 1e-6f || toDir.sqrMagnitude < 1e-6f)
                return false;

            fromDir.Normalize();
            toDir.Normalize();
            if (Vector3.Dot(fromDir, toDir) > 0.999f)
                return false;

            var yaw = 0f;
            var fromFlat = new Vector3(fromDir.x, 0f, fromDir.z);
            var toFlat = new Vector3(toDir.x, 0f, toDir.z);
            if (fromFlat.sqrMagnitude > 1e-4f && toFlat.sqrMagnitude > 1e-4f)
            {
                fromFlat.Normalize();
                toFlat.Normalize();
                var crossY = Vector3.Cross(fromFlat, toFlat).y;
                var dotFlat = Mathf.Clamp(Vector3.Dot(fromFlat, toFlat), -1f, 1f);
                var flatAngle = Mathf.Acos(dotFlat) * Mathf.Rad2Deg;
                if (flatAngle > AxisEpsilon)
                    yaw = Mathf.Sign(crossY) * Mathf.Min(maxAngle, flatAngle);
            }

            var fromHoriz = new Vector2(new Vector2(fromDir.x, fromDir.z).magnitude, fromDir.y);
            var toHoriz = new Vector2(new Vector2(toDir.x, toDir.z).magnitude, toDir.y);
            var pitchDelta = Mathf.Atan2(toHoriz.y, toHoriz.x) * Mathf.Rad2Deg
                             - Mathf.Atan2(fromHoriz.y, fromHoriz.x) * Mathf.Rad2Deg;
            pitchDelta = Mathf.DeltaAngle(0f, pitchDelta);
            var pitch = Mathf.Abs(pitchDelta) > AxisEpsilon
                ? Mathf.Clamp(pitchDelta, -maxAngle, maxAngle)
                : 0f;

            delta = new WormTurnDelta(pitch, yaw);
            return !delta.IsNearlyZero;
        }

        public static Quaternion BuildAlignmentRotation(Vector3 chainForward, Vector3 pathTangent, Vector3 upHint)
        {
            if (chainForward.sqrMagnitude < 1e-6f || pathTangent.sqrMagnitude < 1e-6f)
                return Quaternion.identity;

            chainForward.Normalize();
            pathTangent.Normalize();

            var up = upHint.sqrMagnitude > 1e-6f ? upHint.normalized : Vector3.up;
            if (Mathf.Abs(Vector3.Dot(pathTangent, up)) > 0.98f)
                up = Mathf.Abs(pathTangent.y) > 0.9f ? Vector3.forward : Vector3.up;

            var targetRot = Quaternion.LookRotation(pathTangent, up);
            var chainRot = Quaternion.LookRotation(chainForward, up);
            return targetRot * Quaternion.Inverse(chainRot);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Game.EscapeSnake
{
    /// <summary>头部运动轨迹，供追目标时身体节段按弧长回溯采样。</summary>
    sealed class WormTrailSampler
    {
        readonly List<Vector3> _points = new List<Vector3>(32);
        readonly List<float> _distanceFromHead = new List<float>(32);

        public void Clear()
        {
            _points.Clear();
            _distanceFromHead.Clear();
        }

        public void Record(Vector3 headWorld)
        {
            if (_points.Count > 0 && (_points[0] - headWorld).sqrMagnitude < 1e-8f)
                return;

            if (_points.Count == 0)
            {
                _points.Add(headWorld);
                _distanceFromHead.Add(0f);
                return;
            }

            var step = Vector3.Distance(_points[0], headWorld);
            _points.Insert(0, headWorld);
            _distanceFromHead.Insert(0, 0f);
            for (var i = 1; i < _distanceFromHead.Count; i++)
                _distanceFromHead[i] += step;
        }

        public void TrimToLength(float maxDistanceFromHead)
        {
            var buffer = maxDistanceFromHead + 2f;
            for (var i = _distanceFromHead.Count - 1; i >= 0; i--)
            {
                if (_distanceFromHead[i] <= buffer)
                    continue;
                _points.RemoveAt(i);
                _distanceFromHead.RemoveAt(i);
            }
        }

        public bool TrySample(float distanceBehindHead, out Vector3 position, out Vector3 tangent)
        {
            position = Vector3.zero;
            tangent = Vector3.forward;

            if (_points.Count == 0)
                return false;

            if (distanceBehindHead <= 1e-5f)
            {
                position = _points[0];
                tangent = ResolveTangentAt(0);
                return true;
            }

            for (var i = 0; i < _distanceFromHead.Count - 1; i++)
            {
                var d0 = _distanceFromHead[i];
                var d1 = _distanceFromHead[i + 1];
                if (distanceBehindHead < d0 - 1e-5f || distanceBehindHead > d1 + 1e-5f)
                    continue;

                var span = Mathf.Max(1e-5f, d1 - d0);
                var t = Mathf.Clamp01((distanceBehindHead - d0) / span);
                position = Vector3.Lerp(_points[i], _points[i + 1], t);
                tangent = (_points[i] - _points[i + 1]).normalized;
                if (tangent.sqrMagnitude < 1e-6f)
                    tangent = ResolveTangentAt(i);
                return true;
            }

            var last = _points.Count - 1;
            position = _points[last];
            tangent = last > 0 ? (_points[last - 1] - _points[last]).normalized : Vector3.forward;
            return true;
        }

        Vector3 ResolveTangentAt(int index)
        {
            if (_points.Count >= 2 && index < _points.Count - 1)
                return (_points[index] - _points[index + 1]).normalized;
            if (_points.Count >= 2 && index > 0)
                return (_points[index - 1] - _points[index]).normalized;
            return Vector3.forward;
        }
    }
}

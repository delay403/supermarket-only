using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// AI 专用单向路径：索引 0 为起点方向，<see cref="EndWaypointIndex"/> 为终点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeAiPath : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private int endWaypointIndex = -1;
        [SerializeField] private Color gizmoColor = new Color(0.25f, 0.65f, 1f, 0.9f);
        [SerializeField] private Color endGizmoColor = new Color(0.2f, 0.95f, 0.45f, 0.95f);
        [SerializeField] private float gizmoNodeRadius = 0.3f;

        public int WaypointCount => waypoints != null ? waypoints.Length : 0;

        public int StartWaypointIndex => 0;

        public int EndWaypointIndex
        {
            get
            {
                if (WaypointCount == 0)
                    return 0;

                if (endWaypointIndex < 0)
                    return WaypointCount - 1;

                return Mathf.Clamp(endWaypointIndex, 0, WaypointCount - 1);
            }
        }

        public int LastWaypointIndex => EndWaypointIndex;

        public Transform GetWaypointTransform(int index)
        {
            if (waypoints == null || waypoints.Length == 0)
                return transform;

            index = Mathf.Clamp(index, 0, waypoints.Length - 1);
            return waypoints[index] != null ? waypoints[index] : transform;
        }

        public Vector3 GetWaypointWorld(int index) => GetWaypointTransform(index).position;

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

        public int FindForwardResumeWaypointIndex(Vector3 worldPos, int minIndex = 0)
        {
            if (waypoints == null || waypoints.Length == 0)
                return 0;

            minIndex = Mathf.Clamp(minIndex, 0, waypoints.Length - 1);
            var best = minIndex;
            var bestDist = float.MaxValue;
            for (var i = minIndex; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                    continue;

                var d = (waypoints[i].position - worldPos).sqrMagnitude;
                if (d >= bestDist)
                    continue;

                bestDist = d;
                best = i;
            }

            return best;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Length == 0)
                return;

            for (var i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                    continue;

                Gizmos.color = i == EndWaypointIndex ? endGizmoColor : gizmoColor;
                Gizmos.DrawSphere(waypoints[i].position, gizmoNodeRadius);
                if (i + 1 < waypoints.Length && waypoints[i + 1] != null)
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
#endif
    }
}

using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>等候区：椅子列表，病人到达后随机坐入空闲椅子。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalWaitingZone : MonoBehaviour
    {
        [SerializeField] private Transform[] chairs = System.Array.Empty<Transform>();
        [SerializeField] private Transform wanderBoundsCenter;
        [SerializeField] private Vector3 wanderExtents = new Vector3(3f, 0f, 3f);

        readonly System.Collections.Generic.HashSet<Transform> _occupied = new System.Collections.Generic.HashSet<Transform>();

        public bool TryOccupyChair(out Transform chair)
        {
            chair = null;
            if (chairs == null || chairs.Length == 0)
                return false;

            var free = new System.Collections.Generic.List<Transform>();
            for (var i = 0; i < chairs.Length; i++)
            {
                var c = chairs[i];
                if (c != null && !_occupied.Contains(c))
                    free.Add(c);
            }

            if (free.Count == 0)
                return false;

            chair = free[Random.Range(0, free.Count)];
            _occupied.Add(chair);
            return true;
        }

        public void ReleaseChair(Transform chair)
        {
            if (chair != null)
                _occupied.Remove(chair);
        }

        public Vector3 GetRandomWanderPoint()
        {
            var center = wanderBoundsCenter != null ? wanderBoundsCenter.position : transform.position;
            var raw = center + new Vector3(
                Random.Range(-wanderExtents.x, wanderExtents.x),
                0f,
                Random.Range(-wanderExtents.z, wanderExtents.z));
            return AnimalHospitalNavMeshUtility.SampleOrKeep(raw, 6f);
        }

        public Vector3 ResolveChairDestination(Transform chair)
        {
            if (chair == null)
                return GetRandomWanderPoint();

            // 椅子锚点有时刚好不在 NavMesh 上，导致首个病人“卡住不动”。
            // 尝试更大半径采样，失败则回退到等候区漫游点。
            var chairPos = chair.position;
            if (AnimalHospitalNavMeshUtility.TrySamplePosition(chairPos, 10f, out var hit))
                return hit.position;

            return GetRandomWanderPoint();
        }

        void OnDrawGizmos()
        {
            DrawChairs(new Color(0.2f, 0.85f, 0.95f, 0.45f), 0.16f);
            DrawWanderBounds(
                new Color(0.2f, 0.85f, 0.95f, 0.35f),
                new Color(0.2f, 0.85f, 0.95f, 0.06f));
        }

        void OnDrawGizmosSelected()
        {
            DrawChairs(new Color(0.2f, 0.85f, 0.95f, 0.95f), 0.2f);
            DrawWanderBounds(
                new Color(0.2f, 0.85f, 0.95f, 0.95f),
                new Color(0.2f, 0.85f, 0.95f, 0.15f));
        }

        void DrawChairs(Color color, float radius)
        {
            if (chairs == null)
                return;

            for (var i = 0; i < chairs.Length; i++)
                AnimalHospitalZoneGizmoUtility.DrawPoint(chairs[i], color, radius);
        }

        void DrawWanderBounds(Color wireColor, Color fillColor)
        {
            var centerTransform = wanderBoundsCenter != null ? wanderBoundsCenter : transform;
            var half = new Vector3(
                Mathf.Max(0.05f, wanderExtents.x),
                0.15f,
                Mathf.Max(0.05f, wanderExtents.z));
            AnimalHospitalZoneGizmoUtility.DrawLocalXZVolume(
                centerTransform,
                Vector3.zero,
                half,
                wireColor,
                fillColor);
        }
    }
}

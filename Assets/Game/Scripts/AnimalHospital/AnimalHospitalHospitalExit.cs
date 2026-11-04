using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>医院出入口：病人生成与离开的终点。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalHospitalExit : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform leavePoint;

        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;
        public Transform LeavePoint => leavePoint != null ? leavePoint : SpawnPoint;

        public Vector3 ResolveSpawnPosition() =>
            AnimalHospitalNavMeshUtility.SampleOrKeep(SpawnPoint.position, 10f);

        public Vector3 ResolveLeavePosition() =>
            AnimalHospitalNavMeshUtility.SampleOrKeep(LeavePoint.position, 10f);

        void OnDrawGizmos()
        {
            AnimalHospitalZoneGizmoUtility.DrawPoint(SpawnPoint, new Color(0.25f, 0.95f, 0.35f, 0.5f));
            AnimalHospitalZoneGizmoUtility.DrawPoint(LeavePoint, new Color(0.95f, 0.3f, 0.25f, 0.5f));
        }

        void OnDrawGizmosSelected()
        {
            AnimalHospitalZoneGizmoUtility.DrawPoint(SpawnPoint, new Color(0.25f, 0.95f, 0.35f, 1f), 0.28f);
            AnimalHospitalZoneGizmoUtility.DrawPoint(LeavePoint, new Color(0.95f, 0.3f, 0.25f, 1f), 0.28f);
            Gizmos.color = new Color(0.95f, 0.3f, 0.25f, 0.35f);
            Gizmos.DrawLine(SpawnPoint.position, LeavePoint.position);
        }
    }
}

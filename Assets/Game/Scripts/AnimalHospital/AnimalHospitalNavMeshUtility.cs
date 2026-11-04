using UnityEngine;
using UnityEngine.AI;

namespace Game.AnimalHospital
{
    /// <summary>医院玩法 NavMesh 采样与贴地辅助。</summary>
    public static class AnimalHospitalNavMeshUtility
    {
        public const float DefaultSampleRadius = 8f;

        public static bool TrySamplePosition(Vector3 near, float maxDistance, out NavMeshHit hit)
        {
            return NavMesh.SamplePosition(near, out hit, Mathf.Max(0.5f, maxDistance), NavMesh.AllAreas);
        }

        public static Vector3 SampleOrKeep(Vector3 worldPosition, float maxDistance = DefaultSampleRadius)
        {
            // 多半径重试：避免锚点/椅子/门口生成点落在 NavMesh 采样失败区域，
            // 从而导致 NavMeshAgent 路径长期 invalid（表现为“站着不动/不去接待点位”）。
            var radii = new[]
            {
                Mathf.Max(0.5f, maxDistance),
                Mathf.Max(1f, maxDistance * 2f),
                Mathf.Max(2f, maxDistance * 4f),
            };

            for (var i = 0; i < radii.Length; i++)
            {
                var r = radii[i];
                if (TrySamplePosition(worldPosition, r, out var hit))
                    return hit.position;
            }

            return worldPosition;
        }

        public static bool TrySamplePathStart(NavMeshAgent agent, Vector3 near, float maxDistance, out Vector3 result)
        {
            result = near;
            if (agent == null)
                return false;

            if (!agent.enabled)
                agent.enabled = true;

            // Instantiate / Warp 同帧后，仅依赖 isOnNavMesh 往往不可靠；始终 Sample+Warp 重新绑定。
            if (TrySamplePosition(near, maxDistance, out var hit))
            {
                agent.Warp(hit.position);
                result = hit.position;
                return agent.isOnNavMesh;
            }

            if (agent.isOnNavMesh)
            {
                result = agent.nextPosition;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 同步计算并写入路径，避免 Warp/Instantiate 后同帧 SetDestination 出现
        /// PathComplete + hasPath=false + remainingDistance=0 的空路径卡死。
        /// </summary>
        public static bool TrySetPath(NavMeshAgent agent, Vector3 destination, out NavMeshPathStatus status, out int cornerCount)
        {
            status = NavMeshPathStatus.PathInvalid;
            cornerCount = 0;
            if (agent == null || !agent.enabled)
                return false;

            if (!agent.isOnNavMesh)
                return false;

            agent.isStopped = false;
            agent.updatePosition = true;
            agent.updateRotation = true;

            var path = new NavMeshPath();
            var from = agent.nextPosition;
            if (!NavMesh.CalculatePath(from, destination, agent.areaMask, path))
                return false;

            status = path.status;
            cornerCount = path.corners != null ? path.corners.Length : 0;
            if (status == NavMeshPathStatus.PathInvalid || cornerCount < 2)
                return false;

            return agent.SetPath(path);
        }
    }
}

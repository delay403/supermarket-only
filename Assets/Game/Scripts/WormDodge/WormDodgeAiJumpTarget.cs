using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// AI 可跳跃目标点（悬空平台等）。与 <see cref="EscapeSnake.WormSafeZone"/> 类似，
    /// 但不必是安全区；挂上后 AI 在检测半径内会偶尔跳向落点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeAiJumpTarget : MonoBehaviour
    {
        [Tooltip("落点；留空则用本物体位置。")]
        [SerializeField] private Transform landingPoint;

        [Tooltip("仅当 AI 与落点水平距离小于此值时才视为可跳（米）。")]
        [SerializeField] private float maxHorizontalApproachDistance = 18f;

        public Vector3 LandingPosition => landingPoint != null ? landingPoint.position : transform.position;

        public float MaxHorizontalApproachDistance => Mathf.Max(1f, maxHorizontalApproachDistance);

        public bool IsWithinApproachRange(Vector3 fromWorldPos)
        {
            var landing = LandingPosition;
            var planar = landing - fromWorldPos;
            planar.y = 0f;
            var max = MaxHorizontalApproachDistance;
            return planar.sqrMagnitude <= max * max;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var landing = LandingPosition;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(landing, 0.45f);
            Gizmos.DrawLine(transform.position, landing);

            var radius = MaxHorizontalApproachDistance;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.25f);
            Gizmos.DrawWireSphere(landing, radius);
        }
#endif
    }
}

using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>医院各区域点位/范围的可视化 Gizmo 绘制。</summary>
    public static class AnimalHospitalZoneGizmoUtility
    {
        public static void DrawPoint(Transform point, Color color, float radius = 0.22f)
        {
            if (point == null)
                return;

            var position = point.position;
            Gizmos.color = color;
            Gizmos.DrawSphere(position, radius);
            Gizmos.DrawLine(position, position + Vector3.up * 0.75f);
        }

        public static void DrawPoint(Vector3 position, Color color, float radius = 0.22f)
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(position, radius);
            Gizmos.DrawLine(position, position + Vector3.up * 0.75f);
        }

        public static void DrawLocalXZVolume(
            Transform volumeTransform,
            Vector3 localCenter,
            Vector3 localHalfExtents,
            Color wireColor,
            Color fillColor)
        {
            if (volumeTransform == null)
                return;

            var size = new Vector3(
                Mathf.Max(0.05f, localHalfExtents.x) * 2f,
                Mathf.Max(0.05f, localHalfExtents.y) * 2f,
                Mathf.Max(0.05f, localHalfExtents.z) * 2f);

            var matrix = volumeTransform.localToWorldMatrix;
            Gizmos.matrix = matrix;

            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(localCenter, size);

            Gizmos.color = fillColor;
            Gizmos.DrawCube(localCenter, size);

            Gizmos.matrix = Matrix4x4.identity;
        }

        public static void DrawWorldBounds(Bounds bounds, Color wireColor, Color fillColor)
        {
            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            Gizmos.color = fillColor;
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}

using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>动物医院场景区域 Gizmo 绘制辅助。</summary>
    public static class AnimalHospitalGizmoDrawing
    {
        public static void DrawPoint(Transform point, Color color, float radius = 0.28f)
        {
            if (point == null)
                return;

            Gizmos.color = color;
            Gizmos.DrawWireSphere(point.position, radius);
            Gizmos.DrawLine(
                point.position,
                point.position + point.up * (radius * 1.6f));
        }

        public static void DrawLocalBounds(
            Transform anchor,
            Vector3 localCenter,
            Vector3 localSize,
            Color wireColor,
            Color fillColor)
        {
            if (anchor == null)
                return;

            var previous = Gizmos.matrix;
            Gizmos.matrix = anchor.localToWorldMatrix;

            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(localCenter, localSize);

            Gizmos.color = fillColor;
            Gizmos.DrawCube(localCenter, localSize);

            Gizmos.matrix = previous;
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

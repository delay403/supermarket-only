using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>编辑器中绘制核弹爆炸范围 Gizmo（与运行时摧毁判定一致）。</summary>
    public static class NukeExplosionGizmos
    {
        public const int CircleSegments = 72;

        public struct RangeRadii
        {
            public float ConfiguredRadius;
            public float VisualHorizontalRadius;
            public float HouseDestructionRadius;
        }

        public static RangeRadii ComputeRadii(GameObject vfxRoot, float explosionRadius)
        {
            var configured = Mathf.Max(0.01f, explosionRadius);
            var center = vfxRoot != null ? vfxRoot.transform.position : Vector3.zero;
            var visual = NukeExplosionEffect.EstimateVisualHorizontalRadius(vfxRoot, configured, center);
            var houseDestruction = NukeExplosionEffect.EstimateHouseDestructionRadius(vfxRoot, configured, center);

            return new RangeRadii
            {
                ConfiguredRadius = configured,
                VisualHorizontalRadius = visual,
                HouseDestructionRadius = houseDestruction,
            };
        }

        public static void Draw(
            Vector3 center,
            RangeRadii radii,
            Color configuredColor,
            Color visualColor,
            Color houseDestructionColor,
            bool drawConfigured = true,
            bool drawVisual = true,
            bool drawHouseDestruction = true)
        {
            if (drawConfigured)
                DrawHorizontalCircle(center, radii.ConfiguredRadius, configuredColor);

            if (drawVisual)
                DrawHorizontalCircle(center, radii.VisualHorizontalRadius, visualColor);

            if (drawHouseDestruction)
                DrawHorizontalCircle(center, radii.HouseDestructionRadius, houseDestructionColor);

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(center, 0.35f);
        }

        public static void DrawHorizontalCircle(Vector3 center, float radius, Color color)
        {
            if (radius <= 0f)
                return;

            Gizmos.color = color;
            var prev = center + new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= CircleSegments; i++)
            {
                var angle = i / (float)CircleSegments * Mathf.PI * 2f;
                var next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

#if UNITY_EDITOR
        public static void DrawLabel(Vector3 center, RangeRadii radii)
        {
            var text =
                $"配置半径: {radii.ConfiguredRadius:F1}m\n" +
                $"特效水平半径: {radii.VisualHorizontalRadius:F1}m\n" +
                $"房屋摧毁半径: {radii.HouseDestructionRadius:F1}m";
            UnityEditor.Handles.Label(center + Vector3.up * 2f, text);
        }
#endif
    }
}

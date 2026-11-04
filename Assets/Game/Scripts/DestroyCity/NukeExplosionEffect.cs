using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 爆炸物体特效：视觉缩放与房屋摧毁半径使用不同除数，便于分别校准预制体与摧毁范围。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NukeExplosionEffect : MonoBehaviour
    {
        /// <summary>房屋摧毁半径 / 无 Renderer 时的 fallback 使用。</summary>
        public const float HouseDestructionScaleDivisor = 32f;

        /// <summary>爆炸预制体目标 uniform scale = 配置半径 ÷ 该值。</summary>
        public const float VisualScaleDivisor = 50f;

        /// <summary>低等级核弹半径较小时，避免爆炸视觉缩放过小导致看不见。</summary>
        public const float MinVisualUniformScale = 0.45f;

        public const float HouseDestructionRadiusScale = 2f;

        public const int MinMissileLevelForSmallExplosions = 4;
        public const int MaxSmallExplosionCount = 8;

        const float MinSmallExplosionScaleRatio = 0.12f;
        const float MaxSmallExplosionScaleRatio = 0.45f;
        const float SmallExplosionPlacementEpsilon = 0.05f;

        [SerializeField] private float growDuration = 1.4f;
        [SerializeField] private AnimationCurve growCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float lifetimeAfterGrow = 2f;
        [Tooltip("放大起始尺寸 = 目标缩放 × 该比例。")]
        [SerializeField] [Range(0.01f, 1f)] private float startScaleFraction = 0.08f;

        [Header("Editor Gizmo")]
        [SerializeField] private bool drawExplosionRangeGizmo = true;
        [SerializeField] private float editorPreviewExplosionRadius = 50f;
        [SerializeField] private Color gizmoConfiguredColor = new Color(1f, 0.85f, 0.1f, 0.95f);
        [SerializeField] private Color gizmoVisualColor = new Color(0.2f, 0.85f, 1f, 0.85f);
        [SerializeField] private Color gizmoHouseDestructionColor = new Color(1f, 0.25f, 0.15f, 0.95f);

        public float GrowDuration => growDuration;
        public float LifetimeAfterGrow => lifetimeAfterGrow;

        float _targetVisualScale = 1f;
        float _targetDestructionScale = 1f;
        float _explosionRadius;
        bool _visualOnly;
        Coroutine _co;
        readonly List<GameObject> _smallExplosionInstances = new List<GameObject>();

        public static float VisualScaleFromExplosionRadius(float explosionRadius) =>
            Mathf.Max(MinVisualUniformScale, Mathf.Max(0.01f, explosionRadius) / VisualScaleDivisor);

        public static float DestructionScaleFromExplosionRadius(float explosionRadius) =>
            Mathf.Max(0.01f, explosionRadius) / HouseDestructionScaleDivisor;

        public void Play(float explosionRadius)
        {
            _visualOnly = false;
            _explosionRadius = explosionRadius;
            _targetVisualScale = VisualScaleFromExplosionRadius(explosionRadius);
            _targetDestructionScale = DestructionScaleFromExplosionRadius(explosionRadius);
            if (_co != null)
                StopCoroutine(_co);
            _co = StartCoroutine(CoGrow());
        }

        public void PlayVisualScaleOnly(float targetUniformScale)
        {
            _visualOnly = true;
            _explosionRadius = 0f;
            _targetVisualScale = Mathf.Max(0.01f, targetUniformScale);
            _targetDestructionScale = 0f;
            if (_co != null)
                StopCoroutine(_co);
            _co = StartCoroutine(CoGrow());
        }

        public static NukeExplosionEffect Spawn(
            GameObject prefab,
            Vector3 worldPosition,
            float explosionRadius,
            float growDurationOverride = -1f,
            float lifetimeAfterGrowOverride = -1f,
            int missileLevel = 1,
            GameObject smallExplosionPrefab = null)
        {
            if (prefab == null)
                return null;

            var inst = Instantiate(prefab, worldPosition, Quaternion.identity);
            if (!inst.TryGetComponent(out NukeExplosionEffect fx))
                fx = inst.AddComponent<NukeExplosionEffect>();

            if (growDurationOverride > 0f)
                fx.growDuration = growDurationOverride;
            if (lifetimeAfterGrowOverride >= 0f)
                fx.lifetimeAfterGrow = lifetimeAfterGrowOverride;

            fx.Play(explosionRadius);
            fx.TryScheduleSmallExplosions(missileLevel, smallExplosionPrefab ?? prefab);
            return fx;
        }

        public void TryScheduleSmallExplosions(int missileLevel, GameObject smallExplosionPrefab)
        {
            if (_visualOnly || missileLevel < MinMissileLevelForSmallExplosions || smallExplosionPrefab == null)
                return;

            var count = Random.Range(0, MaxSmallExplosionCount + 1);
            if (count <= 0)
                return;

            var bigCenter = transform.position;
            var bigVisualRadius = ResolveBigExplosionVisualRadius(bigCenter);
            if (bigVisualRadius <= SmallExplosionPlacementEpsilon)
                return;

            for (var i = 0; i < count; i++)
            {
                var scaleRatio = Random.Range(MinSmallExplosionScaleRatio, MaxSmallExplosionScaleRatio);
                var targetScale = _targetVisualScale * scaleRatio;
                var smallRadius = bigVisualRadius * scaleRatio;
                if (!TryPickSmallExplosionPosition(bigVisualRadius, smallRadius, out var localOffset))
                    continue;

                var spawnDelay = Random.Range(0f, Mathf.Max(0.05f, growDuration));
                StartCoroutine(CoSpawnSmallExplosion(
                    smallExplosionPrefab,
                    bigCenter + localOffset,
                    targetScale,
                    spawnDelay));
            }
        }

        float ResolveBigExplosionVisualRadius(Vector3 center)
        {
            var radius = EstimateVisualHorizontalRadius(gameObject, _explosionRadius, center);
            if (radius > SmallExplosionPlacementEpsilon)
                return radius;

            return Mathf.Max(SmallExplosionPlacementEpsilon, _explosionRadius * 0.5f);
        }

        static bool TryPickSmallExplosionPosition(
            float bigVisualRadius,
            float smallVisualRadius,
            out Vector3 localOffset)
        {
            localOffset = default;
            var maxDistance = bigVisualRadius - smallVisualRadius - SmallExplosionPlacementEpsilon;
            if (maxDistance <= 0f)
                return false;

            var distance = Random.Range(0f, maxDistance);
            var angle = Random.Range(0f, Mathf.PI * 2f);
            localOffset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            return true;
        }

        IEnumerator CoSpawnSmallExplosion(
            GameObject smallExplosionPrefab,
            Vector3 worldPosition,
            float targetUniformScale,
            float spawnDelay)
        {
            if (spawnDelay > 0f)
                yield return new WaitForSeconds(spawnDelay);

            if (this == null)
                yield break;

            var inst = Instantiate(smallExplosionPrefab, worldPosition, Quaternion.identity);
            _smallExplosionInstances.Add(inst);

            if (!inst.TryGetComponent(out NukeExplosionEffect fx))
                fx = inst.AddComponent<NukeExplosionEffect>();

            fx.growDuration = growDuration;
            fx.growCurve = growCurve;
            fx.startScaleFraction = startScaleFraction;
            fx.lifetimeAfterGrow = 0f;
            fx.PlayVisualScaleOnly(targetUniformScale);
        }

        IEnumerator CoGrow()
        {
            var targetVisual = Vector3.one * _targetVisualScale;
            var startVisual = targetVisual * Mathf.Clamp(startScaleFraction, 0.01f, 1f);
            var startDestructionScale = _targetDestructionScale * Mathf.Clamp(startScaleFraction, 0.01f, 1f);
            transform.localScale = startVisual;

            var duration = Mathf.Max(0.05f, growDuration);
            var elapsed = 0f;
            var center = transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = growCurve != null && growCurve.length > 0
                    ? growCurve.Evaluate(Mathf.Clamp01(elapsed / duration))
                    : Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startVisual, targetVisual, t);

                if (!_visualOnly)
                {
                    var destructionScale = Mathf.Lerp(startDestructionScale, _targetDestructionScale, t);
                    NukeHouseDestructionService.ProcessExplosionWave(
                        center,
                        MeasureHouseDestructionRadiusAtUniformScale(center, destructionScale));
                }

                yield return null;
            }

            transform.localScale = targetVisual;
            if (!_visualOnly)
            {
                NukeHouseDestructionService.ProcessExplosionWave(
                    center,
                    MeasureHouseDestructionRadiusAtUniformScale(center, _targetDestructionScale));
                NukeHouseDestructionService.ScheduleResetSunkHouses();
            }

            if (lifetimeAfterGrow > 0f)
            {
                yield return new WaitForSeconds(lifetimeAfterGrow);
                DestroySmallExplosionInstances();
                Destroy(gameObject);
            }
        }

        void DestroySmallExplosionInstances()
        {
            for (var i = 0; i < _smallExplosionInstances.Count; i++)
            {
                var inst = _smallExplosionInstances[i];
                if (inst != null)
                    Destroy(inst);
            }

            _smallExplosionInstances.Clear();
        }

        void OnDestroy()
        {
            DestroySmallExplosionInstances();
        }

        float MeasureHouseDestructionRadiusAtUniformScale(Vector3 center, float destructionUniformScale)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                return NukeHouseDestructionService.ComputeWorldRadiusFromScale(destructionUniformScale)
                       * HouseDestructionRadiusScale;
            }

            var scaleFactor = destructionUniformScale / Mathf.Max(0.001f, transform.localScale.x);
            var minRadius = float.MaxValue;
            var found = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                var bounds = renderer.bounds;
                var dx = Mathf.Max(center.x - bounds.min.x, bounds.max.x - center.x) * scaleFactor;
                var dz = Mathf.Max(center.z - bounds.min.z, bounds.max.z - center.z) * scaleFactor;
                var radius = Mathf.Max(dx, dz);
                if (radius <= 0f)
                    continue;

                minRadius = Mathf.Min(minRadius, radius);
                found = true;
            }

            if (!found)
            {
                return NukeHouseDestructionService.ComputeWorldRadiusFromScale(destructionUniformScale)
                       * HouseDestructionRadiusScale;
            }

            return minRadius * HouseDestructionRadiusScale;
        }

        public static float EstimateMeshHorizontalRadius(GameObject root, Vector3 center, float targetUniformScale) =>
            EstimateMeshHorizontalOuterRadius(root, targetUniformScale);

        public static float EstimateMeshHorizontalOuterRadius(GameObject root, float targetUniformScale)
        {
            if (root == null)
                return NukeHouseDestructionService.ComputeWorldRadiusFromScale(targetUniformScale);

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return NukeHouseDestructionService.ComputeWorldRadiusFromScale(targetUniformScale);

            var prefabScale = Mathf.Max(0.001f, root.transform.localScale.x);
            var scaleFactor = targetUniformScale / prefabScale;
            var maxRadius = 0f;
            var found = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                var bounds = renderer.bounds;
                var extentX = bounds.extents.x * scaleFactor;
                var extentZ = bounds.extents.z * scaleFactor;
                var radius = Mathf.Max(extentX, extentZ);
                if (radius <= 0f)
                    continue;

                maxRadius = Mathf.Max(maxRadius, radius);
                found = true;
            }

            if (!found)
                return NukeHouseDestructionService.ComputeWorldRadiusFromScale(targetUniformScale);

            return maxRadius;
        }

        public static float EstimateVisualHorizontalRadius(GameObject root, float explosionRadius, Vector3 center)
        {
            var visualScale = VisualScaleFromExplosionRadius(explosionRadius);
            if (root != null && root.transform != null)
            {
                var liveRadius = EstimateLiveInstanceHorizontalOuterRadius(root, center, visualScale);
                if (liveRadius > SmallExplosionPlacementEpsilon)
                    return liveRadius;
            }

            return Mathf.Max(
                SmallExplosionPlacementEpsilon,
                EstimateMeshHorizontalOuterRadius(root, visualScale));
        }

        static float EstimateLiveInstanceHorizontalOuterRadius(GameObject root, Vector3 center, float targetUniformScale)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return 0f;

            var currentScale = Mathf.Max(0.001f, root.transform.localScale.x);
            var scaleFactor = targetUniformScale / currentScale;
            var maxRadius = 0f;
            var found = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                var bounds = renderer.bounds;
                var dx = Mathf.Max(center.x - bounds.min.x, bounds.max.x - center.x) * scaleFactor;
                var dz = Mathf.Max(center.z - bounds.min.z, bounds.max.z - center.z) * scaleFactor;
                var radius = Mathf.Max(dx, dz);
                if (radius <= 0f)
                    continue;

                maxRadius = Mathf.Max(maxRadius, radius);
                found = true;
            }

            return found ? maxRadius : 0f;
        }

        public static float EstimateHouseDestructionRadius(GameObject root, float explosionRadius, Vector3 center)
        {
            var destructionScale = DestructionScaleFromExplosionRadius(explosionRadius);
            return EstimateMeshHorizontalRadius(root, center, destructionScale) * HouseDestructionRadiusScale;
        }

        void DrawExplosionRangeGizmoInternal(float explosionRadius)
        {
            if (!drawExplosionRangeGizmo || explosionRadius <= 0f)
                return;

            var center = transform.position;
            var radii = NukeExplosionGizmos.ComputeRadii(gameObject, explosionRadius);
            NukeExplosionGizmos.Draw(
                center,
                radii,
                gizmoConfiguredColor,
                gizmoVisualColor,
                gizmoHouseDestructionColor);
#if UNITY_EDITOR
            NukeExplosionGizmos.DrawLabel(center, radii);
#endif
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected() => DrawExplosionRangeGizmoInternal(
            Application.isPlaying && _explosionRadius > 0f ? _explosionRadius : editorPreviewExplosionRadius);

        void OnDrawGizmos()
        {
            if (!drawExplosionRangeGizmo)
                return;

            DrawExplosionRangeGizmoInternal(
                Application.isPlaying && _explosionRadius > 0f ? _explosionRadius : editorPreviewExplosionRadius);
        }
#endif
    }
}

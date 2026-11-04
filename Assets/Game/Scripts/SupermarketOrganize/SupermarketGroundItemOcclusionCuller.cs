using System.Collections.Generic;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>
    /// 地面物品遮挡剔除：被其它物体完全挡住时关闭 Renderer/Collider，减轻堆叠区 overdraw 与 batch。
    /// 准星瞄准、手持、扫描高亮物品始终可见。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketGroundItemOcclusionCuller : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SupermarketOrganizeConfig config;
        [SerializeField] private SupermarketOrganizeService service;
        [SerializeField] private SupermarketCrosshairInteractor crosshair;
        [SerializeField] private SupermarketItemHighlightController highlights;

        readonly HashSet<SupermarketItem> _exemptItems = new HashSet<SupermarketItem>();
        readonly Dictionary<SupermarketItem, int> _occludedStreak = new Dictionary<SupermarketItem, int>();
        readonly Plane[] _frustumPlanes = new Plane[6];
        readonly RaycastHit[] _rayHits = new RaycastHit[16];
        readonly List<Vector3> _samplePoints = new List<Vector3>(12);
        readonly List<(Vector3 point, float score)> _cornerScratch = new List<(Vector3, float)>(8);

        float _nextCullTick;
        int _roundRobinIndex;
        bool _invalidateAll;

        public void Configure(
            Camera camera,
            SupermarketOrganizeConfig organizeConfig,
            SupermarketOrganizeService organizeService,
            SupermarketCrosshairInteractor interactor,
            SupermarketItemHighlightController highlightController)
        {
            targetCamera = camera;
            config = organizeConfig;
            service = organizeService;
            crosshair = interactor;
            highlights = highlightController;
            _invalidateAll = true;
        }

        public void InvalidateAll()
        {
            _invalidateAll = true;
            _occludedStreak.Clear();
        }

        void LateUpdate()
        {
            config ??= SupermarketOrganizeConfig.ResolveRuntime();
            if (config == null || !config.enableGroundItemOcclusionCull)
                return;

            if (service == null || !service.IsGameRunning)
                return;

            targetCamera ??= Camera.main;
            if (targetCamera == null)
                return;

            RefreshExemptions();

            if (_invalidateAll)
            {
                ForceAllGroundItemsVisible();
                _invalidateAll = false;
            }

            if (Time.time < _nextCullTick)
                return;

            _nextCullTick = Time.time + Mathf.Max(0.02f, config.occlusionCullUpdateInterval);
            CullGroundItemBatch();
        }

        void RefreshExemptions()
        {
            _exemptItems.Clear();

            if (crosshair != null && crosshair.AimItem != null)
                _exemptItems.Add(crosshair.AimItem);

            var handleZone = service.HandleZone;
            if (handleZone != null)
            {
                var heldItems = handleZone.Items;
                for (var i = 0; i < heldItems.Count; i++)
                {
                    var held = heldItems[i];
                    if (held != null)
                        _exemptItems.Add(held);
                }
            }

            if (highlights != null)
                highlights.CollectExemptItems(_exemptItems);

            foreach (var item in _exemptItems)
            {
                if (item == null)
                    continue;

                _occludedStreak.Remove(item);
                item.ForceGroundVisualVisible();
            }
        }

        void ForceAllGroundItemsVisible()
        {
            var groundItems = service.GroundItems;
            for (var i = 0; i < groundItems.Count; i++)
                groundItems[i]?.ForceGroundVisualVisible();
        }

        void CullGroundItemBatch()
        {
            var groundItems = service.GroundItems;
            if (groundItems == null || groundItems.Count == 0)
                return;

            GeometryUtility.CalculateFrustumPlanes(targetCamera, _frustumPlanes);
            var cameraPosition = targetCamera.transform.position;
            var maxDistance = config.occlusionCullMaxDistance;
            var maxDistanceSqr = maxDistance * maxDistance;
            var perTick = Mathf.Max(8, config.occlusionCullItemsPerTick);
            var processed = 0;

            while (processed < perTick)
            {
                if (_roundRobinIndex >= groundItems.Count)
                    _roundRobinIndex = 0;

                if (groundItems.Count == 0)
                    break;

                var item = groundItems[_roundRobinIndex++];
                processed++;

                if (item == null || item.State != SupermarketItemState.OnGround)
                {
                    _occludedStreak.Remove(item);
                    continue;
                }

                if (_exemptItems.Contains(item))
                    continue;

                var settle = item.GetComponent<SupermarketItemPhysicsSettle>();
                if (settle != null && !settle.IsSettled)
                {
                    _occludedStreak.Remove(item);
                    item.ForceGroundVisualVisible();
                    continue;
                }

                var bounds = item.GetWorldRenderBounds();
                var offset = bounds.center - cameraPosition;
                if (offset.sqrMagnitude > maxDistanceSqr)
                {
                    ApplyOcclusionResult(item, fullyOccluded: true);
                    continue;
                }

                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                {
                    ApplyOcclusionResult(item, fullyOccluded: true);
                    continue;
                }

                var isVisible = IsItemConsideredVisible(item, bounds, cameraPosition);
                ApplyOcclusionResult(item, fullyOccluded: !isVisible);
            }

            PruneOccludedStreak(groundItems);
        }

        bool IsItemConsideredVisible(SupermarketItem item, Bounds bounds, Vector3 cameraPosition)
        {
            BuildCameraFacingSamplePoints(bounds, cameraPosition);
            if (_samplePoints.Count == 0)
                return true;

            LayerMask mask = config != null
                ? config.occlusionCullLayerMask
                : Physics.DefaultRaycastLayers;

            var raySkin = config != null ? config.occlusionCullRaySkin : 0.04f;
            raySkin = Mathf.Clamp(raySkin, 0.01f, 0.2f);

            var visibleCount = 0;
            for (var i = 0; i < _samplePoints.Count; i++)
            {
                if (IsSampleVisible(item, _samplePoints[i], cameraPosition, mask, raySkin))
                    visibleCount++;
            }

            var ratio = config != null ? config.occlusionCullMinVisibleSampleRatio : 0.2f;
            ratio = Mathf.Clamp(ratio, 0.05f, 1f);
            var required = Mathf.Max(1, Mathf.CeilToInt(_samplePoints.Count * ratio));
            return visibleCount >= required;
        }

        void ApplyOcclusionResult(SupermarketItem item, bool fullyOccluded)
        {
            if (item == null)
                return;

            if (!fullyOccluded)
            {
                _occludedStreak[item] = 0;
                item.SetGroundVisualVisible(true);
                return;
            }

            var confirm = config != null ? Mathf.Max(1, config.occlusionCullHideConfirmFrames) : 3;
            _occludedStreak.TryGetValue(item, out var streak);
            streak++;
            _occludedStreak[item] = streak;

            if (streak >= confirm)
                item.SetGroundVisualVisible(false);
        }

        readonly List<SupermarketItem> _streakRemovalBuffer = new List<SupermarketItem>();
        readonly HashSet<SupermarketItem> _groundItemLookup = new HashSet<SupermarketItem>();

        void PruneOccludedStreak(IReadOnlyList<SupermarketItem> groundItems)
        {
            if (_occludedStreak.Count == 0)
                return;

            _groundItemLookup.Clear();
            for (var i = 0; i < groundItems.Count; i++)
            {
                var item = groundItems[i];
                if (item != null)
                    _groundItemLookup.Add(item);
            }

            _streakRemovalBuffer.Clear();
            foreach (var pair in _occludedStreak)
            {
                if (pair.Key == null || pair.Key.State != SupermarketItemState.OnGround
                    || !_groundItemLookup.Contains(pair.Key))
                {
                    _streakRemovalBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < _streakRemovalBuffer.Count; i++)
                _occludedStreak.Remove(_streakRemovalBuffer[i]);
        }

        bool IsSampleVisible(
            SupermarketItem item,
            Vector3 target,
            Vector3 cameraPosition,
            LayerMask mask,
            float raySkin)
        {
            var toTarget = target - cameraPosition;
            var distance = toTarget.magnitude;
            if (distance <= raySkin)
                return true;

            var direction = toTarget / distance;
            var hitCount = Physics.RaycastNonAlloc(
                cameraPosition,
                direction,
                _rayHits,
                distance - raySkin,
                mask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
                return true;

            var nearest = FindNearestHit(_rayHits, hitCount);
            if (nearest.collider == null)
                return true;

            return IsColliderFromItem(nearest.collider, item);
        }

        static RaycastHit FindNearestHit(RaycastHit[] hits, int count)
        {
            var best = hits[0];
            for (var i = 1; i < count; i++)
            {
                if (hits[i].distance < best.distance)
                    best = hits[i];
            }

            return best;
        }

        static bool IsColliderFromItem(Collider collider, SupermarketItem item)
        {
            if (collider == null || item == null)
                return false;

            return collider.transform == item.transform
                   || collider.transform.IsChildOf(item.transform);
        }

        void BuildCameraFacingSamplePoints(Bounds bounds, Vector3 cameraPosition)
        {
            _samplePoints.Clear();
            var center = bounds.center;
            var extents = bounds.extents;

            var toCamera = cameraPosition - center;
            if (toCamera.sqrMagnitude < 1e-8f)
                toCamera = Vector3.forward;
            toCamera.Normalize();

            _samplePoints.Add(center);
            _samplePoints.Add(center + Vector3.up * extents.y * 0.92f);

            _cornerScratch.Clear();
            AppendCorner(_cornerScratch, center, extents, -1, -1, -1, toCamera);
            AppendCorner(_cornerScratch, center, extents, 1, -1, -1, toCamera);
            AppendCorner(_cornerScratch, center, extents, -1, -1, 1, toCamera);
            AppendCorner(_cornerScratch, center, extents, 1, -1, 1, toCamera);
            AppendCorner(_cornerScratch, center, extents, -1, 1, -1, toCamera);
            AppendCorner(_cornerScratch, center, extents, 1, 1, -1, toCamera);
            AppendCorner(_cornerScratch, center, extents, -1, 1, 1, toCamera);
            AppendCorner(_cornerScratch, center, extents, 1, 1, 1, toCamera);
            _cornerScratch.Sort((a, b) => b.score.CompareTo(a.score));

            var sampleBudget = config != null ? Mathf.Clamp(config.occlusionCullRaySamples, 1, 9) : 5;
            var cornerTake = sampleBudget <= 1 ? 0 : sampleBudget <= 3 ? 2 : sampleBudget <= 5 ? 4 : 6;
            for (var i = 0; i < cornerTake && i < _cornerScratch.Count; i++)
                _samplePoints.Add(_cornerScratch[i].point);

            if (sampleBudget >= 5)
            {
                var top = center + Vector3.up * extents.y * 0.75f;
                var right = Vector3.Cross(Vector3.up, toCamera);
                if (right.sqrMagnitude < 1e-6f)
                    right = Vector3.right;
                else
                    right.Normalize();

                _samplePoints.Add(top + toCamera * Mathf.Max(extents.x, extents.z) * 0.65f);
                _samplePoints.Add(top + right * extents.x * 0.55f);
                _samplePoints.Add(top - right * extents.x * 0.55f);
            }
        }

        static void AppendCorner(
            List<(Vector3 point, float score)> list,
            Vector3 center,
            Vector3 extents,
            int sx,
            int sy,
            int sz,
            Vector3 toCamera)
        {
            var offset = new Vector3(sx * extents.x, sy * extents.y, sz * extents.z);
            var point = center + offset;
            var dir = offset.sqrMagnitude > 1e-8f ? offset.normalized : toCamera;
            list.Add((point, Vector3.Dot(dir, toCamera)));
        }
    }
}

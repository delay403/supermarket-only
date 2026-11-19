using System;
using System.Collections.Generic;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>根据货架槽位总数生成物品；可选从天空掉落并启用短暂物理。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketItemSpawner : MonoBehaviour
    {
        [SerializeField] private Transform scatterCenter;
        [SerializeField] private SupermarketItemSpawnZone[] spawnZones;
        [SerializeField] private SupermarketItemDatabase itemDatabase;
        [SerializeField] private SupermarketOrganizeConfig config;
        [SerializeField] private Transform itemsRoot;

        readonly List<SupermarketItem> _spawnedItems = new List<SupermarketItem>();

        public void ClearSpawnedItems()
        {
            for (var i = _spawnedItems.Count - 1; i >= 0; i--)
            {
                var item = _spawnedItems[i];
                if (item != null)
                    Destroy(item.gameObject);
            }

            _spawnedItems.Clear();

            var root = itemsRoot != null ? itemsRoot : transform;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        public IReadOnlyList<SupermarketItem> SpawnAllItems(
            IEnumerable<SupermarketContainerZone> containerZones = null, bool includeDefectiveItems = false)
        {
            ClearSpawnedItems();
            var result = new List<SupermarketItem>();
            itemDatabase ??= SupermarketItemDatabase.ResolveRuntime();
            config ??= SupermarketOrganizeConfig.ResolveRuntime();

            if (itemDatabase == null || itemDatabase.items == null)
                return result;

            var spawnCounts = BuildSpawnCounts(containerZones);
            var rng = new System.Random(config != null ? config.scatterSeed : 0);
            var root = itemsRoot != null ? itemsRoot : transform;
            var activeSpawnZones = ResolveSpawnZones();
            var eligibleDefinitions = new List<SupermarketItemDef>();

            for (var i = 0; i < itemDatabase.items.Count; i++)
            {
                var def = itemDatabase.items[i];
                if (def == null || def.worldPrefab == null || string.IsNullOrWhiteSpace(def.itemId))
                    continue;

                if (!spawnCounts.TryGetValue(def.itemId, out var count) || count <= 0)
                    continue;

                eligibleDefinitions.Add(def);
                for (var n = 0; n < count; n++)
                {
                    result.Add(SpawnItem(def, root, activeSpawnZones, rng, n + 1, false));
                }
            }

            if (includeDefectiveItems && result.Count > 0)
            {
                var qualityRng = new System.Random(unchecked(Environment.TickCount ^ GetInstanceID()));
                var defectiveCount = Mathf.Clamp(Mathf.RoundToInt(result.Count * 0.10f), 1, 8);
                for (var i = 0; i < defectiveCount; i++)
                {
                    var def = eligibleDefinitions[qualityRng.Next(eligibleDefinitions.Count)];
                    result.Add(SpawnItem(def, root, activeSpawnZones, qualityRng, result.Count + 1, true));
                }
            }

            return result;
        }

        SupermarketItem SpawnItem(SupermarketItemDef def, Transform root,
            IReadOnlyList<SupermarketItemSpawnZone> zones, System.Random rng, int index, bool defective)
        {
            var position = SampleSpawnPosition(zones, rng);
            var rotation = SampleSpawnRotation(config, rng);
            var instance = Instantiate(def.worldPrefab, position, rotation, root);
            instance.name = $"{def.itemId}_{index}";
            var item = instance.GetComponent<SupermarketItem>() ?? instance.AddComponent<SupermarketItem>();
            item.Configure(def);
            if (defective)
                item.MarkDefective();
            item.ApplyShadowSetting(def.disableShadows);
            Physics.SyncTransforms();
            SetupPhysicsDrop(item, config, rng);
            _spawnedItems.Add(item);
            return item;
        }

        public int RelocateGroundItems(IReadOnlyList<SupermarketItem> items, System.Random rng = null)
        {
            if (items == null || items.Count == 0)
                return 0;

            config ??= SupermarketOrganizeConfig.ResolveRuntime();
            rng ??= new System.Random(Environment.TickCount ^ (int)(Time.time * 1000f));
            var activeSpawnZones = ResolveSpawnZones();
            var root = itemsRoot != null ? itemsRoot : transform;
            var relocated = 0;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.State != SupermarketItemState.OnGround)
                    continue;

                if (RelocateGroundItem(item, activeSpawnZones, root, rng))
                    relocated++;
            }

            return relocated;
        }

        bool RelocateGroundItem(
            SupermarketItem item,
            IReadOnlyList<SupermarketItemSpawnZone> zones,
            Transform root,
            System.Random rng)
        {
            if (item == null)
                return false;

            item.transform.SetParent(root, true);
            var pos = SampleSpawnPosition(zones, rng);
            var rot = SampleSpawnRotation(config, rng);
            item.transform.SetPositionAndRotation(pos, rot);
            item.ClearPlacement();
            item.SetState(SupermarketItemState.OnGround);
            Physics.SyncTransforms();

            var settle = item.GetComponent<SupermarketItemPhysicsSettle>();
            if (settle == null)
                settle = item.gameObject.AddComponent<SupermarketItemPhysicsSettle>();

            settle.enabled = true;
            EnsureCollider(item);
            item.EnableGroundInteraction();

            if (config != null && config.enableDropSpawn)
                SetupPhysicsDrop(item, config, rng);
            else
                settle.FreezePhysics();

            item.ForceGroundVisualVisible();
            return true;
        }

        static Dictionary<string, int> BuildSpawnCounts(IEnumerable<SupermarketContainerZone> containerZones)
        {
            var counts = new Dictionary<string, int>();
            if (containerZones == null)
                return counts;

            foreach (var zone in containerZones)
            {
                if (zone == null || string.IsNullOrWhiteSpace(zone.TargetItemId))
                    continue;

                var id = zone.TargetItemId.Trim();
                counts.TryGetValue(id, out var current);
                counts[id] = current + zone.Capacity;
            }

            return counts;
        }

        List<SupermarketItemSpawnZone> ResolveSpawnZones()
        {
            var result = new List<SupermarketItemSpawnZone>();
            if (spawnZones != null)
            {
                for (var i = 0; i < spawnZones.Length; i++)
                {
                    var zone = spawnZones[i];
                    if (zone != null && zone.isActiveAndEnabled)
                        result.Add(zone);
                }
            }

            if (result.Count > 0)
                return result;

#if UNITY_2023_1_OR_NEWER
            var found = FindObjectsByType<SupermarketItemSpawnZone>(FindObjectsSortMode.None);
#else
            var found = FindObjectsOfType<SupermarketItemSpawnZone>();
#endif
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].isActiveAndEnabled)
                    result.Add(found[i]);
            }

            return result;
        }

        Vector3 SampleSpawnPosition(IReadOnlyList<SupermarketItemSpawnZone> zones, System.Random rng)
        {
            if (zones != null
                && zones.Count > 0
                && SupermarketItemSpawnZone.TrySampleFromZones(zones, rng, out var zonePoint))
            {
                return ApplySpawnHeight(zonePoint, rng);
            }

            var center = scatterCenter != null ? scatterCenter.position : transform.position;
            var half = config != null ? config.scatterAreaHalfExtents : new Vector2(6f, 6f);
            var x = (float)(rng.NextDouble() * 2.0 - 1.0) * half.x;
            var z = (float)(rng.NextDouble() * 2.0 - 1.0) * half.y;
            return ApplySpawnHeight(center + new Vector3(x, 0f, z), rng);
        }

        Vector3 ApplySpawnHeight(Vector3 samplePoint, System.Random rng)
        {
            var basePoint = ResolveGroundPoint(samplePoint);

            if (config != null && config.enableDropSpawn)
            {
                var min = Mathf.Min(config.dropHeightMin, config.dropHeightMax);
                var max = Mathf.Max(config.dropHeightMin, config.dropHeightMax);
                var height = min + (float)rng.NextDouble() * (max - min);
                basePoint.y += height;
                return basePoint;
            }

            var groundHeight = config != null ? config.scatterHeight : 0.35f;
            basePoint.y += groundHeight;
            return basePoint;
        }

        Vector3 ResolveGroundPoint(Vector3 samplePoint)
        {
            if (config != null && config.useFixedSpawnFloorY)
                return new Vector3(samplePoint.x, config.spawnFloorWorldY, samplePoint.z);

            return SnapToGround(samplePoint);
        }

        Vector3 SnapToGround(Vector3 point)
        {
            var rayHeight = config != null ? config.spawnGroundRaycastHeight : 30f;
            var rayDistance = config != null ? config.spawnGroundRaycastDistance : 40f;
            var mask = config != null && config.spawnGroundMask.value != 0
                ? config.spawnGroundMask
                : (LayerMask)~0;

            var origin = new Vector3(point.x, rayHeight, point.z);
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                rayDistance,
                mask,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
                return new Vector3(point.x, point.y, point.z);

            var best = hits[0];
            for (var i = 1; i < hits.Length; i++)
            {
                if (hits[i].point.y < best.point.y)
                    best = hits[i];
            }

            return best.point;
        }

        static void SetupPhysicsDrop(SupermarketItem item, SupermarketOrganizeConfig config, System.Random rng)
        {
            if (item == null)
                return;

            EnsureCollider(item);
            item.EnableGroundInteraction();

            var settle = item.GetComponent<SupermarketItemPhysicsSettle>();
            if (settle == null)
                settle = item.gameObject.AddComponent<SupermarketItemPhysicsSettle>();

            var useDrop = config == null || config.enableDropSpawn;
            if (!useDrop)
            {
                settle.FreezePhysics();
                return;
            }

            settle.BeginDrop(config);

            var body = item.GetComponent<Rigidbody>();
            if (body == null)
                body = item.gameObject.AddComponent<Rigidbody>();

            body.constraints = RigidbodyConstraints.None;
            body.detectCollisions = true;
            body.WakeUp();

            if (config != null && config.dropInitialDownwardSpeed > 0f)
            {
                var velocity = body.velocity;
                velocity.y = -Mathf.Abs(config.dropInitialDownwardSpeed);
                body.velocity = velocity;
            }

            var impulse = config != null ? config.dropHorizontalImpulse : 0.35f;
            if (impulse > 0f)
            {
                var randomDir = new Vector3(
                    (float)rng.NextDouble() * 2f - 1f,
                    0f,
                    (float)rng.NextDouble() * 2f - 1f);
                if (randomDir.sqrMagnitude > 1e-4f)
                    body.AddForce(randomDir.normalized * impulse, ForceMode.VelocityChange);
            }

            ApplyDropAngularImpulse(body, config, rng);
        }

        static Quaternion SampleSpawnRotation(SupermarketOrganizeConfig config, System.Random rng)
        {
            var range = config != null
                ? config.dropSpawnRotationRange
                : new Vector3(90f, 360f, 90f);

            return Quaternion.Euler(
                SampleRotationAxis(range.x, rng),
                SampleRotationYaw(range.y, rng),
                SampleRotationAxis(range.z, rng));
        }

        static float SampleRotationAxis(float maxAbsDegrees, System.Random rng)
        {
            if (maxAbsDegrees <= 0f)
                return 0f;

            return ((float)rng.NextDouble() * 2f - 1f) * maxAbsDegrees;
        }

        static float SampleRotationYaw(float yawRangeDegrees, System.Random rng)
        {
            if (yawRangeDegrees >= 360f)
                return (float)rng.NextDouble() * 360f;

            return SampleRotationAxis(yawRangeDegrees, rng);
        }

        static void ApplyDropAngularImpulse(Rigidbody body, SupermarketOrganizeConfig config, System.Random rng)
        {
            if (body == null)
                return;

            var maxAngularSpeed = config != null ? config.dropAngularImpulse : 0f;
            if (maxAngularSpeed <= 0f)
                return;

            var axis = RandomUnitVector(rng);
            if (axis.sqrMagnitude <= 1e-6f)
                return;

            var speed = (float)rng.NextDouble() * maxAngularSpeed;
            body.angularVelocity = axis * speed;
        }

        static Vector3 RandomUnitVector(System.Random rng)
        {
            var x = (float)rng.NextDouble() * 2f - 1f;
            var y = (float)rng.NextDouble() * 2f - 1f;
            var z = (float)rng.NextDouble() * 2f - 1f;
            var vector = new Vector3(x, y, z);
            return vector.sqrMagnitude <= 1e-6f ? Vector3.up : vector.normalized;
        }

        static void EnsureCollider(SupermarketItem item)
        {
            if (item.GetComponentInChildren<Collider>() != null)
                return;

            var renderer = item.GetComponentInChildren<Renderer>();
            var box = item.gameObject.AddComponent<BoxCollider>();
            if (renderer != null)
            {
                box.center = item.transform.InverseTransformPoint(renderer.bounds.center);
                var size = renderer.bounds.size;
                var lossy = item.transform.lossyScale;
                box.size = new Vector3(
                    lossy.x > 1e-4f ? size.x / lossy.x : size.x,
                    lossy.y > 1e-4f ? size.y / lossy.y : size.y,
                    lossy.z > 1e-4f ? size.z / lossy.z : lossy.z);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>
    /// 货架放置区：行列网格、物品 ID 匹配、TipObject 与放置预览。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketContainerZone : MonoBehaviour
    {
        [SerializeField] private string targetItemId = "item_default";
        [SerializeField] private int rows = 2;
        [SerializeField] private int columns = 4;
        [SerializeField] private Transform gridOrigin;
        [SerializeField] private Vector3 cellSpacing = new Vector3(0.25f, 0.12f, 0.25f);
        [SerializeField] private Transform tipObject;
        [SerializeField] private Transform shelfCompleteFxAnchor;
        [SerializeField] private Collider zoneTrigger;
        [SerializeField] private Color placePreviewTint = new Color(1f, 1f, 1f, 0.35f);

        [Header("Item Rotation")]
        [Tooltip("启用后，货架上的物品使用固定世界旋转，不受 Container 自身旋转影响。")]
        [SerializeField] private bool useFixedItemWorldRotation;

        [Tooltip("物品在货架上的固定世界欧拉角（仅当上方选项启用时生效）。")]
        [SerializeField] private Vector3 fixedItemWorldRotationEuler = Vector3.zero;

        readonly List<SupermarketItem> _placedItems = new List<SupermarketItem>();
        GameObject _placePreviewRoot;
        readonly List<Renderer> _previewRenderers = new List<Renderer>();
        Material _previewMaterial;

        public string TargetItemId => targetItemId;
        public int Rows => Mathf.Max(1, rows);
        public int Columns => Mathf.Max(1, columns);
        public int Capacity => Rows * Columns;
        public int PlacedCount => _placedItems.Count;
        public bool IsFull => _placedItems.Count >= Capacity;
        public bool IsFullyStocked => IsFull && _placedItems.TrueForAll(
            item => item != null && item.State == SupermarketItemState.OnShelf);
        public int NextSlotIndex => PlacedCount;
        public Collider ZoneTrigger => zoneTrigger != null ? zoneTrigger : zoneTrigger = GetComponent<Collider>();

        public bool MatchesItem(string itemId) =>
            !string.IsNullOrWhiteSpace(itemId)
            && !string.IsNullOrWhiteSpace(targetItemId)
            && string.Equals(itemId.Trim(), targetItemId.Trim(), StringComparison.OrdinalIgnoreCase);

        public bool HasPlacedSameIdBefore => _placedItems.Count > 0;
        public bool UseFixedItemWorldRotation => useFixedItemWorldRotation;
        public Vector3 FixedItemWorldRotationEuler => fixedItemWorldRotationEuler;

        public Vector3 GetShelfCompleteFxWorldPosition() =>
            shelfCompleteFxAnchor != null ? shelfCompleteFxAnchor.position : transform.position;

        public bool TryAcceptItem(SupermarketItem item, out int slotIndex)
        {
            slotIndex = -1;
            if (item == null || item.IsDefective || IsFull || !MatchesItem(item.ItemId))
                return false;

            slotIndex = _placedItems.Count;
            _placedItems.Add(item);
            HidePlacePreview();
            SetTipVisible(false);
            return true;
        }

        public void RemoveItem(SupermarketItem item)
        {
            if (item == null)
                return;

            _placedItems.Remove(item);
        }

        public void SetTipVisible(bool visible)
        {
            if (tipObject == null)
                return;

            var tipGo = tipObject.gameObject;
            if (tipGo.activeSelf == visible)
                return;

            tipGo.SetActive(visible);
        }

        public void ShowPlacePreview(SupermarketItem sourceItem, SupermarketOrganizeConfig config)
        {
            if (sourceItem == null || IsFull || !MatchesItem(sourceItem.ItemId))
            {
                HidePlacePreview();
                return;
            }

            EnsurePreview(sourceItem, config);
            if (_placePreviewRoot == null)
                return;

            var slotIndex = NextSlotIndex;
            _placePreviewRoot.transform.SetParent(transform, false);
            ApplyPlacedItemTransform(_placePreviewRoot.transform, slotIndex);
            _placePreviewRoot.SetActive(true);
        }

        public void HidePlacePreview()
        {
            if (_placePreviewRoot != null && _placePreviewRoot.activeSelf)
                _placePreviewRoot.SetActive(false);
        }

        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            slotIndex = Mathf.Clamp(slotIndex, 0, Capacity - 1);
            var row = slotIndex / Columns;
            var col = slotIndex % Columns;
            var origin = gridOrigin != null ? gridOrigin : transform;
            var local = new Vector3(col * cellSpacing.x, row * cellSpacing.y, row * cellSpacing.z);
            return origin.TransformPoint(local);
        }

        public Quaternion GetSlotWorldRotation()
        {
            if (useFixedItemWorldRotation)
                return Quaternion.Euler(fixedItemWorldRotationEuler);

            var origin = gridOrigin != null ? gridOrigin : transform;
            return origin.rotation;
        }

        public void ApplyPlacedItemTransform(Transform itemTransform, int slotIndex)
        {
            if (itemTransform == null)
                return;

            itemTransform.localPosition = GetSlotLocalPosition(slotIndex);
            itemTransform.localRotation = GetSlotLocalRotation(slotIndex);

            if (useFixedItemWorldRotation)
                itemTransform.rotation = Quaternion.Euler(fixedItemWorldRotationEuler);
        }

        public Vector3 GetSlotLocalPosition(int slotIndex)
        {
            var world = GetSlotWorldPosition(slotIndex);
            return transform.InverseTransformPoint(world);
        }

        public Quaternion GetSlotLocalRotation(int slotIndex)
        {
            var world = GetSlotWorldRotation();
            return Quaternion.Inverse(transform.rotation) * world;
        }

        void EnsurePreview(SupermarketItem sourceItem, SupermarketOrganizeConfig config)
        {
            if (_placePreviewRoot != null)
                return;

            _placePreviewRoot = Instantiate(sourceItem.gameObject, transform);
            _placePreviewRoot.name = "PlacePreview";
            StripPreviewObject(_placePreviewRoot);

            if (config != null)
                placePreviewTint = config.shelfPlacePreviewColor;

            ApplyPreviewMaterial(_placePreviewRoot, placePreviewTint);
            _placePreviewRoot.SetActive(false);
        }

        static void StripPreviewObject(GameObject previewRoot)
        {
            var behaviours = previewRoot.GetComponentsInChildren<Behaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour is Renderer || behaviour is Transform)
                    continue;

                behaviour.enabled = false;
            }

            var colliders = previewRoot.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            var bodies = previewRoot.GetComponentsInChildren<Rigidbody>(true);
            for (var i = 0; i < bodies.Length; i++)
                Destroy(bodies[i]);
        }

        void ApplyPreviewMaterial(GameObject previewRoot, Color tint)
        {
            _previewRenderers.Clear();
            previewRoot.GetComponentsInChildren(true, _previewRenderers);
            _previewMaterial ??= CreatePreviewMaterial(tint);

            for (var i = 0; i < _previewRenderers.Count; i++)
            {
                var renderer = _previewRenderers[i];
                if (renderer == null)
                    continue;

                var materials = renderer.sharedMaterials;
                var tinted = new Material[materials.Length];
                for (var m = 0; m < materials.Length; m++)
                    tinted[m] = _previewMaterial;

                renderer.sharedMaterials = tinted;
            }
        }

        static Material CreatePreviewMaterial(Color tint)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                           ?? Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.color = tint;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", tint);

            ConfigureTransparent(material);
            return material;
        }

        static void ConfigureTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        void Reset()
        {
            zoneTrigger = GetComponent<Collider>();
            if (gridOrigin == null)
            {
                var originGo = new GameObject("GridOrigin");
                originGo.transform.SetParent(transform, false);
                gridOrigin = originGo.transform;
            }

            var tip = transform.Find("TipObject");
            if (tip != null)
                tipObject = tip;
        }

        void Awake()
        {
            if (zoneTrigger == null)
                zoneTrigger = GetComponent<Collider>();
            if (gridOrigin == null)
                gridOrigin = transform;

            SetTipVisible(false);
            HidePlacePreview();
        }

        void LateUpdate()
        {
            if (!useFixedItemWorldRotation || _placedItems.Count == 0)
                return;

            var worldRotation = Quaternion.Euler(fixedItemWorldRotationEuler);
            for (var i = 0; i < _placedItems.Count; i++)
            {
                var item = _placedItems[i];
                if (item == null)
                    continue;

                item.transform.rotation = worldRotation;
            }
        }

        void OnDestroy()
        {
            if (_previewMaterial != null)
                Destroy(_previewMaterial);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 0.35f, 0.85f);
            for (var slot = 0; slot < Capacity; slot++)
            {
                var pos = GetSlotWorldPosition(slot);
                Gizmos.DrawWireCube(pos, Vector3.one * 0.12f);
            }

            Gizmos.color = new Color(0.95f, 0.75f, 0.1f, 0.35f);
            if (zoneTrigger is BoxCollider box)
            {
                Gizmos.matrix = zoneTrigger.transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
        }
    }
}

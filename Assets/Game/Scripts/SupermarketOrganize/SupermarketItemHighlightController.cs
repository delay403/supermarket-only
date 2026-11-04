using System.Collections.Generic;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>
    /// 管理物品高亮（叠加材质）与外描边（QuickOutline）。
    /// 仅对需要的物体启用，避免全场景常驻描边开销。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketItemHighlightController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Material highlightOverlayMaterial;

        readonly Dictionary<SupermarketItem, ItemVisualState> _states = new Dictionary<SupermarketItem, ItemVisualState>();
        SupermarketItem _aimItem;
        SupermarketItem _heldItem;
        readonly HashSet<SupermarketItem> _sameIdItems = new HashSet<SupermarketItem>();

        Material _runtimeAimOverlayMaterial;
        Material _runtimeSameIdOverlayMaterial;
        Color _aimColor = new Color(1f, 1f, 0.35f, 0.35f);
        Color _sameIdHighlightColor = new Color(0.35f, 0.85f, 1f, 0.35f);
        Color _sameIdOutlineColor = Color.white;
        Color _heldItemOutlineColor = new Color(0.25f, 0.95f, 0.45f, 1f);
        float _sameIdOutlineWidth = 2.5f;
        float _heldItemOutlineWidth = 4f;

        sealed class ItemVisualState
        {
            public Renderer[] Renderers;
            public Material[][] OriginalSharedMaterials;
            public Outline Outline;
            public bool AimActive;
            public bool SameIdActive;
            public bool HeldActive;
        }

        public void ApplyColors(
            Color aimColor,
            Color sameIdHighlightColor,
            Color sameIdOutlineColor,
            float sameIdOutlineWidth,
            Color heldItemOutlineColor,
            float heldItemOutlineWidth)
        {
            _aimColor = aimColor;
            _sameIdHighlightColor = sameIdHighlightColor;
            _sameIdOutlineColor = sameIdOutlineColor;
            _sameIdOutlineWidth = sameIdOutlineWidth;
            _heldItemOutlineColor = heldItemOutlineColor;
            _heldItemOutlineWidth = heldItemOutlineWidth;
            RefreshOverlayMaterialColors();
            RefreshSameIdVisuals();
            RefreshHeldVisual();
        }

        public void SetAimTarget(SupermarketItem item)
        {
            if (item != null && (item.State == SupermarketItemState.OnShelf
                                 || item.State == SupermarketItemState.InRobotHandle
                                 || item.State == SupermarketItemState.InHandle
                                 || item.State == SupermarketItemState.Moving))
                item = null;

            if (_aimItem == item)
                return;

            if (_aimItem != null)
                SetAimHighlight(_aimItem, false);

            _aimItem = item;

            if (_aimItem != null)
                SetAimHighlight(_aimItem, true);
        }

        public void SetHeldItem(SupermarketItem item)
        {
            if (_heldItem != item)
            {
                if (_heldItem != null)
                    ClearItemVisuals(_heldItem);

                _heldItem = item;
            }

            if (_heldItem != null)
                ClearItemVisuals(_heldItem);
            else if (item == null)
                _heldItem = null;
        }

        public void ClearHeldItemVisuals(SupermarketItem item) => ClearItemVisuals(item);

        public void ClearAllHeldVisuals(IEnumerable<SupermarketItem> items)
        {
            if (items == null)
                return;

            foreach (var item in items)
                ClearItemVisuals(item);
        }

        public void RefreshHeldOutlineNow()
        {
            if (_heldItem == null)
                return;

            ClearItemVisuals(_heldItem);
        }

        public void SetSameIdHighlights(IEnumerable<SupermarketItem> items)
        {
            var next = new HashSet<SupermarketItem>();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item != null && item.State == SupermarketItemState.OnGround)
                        next.Add(item);
                }
            }

            foreach (var old in _sameIdItems)
            {
                if (!next.Contains(old))
                    SetSameIdHighlight(old, false);
            }

            foreach (var item in next)
            {
                if (!_sameIdItems.Contains(item))
                    SetSameIdHighlight(item, true);
            }

            _sameIdItems.Clear();
            foreach (var item in next)
                _sameIdItems.Add(item);
        }

        public void ClearAll()
        {
            SetAimTarget(null);
            SetHeldItem(null);
            SetSameIdHighlights(null);
        }

        public void CollectExemptItems(HashSet<SupermarketItem> items)
        {
            if (items == null)
                return;

            if (_heldItem != null)
                items.Add(_heldItem);

            if (_aimItem != null)
                items.Add(_aimItem);

            foreach (var item in _sameIdItems)
            {
                if (item != null)
                    items.Add(item);
            }
        }

        void Awake()
        {
            EnsureOverlayMaterials();
        }

        void EnsureOverlayMaterials()
        {
            if (_runtimeAimOverlayMaterial == null)
                _runtimeAimOverlayMaterial = CreateOverlayMaterial(_aimColor, highlightOverlayMaterial);

            if (_runtimeSameIdOverlayMaterial == null)
                _runtimeSameIdOverlayMaterial = CreateOverlayMaterial(_sameIdHighlightColor, highlightOverlayMaterial);

            RefreshOverlayMaterialColors();
        }

        static Material CreateOverlayMaterial(Color color, Material template)
        {
            Material material;
            if (template != null)
                material = new Material(template);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                               ?? Shader.Find("Unlit/Color")
                               ?? Shader.Find("Sprites/Default");
                material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            }

            ApplyOverlayColor(material, color);
            return material;
        }

        void RefreshOverlayMaterialColors()
        {
            if (_runtimeAimOverlayMaterial != null)
                ApplyOverlayColor(_runtimeAimOverlayMaterial, _aimColor);

            if (_runtimeSameIdOverlayMaterial != null)
                ApplyOverlayColor(_runtimeSameIdOverlayMaterial, _sameIdHighlightColor);
        }

        static void ApplyOverlayColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);
            else if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);
        }

        ItemVisualState GetOrCreateState(SupermarketItem item)
        {
            if (item == null)
                return null;

            if (_states.TryGetValue(item, out var state))
                return state;

            state = new ItemVisualState
            {
                Renderers = item.GetComponentsInChildren<Renderer>(true),
            };

            state.OriginalSharedMaterials = new Material[state.Renderers.Length][];
            for (var i = 0; i < state.Renderers.Length; i++)
            {
                var renderer = state.Renderers[i];
                state.OriginalSharedMaterials[i] = renderer != null
                    ? renderer.sharedMaterials
                    : System.Array.Empty<Material>();
            }

            _states[item] = state;
            return state;
        }

        void SetAimHighlight(SupermarketItem item, bool active)
        {
            var state = GetOrCreateState(item);
            if (state == null || state.AimActive == active)
                return;

            state.AimActive = active;
            ApplyRendererMaterials(state);
            RefreshOutline(item, state);
        }

        void SetSameIdHighlight(SupermarketItem item, bool active)
        {
            var state = GetOrCreateState(item);
            if (state == null || state.SameIdActive == active)
                return;

            state.SameIdActive = active;
            ApplyRendererMaterials(state);
            RefreshOutline(item, state);
        }

        void ClearItemVisuals(SupermarketItem item)
        {
            if (item == null)
                return;

            if (!_states.TryGetValue(item, out var state))
                return;

            state.AimActive = false;
            state.SameIdActive = false;
            state.HeldActive = false;
            ApplyRendererMaterials(state);
            SetOutlineHighlight(item, false, _sameIdOutlineColor, Outline.Mode.OutlineAll, _sameIdOutlineWidth);
        }

        void RefreshOutline(SupermarketItem item, ItemVisualState state)
        {
            if (state == null)
                return;

            var showOutline = state.HeldActive || state.AimActive || state.SameIdActive;
            if (!showOutline)
            {
                SetOutlineHighlight(item, false, _sameIdOutlineColor, Outline.Mode.OutlineAll, _sameIdOutlineWidth);
                return;
            }

            if (state.HeldActive)
            {
                SetOutlineHighlight(
                    item,
                    true,
                    _heldItemOutlineColor,
                    Outline.Mode.OutlineAll,
                    _heldItemOutlineWidth);
                return;
            }

            var color = state.AimActive ? _aimColor : _sameIdOutlineColor;
            var mode = state.AimActive ? Outline.Mode.OutlineVisible : Outline.Mode.OutlineAll;
            SetOutlineHighlight(item, true, color, mode, _sameIdOutlineWidth);
        }

        void SetOutlineHighlight(
            SupermarketItem item,
            bool active,
            Color color,
            Outline.Mode mode,
            float width)
        {
            var state = GetOrCreateState(item);
            if (state == null)
                return;

            if (active)
            {
                if (state.Outline == null)
                {
                    state.Outline = item.GetComponent<Outline>();
                    if (state.Outline == null)
                        state.Outline = item.gameObject.AddComponent<Outline>();
                }

                state.Outline.OutlineMode = mode;
                state.Outline.OutlineColor = color;
                state.Outline.OutlineWidth = width;
                state.Outline.enabled = true;
            }
            else if (state.Outline != null)
            {
                state.Outline.enabled = false;
            }
        }

        void ApplyRendererMaterials(ItemVisualState state)
        {
            if (state.Renderers == null)
                return;

            for (var i = 0; i < state.Renderers.Length; i++)
            {
                var renderer = state.Renderers[i];
                if (renderer == null)
                    continue;

                var originals = state.OriginalSharedMaterials[i];
                if (originals == null || originals.Length == 0)
                    continue;

                Material overlay = null;
                if (state.AimActive)
                    overlay = _runtimeAimOverlayMaterial;
                else if (state.SameIdActive)
                    overlay = _runtimeSameIdOverlayMaterial;

                if (overlay != null)
                {
                    var combined = new Material[originals.Length + 1];
                    for (var m = 0; m < originals.Length; m++)
                        combined[m] = originals[m];
                    combined[combined.Length - 1] = overlay;
                    renderer.materials = combined;
                }
                else
                {
                    renderer.sharedMaterials = originals;
                }
            }
        }

        void RefreshSameIdVisuals()
        {
            var copy = new List<SupermarketItem>(_sameIdItems);
            _sameIdItems.Clear();
            SetSameIdHighlights(copy);
        }

        void RefreshHeldVisual()
        {
            var current = _heldItem;
            _heldItem = null;
            SetHeldItem(current);
        }

        void OnDestroy()
        {
            if (_runtimeAimOverlayMaterial != null)
                Destroy(_runtimeAimOverlayMaterial);

            if (_runtimeSameIdOverlayMaterial != null)
                Destroy(_runtimeSameIdOverlayMaterial);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 准星瞄准高亮（叠加材质 + 白色外描边）与流程引导高亮（QuickOutline）。
    /// 逻辑对齐超市收纳 <see cref="Game.SupermarketOrganize.SupermarketItemHighlightController"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalHighlightController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Material highlightOverlayMaterial;

        readonly Dictionary<AnimalHospitalInteractable, VisualState> _states =
            new Dictionary<AnimalHospitalInteractable, VisualState>();

        AnimalHospitalInteractable _aimTarget;
        AnimalHospitalInteractable _guideTarget;

        Material _runtimeAimOverlay;
        Material _runtimeGuideOverlay;
        Color _aimColor = new Color(1f, 1f, 0.35f, 0.35f);
        Color _aimOutlineColor = Color.white;
        float _aimOutlineWidth = 2.5f;
        Color _guideColor = new Color(0.25f, 0.95f, 0.45f, 0.45f);
        Color _guideOutlineColor = new Color(0.25f, 0.95f, 0.45f, 1f);
        float _guideOutlineWidth = 3.5f;

        sealed class VisualState
        {
            public Renderer[] Renderers;
            public Material[][] OriginalSharedMaterials;
            public Outline Outline;
            public bool AimActive;
            public bool GuideActive;
        }

        public void ApplyColors(
            Color aimColor,
            Color guideColor,
            Color aimOutlineColor,
            float aimOutlineWidth,
            Color guideOutlineColor,
            float guideOutlineWidth)
        {
            _aimColor = aimColor;
            _guideColor = guideColor;
            _aimOutlineColor = aimOutlineColor;
            _aimOutlineWidth = aimOutlineWidth;
            _guideOutlineColor = guideOutlineColor;
            _guideOutlineWidth = guideOutlineWidth;
            RefreshOverlayColors();
            RefreshAim();
            RefreshGuide();
        }

        public void SetAimTarget(AnimalHospitalInteractable target)
        {
            if (_aimTarget == target)
                return;

            if (_aimTarget != null)
                SetAim(_aimTarget, false);

            _aimTarget = target;
            if (_aimTarget != null)
                SetAim(_aimTarget, true);
        }

        public void SetGuideTarget(AnimalHospitalInteractable target)
        {
            if (_guideTarget == target)
                return;

            if (_guideTarget != null)
            {
                SetGuide(_guideTarget, false);
                _guideTarget.SetGuideHighlighted(false);
            }

            _guideTarget = target;
            if (_guideTarget != null)
            {
                SetGuide(_guideTarget, true);
                _guideTarget.SetGuideHighlighted(true);
            }
        }

        public void ClearAll()
        {
            SetAimTarget(null);
            SetGuideTarget(null);
            foreach (var pair in _states)
                RestoreMaterials(pair.Value);
            _states.Clear();
        }

        public void ClearVisuals(AnimalHospitalInteractable target)
        {
            if (target == null)
                return;

            if (_aimTarget == target)
                _aimTarget = null;

            if (_guideTarget == target)
            {
                _guideTarget.SetGuideHighlighted(false);
                _guideTarget = null;
            }

            if (!_states.TryGetValue(target, out var state))
                return;

            state.AimActive = false;
            state.GuideActive = false;
            RestoreMaterials(state);
            SetOutline(target, state, false, Color.white, Outline.Mode.OutlineAll, _aimOutlineWidth);
        }

        void SetAim(AnimalHospitalInteractable target, bool active)
        {
            var state = GetOrCreate(target);
            state.AimActive = active;
            ApplyVisual(target, state);
        }

        void SetGuide(AnimalHospitalInteractable target, bool active)
        {
            var state = GetOrCreate(target);
            state.GuideActive = active;
            ApplyVisual(target, state);
        }

        void RefreshAim()
        {
            if (_aimTarget != null)
                SetAim(_aimTarget, true);
        }

        void RefreshGuide()
        {
            if (_guideTarget != null)
                SetGuide(_guideTarget, true);
        }

        VisualState GetOrCreate(AnimalHospitalInteractable target)
        {
            if (_states.TryGetValue(target, out var state))
                return state;

            state = new VisualState
            {
                Renderers = CollectHighlightRenderers(target),
            };
            CaptureOriginal(state);
            _states[target] = state;
            return state;
        }

        static Renderer[] CollectHighlightRenderers(AnimalHospitalInteractable target)
        {
            if (target == null)
                return System.Array.Empty<Renderer>();

            var all = target.GetComponentsInChildren<Renderer>(true);
            var filtered = new List<Renderer>(all.Length);
            for (var i = 0; i < all.Length; i++)
            {
                if (ShouldHighlightRenderer(all[i]))
                    filtered.Add(all[i]);
            }

            return filtered.ToArray();
        }

        static bool ShouldHighlightRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                return false;
            if (renderer.GetComponentInParent<Canvas>(true) != null)
                return false;
            return true;
        }

        void CaptureOriginal(VisualState state)
        {
            state.OriginalSharedMaterials = new Material[state.Renderers.Length][];
            for (var i = 0; i < state.Renderers.Length; i++)
            {
                var r = state.Renderers[i];
                state.OriginalSharedMaterials[i] = r != null ? r.sharedMaterials : System.Array.Empty<Material>();
            }
        }

        void ApplyVisual(AnimalHospitalInteractable target, VisualState state)
        {
            if (state.Renderers == null)
                return;

            if (state.AimActive || state.GuideActive)
            {
                Material overlay = state.GuideActive
                    ? EnsureGuideOverlay()
                    : EnsureAimOverlay();
                ApplyOverlay(state, overlay);
            }
            else
            {
                RestoreMaterials(state);
            }

            RefreshOutline(target, state);
        }

        void RefreshOutline(AnimalHospitalInteractable target, VisualState state)
        {
            if (state.AimActive)
            {
                SetOutline(target, state, true, _aimOutlineColor, Outline.Mode.OutlineVisible, _aimOutlineWidth);
                return;
            }

            if (state.GuideActive)
            {
                SetOutline(target, state, true, _guideOutlineColor, Outline.Mode.OutlineAll, _guideOutlineWidth);
                return;
            }

            SetOutline(target, state, false, Color.white, Outline.Mode.OutlineAll, _aimOutlineWidth);
        }

        void ApplyOverlay(VisualState state, Material overlay)
        {
            if (overlay == null)
                return;

            for (var i = 0; i < state.Renderers.Length; i++)
            {
                var r = state.Renderers[i];
                if (r == null)
                    continue;

                var original = state.OriginalSharedMaterials[i];
                if (original == null || original.Length == 0)
                    continue;

                var next = new Material[original.Length + 1];
                for (var m = 0; m < original.Length; m++)
                    next[m] = original[m];
                next[original.Length] = overlay;
                r.materials = next;
            }
        }

        void RestoreMaterials(VisualState state)
        {
            if (state.Renderers == null || state.OriginalSharedMaterials == null)
                return;

            for (var i = 0; i < state.Renderers.Length; i++)
            {
                var r = state.Renderers[i];
                if (r != null)
                    r.sharedMaterials = state.OriginalSharedMaterials[i];
            }
        }

        void SetOutline(
            AnimalHospitalInteractable target,
            VisualState state,
            bool enabled,
            Color color,
            Outline.Mode mode,
            float width)
        {
            if (!enabled)
            {
                if (state.Outline != null)
                    state.Outline.enabled = false;
                return;
            }

            if (state.Outline == null)
            {
                state.Outline = target.gameObject.GetComponent<Outline>()
                                ?? target.gameObject.AddComponent<Outline>();
            }

            state.Outline.enabled = true;
            state.Outline.OutlineMode = mode;
            state.Outline.OutlineColor = color;
            state.Outline.OutlineWidth = width;
        }

        Material EnsureAimOverlay()
        {
            if (_runtimeAimOverlay == null)
                _runtimeAimOverlay = CreateOverlay(_aimColor);
            return _runtimeAimOverlay;
        }

        Material EnsureGuideOverlay()
        {
            if (_runtimeGuideOverlay == null)
                _runtimeGuideOverlay = CreateOverlay(_guideColor);
            return _runtimeGuideOverlay;
        }

        void RefreshOverlayColors()
        {
            if (_runtimeAimOverlay != null)
                SetColor(_runtimeAimOverlay, _aimColor);
            if (_runtimeGuideOverlay != null)
                SetColor(_runtimeGuideOverlay, _guideColor);
        }

        Material CreateOverlay(Color color)
        {
            Material mat = null;
            if (highlightOverlayMaterial != null)
                mat = new Material(highlightOverlayMaterial);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                               ?? Shader.Find("Unlit/Color")
                               ?? Shader.Find("Universal Render Pipeline/Lit")
                               ?? Shader.Find("Standard");
                if (shader != null)
                    mat = new Material(shader);
            }

            if (mat != null)
                SetColor(mat, color);
            return mat;
        }

        static void SetColor(Material mat, Color color)
        {
            if (mat.HasProperty(BaseColorId))
                mat.SetColor(BaseColorId, color);
            if (mat.HasProperty(ColorId))
                mat.SetColor(ColorId, color);
        }

        void OnDestroy()
        {
            if (_runtimeAimOverlay != null)
                Destroy(_runtimeAimOverlay);
            if (_runtimeGuideOverlay != null)
                Destroy(_runtimeGuideOverlay);
        }
    }
}

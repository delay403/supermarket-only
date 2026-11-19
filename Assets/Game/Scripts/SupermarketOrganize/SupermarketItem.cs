using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>场景内可交互物品；需 Collider。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketItem : MonoBehaviour
    {
        [SerializeField] private string itemId = "item_default";
        [Tooltip("手持堆叠时沿 HandleZone 本地 Y 轴占用的宽度（仅在预制体上配置）。")]
        [SerializeField] private float handleWidth = 0.2f;
        [Tooltip("多持（≥2 件）时该物品在 HandleZone 下的本地欧拉角。单持时为 identity。")]
        [SerializeField] private Vector3 multiHoldLocalRotationEuler = Vector3.zero;
        [SerializeField] private Collider interactionCollider;

        SupermarketItemState _state = SupermarketItemState.OnGround;
        SupermarketContainerZone _placedZone;
        int _placedSlotIndex = -1;
        Collider[] _colliders;
        Renderer[] _renderers;
        bool _groundVisualVisible = true;

        public bool IsGroundVisualVisible => _groundVisualVisible;

        public string ItemId => itemId;
        public float HandleWidth => handleWidth;
        public Vector3 MultiHoldLocalRotationEuler => multiHoldLocalRotationEuler;
        public SupermarketItemState State => _state;
        public bool IsDefective { get; private set; }

        public void MarkDefective() => IsDefective = true;
        public SupermarketContainerZone PlacedZone => _placedZone;
        public int PlacedSlotIndex => _placedSlotIndex;
        public Collider InteractionCollider => interactionCollider != null ? interactionCollider : interactionCollider = GetComponentInChildren<Collider>();

        public void Configure(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            itemId = id.Trim();
        }

        public void Configure(SupermarketItemDef def)
        {
            if (def == null)
                return;

            if (!string.IsNullOrWhiteSpace(def.itemId))
                itemId = def.itemId.Trim();
        }

        public void SetState(SupermarketItemState state)
        {
            _state = state;
            if (_state != SupermarketItemState.OnGround)
                ForceGroundVisualVisible();
        }

        public void SetPlaced(SupermarketContainerZone zone, int slotIndex)
        {
            _placedZone = zone;
            _placedSlotIndex = slotIndex;
            _state = SupermarketItemState.OnShelf;
            FinalizeAsShelfItem();
        }

        public void ClearPlacement()
        {
            _placedZone = null;
            _placedSlotIndex = -1;
        }

        public void EnableGroundInteraction()
        {
            if (_state != SupermarketItemState.OnGround)
                return;

            SetCollidersEnabled(true);
        }

        public void DisablePhysicsInteraction()
        {
            SetCollidersEnabled(false);

            var body = GetComponent<Rigidbody>();
            if (body == null)
                return;

            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.constraints = RigidbodyConstraints.FreezeAll;
        }

        public void PrepareForHandle()
        {
            GetComponent<SupermarketItemPhysicsSettle>()?.FreezePhysics();
            DisablePhysicsInteraction();

            var motion = GetComponent<SupermarketItemMotion>();
            if (motion != null)
            {
                motion.Cancel();
                motion.enabled = true;
            }
        }

        /// <summary>手持就位后不再由任何脚本改位移/旋转，仅随父物体 Transform 运动。</summary>
        public void ReleaseTransformControl()
        {
            var motion = GetComponent<SupermarketItemMotion>();
            if (motion != null)
            {
                motion.Cancel();
                motion.enabled = false;
            }

            var settle = GetComponent<SupermarketItemPhysicsSettle>();
            if (settle != null)
            {
                settle.FreezePhysics();
                settle.enabled = false;
            }

            DisablePhysicsInteraction();
        }

        public void FinalizeAsShelfItem()
        {
            ReleaseTransformControl();
            SetCollidersEnabled(false);
        }

        public void ApplyShadowSetting(bool disableShadows)
        {
            if (!disableShadows)
                return;

            EnsureRenderersCached();
            for (var i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null)
                    continue;

                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
        }

        public Bounds GetWorldRenderBounds()
        {
            EnsureRenderersCached();
            var hasBounds = false;
            var bounds = new Bounds(transform.position, Vector3.one * 0.2f);
            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                    continue;

                var rendererBounds = renderer.bounds;
                if (rendererBounds.size.sqrMagnitude <= 1e-8f)
                    continue;

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            if (!hasBounds)
            {
                var collider = InteractionCollider;
                if (collider != null && collider.enabled)
                    return collider.bounds;
            }

            return bounds;
        }

        public void SetGroundVisualVisible(bool visible)
        {
            if (_state != SupermarketItemState.OnGround || _groundVisualVisible == visible)
                return;

            _groundVisualVisible = visible;
            ApplyGroundVisualState(visible);
        }

        public void ForceGroundVisualVisible()
        {
            _groundVisualVisible = true;
            ApplyGroundVisualState(true);
        }

        void ApplyGroundVisualState(bool visible)
        {
            EnsureRenderersCached();
            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer != null)
                    renderer.enabled = visible;
            }

            SetCollidersEnabled(visible);
        }

        void EnsureRenderersCached()
        {
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);
        }

        void SetCollidersEnabled(bool enabled)
        {
            _colliders ??= GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < _colliders.Length; i++)
            {
                var collider = _colliders[i];
                if (collider != null)
                    collider.enabled = enabled;
            }
        }

        void Reset()
        {
            interactionCollider = GetComponentInChildren<Collider>();
        }

        void Awake()
        {
            if (interactionCollider == null)
                interactionCollider = GetComponentInChildren<Collider>();

            _colliders = GetComponentsInChildren<Collider>(true);
        }
    }
}

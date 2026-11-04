using Game.Interaction;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>燃料生产器上的 3D 生产按钮。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FuelProducerProduceButton : WorldInteractableBase
    {
        [SerializeField] private int interactionPriorityOverride = 20;
        [SerializeField] private Transform pressTarget;
        [SerializeField] private float pressDepth = 0.08f;
        [SerializeField] private float pressDuration = 0.12f;
        [Tooltip("按钮按下位移的本地轴方向（默认 Z 轴）。")]
        [SerializeField] private Vector3 pressLocalAxis = Vector3.forward;
        [SerializeField] private bool debugLog;

        FuelProducerController _producer;
        Vector3 _restLocalPos;
        Coroutine _pressCo;

        protected override void Awake()
        {
            base.Awake();
            interactionPriority = interactionPriorityOverride;
            if (pressTarget == null)
                pressTarget = transform;
            _restLocalPos = pressTarget.localPosition;

            _producer = GetComponentInParent<FuelProducerController>();
            EnsurePressTargetHitVolume();
        }

        public void Bind(FuelProducerController producer)
        {
            _producer = producer;
        }

        public void RefreshHitVolume() => EnsurePressTargetHitVolume();

        protected override bool CanInteractInternal(WorldInteractionContext context)
        {
            if (_producer == null)
            {
                if (debugLog)
                    Debug.LogWarning("[FuelProduceButton] 未绑定 FuelProducerController", this);
                return false;
            }

            return true;
        }

        protected override void OnInteract(WorldInteractionContext context)
        {
            if (debugLog)
                Debug.Log($"[FuelProduceButton] 点击生产 producer={_producer?.ProducerId}", this);

            _producer?.OnProduceButtonPressed();
            PlayPressAnimation();
        }

        void EnsurePressTargetHitVolume()
        {
            if (pressTarget == null)
                return;

            var layer = gameObject.layer;
            SetLayerRecursive(pressTarget.gameObject, layer);

            var col = pressTarget.GetComponent<Collider>();
            if (col == null)
                col = pressTarget.GetComponentInChildren<Collider>(true);

            if (col != null)
            {
                SetInteractionCollider(col);
                EnsureRelayOnPressTarget();
                return;
            }

            var renderers = pressTarget.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var box = GetComponent<BoxCollider>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();

            var t = transform;
            box.center = t.InverseTransformPoint(bounds.center);
            var lossy = t.lossyScale;
            box.size = new Vector3(
                bounds.size.x / Mathf.Max(0.001f, lossy.x),
                bounds.size.y / Mathf.Max(0.001f, lossy.y),
                bounds.size.z / Mathf.Max(0.001f, lossy.z));
            SetInteractionCollider(box);
            EnsureRelayOnPressTarget();
        }

        void EnsureRelayOnPressTarget()
        {
            if (pressTarget == null || pressTarget == transform)
                return;

            var relay = pressTarget.GetComponent<WorldInteractableRelay>();
            if (relay == null)
                relay = pressTarget.gameObject.AddComponent<WorldInteractableRelay>();
            relay.Bind(this);
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (var i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        public void PlayPressAnimation()
        {
            if (pressTarget == null)
                return;

            if (_pressCo != null)
                StopCoroutine(_pressCo);
            _pressCo = StartCoroutine(CoPress());
        }

        System.Collections.IEnumerator CoPress()
        {
            var axis = pressLocalAxis.sqrMagnitude > 0.0001f ? pressLocalAxis.normalized : Vector3.forward;
            var pressed = _restLocalPos + axis * pressDepth;
            var t = 0f;
            while (t < pressDuration)
            {
                t += Time.unscaledDeltaTime;
                pressTarget.localPosition = Vector3.Lerp(_restLocalPos, pressed, t / pressDuration);
                yield return null;
            }

            t = 0f;
            while (t < pressDuration)
            {
                t += Time.unscaledDeltaTime;
                pressTarget.localPosition = Vector3.Lerp(pressed, _restLocalPos, t / pressDuration);
                yield return null;
            }

            pressTarget.localPosition = _restLocalPos;
            _pressCo = null;
        }

#if UNITY_EDITOR
        void Reset()
        {
            debugLog = true;
        }
#endif
    }
}

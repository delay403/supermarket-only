using Game.Interaction;
using Game.Save;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>生产区内的燃料实例，可射线交互拿起。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FuelInstanceView : WorldInteractableBase
    {
        [SerializeField] private int interactionPriorityOverride = 10;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform scaleRoot;
        [SerializeField] private FuelInstanceXpBar xpBar;
        [Header("经验动效")]
        [SerializeField] private float scalePunchPeak = 1.05f;
        [SerializeField] private float scalePunchDip = 0.95f;
        [SerializeField] private float scalePunchDuration = 0.18f;

        [Header("交互")]
        [SerializeField] private WorldInteractionSlot interactionSlot;

        DestroyCityModeSaveData.FuelInstanceSaveEntry _entry;
        Vector3 _baseLocalScale = Vector3.one;
        Coroutine _scaleCo;

        public string InstanceId => _entry.instanceId;

        Transform ScaleTarget
        {
            get
            {
                if (scaleRoot != null)
                    return scaleRoot;
                if (visualRoot != null)
                    return visualRoot;
                return transform;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            interactionPriority = interactionPriorityOverride;
            CacheVisualRoots();
            if (xpBar == null)
                xpBar = GetComponentInChildren<FuelInstanceXpBar>(true);
            FuelInstanceVisualUtility.EnsurePickupCollider(transform, gameObject.layer);
            if (interactionSlot != null)
                interactionSlot.BindInteractable(this);
        }

        void CacheVisualRoots()
        {
            if (scaleRoot == null)
            {
                var found = transform.Find("ScaleRoot");
                if (found != null)
                    scaleRoot = found;
            }

            if (visualRoot == null)
                visualRoot = scaleRoot != null ? scaleRoot : transform;

            _baseLocalScale = ScaleTarget.localScale;
        }

        public void Bind(DestroyCityModeSaveData.FuelInstanceSaveEntry entry, bool animateBar = true)
        {
            _entry = entry;
            xpBar?.Refresh(entry.level, entry.xp, animateBar);
        }

        /// <summary>液滴抵达后播放缩放动效。</summary>
        public void NotifyDropletArrived()
        {
            if (!this || !isActiveAndEnabled)
                return;

            PlayScalePunch();
        }

        public void PlayScalePunch()
        {
            if (!this)
                return;

            var target = ScaleTarget;
            if (target == null)
                CacheVisualRoots();

            if (_scaleCo != null)
                StopCoroutine(_scaleCo);
            _scaleCo = StartCoroutine(CoScalePunch());
        }

        System.Collections.IEnumerator CoScalePunch()
        {
            if (!this)
                yield break;

            var target = ScaleTarget;
            if (target == null)
                yield break;
            var peak = _baseLocalScale * scalePunchPeak;
            var dip = _baseLocalScale * scalePunchDip;
            var third = scalePunchDuration * 0.33f;

            yield return CoLerpScale(target, target.localScale, peak, third);
            yield return CoLerpScale(target, target.localScale, dip, third);
            yield return CoLerpScale(target, target.localScale, _baseLocalScale, third);

            target.localScale = _baseLocalScale;
            _scaleCo = null;
        }

        static System.Collections.IEnumerator CoLerpScale(Transform target, Vector3 from, Vector3 to, float duration)
        {
            if (target == null)
                yield break;

            if (duration <= 0f)
            {
                target.localScale = to;
                yield break;
            }

            var t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(from, to, t / duration);
                yield return null;
            }

            target.localScale = to;
        }

        protected override bool CanInteractInternal(WorldInteractionContext context)
        {
            if (string.IsNullOrWhiteSpace(_entry.instanceId))
                return false;

            var carry = context.InteractorRoot.GetComponentInParent<PlayerFuelCarryController>();
            return carry != null;
        }

        protected override void OnInteract(WorldInteractionContext context)
        {
            var carry = context.InteractorRoot.GetComponentInParent<PlayerFuelCarryController>();
            if (carry == null)
                return;

            carry.TryPickup(this);
        }
    }
}

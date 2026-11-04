using Game.Audio;
using UnityEngine;

namespace Game.WormDodge.Pickups
{
    /// <summary>
    /// 躲避虫子道具基类：Trigger 接触后播放音效、隐藏并在新一局重置。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public abstract class WormDodgePickupBase : MonoBehaviour
    {
        [Header("Pickup")]
        [Tooltip("被拾取后隐藏的可视节点；为空则隐藏自身。")]
        [SerializeField] private Transform visualRoot;

        [SerializeField] protected SfxId pickupSfx = SfxId.ResourceCollect;

        [Tooltip("每局仅可被有效拾取一次。")]
        [SerializeField] private bool oncePerRun = true;

        Collider _trigger;
        bool _collectedThisRun;

        protected virtual bool RequiresActiveRun => true;

        protected abstract bool CanCollect(WormDodgeParticipant participant);

        protected abstract void ApplyPickup(WormDodgeParticipant participant);

        void Awake()
        {
            _trigger = GetComponent<Collider>();
            EnsureTriggerConfigured();
        }

        void OnEnable() => WormDodgeDirector.RunReset += OnRunReset;

        void OnDisable() => WormDodgeDirector.RunReset -= OnRunReset;

        void OnTriggerEnter(Collider other)
        {
            if (oncePerRun && _collectedThisRun)
                return;

            if (RequiresActiveRun)
            {
                var director = WormDodgeDirector.Instance;
                if (director == null || !director.IsRunActive)
                    return;
            }

            if (!TryGetParticipant(other, out var participant))
                return;

            if (!CanCollect(participant))
                return;

            ApplyPickup(participant);
            PlayPickupSfx();
            SetCollectedVisual(true);
        }

        void OnRunReset()
        {
            _collectedThisRun = false;
            SetCollectedVisual(false);
        }

        protected static bool TryGetParticipant(Collider other, out WormDodgeParticipant participant)
        {
            participant = other.GetComponent<WormDodgeParticipant>();
            if (participant == null)
                participant = other.GetComponentInParent<WormDodgeParticipant>();

            return participant != null;
        }

        protected WormDodgeParticipantPickupEffects ResolveEffects(WormDodgeParticipant participant)
        {
            if (participant == null)
                return null;

            var effects = participant.GetComponent<WormDodgeParticipantPickupEffects>();
            if (effects == null)
                effects = participant.GetComponentInParent<WormDodgeParticipantPickupEffects>();

            return effects;
        }

        void PlayPickupSfx()
        {
            if (pickupSfx == SfxId.None)
                return;

            AudioManager.Instance?.PlaySfx3D(pickupSfx, transform.position);
        }

        void SetCollectedVisual(bool collected)
        {
            _collectedThisRun = collected;

            if (_trigger != null)
                _trigger.enabled = !collected;

            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(!collected);
                return;
            }

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer != null)
                    renderer.enabled = !collected;
            }
        }

        void EnsureTriggerConfigured()
        {
            if (_trigger == null)
                _trigger = GetComponent<Collider>();

            if (_trigger != null)
                _trigger.isTrigger = true;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _trigger = GetComponent<Collider>();
            EnsureTriggerConfigured();
        }
#endif
    }
}

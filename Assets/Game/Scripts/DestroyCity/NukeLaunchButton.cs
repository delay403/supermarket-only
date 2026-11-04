using Game.Interaction;
using UnityEngine;

namespace Game.DestroyCity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class NukeLaunchButton : WorldInteractableBase
    {
        [SerializeField] private int interactionPriorityOverride = 30;
        [SerializeField] private Transform pressTarget;
        [SerializeField] private float pressDepth = 0.1f;

        NukeLauncherController _launcher;
        Vector3 _restLocalPos;

        protected override void Awake()
        {
            base.Awake();
            interactionPriority = interactionPriorityOverride;
            if (pressTarget == null)
                pressTarget = transform;
            _restLocalPos = pressTarget.localPosition;
        }

        public void Bind(NukeLauncherController launcher) => _launcher = launcher;

        protected override bool CanInteractInternal(WorldInteractionContext context) =>
            _launcher != null && _launcher.CanLaunch();

        protected override void OnInteract(WorldInteractionContext context)
        {
            _launcher?.OnLaunchButtonPressed();
        }

        public void PlayLaunchFeedback()
        {
            if (pressTarget == null)
                return;
            pressTarget.localPosition = _restLocalPos + Vector3.down * pressDepth;
            Invoke(nameof(ResetPress), 0.15f);
        }

        void ResetPress()
        {
            if (pressTarget != null)
                pressTarget.localPosition = _restLocalPos;
        }
    }
}

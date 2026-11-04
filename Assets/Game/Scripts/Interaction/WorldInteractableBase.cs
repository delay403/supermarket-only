using UnityEngine;

namespace Game.Interaction
{
    /// <summary><see cref="IWorldInteractable"/> 基类：要求 Collider，可挂在按钮/燃料等 3D 物体上。</summary>
    [DisallowMultipleComponent]
    public abstract class WorldInteractableBase : MonoBehaviour, IWorldInteractable
    {
        [SerializeField] private Collider interactionCollider;
        [SerializeField] protected int interactionPriority;
        [Tooltip("交互冷却（秒，unscaled）；防止连点。")]
        [SerializeField] private float interactCooldownSeconds = 0.15f;

        float _cooldownUntilUnscaled;

        public int InteractionPriority => interactionPriority;

        public Collider InteractionCollider =>
            interactionCollider != null ? interactionCollider : GetComponent<Collider>();

        protected virtual void Reset()
        {
            interactionCollider = GetComponent<Collider>();
        }

        protected virtual void Awake()
        {
            if (interactionCollider == null)
                interactionCollider = GetComponent<Collider>();
        }

        protected void SetInteractionCollider(Collider col)
        {
            if (col != null)
                interactionCollider = col;
        }

        public virtual bool CanInteract(WorldInteractionContext context)
        {
            if (!isActiveAndEnabled)
                return false;
            if (Time.unscaledTime < _cooldownUntilUnscaled)
                return false;
            return CanInteractInternal(context);
        }

        public void Interact(WorldInteractionContext context)
        {
            if (!CanInteract(context))
                return;

            if (interactCooldownSeconds > 0f)
                _cooldownUntilUnscaled = Time.unscaledTime + interactCooldownSeconds;

            OnInteract(context);
        }

        protected abstract bool CanInteractInternal(WorldInteractionContext context);
        protected abstract void OnInteract(WorldInteractionContext context);
    }
}

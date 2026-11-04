using UnityEngine;

namespace Game.Interaction
{
    /// <summary>从 Physics 命中 Collider 解析 <see cref="IWorldInteractable"/>。</summary>
    public static class WorldInteractableResolver
    {
        public static IWorldInteractable Resolve(Collider hitCollider)
        {
            if (hitCollider == null)
                return null;

            var direct = hitCollider.GetComponentInParent<IWorldInteractable>();
            if (direct != null)
                return direct;

            var relay = hitCollider.GetComponentInParent<WorldInteractableRelay>();
            return relay != null ? relay.Target : null;
        }
    }

    /// <summary>将子物体/兄弟物体的 Collider 命中转发到真正的交互组件。</summary>
    [DisallowMultipleComponent]
    public sealed class WorldInteractableRelay : MonoBehaviour
    {
        [SerializeField] private WorldInteractableBase target;

        public IWorldInteractable Target => target;

        public void Bind(WorldInteractableBase interactable) => target = interactable;

        public bool OwnsCollider(Collider col)
        {
            if (col == null || target == null)
                return false;

            if (target.InteractionCollider == col)
                return true;

            return col.transform == transform || col.transform.IsChildOf(transform);
        }
    }
}

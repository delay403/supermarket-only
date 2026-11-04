using UnityEngine;

namespace Game.Interaction
{
    /// <summary>场景内可通过射线点击/触摸交互的对象（非 UI）。</summary>
    public interface IWorldInteractable
    {
        /// <summary>是否允许交互（如上锁、冷却中返回 false）。</summary>
        bool CanInteract(WorldInteractionContext context);

        /// <summary>交互优先级；同距离时越大越优先。</summary>
        int InteractionPriority { get; }

        /// <summary>执行交互。</summary>
        void Interact(WorldInteractionContext context);

        /// <summary>可选：用于射线检测的 Collider；为 null 时使用挂载物体上的 Collider。</summary>
        Collider InteractionCollider { get; }
    }
}

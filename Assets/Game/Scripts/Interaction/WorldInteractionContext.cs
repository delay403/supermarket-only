using UnityEngine;

namespace Game.Interaction
{
    public enum WorldInteractionPointerKind
    {
        Mouse = 0,
        Touch = 1,
    }

    /// <summary>一次世界交互的上下文信息。</summary>
    public readonly struct WorldInteractionContext
    {
        public readonly Camera Camera;
        public readonly Vector2 ScreenPosition;
        public readonly int PointerId;
        public readonly WorldInteractionPointerKind PointerKind;
        public readonly RaycastHit Hit;
        public readonly Transform InteractorRoot;

        public WorldInteractionContext(
            Camera camera,
            Vector2 screenPosition,
            int pointerId,
            WorldInteractionPointerKind pointerKind,
            RaycastHit hit,
            Transform interactorRoot)
        {
            Camera = camera;
            ScreenPosition = screenPosition;
            PointerId = pointerId;
            PointerKind = pointerKind;
            Hit = hit;
            InteractorRoot = interactorRoot;
        }

        /// <summary>交互按钮触发时使用；无需真实射线命中。</summary>
        public static WorldInteractionContext CreateForInteractor(
            Camera camera,
            Transform interactorRoot,
            IWorldInteractable target)
        {
            var hit = default(RaycastHit);
            var col = target?.InteractionCollider;
            if (col != null && interactorRoot != null)
            {
                var origin = interactorRoot.position + Vector3.up * 0.5f;
                var delta = col.bounds.center - origin;
                var maxDist = delta.magnitude + 0.5f;
                if (maxDist > 0.01f)
                {
                    Physics.Raycast(
                        origin,
                        delta / maxDist,
                        out hit,
                        maxDist,
                        ~0,
                        QueryTriggerInteraction.Collide);
                }
            }

            return new WorldInteractionContext(
                camera,
                Vector2.zero,
                -1,
                WorldInteractionPointerKind.Touch,
                hit,
                interactorRoot);
        }
    }
}

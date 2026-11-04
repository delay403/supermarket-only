using UnityEngine;

namespace Game.Interaction
{
    /// <summary>
    /// 单一用途交互触发区：进入后显示对应图标的 HUD 按钮，点击触发唯一绑定的目标。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WorldInteractionSlot : MonoBehaviour, IWorldInteractionArea
    {
        [SerializeField] private WorldInteractionKind kind = WorldInteractionKind.Custom;
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private int selectionPriority = 10;
        [SerializeField] private WorldInteractableBase interactableTarget;
        [SerializeField] private MonoBehaviour handlerTarget;

        public WorldInteractionKind Kind => kind;
        public Sprite IconSprite => iconSprite;
        public Transform InteractionTransform => transform;

        void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        public void BindInteractable(WorldInteractableBase target)
        {
            interactableTarget = target;
            if (target != null)
                handlerTarget = null;
        }

        public void BindHandler(MonoBehaviour handler)
        {
            handlerTarget = handler;
            if (handler != null)
                interactableTarget = null;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!WorldInteractionPromptController.IsPlayerCollider(other))
                return;

            WorldInteractionPromptController.EnsureForPlayer()?.RegisterArea(
                this,
                WorldInteractionPromptController.ResolveInteractorRoot(other));
        }

        void OnTriggerExit(Collider other)
        {
            if (!WorldInteractionPromptController.IsPlayerCollider(other))
                return;

            WorldInteractionPromptController.Instance?.UnregisterArea(
                this,
                WorldInteractionPromptController.ResolveInteractorRoot(other));
        }

        public bool TrySelectInteraction(
            Transform interactorRoot,
            out IWorldInteractable interactable,
            out IWorldInteractionZoneHandler handler,
            out Sprite promptIcon,
            out int selectionPriorityOut)
        {
            interactable = null;
            handler = null;
            promptIcon = iconSprite;
            selectionPriorityOut = selectionPriority;

            if (interactorRoot == null)
                return false;

            if (interactableTarget != null)
            {
                interactable = interactableTarget;
                return true;
            }

            if (handlerTarget is IWorldInteractionZoneHandler zoneHandler)
            {
                handler = zoneHandler;
                return true;
            }

            return false;
        }
    }
}

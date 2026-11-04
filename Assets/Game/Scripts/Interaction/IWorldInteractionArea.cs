using UnityEngine;

namespace Game.Interaction
{
    /// <summary>
    /// 玩家进入/离开时可向 <see cref="WorldInteractionPromptController"/> 注册的交互区域。
    /// </summary>
    public interface IWorldInteractionArea
    {
        Transform InteractionTransform { get; }

        bool TrySelectInteraction(
            Transform interactorRoot,
            out IWorldInteractable interactable,
            out IWorldInteractionZoneHandler handler,
            out Sprite promptIcon,
            out int selectionPriority);
    }
}

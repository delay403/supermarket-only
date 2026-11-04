using UnityEngine;

namespace Game.Interaction
{
    /// <summary>
    /// 非 <see cref="IWorldInteractable"/> 的自定义交互（如燃料放置区灌注），由交互按钮触发。
    /// </summary>
    public interface IWorldInteractionZoneHandler
    {
        int InteractionPriority { get; }

        bool CanInteract(Transform interactorRoot);

        void Interact(Transform interactorRoot);
    }
}

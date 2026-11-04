using Game.Interaction;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 燃料生产器交互入口：引用生产按钮对应的独立 <see cref="WorldInteractionSlot"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FuelProducerInteractionHub : MonoBehaviour
    {
        [SerializeField] private WorldInteractionSlot produceSlot;

        public WorldInteractionSlot ProduceSlot => produceSlot;

        public void Bind(FuelProducerProduceButton produceButton, WorldInteractionSlot produceTrigger = null)
        {
            if (produceTrigger != null)
                produceSlot = produceTrigger;

            if (produceSlot != null && produceButton != null)
                produceSlot.BindInteractable(produceButton);
        }
    }
}

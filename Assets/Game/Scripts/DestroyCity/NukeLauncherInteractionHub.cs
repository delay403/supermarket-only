using Game.Interaction;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 核弹发射器交互入口：引用发射、灌注燃料各自独立的 <see cref="WorldInteractionSlot"/> 触发区。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NukeLauncherInteractionHub : MonoBehaviour
    {
        [SerializeField] private WorldInteractionSlot launchSlot;
        [SerializeField] private WorldInteractionSlot depositFuelSlot;

        public WorldInteractionSlot LaunchSlot => launchSlot;
        public WorldInteractionSlot DepositFuelSlot => depositFuelSlot;

        public void Bind(
            NukeLaunchButton launchButton,
            NukeFuelDepositZone depositZone,
            WorldInteractionSlot launchTrigger = null,
            WorldInteractionSlot depositTrigger = null)
        {
            if (launchTrigger != null)
                launchSlot = launchTrigger;
            if (depositTrigger != null)
                depositFuelSlot = depositTrigger;

            if (launchSlot != null && launchButton != null)
                launchSlot.BindInteractable(launchButton);

            if (depositFuelSlot != null && depositZone != null)
                depositFuelSlot.BindHandler(depositZone);
        }
    }
}

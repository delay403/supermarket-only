using Game.Fishing;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 将 HUD / FGUI 钓鱼按钮与 <see cref="PlayerFishingController"/> 桥接。
    /// 挂于玩家或 HUD 根节点，由 UI 层调用公开方法。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingHudInputBridge : MonoBehaviour
    {
        [SerializeField] private PlayerFishingController fishing;

        public PlayerFishingController Fishing => ResolveFishing();

        PlayerFishingController ResolveFishing()
        {
            if (fishing != null)
                return fishing;

            var motor = GetComponent<PlayerMotor>() ?? GetComponentInParent<PlayerMotor>();
            if (motor == null)
                return null;

            fishing = motor.GetComponent<PlayerFishingController>();
            if (fishing == null && GameplayModuleContext.IsModuleActive(GameplayModule.Fishing))
                fishing = motor.gameObject.AddComponent<PlayerFishingController>();
            return fishing;
        }

        public bool CanStartFishing()
        {
            var f = ResolveFishing();
            if (f == null || !f.IsInsideFishingZone || f.IsSessionActive)
                return false;

            var lineDriver = f.GetComponent<PlayerFishingLineDriver>();
            if (lineDriver != null && lineDriver.IsCatchRetrieveBlockingRefish)
                return false;

            var catchNotifier = f.GetComponent<FishingCatchNotificationPresenter>();
            if (catchNotifier != null && catchNotifier.IsAwaitingCatchRewardToast)
                return false;

            return true;
        }

        public bool CanReelOrCancel()
        {
            var f = ResolveFishing();
            return f != null && f.IsSessionActive;
        }

        public bool CanReel()
        {
            var f = ResolveFishing();
            return f != null && f.CanShowReelButton();
        }

        public void OnFishingButtonPressed()
        {
            OnFishingButtonPressedWithResult();
        }

        public bool OnFishingButtonPressedWithResult()
        {
            var f = ResolveFishing();
            if (f == null)
                return false;

            if (f.IsSessionActive)
            {
                if (!f.IsGameplayActive)
                    return false;
                return f.TryReel();
            }

            return f.TryStartFishing();
        }

        public void OnCancelFishingPressed()
        {
            ResolveFishing()?.CancelFishing();
        }

        public void OnReelButtonPressed()
        {
            ResolveFishing()?.TryReel();
        }
    }
}

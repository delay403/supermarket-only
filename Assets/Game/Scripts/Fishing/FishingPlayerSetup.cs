using Game.Collectibles.Stats;
using Game.Core;
using Game.DestroyCity;
using Game.Fishing;
using Game.Interaction;
using Game.Player;
using Game.WormDodge;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>钓鱼场景玩家装配：仅在本玩法激活时运行，并清理其它模式误挂组件。</summary>
    public static class FishingPlayerSetup
    {
        public static void RefreshSceneServices()
        {
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.Fishing))
                return;

            var motor = Object.FindObjectOfType<PlayerMotor>();
            if (motor == null)
                return;

            ConfigurePlayer(motor.gameObject);
        }

        public static void ConfigurePlayer(GameObject player)
        {
            if (player == null || !GameplayModuleContext.IsModuleActive(GameplayModule.Fishing))
                return;

            DestroyCityPlayerSetup.SuppressDestroyCityComponents(player);
            DestroyCityPlayerSetup.SuppressWormDodgeComponents(player);
            DestroyCityPlayerSetup.SuppressSupermarketOrganizeComponents(player);
            EnableFishingComponents(player);
        }

        static void EnableFishingComponents(GameObject player)
        {
            EnableIfPresent<PlayerFishingController>(player);
            EnableIfPresent<FishingStatsAggregator>(player);
            EnableIfPresent<FishingCameraFeedback>(player);
            EnableIfPresent<FishingCatchNotificationPresenter>(player);
            EnableIfPresent<PlayerFishingLineDriver>(player);
            EnableIfPresent<PlayerFishingAnimatorDriver>(player);
            EnableIfPresent<PlayerFishingCatchPresenter>(player);
            EnableIfPresent<FishingPresentationFx>(player);
            EnableIfPresent<FishingHudInputBridge>(player);
            EnableIfPresent<FishingPlaytestInput>(player);
            EnableIfPresent<PlayerAutoFishingController>(player);

            var rig = player.GetComponent<PlayerRig>();
            if (rig != null && !rig.enabled)
                rig.enabled = true;
        }

        static void EnableIfPresent<T>(GameObject player) where T : Behaviour
        {
            var c = player.GetComponent<T>();
            if (c != null)
                c.enabled = true;
        }
    }
}

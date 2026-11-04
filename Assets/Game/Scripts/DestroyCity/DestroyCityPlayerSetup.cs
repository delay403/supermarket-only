using Game.Collectibles.Stats;
using Game.Core;
using Game.EscapeSnake;
using Game.Fishing;
using Game.Interaction;
using Game.Player;
using Game.UI.Framework;
using Game.SupermarketOrganize;
using Game.WormDodge;
using Game.WormDodge.Pickups;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 摧毁城市玩家侧装配（对标钓鱼场景的 <see cref="PlayerFishingController"/> 自检逻辑）。
    /// 由 <see cref="PlayerDestroyCityController"/> 与 <see cref="Game.Core.GameBootstrapper"/> 调用。
    /// </summary>
    public static class DestroyCityPlayerSetup
    {
        public const string InteractableLayerName = "WorldInteractable";

        public static void ConfigurePlayer(GameObject player, Camera gameplayCamera = null)
        {
            if (player == null || !GameplayModuleContext.IsModuleActive(GameplayModule.DestroyCity))
                return;

            DestroyCityPlayerSetup.SuppressFishingComponents(player);
            DestroyCityPlayerSetup.SuppressWormDodgeComponents(player);
            SuppressSupermarketOrganizeComponents(player);

            if (player.GetComponent<DestroyCityStatsAggregator>() == null)
                player.AddComponent<DestroyCityStatsAggregator>();

            if (player.GetComponent<PlayerFuelCarryController>() == null)
                player.AddComponent<PlayerFuelCarryController>();

            if (player.GetComponent<PlayerDestroyCityController>() == null)
                player.AddComponent<PlayerDestroyCityController>();

            var interactor = player.GetComponent<WorldRaycastInteractor>();
            if (interactor == null)
                interactor = player.AddComponent<WorldRaycastInteractor>();

            gameplayCamera ??= Camera.main;
            var mask = LayerMask.GetMask(InteractableLayerName);
            if (mask == 0)
                mask = ~0;

            interactor.Configure(gameplayCamera, player.transform, mask);

            if (player.GetComponent<WorldInteractionPromptController>() == null)
                player.AddComponent<WorldInteractionPromptController>();

            var rig = player.GetComponent<PlayerRig>();
            if (rig != null && !rig.enabled)
                rig.enabled = true;
        }

        public static void SuppressFishingComponents(GameObject player)
        {
            if (player == null)
                return;

            DisableIfPresent<PlayerFishingController>(player);
            DisableIfPresent<FishingStatsAggregator>(player);
            DisableIfPresent<FishingCameraFeedback>(player);
            DisableIfPresent<FishingCatchNotificationPresenter>(player);
            DisableIfPresent<PlayerFishingLineDriver>(player);
            DisableIfPresent<PlayerFishingAnimatorDriver>(player);
            DisableIfPresent<PlayerFishingCatchPresenter>(player);
            DisableIfPresent<FishingPresentationFx>(player);
            DisableIfPresent<FishingHudInputBridge>(player);
            DisableIfPresent<FishingPlaytestInput>(player);
            DisableIfPresent<PlayerAutoFishingController>(player);
        }

        public static void SuppressDestroyCityComponents(GameObject player)
        {
            if (player == null)
                return;

            DisableIfPresent<DestroyCityStatsAggregator>(player);
            DisableIfPresent<PlayerFuelCarryController>(player);
            DisableIfPresent<PlayerDestroyCityController>(player);
            DisableIfPresent<WorldRaycastInteractor>(player);
            DisableIfPresent<WorldInteractionPromptController>(player);
        }

        public static void SuppressWormDodgeComponents(GameObject player)
        {
            if (player == null)
                return;

            DisableIfPresent<WormDodgeParticipant>(player);
            DisableIfPresent<WormChaseTarget>(player);
            DisableIfPresent<WormDodgeStatsAggregator>(player);
            DisableIfPresent<WormDodgePlayerMotorBoost>(player);
            DisableIfPresent<WormDodgeParticipantPickupEffects>(player);
            DisableIfPresent<PlayerWormDodgeController>(player);
        }

        public static void SuppressSupermarketOrganizeComponents(GameObject player)
        {
            if (player == null)
                return;

            DisableIfPresent<SupermarketOrganizeStatsAggregator>(player);
            DisableIfPresent<PlayerSupermarketOrganizeController>(player);
            DisableIfPresent<SupermarketCrosshairInteractor>(player);
            DisableIfPresent<SupermarketItemHighlightController>(player);
            DisableIfPresent<SupermarketFirstPersonCamera>(player);
        }

        static void DisableIfPresent<T>(GameObject player) where T : Behaviour
        {
            var c = player.GetComponent<T>();
            if (c != null)
                c.enabled = false;
        }

        public static void RefreshSceneServices()
        {
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.DestroyCity))
                return;

            FuelProducerService.RefreshWorldAreaUnlocks();

            var motor = UnityEngine.Object.FindObjectOfType<PlayerMotor>();
            if (motor != null)
                ConfigurePlayer(motor.gameObject);

            var hud = UnityEngine.Object.FindObjectOfType<DestroyCityHudSection>(true);
            hud?.RefreshAll();
            UiCoordinator.RefreshDestroyCityStats();
            EnsureMissileFlightCamera();
            EnsureLaunchViewModeController();
            RefreshNukeLaunchers();
            NukeHouseDestructionService.InvalidateCache();
            NukeHouseDestructionService.EnsureCached();
        }

        static void EnsureLaunchViewModeController()
        {
            if (UnityEngine.Object.FindObjectOfType<NukeLaunchViewModeController>() != null)
                return;

            var go = new GameObject(nameof(NukeLaunchViewModeController));
            go.AddComponent<NukeLaunchViewModeController>();
        }

        static void EnsureMissileFlightCamera()
        {
            if (UnityEngine.Object.FindObjectOfType<NukeMissileFlightCamera>() != null)
                return;

            var go = new GameObject(nameof(NukeMissileFlightCamera));
            go.AddComponent<NukeMissileFlightCamera>();
        }

        static void RefreshNukeLaunchers()
        {
            var launchers = UnityEngine.Object.FindObjectsOfType<NukeLauncherController>(true);
            NukeLauncherDiagnostics.Log($"RefreshNukeLaunchers | count={launchers.Length}");
            for (var i = 0; i < launchers.Length; i++)
            {
                var launcher = launchers[i];
                if (launcher == null)
                    continue;

                NukeLauncherDiagnostics.Log(
                    $"RefreshNukeLaunchers[{i}] | go={launcher.name} active={launcher.isActiveAndEnabled}",
                    launcher);
                launcher.RefreshAfterBootstrap();
            }
        }
    }
}

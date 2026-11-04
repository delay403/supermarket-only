using Game.Collectibles.Stats;
using Game.Core;
using Game.DestroyCity;
using Game.EscapeSnake;
using Game.Player;
using Game.SupermarketOrganize;
using Game.WormDodge;
using Game.WormDodge.Pickups;
using Game.AnimalHospital.Config;
using UnityEngine;

namespace Game.AnimalHospital
{
    public static class AnimalHospitalPlayerSetup
    {
        public static void ConfigurePlayer(GameObject player, Camera gameplayCamera = null)
        {
            if (player == null || !GameplayModuleContext.IsModuleActive(GameplayModule.AnimalHospital))
                return;

            ApplyPlayerComponents(player, gameplayCamera);
        }

        public static void ConfigurePlayerForSceneSetup(GameObject player, Camera gameplayCamera = null)
        {
            if (player == null)
                return;

            ApplyPlayerComponents(player, gameplayCamera);
        }

        static void ApplyPlayerComponents(GameObject player, Camera gameplayCamera)
        {
            DestroyCityPlayerSetup.SuppressFishingComponents(player);
            DestroyCityPlayerSetup.SuppressDestroyCityComponents(player);
            SupermarketOrganizePlayerSetup.SuppressSupermarketOrganizeComponents(player);
            SuppressWormDodgeComponents(player);

            if (player.GetComponent<AnimalHospitalHighlightController>() == null)
                player.AddComponent<AnimalHospitalHighlightController>();

            if (player.GetComponent<AnimalHospitalCrosshairInteractor>() == null)
                player.AddComponent<AnimalHospitalCrosshairInteractor>();

            if (player.GetComponent<AnimalHospitalTaserBeamController>() == null)
                player.AddComponent<AnimalHospitalTaserBeamController>();

            if (player.GetComponent<AnimalHospitalPropHandPresenter>() == null)
                player.AddComponent<AnimalHospitalPropHandPresenter>();

            if (player.GetComponent<AnimalHospitalPropCombatController>() == null)
                player.AddComponent<AnimalHospitalPropCombatController>();

            var rig = player.GetComponent<PlayerRig>();
            if (rig != null && !rig.enabled)
                rig.enabled = true;

            ConfigureFirstPersonCamera(player, gameplayCamera);
        }

        public static void ConfigureFirstPersonCamera(GameObject player, Camera gameplayCamera = null)
        {
            if (player == null)
                return;

            gameplayCamera ??= Camera.main;
            if (gameplayCamera == null)
                return;

            var thirdPerson = gameplayCamera.GetComponent<ThirdPersonCamera>();
            if (thirdPerson == null)
                thirdPerson = Object.FindObjectOfType<ThirdPersonCamera>();

            var fpCam = gameplayCamera.GetComponent<AnimalHospitalFirstPersonCamera>();
            if (fpCam == null)
                fpCam = gameplayCamera.gameObject.AddComponent<AnimalHospitalFirstPersonCamera>();

            fpCam.Configure(
                player.transform,
                player.GetComponent<LegacyPlayerInput>(),
                AnimalHospitalConfig.ResolveRuntime(),
                thirdPerson);
        }

        public static void SuppressAnimalHospitalComponents(GameObject player)
        {
            if (player == null)
                return;

            DisableIfPresent<AnimalHospitalCrosshairInteractor>(player);
            DisableIfPresent<AnimalHospitalHighlightController>(player);
            DisableIfPresent<AnimalHospitalFirstPersonCamera>(player);
            DisableIfPresent<AnimalHospitalTaserBeamController>(player);
            DisableIfPresent<AnimalHospitalPropHandPresenter>(player);
            DisableIfPresent<AnimalHospitalPropCombatController>(player);
        }

        static void SuppressWormDodgeComponents(GameObject player)
        {
            DisableIfPresent<WormDodgeParticipant>(player);
            DisableIfPresent<Game.EscapeSnake.WormChaseTarget>(player);
            DisableIfPresent<Game.Collectibles.Stats.WormDodgeStatsAggregator>(player);
            DisableIfPresent<WormDodgePlayerMotorBoost>(player);
            DisableIfPresent<WormDodgeParticipantPickupEffects>(player);
            DisableIfPresent<PlayerWormDodgeController>(player);
        }

        static void DisableIfPresent<T>(GameObject player) where T : Behaviour
        {
            var c = player.GetComponent<T>();
            if (c != null)
                c.enabled = false;
        }

        public static void RefreshSceneServices()
        {
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.AnimalHospital))
                return;

            var motor = Object.FindObjectOfType<PlayerMotor>();
            if (motor != null)
                ConfigurePlayer(motor.gameObject);

            var director = Object.FindObjectOfType<AnimalHospitalDirector>();
            director?.BuildRuntimeGraph();

            var hud = Object.FindObjectOfType<AnimalHospitalHudSection>(true);
            hud?.RefreshAll();
        }
    }
}

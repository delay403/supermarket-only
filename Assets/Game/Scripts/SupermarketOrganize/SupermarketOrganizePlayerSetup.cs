using Game.Collectibles.Stats;
using Game.Core;
using Game.DestroyCity;
using Game.EscapeSnake;
using Game.Player;
using Game.WormDodge;
using Game.WormDodge.Pickups;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    public static class SupermarketOrganizePlayerSetup
    {
        public static void ConfigurePlayer(GameObject player, Camera gameplayCamera = null)
        {
            if (player == null || !GameplayModuleContext.IsModuleActive(GameplayModule.SupermarketOrganize))
                return;

            ApplyPlayerComponents(player, gameplayCamera);
        }

        /// <summary>编辑器场景布置时可跳过模块判定，仅装配第一人称与玩法组件。</summary>
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
            SuppressWormDodgeComponents(player);
            Game.AnimalHospital.AnimalHospitalPlayerSetup.SuppressAnimalHospitalComponents(player);

            if (player.GetComponent<SupermarketOrganizeStatsAggregator>() == null)
                player.AddComponent<SupermarketOrganizeStatsAggregator>();

            if (player.GetComponent<PlayerSupermarketOrganizeController>() == null)
                player.AddComponent<PlayerSupermarketOrganizeController>();

            if (player.GetComponent<SupermarketItemHighlightController>() == null)
                player.AddComponent<SupermarketItemHighlightController>();

            if (player.GetComponent<SupermarketCrosshairInteractor>() == null)
                player.AddComponent<SupermarketCrosshairInteractor>();

            if (player.GetComponent<SupermarketOrganizeSkillController>() == null)
                player.AddComponent<SupermarketOrganizeSkillController>();

            if (player.GetComponent<SupermarketOrganizeAttributeApplicator>() == null)
                player.AddComponent<SupermarketOrganizeAttributeApplicator>();

            if (player.GetComponent<SupermarketOrganizePlayerMotorBoost>() == null)
                player.AddComponent<SupermarketOrganizePlayerMotorBoost>();

            gameplayCamera ??= Camera.main;

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

            var fpCam = gameplayCamera.GetComponent<SupermarketFirstPersonCamera>();
            if (fpCam == null)
                fpCam = gameplayCamera.gameObject.AddComponent<SupermarketFirstPersonCamera>();

            fpCam.Configure(
                player.transform,
                player.GetComponent<LegacyPlayerInput>(),
                SupermarketOrganizeConfig.ResolveRuntime(),
                thirdPerson);

            SupermarketHandleZoneAnchor.EnsureOnCamera(
                player.transform,
                gameplayCamera,
                SupermarketOrganizeConfig.ResolveRuntime());
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
            DisableIfPresent<SupermarketOrganizeSkillController>(player);
            DisableIfPresent<SupermarketOrganizeAttributeApplicator>(player);
            DisableIfPresent<SupermarketOrganizePlayerMotorBoost>(player);
        }

        static void SuppressWormDodgeComponents(GameObject player)
        {
            DisableIfPresent<WormDodgeParticipant>(player);
            DisableIfPresent<WormChaseTarget>(player);
            DisableIfPresent<WormDodgeStatsAggregator>(player);
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
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.SupermarketOrganize))
                return;

            var motor = Object.FindObjectOfType<PlayerMotor>();
            if (motor != null)
                ConfigurePlayer(motor.gameObject);

            var director = Object.FindObjectOfType<SupermarketOrganizeDirector>();
            director?.BuildRuntimeGraph();

            var skillHud = Object.FindObjectOfType<SupermarketOrganizeSkillHud>(true);
            if (skillHud != null && director?.SkillController != null)
                skillHud.Bind(director.SkillController);

            var hud = Object.FindObjectOfType<SupermarketOrganizeHudSection>(true);
            hud?.RefreshAll();
        }
    }
}

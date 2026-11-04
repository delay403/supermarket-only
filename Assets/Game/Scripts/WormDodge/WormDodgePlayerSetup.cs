using Game.AI;
using Game.Collectibles.Stats;
using Game.Core;
using Game.DestroyCity;
using Game.EscapeSnake;
using Game.Player;
using Game.UI;
using Game.UI.Framework;
using Game.WormDodge.Pickups;
using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// 躲避虫子场景玩家装配（对标 <see cref="DestroyCity.DestroyCityPlayerSetup"/>）。
    /// </summary>
    public static class WormDodgePlayerSetup
    {
        public static void ConfigurePlayer(GameObject player, Transform spawnPoint = null)
        {
            if (player == null || !GameplayModuleContext.IsModuleActive(GameplayModule.WormDodge))
                return;

            DestroyCityPlayerSetup.SuppressFishingComponents(player);
            DestroyCityPlayerSetup.SuppressDestroyCityComponents(player);
            SupermarketOrganize.SupermarketOrganizePlayerSetup.SuppressSupermarketOrganizeComponents(player);

            var participant = player.GetComponent<WormDodgeParticipant>();
            if (participant == null)
                participant = player.AddComponent<WormDodgeParticipant>();

            var target = player.GetComponent<WormChaseTarget>();
            if (target == null)
                target = player.AddComponent<WormChaseTarget>();

            participant.ConfigureForPlayer(true, spawnPoint);
            target.SetHideOnDeath(false);

            if (player.transform.Find("SafeZoneIndicator") != null)
                WormDodgePresenceSetup.EnsureSafeZoneIndicator(player);

            var rig = player.GetComponent<PlayerRig>();
            if (rig != null && !rig.enabled)
                rig.enabled = true;

            EnsureWormDodgeStats(player);
        }

        static void EnsureWormDodgeStats(GameObject player)
        {
            if (player.GetComponent<WormDodgeStatsAggregator>() == null)
                player.AddComponent<WormDodgeStatsAggregator>();

            if (player.GetComponent<WormDodgePlayerMotorBoost>() == null)
                player.AddComponent<WormDodgePlayerMotorBoost>();

            if (player.GetComponent<WormDodgeParticipantPickupEffects>() == null)
                player.AddComponent<WormDodgeParticipantPickupEffects>();
        }

        public static void RefreshSceneServices()
        {
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.WormDodge))
                return;

            var motor = Object.FindObjectOfType<PlayerMotor>();
            if (motor != null)
            {
                ConfigurePlayer(motor.gameObject);
                if (motor.GetComponent<PlayerWormDodgeController>() == null)
                    motor.gameObject.AddComponent<PlayerWormDodgeController>();
            }

            WormDodgeAudioBootstrap.EnsureRegistered();
            EnsureAiTriggerPhysics();
            EnsureAiSafeZoneIndicators();
            EnsureWormMotorAudio();
            EnsureWormCatchTriggers();

            var director = Object.FindObjectOfType<WormDodgeDirector>();
            director?.RefreshSceneServices();

            UiCoordinator.RefreshWormDodgeStats();

            WormDodgeLeaderboardHud.EnsureInstalledUnder(
                Object.FindObjectOfType<Game.Hud.GameHudUguiPanel>(true)?.transform);

            var hud = Object.FindObjectOfType<WormDodgeHudSection>(true);
            hud?.RefreshAll();
            UiCoordinator.Hud?.RefreshEconomyDisplay();
            var uguiHud = Object.FindObjectOfType<Game.Hud.GameHudUguiPanel>(true);
            uguiHud?.RefreshWormDodgeStaminaDisplay();
        }

        static void EnsureAiTriggerPhysics()
        {
#if UNITY_2023_1_OR_NEWER
            var participants = Object.FindObjectsByType<WormDodgeParticipant>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var participants = Object.FindObjectsOfType<WormDodgeParticipant>();
#endif
            for (var i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                if (participant == null || participant.IsPlayer)
                    continue;

                var body = participant.GetComponent<Rigidbody>();
                if (body == null)
                    body = participant.gameObject.AddComponent<Rigidbody>();

                body.isKinematic = true;
                body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezeRotation;

                var capsule = participant.GetComponent<CapsuleCollider>();
                if (capsule != null)
                    capsule.isTrigger = false;
            }
        }

        static void EnsureWormCatchTriggers()
        {
#if UNITY_2023_1_OR_NEWER
            var motors = Object.FindObjectsByType<WormChainMotor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var motors = Object.FindObjectsOfType<WormChainMotor>();
#endif
            for (var i = 0; i < motors.Length; i++)
            {
                var motor = motors[i];
                if (motor == null)
                    continue;

                EnsureWormTriggerRigidbody(motor.gameObject);

                if (motor.GetComponentInChildren<WormCatchTrigger>(true) != null)
                    continue;

                var colliders = motor.GetComponentsInChildren<Collider>(true);
                for (var c = 0; c < colliders.Length; c++)
                {
                    var col = colliders[c];
                    if (col == null || !col.isTrigger)
                        continue;

                    col.gameObject.AddComponent<WormCatchTrigger>();
                }
            }
        }

        static void EnsureWormTriggerRigidbody(GameObject wormRoot)
        {
            if (wormRoot == null)
                return;

            var body = wormRoot.GetComponent<Rigidbody>();
            if (body == null)
                body = wormRoot.AddComponent<Rigidbody>();

            body.isKinematic = true;
            body.useGravity = false;
        }

        static void EnsureAiSafeZoneIndicators()
        {
#if UNITY_2023_1_OR_NEWER
            var behaviors = Object.FindObjectsByType<AIFakeWormDodgeBehavior>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var behaviors = Object.FindObjectsOfType<AIFakeWormDodgeBehavior>();
#endif
            for (var i = 0; i < behaviors.Length; i++)
            {
                var behavior = behaviors[i];
                if (behavior == null)
                    continue;

                var characterRoot = behavior.transform.Find("CharacterRoot");
                if (characterRoot != null)
                    WormDodgePresenceSetup.EnsureSafeZoneIndicator(characterRoot.gameObject);
            }
        }

        static void EnsureWormMotorAudio()
        {
            WormDodgeAudioBootstrap.EnsureRegistered();

#if UNITY_2023_1_OR_NEWER
            var motors = Object.FindObjectsByType<WormChainMotor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var motors = Object.FindObjectsOfType<WormChainMotor>();
#endif
            for (var i = 0; i < motors.Length; i++)
            {
                var motor = motors[i];
                if (motor == null)
                    continue;

                if (motor.GetComponent<WormChainMotorAudio>() == null)
                    motor.gameObject.AddComponent<WormChainMotorAudio>();
            }
        }
    }
}

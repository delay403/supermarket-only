using UnityEngine;
using Game.Glide;
using Game.Tower;

namespace Game.Player
{
    public static class RespawnService
    {
        public static void RespawnPlayerToActivePoint()
        {
            var point = RespawnPoint.Active;
            if (point == null)
            {
                Debug.LogWarning("[Respawn] No RespawnPoint.Active found.");
                return;
            }

            var player = Object.FindObjectOfType<PlayerMotor>();
            if (player == null)
            {
                Debug.LogWarning("[Respawn] No PlayerMotor found in scene.");
                return;
            }

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(point.transform.position, point.transform.rotation);
            if (cc != null) cc.enabled = true;

            // Reset gameplay state
            var climb = player.GetComponent<ClimbController>();
            if (climb != null) climb.ExitClimb();

            var glide = player.GetComponent<GlideController>();
            if (glide != null) glide.ResetRuntimeStateForRespawn();

            var stamina = player.GetComponent<Stamina>();
            if (stamina != null) stamina.ResetFull();

            // Unlock input & re-enable normal motor
            var input = player.GetComponent<LegacyPlayerInput>();
            if (input != null) input.SetLocked(false);
            player.enabled = true;

            var cam = Object.FindObjectOfType<ThirdPersonCamera>();
            if (cam != null)
            {
                cam.CancelSettlementYawOrbit();
                cam.SetMode(PlayerMode.FreeMove);
                cam.MarkSpawnCameraLayoutNeeded();
            }

            var anim = player.GetComponentInChildren<PlayerAnimatorDriver>();
            if (anim != null)
            {
                anim.SetGlide(false);
                anim.SetVictory(false);
                anim.ResetJumpTrigger();
                anim.SetClimb(false);
            }
        }
    }
}


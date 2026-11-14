using UnityEngine;

namespace Game.Player
{
    public static class RespawnService
    {
        public static void RespawnPlayerToActivePoint()
        {
            var point = RespawnPoint.Active;
            var player = Object.FindObjectOfType<PlayerMotor>();
            if (point == null || player == null)
                return;

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(point.transform.position, point.transform.rotation);
            if (controller != null) controller.enabled = true;
            player.GetComponent<LegacyPlayerInput>()?.SetLocked(false);
            player.enabled = true;
        }
    }
}

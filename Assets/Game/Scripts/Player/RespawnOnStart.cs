using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Ensures player starts at the active respawn point when entering play mode.
    /// </summary>
    public sealed class RespawnOnStart : MonoBehaviour
    {
        [SerializeField] private bool respawnOnStart = true;

        private void Start()
        {
            if (!respawnOnStart) return;
            RespawnService.RespawnPlayerToActivePoint();
        }
    }
}


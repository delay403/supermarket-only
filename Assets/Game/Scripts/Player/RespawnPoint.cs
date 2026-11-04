using UnityEngine;

namespace Game.Player
{
    public sealed class RespawnPoint : MonoBehaviour
    {
        [Tooltip("If true, this becomes the active respawn point on Awake.")]
        [SerializeField] private bool setActiveOnAwake = true;

        public static RespawnPoint Active { get; private set; }

        private void Awake()
        {
            if (setActiveOnAwake || Active == null)
                Active = this;
        }

        public void MakeActive()
        {
            Active = this;
        }
    }
}


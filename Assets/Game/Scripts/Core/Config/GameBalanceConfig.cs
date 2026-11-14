using UnityEngine;

namespace Game.Core.Config
{
    [CreateAssetMenu(menuName = "Game/Config/GameBalanceConfig", fileName = "GameBalanceConfig")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        [Header("Player Movement")]
        [Tooltip("Maximum ground movement speed in metres per second.")]
        public float moveSpeed = 5f;

        [Tooltip("How quickly the player rotates toward the movement direction.")]
        public float rotationSharpness = 12f;

        [Tooltip("Gravity acceleration in metres per second squared; negative points downward.")]
        public float gravity = -25f;
    }
}

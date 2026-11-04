using UnityEngine;

namespace Game.Core.Config
{
    [CreateAssetMenu(menuName = "Game/Config/Player Jump", fileName = "PlayerJumpConfig")]
    public sealed class PlayerJumpConfig : ScriptableObject
    {
        [Tooltip("起跳瞬间向上的垂直速度（m/s），由 CharacterController 与重力共同作用。")]
        public float jumpVerticalSpeed = 8.5f;

        [Tooltip("两次起跳之间的最短间隔（秒），用于防止连点。")]
        public float minIntervalSeconds = 0.12f;

        [Tooltip("若勾选，则使用下方按键；否则仍用空格（与滑翔共用同键时由跳跃脚本按状态分流）。")]
        public bool useCustomJumpKey;

        public KeyCode jumpKey = KeyCode.Space;
    }
}

using UnityEngine;

namespace Game.Leaderboard
{
    /// <summary>
    /// 单机排行榜虚拟玩家的一条配置（可由编辑器批量生成）。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Leaderboard/Virtual Player", fileName = "VirtualPlayer")]
    public sealed class VirtualPlayerDef : ScriptableObject
    {
        [Tooltip("稳定唯一 ID（运行时漂移字典键）；留空则在加载时用 asset 名或 GUID 兜底。")]
        public string virtualId;

        public string displayName;

        public Sprite portrait;

        [Tooltip("虚拟金币基准（运行时会被缓慢上浮随机漂移）。")]
        public long coins;

        [Tooltip("虚拟最远滑翔距离（米）；运行时漂移。")]
        public float glideDistanceMeters;
    }
}

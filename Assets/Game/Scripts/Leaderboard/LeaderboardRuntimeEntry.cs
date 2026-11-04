using UnityEngine;

namespace Game.Leaderboard
{
    /// <summary>
    /// 排行榜单行运行时模型（真实玩家 + 虚拟玩家合并排序后使用）。
    /// </summary>
    public readonly struct LeaderboardRuntimeEntry
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly Sprite Portrait;
        public readonly long Coins;
        public readonly float GlideDistanceMeters;
        public readonly bool IsLocalPlayer;

        public LeaderboardRuntimeEntry(
            string id,
            string displayName,
            Sprite portrait,
            long coins,
            float glideDistanceMeters,
            bool isLocalPlayer)
        {
            Id = id ?? "";
            DisplayName = displayName ?? "";
            Portrait = portrait;
            Coins = coins < 0 ? 0 : coins;
            GlideDistanceMeters = glideDistanceMeters < 0f ? 0f : glideDistanceMeters;
            IsLocalPlayer = isLocalPlayer;
        }

        public long PrimaryScoreLong(LeaderboardKind kind)
        {
            return kind == LeaderboardKind.Coins ? Coins : (long)GlideDistanceMeters;
        }

        public float PrimaryScoreFloat(LeaderboardKind kind)
        {
            return kind == LeaderboardKind.Coins ? Coins : GlideDistanceMeters;
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Economy;
using Game.Leaderboard;
using Game.Save;
using UnityEngine;

namespace Game.UI.Leaderboard
{
    /// <summary>
    /// World Canvas 排行榜：合并存档真实玩家 + <see cref="VirtualPlayerDatabase"/>，
    /// 虚拟分数定时上浮；名次前进时播放轻微上移动画。
    /// </summary>
    public sealed class LeaderboardWorldPanel : MonoBehaviour
    {
        const string LocalPlayerId = "__local_player__";

        [SerializeField] private LeaderboardKind kind = LeaderboardKind.GlideDistanceMeters;
        [SerializeField] private VirtualPlayerDatabase database;

        [Header("位图字 · 0~9（可与滑翔 HUD 共用一套精灵）")]
        [SerializeField] private Sprite[] digitSprites = new Sprite[10];

        [Header("前三名")]
        [SerializeField] private LeaderboardTopSlotView[] topSlots = new LeaderboardTopSlotView[3];

        [Header("第 4～9 名（自上而下）")]
        [SerializeField] private LeaderboardListRowView[] listRows = new LeaderboardListRowView[6];

        [Header("本地玩家展示")]
        [Tooltip("排行榜名称优先读存档 leaderboardDisplayName；空则用 fallbackLocalDisplayName。")]
        [SerializeField] private string fallbackLocalDisplayName = "我";

        [SerializeField] private Sprite localPlayerPortrait;

        [Tooltip("0～1：虚拟玩家随机显示头像的概率（本地玩家始终遵循是否有头像）。")]
        [Range(0f, 1f)]
        [SerializeField] private float virtualPortraitChance = 0.45f;

        [Header("刷新 / 漂移")]
        [SerializeField] private float resortIntervalSeconds = 2f;

        [SerializeField] private float driftIntervalMinSeconds = 4f;
        [SerializeField] private float driftIntervalMaxSeconds = 14f;

        [Tooltip("金币每次漂移增加的最小值。")]
        [SerializeField] private long driftCoinsMin = 80;

        [Tooltip("金币每次漂移增加的最大值。")]
        [SerializeField] private long driftCoinsMax = 8000;

        [Tooltip("距离每次漂移增加的最小值（米）。")]
        [SerializeField] private float driftDistanceMin = 8f;

        [Tooltip("距离每次漂移增加的最大值（米）。")]
        [SerializeField] private float driftDistanceMax = 220f;

        readonly Dictionary<string, long> _runtimeCoins = new Dictionary<string, long>(64);
        readonly Dictionary<string, float> _runtimeDistance = new Dictionary<string, float>(64);
        readonly Dictionary<string, int> _lastRankById = new Dictionary<string, int>(64);

        float _nextResortUnscaled = -999f;
        float _nextDriftUnscaled = -999f;
        readonly List<LeaderboardRuntimeEntry> _scratch = new List<LeaderboardRuntimeEntry>(48);

        void OnEnable()
        {
            SeedRuntimeFromDatabase();
            _lastRankById.Clear();
            _nextDriftUnscaled = Time.unscaledTime + UnityEngine.Random.Range(driftIntervalMinSeconds, driftIntervalMaxSeconds);
            _nextResortUnscaled = Time.unscaledTime;
        }

        void Update()
        {
            if (database == null || database.players == null || database.players.Count == 0)
            {
                BindEmpty();
                return;
            }

            var now = Time.unscaledTime;
            if (now >= _nextDriftUnscaled)
            {
                _nextDriftUnscaled = now + UnityEngine.Random.Range(driftIntervalMinSeconds, driftIntervalMaxSeconds);
                ApplyRandomVirtualDrift();
                ForceResortSoon();
            }

            if (now >= _nextResortUnscaled)
            {
                _nextResortUnscaled = now + Mathf.Max(0.05f, resortIntervalSeconds);
                ResortAndPushUi();
            }
        }

        void SeedRuntimeFromDatabase()
        {
            _runtimeCoins.Clear();
            _runtimeDistance.Clear();
            foreach (var def in database.players)
            {
                if (def == null)
                    continue;

                var id = VirtualPlayerDatabase.ResolveVirtualId(def);
                if (string.IsNullOrEmpty(id))
                    continue;

                _runtimeCoins[id] = def.coins < 0 ? 0 : def.coins;
                _runtimeDistance[id] = def.glideDistanceMeters < 0f ? 0f : def.glideDistanceMeters;
            }
        }

        void ApplyRandomVirtualDrift()
        {
            var list = database.players;
            var pickIdx = UnityEngine.Random.Range(0, list.Count);
            var def = list[pickIdx];
            if (def == null)
                return;

            var id = VirtualPlayerDatabase.ResolveVirtualId(def);
            if (string.IsNullOrEmpty(id))
                return;

            if (!_runtimeCoins.TryGetValue(id, out var coins))
                coins = def.coins;
            if (!_runtimeDistance.TryGetValue(id, out var dist))
                dist = def.glideDistanceMeters;

            var coinsDelta = RandomNonNegativeLong(driftCoinsMin, driftCoinsMax);
            coins = AddClampedNonNegative(coins, coinsDelta, long.MaxValue / 4);
            dist += UnityEngine.Random.Range(driftDistanceMin, driftDistanceMax);
            dist = Mathf.Clamp(dist, 0f, 1e7f);

            _runtimeCoins[id] = coins;
            _runtimeDistance[id] = dist;
        }

        static long RandomNonNegativeLong(long minInclusive, long maxInclusive)
        {
            var lo = Math.Min(minInclusive, maxInclusive);
            var hi = Math.Max(minInclusive, maxInclusive);
            if (hi <= 0)
                return 0;
            if (lo < 0)
                lo = 0;

            if (hi > int.MaxValue - 1 || hi - lo >= int.MaxValue)
            {
                var span = (decimal)(hi - lo + 1);
                var offset = (long)(decimal.Floor((decimal)UnityEngine.Random.value * span));
                return lo + offset;
            }

            var ilo = (int)lo;
            var ihi = (int)hi;
            return UnityEngine.Random.Range(ilo, ihi + 1);
        }

        static long AddClampedNonNegative(long a, long b, long capExclusiveFallback)
        {
            try
            {
                checked
                {
                    var s = a + b;
                    if (s < 0)
                        s = 0;
                    var cap = capExclusiveFallback;
                    if (cap > 0 && s > cap)
                        s = cap;
                    return s;
                }
            }
            catch (OverflowException)
            {
                return Math.Min(capExclusiveFallback, long.MaxValue);
            }
        }

        void ForceResortSoon()
        {
            _nextResortUnscaled = Time.unscaledTime;
        }

        void ResortAndPushUi()
        {
            BuildSortedScratch(_scratch);
            if (_scratch.Count == 0)
            {
                BindEmpty();
                return;
            }

            DetectRankImprovementsAndPlayFx(_scratch);
            BindTopThree(_scratch);
            BindListFourToNine(_scratch);
            RememberRanks(_scratch);
        }

        void DetectRankImprovementsAndPlayFx(List<LeaderboardRuntimeEntry> sorted)
        {
            var cap = Mathf.Min(9, sorted.Count);
            for (var i = 0; i < cap; i++)
            {
                var e = sorted[i];
                var rank = i + 1;
                if (_lastRankById.TryGetValue(e.Id, out var oldRank))
                {
                    if (rank < oldRank)
                    {
                        if (rank >= 1 && rank <= 3 && topSlots != null && rank - 1 < topSlots.Length)
                            topSlots[rank - 1]?.PlayRankUpFx();
                        else if (rank >= 4 && rank <= 9 && listRows != null)
                        {
                            var slot = rank - 4;
                            if (slot >= 0 && slot < listRows.Length)
                                listRows[slot]?.PlayRankUpFx();
                        }
                    }
                }
            }
        }

        void RememberRanks(List<LeaderboardRuntimeEntry> sorted)
        {
            _lastRankById.Clear();
            var cap = Mathf.Min(9, sorted.Count);
            for (var i = 0; i < cap; i++)
                _lastRankById[sorted[i].Id] = i + 1;
        }

        void BuildSortedScratch(List<LeaderboardRuntimeEntry> dst)
        {
            dst.Clear();

            foreach (var def in database.players)
            {
                if (def == null)
                    continue;

                var id = VirtualPlayerDatabase.ResolveVirtualId(def);
                if (string.IsNullOrEmpty(id))
                    continue;

                var coins = _runtimeCoins.TryGetValue(id, out var c) ? c : def.coins;
                var dist = _runtimeDistance.TryGetValue(id, out var d) ? d : def.glideDistanceMeters;

                dst.Add(new LeaderboardRuntimeEntry(id, def.displayName, def.portrait, coins, dist, false));
            }

            var save = SaveService.Data;
            if (save != null)
            {
                var nm = string.IsNullOrWhiteSpace(save.leaderboardDisplayName)
                    ? fallbackLocalDisplayName
                    : save.leaderboardDisplayName.Trim();

                dst.Add(new LeaderboardRuntimeEntry(
                    LocalPlayerId,
                    nm,
                    localPlayerPortrait,
                    save.coins,
                    save.bestGlideDistanceMeters,
                    true));
            }

            dst.Sort((a, b) => CompareEntries(kind, a, b));

            if (dst.Count > 9)
                dst.RemoveRange(9, dst.Count - 9);
        }

        static int CompareEntries(LeaderboardKind kind, LeaderboardRuntimeEntry a, LeaderboardRuntimeEntry b)
        {
            if (kind == LeaderboardKind.Coins)
            {
                var cmp = b.Coins.CompareTo(a.Coins);
                if (cmp != 0)
                    return cmp;
            }
            else
            {
                var cmp = b.GlideDistanceMeters.CompareTo(a.GlideDistanceMeters);
                if (cmp != 0)
                    return cmp;
            }

            var localCmp = b.IsLocalPlayer.CompareTo(a.IsLocalPlayer);
            if (localCmp != 0)
                return localCmp;

            return string.CompareOrdinal(a.Id, b.Id);
        }

        void BindTopThree(List<LeaderboardRuntimeEntry> sorted)
        {
            if (topSlots == null)
                return;

            for (var i = 0; i < 3 && i < topSlots.Length; i++)
            {
                var slot = topSlots[i];
                if (slot == null)
                    continue;

                if (i >= sorted.Count)
                {
                    slot.Bind(false, "", "", null, false);
                    continue;
                }

                var e = sorted[i];
                slot.Bind(true, e.DisplayName, FormatPrimary(e), e.Portrait, ShouldShowPortrait(e));
            }
        }

        void BindListFourToNine(List<LeaderboardRuntimeEntry> sorted)
        {
            if (listRows == null)
                return;

            for (var i = 0; i < listRows.Length; i++)
            {
                var row = listRows[i];
                if (row == null)
                    continue;

                var idx = 3 + i;
                if (idx >= sorted.Count)
                {
                    row.HideRow();
                    continue;
                }

                var e = sorted[idx];
                var rank = idx + 1;
                row.Bind(kind, digitSprites, rank, e.DisplayName, FormatPrimary(e), e.Portrait, ShouldShowPortrait(e));
            }
        }

        bool ShouldShowPortrait(LeaderboardRuntimeEntry e)
        {
            if (e.Portrait == null)
                return false;

            if (e.IsLocalPlayer)
                return true;

            var threshold = Mathf.Clamp01(virtualPortraitChance);
            var u = StableUnitFloat(e.Id + ":portrait");
            return u < threshold;
        }

        static float StableUnitFloat(string key)
        {
            unchecked
            {
                var h = (uint)key.GetHashCode();
                return (h % 10000) / 10000f;
            }
        }

        string FormatPrimary(LeaderboardRuntimeEntry e)
        {
            if (kind == LeaderboardKind.Coins)
                return EconomyDisplayFormat.Format(e.Coins);

            return $"{Mathf.FloorToInt(e.GlideDistanceMeters)}m";
        }

        void BindEmpty()
        {
            if (topSlots != null)
            {
                foreach (var s in topSlots)
                    s?.Bind(false, "", "", null, false);
            }

            if (listRows != null)
            {
                foreach (var r in listRows)
                    r?.HideRow();
            }
        }
    }
}

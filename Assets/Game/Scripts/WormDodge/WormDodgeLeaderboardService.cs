using System;
using System.Collections.Generic;
using Game.AI;
using Game.Save;
using UnityEngine;

namespace Game.WormDodge
{
    public sealed class WormDodgeLeaderboardEntry
    {
        public WormDodgeParticipant Participant;
        public string DisplayName;
        public int Floor;
        public bool IsPlayer;
        public bool IsAlive;
    }

    /// <summary>
    /// 躲避虫子局内排行榜：使用场景中真实参与者（玩家 + AI）数据。
    /// </summary>
    public static class WormDodgeLeaderboardService
    {
        static readonly List<WormDodgeLeaderboardEntry> Entries = new List<WormDodgeLeaderboardEntry>();

        public static event Action Changed;

        public static IReadOnlyList<WormDodgeLeaderboardEntry> AllEntries => Entries;

        public static int SurvivorCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].IsAlive)
                        count++;
                }

                return count;
            }
        }

        public static int PlayerRank
        {
            get
            {
                for (var i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].IsPlayer)
                        return i + 1;
                }

                return Entries.Count + 1;
            }
        }

        public static void EnsureSeeded()
        {
            if (Entries.Count > 0)
                return;

            SyncFromParticipants(FindSceneParticipants());
        }

        public static void ResetForNewRun(WormDodgeParticipant[] participants)
        {
            Entries.Clear();
            if (participants == null)
            {
                NotifyChanged();
                return;
            }

            for (var i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                if (participant == null)
                    continue;

                Entries.Add(CreateEntryFrom(participant));
            }

            SortAndNotify();
        }

        public static void SyncFromParticipants(WormDodgeParticipant[] participants)
        {
            if (participants == null || participants.Length == 0)
            {
                if (Entries.Count > 0)
                {
                    Entries.Clear();
                    NotifyChanged();
                }

                return;
            }

            var touched = false;
            for (var i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                if (participant == null)
                    continue;

                var entry = FindEntry(participant);
                if (entry == null)
                {
                    Entries.Add(CreateEntryFrom(participant));
                    touched = true;
                    continue;
                }

                if (ApplyParticipantToEntry(entry, participant))
                    touched = true;
            }

            if (touched)
                SortAndNotify();
        }

        public static void ForceRefresh()
        {
            SyncFromParticipants(FindSceneParticipants());
            SortAndNotify();
        }

        public static void SyncPlayer(int floor, bool isAlive, string displayName = "我")
        {
            WormDodgeLeaderboardEntry player = null;
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].IsPlayer)
                {
                    player = Entries[i];
                    break;
                }
            }

            if (player == null)
            {
                player = new WormDodgeLeaderboardEntry { IsPlayer = true };
                Entries.Add(player);
            }

            player.DisplayName = string.IsNullOrWhiteSpace(displayName) ? "我" : displayName.Trim();
            player.Floor = Mathf.Max(0, floor);
            player.IsAlive = isAlive;
            if (player.Participant != null)
                player.Participant.RecordFloorReached(player.Floor);

            SortAndNotify();
        }

        public static void MarkPlayerEliminated(int floor)
        {
            SyncPlayer(floor, false);
        }

        static WormDodgeLeaderboardEntry CreateEntryFrom(WormDodgeParticipant participant)
        {
            var entry = new WormDodgeLeaderboardEntry
            {
                Participant = participant,
                IsPlayer = participant.IsPlayer,
            };
            ApplyParticipantToEntry(entry, participant);
            return entry;
        }

        static bool ApplyParticipantToEntry(WormDodgeLeaderboardEntry entry, WormDodgeParticipant participant)
        {
            var name = participant.IsPlayer
                ? ResolvePlayerDisplayName()
                : ResolveAiDisplayName(participant);
            var floor = participant.HighestFloorReached;
            var alive = participant.IsAliveForLeaderboard;
            if (participant.IsPlayer && WormDodgeDirector.Instance != null)
            {
                alive = WormDodgeDirector.Instance.IsRunActive
                        && !WormDodgeDirector.Instance.IsDefeatPending
                        && alive;
            }

            var changed = entry.DisplayName != name || entry.Floor != floor || entry.IsAlive != alive;
            entry.DisplayName = name;
            entry.Floor = floor;
            entry.IsAlive = alive;
            entry.IsPlayer = participant.IsPlayer;
            entry.Participant = participant;
            return changed;
        }

        static WormDodgeLeaderboardEntry FindEntry(WormDodgeParticipant participant)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Participant == participant)
                    return Entries[i];
            }

            return null;
        }

        static string ResolveAiDisplayName(WormDodgeParticipant participant)
        {
            if (participant == null)
                return "AI";

            var custom = participant.GetLeaderboardDisplayName();
            if (!string.IsNullOrWhiteSpace(custom) &&
                !string.Equals(custom, "CharacterRoot", StringComparison.Ordinal))
                return custom;

            var behavior = participant.GetComponentInParent<AIFakeWormDodgeBehavior>();
            if (behavior != null && !string.IsNullOrWhiteSpace(behavior.DisplayName))
                return behavior.DisplayName.Trim();

            return custom;
        }

        static string ResolvePlayerDisplayName()
        {
            var name = SaveService.Data?.leaderboardDisplayName;
            return string.IsNullOrWhiteSpace(name) ? "我" : name.Trim();
        }

        static WormDodgeParticipant[] FindSceneParticipants()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<WormDodgeParticipant>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<WormDodgeParticipant>(true);
#endif
        }

        static void SortAndNotify()
        {
            Entries.Sort((a, b) =>
            {
                var aliveCompare = b.IsAlive.CompareTo(a.IsAlive);
                if (aliveCompare != 0)
                    return aliveCompare;
                return b.Floor.CompareTo(a.Floor);
            });
            NotifyChanged();
        }

        static void NotifyChanged() => Changed?.Invoke();
    }
}

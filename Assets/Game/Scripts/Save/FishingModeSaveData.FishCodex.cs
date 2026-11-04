using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Save
{
    public sealed partial class FishingModeSaveData
    {
        /// <summary>已发现（至少钓获过一次）的鱼；JsonUtility 列表。</summary>
        public List<FishDiscoveryEntry> fishDiscoveries = new List<FishDiscoveryEntry>();

        [Serializable]
        public struct FishDiscoveryEntry
        {
            public string fishId;
            public int catchCount;
        }

        public bool IsFishDiscovered(string fishId)
        {
            return GetFishCatchCount(fishId) > 0;
        }

        public int GetFishCatchCount(string fishId)
        {
            if (string.IsNullOrWhiteSpace(fishId) || fishDiscoveries == null)
                return 0;

            var id = fishId.Trim();
            for (var i = 0; i < fishDiscoveries.Count; i++)
            {
                if (fishDiscoveries[i].fishId == id)
                    return Mathf.Max(0, fishDiscoveries[i].catchCount);
            }

            return 0;
        }

        public void RecordFishCatch(string fishId)
        {
            if (string.IsNullOrWhiteSpace(fishId))
                return;

            if (fishDiscoveries == null)
                fishDiscoveries = new List<FishDiscoveryEntry>();

            var id = fishId.Trim();
            for (var i = 0; i < fishDiscoveries.Count; i++)
            {
                if (fishDiscoveries[i].fishId != id)
                    continue;

                var e = fishDiscoveries[i];
                e.catchCount = Mathf.Max(0, e.catchCount) + 1;
                fishDiscoveries[i] = e;
                return;
            }

            fishDiscoveries.Add(new FishDiscoveryEntry { fishId = id, catchCount = 1 });
        }

        public void EnsureFishCodexMigrated()
        {
            if (fishDiscoveries == null)
                fishDiscoveries = new List<FishDiscoveryEntry>();
        }
    }
}

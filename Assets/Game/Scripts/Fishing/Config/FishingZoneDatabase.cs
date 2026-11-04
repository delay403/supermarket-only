using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Fishing.Config
{
    [CreateAssetMenu(menuName = "Game/Fishing/FishingZoneDatabase", fileName = "FishingZoneDatabase")]
    public sealed class FishingZoneDatabase : ScriptableObject
    {
        public List<FishingZoneDef> zones = new List<FishingZoneDef>();

        Dictionary<string, FishingZoneDef> _byId;

        public bool TryGet(string id, out FishingZoneDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(id ?? string.Empty, out def);
        }

        void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, FishingZoneDef>(StringComparer.Ordinal);
            foreach (var z in zones)
            {
                if (z == null || string.IsNullOrWhiteSpace(z.zoneId)) continue;
                _byId[z.zoneId] = z;
            }
        }
    }
}

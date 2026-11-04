using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Collectibles.Trails
{
    [CreateAssetMenu(menuName = "Game/Collectibles/TrailDatabase", fileName = "TrailDatabase")]
    public sealed class TrailDatabase : ScriptableObject
    {
        public List<TrailDef> trails = new List<TrailDef>();

        private Dictionary<string, TrailDef> _byId;

        public bool TryGet(string id, out TrailDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(id ?? string.Empty, out def);
        }

        public TrailDef GetOrNull(string id)
        {
            TryGet(id, out var def);
            return def;
        }

        private void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, TrailDef>(StringComparer.Ordinal);
            foreach (var t in trails)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.trailId)) continue;
                _byId[t.trailId] = t;
            }
        }
    }
}


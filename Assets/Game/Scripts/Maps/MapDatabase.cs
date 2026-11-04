using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Maps
{
    [CreateAssetMenu(menuName = "Game/Maps/MapDatabase", fileName = "MapDatabase")]
    public sealed class MapDatabase : ScriptableObject
    {
        public List<MapDef> maps = new List<MapDef>();

        private Dictionary<string, MapDef> _byId;

        public bool TryGet(string mapId, out MapDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(mapId ?? string.Empty, out def);
        }

        public MapDef GetOrNull(string mapId)
        {
            TryGet(mapId, out var def);
            return def;
        }

        private void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, MapDef>(StringComparer.Ordinal);
            foreach (var m in maps)
            {
                if (m == null || string.IsNullOrWhiteSpace(m.mapId)) continue;
                _byId[m.mapId] = m;
            }
        }
    }
}


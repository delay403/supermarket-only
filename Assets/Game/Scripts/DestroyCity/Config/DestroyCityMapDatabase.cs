using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.DestroyCity.Config
{
    [CreateAssetMenu(menuName = "Game/DestroyCity/DestroyCityMapDatabase", fileName = "DestroyCityMapDatabase")]
    public sealed class DestroyCityMapDatabase : ScriptableObject
    {
        public List<DestroyCityMapDef> maps = new List<DestroyCityMapDef>();

        Dictionary<string, DestroyCityMapDef> _byId;

        public bool TryGet(string id, out DestroyCityMapDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(id ?? string.Empty, out def);
        }

        void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, DestroyCityMapDef>(StringComparer.Ordinal);
            foreach (var m in maps)
            {
                if (m == null || string.IsNullOrWhiteSpace(m.mapId)) continue;
                _byId[m.mapId] = m;
            }
        }
    }
}

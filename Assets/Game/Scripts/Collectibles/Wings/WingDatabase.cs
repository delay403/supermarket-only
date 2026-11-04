using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Collectibles.Wings
{
    [CreateAssetMenu(menuName = "Game/Collectibles/WingDatabase", fileName = "WingDatabase")]
    public sealed class WingDatabase : ScriptableObject
    {
        public List<WingDef> wings = new List<WingDef>();

        private Dictionary<string, WingDef> _byId;

        public bool TryGet(string id, out WingDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(id ?? string.Empty, out def);
        }

        public WingDef GetOrNull(string id)
        {
            TryGet(id, out var def);
            return def;
        }

        private void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, WingDef>(StringComparer.Ordinal);
            foreach (var w in wings)
            {
                if (w == null || string.IsNullOrWhiteSpace(w.wingId)) continue;
                _byId[w.wingId] = w;
            }
        }
    }
}


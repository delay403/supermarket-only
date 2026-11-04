using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Collectibles.Rods
{
    [CreateAssetMenu(menuName = "Game/Collectibles/RodDatabase", fileName = "RodDatabase")]
    public sealed class RodDatabase : ScriptableObject
    {
        public List<RodDef> rods = new List<RodDef>();

        Dictionary<string, RodDef> _byId;

        public bool TryGet(string id, out RodDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(id ?? string.Empty, out def);
        }

        public RodDef GetOrNull(string id)
        {
            TryGet(id, out var def);
            return def;
        }

        void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, RodDef>(StringComparer.Ordinal);
            foreach (var r in rods)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.rodId)) continue;
                _byId[r.rodId] = r;
            }
        }
    }
}

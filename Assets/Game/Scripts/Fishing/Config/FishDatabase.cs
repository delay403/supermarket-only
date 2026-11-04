using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Fishing.Config
{
    [CreateAssetMenu(menuName = "Game/Fishing/FishDatabase", fileName = "FishDatabase")]
    public sealed class FishDatabase : ScriptableObject
    {
        public List<FishDef> fish = new List<FishDef>();

        Dictionary<string, FishDef> _byId;

        public bool TryGet(string id, out FishDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(id ?? string.Empty, out def);
        }

        void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, FishDef>(StringComparer.Ordinal);
            foreach (var f in fish)
            {
                if (f == null || string.IsNullOrWhiteSpace(f.fishId)) continue;
                _byId[f.fishId] = f;
            }
        }
    }
}

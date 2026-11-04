using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.DestroyCity.Config
{
    [CreateAssetMenu(menuName = "Game/DestroyCity/FuelProducerDatabase", fileName = "FuelProducerDatabase")]
    public sealed class FuelProducerDatabase : ScriptableObject
    {
        public List<FuelProducerDef> producers = new List<FuelProducerDef>();

        Dictionary<string, FuelProducerDef> _byId;

        public bool TryGet(string id, out FuelProducerDef def)
        {
            EnsureIndex();
            return _byId.TryGetValue(id ?? string.Empty, out def);
        }

        void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, FuelProducerDef>(StringComparer.Ordinal);
            foreach (var p in producers)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.producerId)) continue;
                _byId[p.producerId] = p;
            }
        }
    }
}

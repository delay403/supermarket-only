using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.DestroyCity.Config
{
    [CreateAssetMenu(menuName = "Game/DestroyCity/BuildingDatabase", fileName = "DestroyCityBuildingDatabase")]
    public sealed class DestroyCityBuildingDatabase : ScriptableObject
    {
        public List<DestroyCityBuildingDef> buildings = new List<DestroyCityBuildingDef>();

        Dictionary<DestroyCityBuildingCategory, DestroyCityBuildingDef> _byCategory;

        public bool TryGet(DestroyCityBuildingCategory category, out DestroyCityBuildingDef def)
        {
            EnsureIndex();
            return _byCategory.TryGetValue(category, out def);
        }

        public long GetCoinValue(DestroyCityBuildingCategory category)
        {
            return TryGet(category, out var def) ? def.coinValue : 0L;
        }

        void EnsureIndex()
        {
            if (_byCategory != null)
                return;

            _byCategory = new Dictionary<DestroyCityBuildingCategory, DestroyCityBuildingDef>();
            foreach (var b in buildings)
            {
                if (b == null)
                    continue;
                _byCategory[b.category] = b;
            }
        }
    }
}

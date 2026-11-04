using System.Collections.Generic;
using Game.Core.Config;
using Game.Core.Content;
using UnityEngine;

namespace Game.SupermarketOrganize.Config
{
    [CreateAssetMenu(menuName = "Game/Supermarket Organize/Map Database", fileName = "SupermarketMapDatabase")]
    public sealed class SupermarketMapDatabase : ScriptableObject
    {
        public List<SupermarketMapDef> maps = new List<SupermarketMapDef>();

        public bool TryGet(string mapId, out SupermarketMapDef def)
        {
            def = null;
            if (maps == null || string.IsNullOrWhiteSpace(mapId))
                return false;

            for (var i = 0; i < maps.Count; i++)
            {
                var entry = maps[i];
                if (entry != null && entry.mapId == mapId)
                {
                    def = entry;
                    return true;
                }
            }

            return false;
        }

        public static SupermarketMapDatabase ResolveRuntime() =>
            ConfigRegistry.SupermarketMaps ?? GameContentService.Resolve<SupermarketMapDatabase>();
    }
}

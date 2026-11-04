using System;
using System.Collections.Generic;
using Game.Core.Config;
using Game.Core.Content;
using UnityEngine;

namespace Game.SupermarketOrganize.Config
{
    [CreateAssetMenu(menuName = "Game/Supermarket Organize/Item Database", fileName = "SupermarketItemDatabase")]
    public sealed class SupermarketItemDatabase : ScriptableObject
    {
        public List<SupermarketItemDef> items = new List<SupermarketItemDef>();

        public bool TryGet(string itemId, out SupermarketItemDef def)
        {
            def = null;
            if (items == null || string.IsNullOrWhiteSpace(itemId))
                return false;

            for (var i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                if (entry != null
                    && string.Equals(entry.itemId?.Trim(), itemId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    def = entry;
                    return true;
                }
            }

            return false;
        }

        public static SupermarketItemDatabase ResolveRuntime() =>
            ConfigRegistry.SupermarketItems ?? GameContentService.Resolve<SupermarketItemDatabase>();
    }
}

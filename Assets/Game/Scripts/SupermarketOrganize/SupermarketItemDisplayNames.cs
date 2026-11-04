using Game.SupermarketOrganize.Config;

namespace Game.SupermarketOrganize
{
    public static class SupermarketItemDisplayNames
    {
        public static string Resolve(SupermarketItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                return "物品";

            var db = SupermarketItemDatabase.ResolveRuntime();
            if (db != null && db.TryGet(item.ItemId, out var def) && def != null &&
                !string.IsNullOrWhiteSpace(def.displayName))
                return def.displayName.Trim();

            return item.ItemId;
        }
    }
}

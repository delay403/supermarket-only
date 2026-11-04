using Game.Core.Config;

namespace Game.Fishing.Config
{
    public static class FishingZoneFishEntryUtility
    {
        public static FishDef ResolveFishDef(FishingZoneFishEntry entry)
        {
            if (entry.fish != null)
                return entry.fish;

            if (string.IsNullOrWhiteSpace(entry.fishId))
                return null;

            var db = ConfigRegistry.Fish;
            return db != null && db.TryGet(entry.fishId.Trim(), out var def) ? def : null;
        }

        public static string ResolveFishId(FishingZoneFishEntry entry)
        {
            var def = ResolveFishDef(entry);
            return def != null ? def.fishId : entry.fishId?.Trim() ?? "";
        }

        public static bool IsValidPoolEntry(FishingZoneFishEntry entry)
        {
            return ResolveFishDef(entry) != null;
        }

        public static int GetConfiguredWeight(FishingZoneFishEntry entry)
        {
            return entry.weight > 0 ? entry.weight : 1;
        }
    }
}

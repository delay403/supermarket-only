using Game.Save;

namespace Game.Fishing
{
    public static class FishCodexEquipService
    {
        public static string GetEquippedBackFishId() =>
            SaveService.Data?.fishing?.equippedBackFishId ?? "";

        public static bool TryEquipBackFish(string fishId)
        {
            if (SaveService.Data?.fishing == null)
                return false;
            if (!string.IsNullOrWhiteSpace(fishId) && !FishCodexService.IsDiscovered(fishId))
                return false;

            SaveService.Data.fishing.equippedBackFishId = string.IsNullOrWhiteSpace(fishId)
                ? ""
                : fishId.Trim();
            SaveService.Save();
            return true;
        }
    }
}

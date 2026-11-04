using Game.Save;

namespace Game.UI.MainHome
{
    /// <summary>主页副模式解锁状态（主模式 = 列表第一项，默认已解锁）。</summary>
    public static class MainHomeModeUnlockAccess
    {
        public static bool IsUnlocked(MainHomeGameModeEntry entry, bool isMainMode)
        {
            if (isMainMode || entry == null)
                return true;

            var modeId = entry.modeId;
            if (string.IsNullOrWhiteSpace(modeId))
                return false;

            var save = SaveService.Data;
            return save != null && save.IsMainHomeModeUnlocked(modeId);
        }

        public static void Unlock(string modeId)
        {
            if (string.IsNullOrWhiteSpace(modeId))
                return;

            var save = SaveService.Data;
            if (save == null)
                return;

            save.UnlockMainHomeMode(modeId);
            SaveService.Save();
        }
    }
}

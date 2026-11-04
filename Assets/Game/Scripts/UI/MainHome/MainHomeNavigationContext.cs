namespace Game.UI.MainHome
{
    /// <summary>MainHome 场景打开时的导航状态（从玩法场景返回时跳过 Lobby，直接显示选关页）。</summary>
    public static class MainHomeNavigationContext
    {
        static bool _openToModeSelect;

        public static void RequestOpenToModeSelect() => _openToModeSelect = true;

        public static bool ConsumeOpenToModeSelect()
        {
            var value = _openToModeSelect;
            _openToModeSelect = false;
            return value;
        }
    }
}

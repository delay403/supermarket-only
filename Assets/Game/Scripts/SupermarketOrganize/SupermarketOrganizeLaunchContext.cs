namespace Game.SupermarketOrganize
{
    /// <summary>
    /// 从 MainHome 快捷进入超市收纳场景时的启动参数。
    /// TT 直连进玩法时不应设置 SkipInGameHomePage，以便在场景内选择难度。
    /// </summary>
    public static class SupermarketOrganizeLaunchContext
    {
        static bool _skipInGameHomePage;

        public static void RequestSkipInGameHomePage() => _skipInGameHomePage = true;

        public static bool ConsumeSkipInGameHomePage()
        {
            var value = _skipInGameHomePage;
            _skipInGameHomePage = false;
            return value;
        }
    }
}

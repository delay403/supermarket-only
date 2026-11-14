namespace Game.SupermarketOrganize
{
    /// <summary>
    /// 超市收纳场景的启动参数。
    /// 直接进入玩法时不应跳过场景内的难度选择页。
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

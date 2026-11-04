namespace Game.SupermarketOrganize.Config
{
    /// <summary>
    /// 超市模式配置资产路径（Content/Configs，非 Resources.Load）。
    /// </summary>
    public static class SupermarketContentPaths
    {
        public const string ConfigFolderName = "SupermarketOrganize";
    }

    [System.Obsolete("改用 GameContentPaths.SupermarketConfigRoot 与场景直接引用，不再 Resources.Load。")]
    public static class SupermarketResourcePaths
    {
        public const string Root = "SupermarketOrganize";
        public const string OrganizeConfig = Root + "/SupermarketOrganizeConfig";
        public const string ItemDatabase = Root + "/SupermarketItemDatabase";
        public const string MapDatabase = Root + "/SupermarketMapDatabase";
        public const string ProgressionConfig = Root + "/SupermarketOrganizeProgressionConfig";
        public const string UpgradeUiConfig = Root + "/SupermarketOrganizeUpgradeUiConfig";
        public const string HomeConfig = Root + "/SupermarketOrganizeHomeConfig";
    }
}

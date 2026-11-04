namespace Game.Core
{
    /// <summary>
    /// 产品形态：决定存档命名空间与启用的玩法模块。
    /// 同一设备可同时存在多种形态的 PlayerPrefs 条目，互不读取。
    /// </summary>
    public enum GameProductMode
    {
        /// <summary>仅爬塔：存档仅含爬塔进度；钓鱼模块不读写。</summary>
        TowerOnly = 0,

        /// <summary>仅钓鱼：存档仅含钓鱼进度；爬塔模块不读写。</summary>
        FishingOnly = 1,

        /// <summary>融合：同一存档同时维护爬塔与钓鱼进度。</summary>
        Hybrid = 2,

        /// <summary>仅摧毁城市：存档仅含摧毁城市进度；爬塔/钓鱼模块不读写。</summary>
        DestroyCityOnly = 3,

        /// <summary>仅躲避虫子：存档仅含躲避虫子进度；爬塔/钓鱼/摧毁城市模块不读写。</summary>
        WormDodgeOnly = 4,

        /// <summary>仅超市收纳：存档仅含超市收纳进度。</summary>
        SupermarketOrganizeOnly = 5,

        /// <summary>仅动物医院：存档仅含动物医院进度。</summary>
        AnimalHospitalOnly = 6,
    }

    /// <summary>
    /// 可组合的玩法模块（用于多游戏融合包体）。
    /// 在 <see cref="GameProductSettings"/> 中勾选 <c>useGameplayModuleFlags</c> 后生效。
    /// </summary>
    [System.Flags]
    public enum GameplayModule
    {
        None = 0,
        Tower = 1 << 0,
        Fishing = 1 << 1,
        DestroyCity = 1 << 2,
        WormDodge = 1 << 3,
        SupermarketOrganize = 1 << 4,
        AnimalHospital = 1 << 5,
    }
}

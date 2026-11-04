namespace Game.Audio
{
    public enum SfxId
    {
        None = 0,

        UiButtonClick = 1,
        /// <summary>获得金币、钻石、奖杯等资源（奖杯逻辑可后续接同一枚举）。</summary>
        ResourceCollect = 2,
        /// <summary>失去金币、钻石等。</summary>
        ResourceDeduct = 3,
        /// <summary>场景交互区「打开背包/商店」等按钮。</summary>
        Interact = 4,

        /// <summary>通用面板从关闭到打开（含商店、地图、签到、结算等）。</summary>
        UiPanelOpen = 5,
        /// <summary>商店左侧卡片、列表项被玩家选中。</summary>
        UiSelect = 6,
        /// <summary>金币不足、操作失败、不可点按钮被点等。</summary>
        UiError = 7,

        /// <summary>循环环境音：爬塔高度 + 滑翔坠落速度驱动音量/音高（见 <see cref="AudioManager"/>）。</summary>
        AmbientWind = 8,

        Move = 10,
        Jump = 11,

        GlideLand = 20,
        SettlementWin = 21,

        /// <summary>设置面板音量条拖动结束（可共用短音效）。</summary>
        UiSliderChange = 22,

        /// <summary>滑翔阶段位图距离 HUD 达到某一阈值时播放。</summary>
        GlideDistanceMilestone = 23,

        HatchStart = 30,
        PetObtainHatch = 31,
        PetObtainMerge = 32,

        /// <summary>鱼线触水 / 收鱼水平移动水花。</summary>
        FishingWaterSplash = 40,

        /// <summary>钓获成功脚下结算音效。</summary>
        FishingCatchSettle = 41,

        /// <summary>HUD 飘字出现 / 累加（力量、钓鱼进度等）。</summary>
        UiFloatPopup = 42,

        /// <summary>钓鱼进度条每次增长。</summary>
        FishingProgressGain = 43,

        /// <summary>抽杆按钮随机音效（三选一）。</summary>
        FishingReelPress1 = 44,
        FishingReelPress2 = 45,
        FishingReelPress3 = 46,

        /// <summary>金币解锁新区域 / 门。</summary>
        AreaUnlock = 47,

        /// <summary>钓获庆祝（可按角色在 CatchCelebrateLibrary 中指定不同 SfxId）。</summary>
        FishingCatchCelebrate1 = 48,
        FishingCatchCelebrate2 = 49,
        FishingCatchCelebrate3 = 50,
        FishingCatchCelebrate4 = 51,

        /// <summary>摧毁城市 — 燃料升级。</summary>
        DestroyCityFuelUpgrade = 52,

        /// <summary>摧毁城市 — 导弹升级。</summary>
        DestroyCityNukeUpgrade = 53,

        /// <summary>摧毁城市 — 导弹发射。</summary>
        DestroyCityMissileLaunch = 54,

        /// <summary>摧毁城市 — 发射井警报循环。</summary>
        DestroyCityMissileAlarm = 55,

        /// <summary>摧毁城市 — 导弹着地爆炸。</summary>
        DestroyCityMissileExplosion = 56,

        /// <summary>摧毁城市 — 燃料生产器生产按钮。</summary>
        DestroyCityFuelProduceButton = 57,

        /// <summary>摧毁城市 — 燃料实例获得经验。</summary>
        DestroyCityFuelXpGain = 58,

        /// <summary>摧毁城市 — 发射井发射按钮。</summary>
        DestroyCityLaunchButton = 59,

        /// <summary>摧毁城市 — 导弹飞行循环（须 Loop）。</summary>
        DestroyCityMissileFlight = 60,

        /// <summary>躲避虫子 — 多节虫移动循环（须 Loop）。在 GameBootstrapper 绑定 Clip。</summary>
        WormMoveLoop = 61,

        /// <summary>躲避虫子 — 多节虫在 BuildingWall 层墙体内循环（须 Loop）。</summary>
        WormInsideWallLoop = 62,

        /// <summary>超市收纳 — 拾取物品。</summary>
        SupermarketOrganizePickUp = 63,

        /// <summary>超市收纳 — 放置物品。</summary>
        SupermarketOrganizePlace = 64,

        /// <summary>超市收纳 — 选中/瞄准物品。</summary>
        SupermarketOrganizeSelect = 65,

        /// <summary>超市收纳 — 错误货架放置。</summary>
        SupermarketOrganizePlaceWrong = 66,

        /// <summary>超市收纳 — 单个货架全部摆满。</summary>
        SupermarketOrganizeShelfComplete = 67,
    }
}

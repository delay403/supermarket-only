namespace Game.UI.Framework
{
    public enum UiPanelId
    {
        None = 0,
        Hud = 1,
        Result = 2,
        Pet = 3,
        PetHatch = 8,
        WingShop = 4,
        TrailShop = 5,
        MapSelect = 6,
        SignIn = 7,
        CharacterShop = 9,
        Settings = 10,
        LuckyWheel = 11,
        /// <summary>已合并至 <see cref="CoinsHub"/>；保留枚举值仅供旧场景 Prefab 序列化。</summary>
        FreeCoinsOffer = 12,
        FreeDiamondsOffer = 13,
        OnlineReward = 14,
        RewardDialog = 15,
        /// <summary>立即领取：攀爬速度翻倍 Buff（<see cref="Game.UI.FreeCurrencyOfferPanel"/> + <c>OfferKind.DoubleClimbSpeed</c>）。</summary>
        FreeDoubleClimbSpeed = 16,
        /// <summary>立即领取：滑翔结算金币翻倍 Buff（<see cref="Game.UI.FreeCurrencyOfferPanel"/> + <c>OfferKind.DoubleSettlementCoins</c>）。</summary>
        FreeDoubleSettlementCoins = 17,
        TreadmillUnlock = 18,
        TreadmillResult = 19,
        /// <summary>立即领取：立即获得当前档位跑步机 N 分钟锻炼收益（<see cref="Game.UI.FreeCurrencyOfferPanel"/> + <c>OfferKind.TreadmillInstantReward</c>）。</summary>
        FreeTreadmillInstantReward = 20,
        RodShop = 21,
        FishCodex = 22,
        StrengthTrainingUnlock = 23,
        /// <summary>立即领取：解锁自动钓鱼通行证（<see cref="Game.UI.FreeCurrencyOfferPanel"/> + OfferKind.AutoFishing）。</summary>
        FreeAutoFishing = 24,
        DiscountOffer = 25,
        /// <summary>金币 + 入口：领取金币 / 钻石分档兑换金币。</summary>
        CoinsHub = 27,
        /// <summary>立即领取：解锁自动燃料生产器（<see cref="Game.UI.FreeCurrencyOfferPanel"/> + OfferKind.UnlockFuelProducer）。</summary>
        FreeUnlockFuelProducer = 28,
        /// <summary>躲避虫子局内结算。</summary>
        WormDodgeResult = 29,
        /// <summary>躲避虫子模式内主页。</summary>
        WormDodgeHome = 30,
        /// <summary>躲避虫子楼层奖励弹窗。</summary>
        WormDodgeFloorReward = 31,
        /// <summary>躲避虫子排行榜。</summary>
        WormDodgeLeaderboard = 32,
        /// <summary>超市收纳技能/属性升级。</summary>
        SupermarketOrganizeUpgrade = 33,
        /// <summary>超市收纳扫地机器人面板。</summary>
        SupermarketOrganizeSweepRobot = 34,
        /// <summary>超市收纳模式内主页。</summary>
        SupermarketOrganizeHome = 35,
        /// <summary>超市收纳体力补充面板。</summary>
        SupermarketOrganizeStamina = 36,
        /// <summary>超市收纳胜利结算。</summary>
        SupermarketOrganizeResult = 37,
        /// <summary>动物医院模式内主页。</summary>
        AnimalHospitalHome = 38,
        /// <summary>动物医院日结算。</summary>
        AnimalHospitalResult = 39,
        /// <summary>动物医院咖啡机领取确认。</summary>
        AnimalHospitalCoffeeOffer = 40,
        /// <summary>动物医院理智归零死亡面板。</summary>
        AnimalHospitalDeath = 41,
    }
}

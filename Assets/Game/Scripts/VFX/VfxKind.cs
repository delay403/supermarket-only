namespace Game.VFX
{
    /// <summary>Gameplay VFX routed through <see cref="GameplayVfxManager"/> pooled prefabs。</summary>
    public enum VfxKind
    {
        /// <summary>爬塔滑翔流程 — 坠落段气流。</summary>
        AerialFallWind = 0,

        /// <summary>爬塔滑翔流程 — 滑翔段气流。</summary>
        AerialGlideWind = 1,

        LandingDustStrong = 10,
        LandingDustWeak = 11,

        /// <summary>落地砸击（与地面烟雾同一触点播放）。</summary>
        LandingGroundSlam = 12,

        SettlementVictory = 20,

        /// <summary>鱼线落水 / 收鱼水平移动水面水花。</summary>
        FishingWaterSplash = 30,

        /// <summary>钓获结算：角色脚下庆祝粒子。</summary>
        FishingCatchCelebrate = 31,

        /// <summary>鱼线落水 / 收杆 / 钓鱼过程中随机水面涟漪。</summary>
        FishingWaterRipple = 32,

        /// <summary>摧毁城市 — 燃料实例 / 生产器升级庆祝粒子。</summary>
        DestroyCityFuelUpgrade = 40,

        /// <summary>摧毁城市 — 导弹灌注升级庆祝粒子。</summary>
        DestroyCityNukeUpgrade = 41,

        /// <summary>摧毁城市 — 导弹发射（可选世界粒子，拖尾由预制体 Trail 子物体负责）。</summary>
        DestroyCityMissileLaunch = 42,

        /// <summary>摧毁城市 — 导弹爆炸物体特效（可选 Bootstrap 回退）。</summary>
        DestroyCityMissileExplosion = 43,

        /// <summary>躲避虫子 — 加速道具：角色脚下持续特效（与 buff 同长）。</summary>
        WormDodgeSpeedBoost = 50,

        /// <summary>躲避虫子 — 超级跳跃道具：角色脚下持续特效（与 buff 同长）。</summary>
        WormDodgeSuperJump = 51,

        /// <summary>躲避虫子 — 金币道具：拾取时一次性播放。</summary>
        WormDodgeCoin = 52,

        /// <summary>超市收纳 — 拾取物品粒子（可选 Bootstrap 绑定）。</summary>
        SupermarketOrganizePickUp = 53,

        /// <summary>超市收纳 — 放置物品粒子（可选 Bootstrap 绑定）。</summary>
        SupermarketOrganizePlace = 54,

        /// <summary>超市收纳 — 胜利庆祝粒子（可选 Bootstrap 绑定）。</summary>
        SupermarketOrganizeVictory = 55,

        /// <summary>超市收纳 — 单个货架全部摆满。</summary>
        SupermarketOrganizeShelfComplete = 56,
    }
}

using UnityEngine;

namespace Game.Core.Config
{
    [System.Serializable]
    public struct FishingWorldAreaRewardTier
    {
        [Tooltip("对应 CoinUnlockAreaTrigger.saveKey；列表顺序代表解锁进度。")]
        public string areaSaveKey;

        [Tooltip("免费金币（FreeCoinsOffer）在此档的数值。")]
        public long freeCoins;

        [Tooltip("增加力量（力量按钮）在此档的数值。")]
        public float strengthBonus;
    }

    [System.Serializable]
    public struct ProgressRewardCoinTier
    {
        [Tooltip("达到该进度值（含）时使用本档金币；按 minProgress 升序配置。")]
        public int minProgress;

        [Tooltip("免费金币在此档的数值。")]
        public long freeCoins;
    }

    [CreateAssetMenu(menuName = "Game/Config/GameBalanceConfig", fileName = "GameBalanceConfig")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        [Header("玩家移动（基础）")]
        [Tooltip("地面移动速度上限（m/s）。")]
        public float moveSpeed = 5f;
        [Tooltip("角色朝向跟随移动方向的锐度，越大转向越快。")]
        public float rotationSharpness = 12f;
        [Tooltip("重力加速度（m/s^2），负值向下。")]
        public float gravity = -25f;
        public float jumpSpeed = 0f;

        [Header("摄像机（自由移动）")]
        [Tooltip(
            "自由移动轨道：相对 pivot 的偏移，由当前 yaw/pitch 旋转后决定相机<strong>位置</strong>；相机朝向始终 LookAt pivot（角色上方跟随点）。\n" +
            "改 Z 符号会绕到角色另一侧，镜头仍会看向角色。")]
        public Vector3 freeCamOffset = new Vector3(0f, 3.5f, -6.5f);
        [Tooltip(
            "仅关卡开始与重生回出生点时应用：相对「角色 transform」的本地偏移（用当时角色完整旋转；与 freeCamOffset 独立）。\n" +
            "非零：将相机摆在 pivot + rotation*本向量处，并反解 yaw/pitch 与轨道 freeOffset；攀爬、后空翻/坠落中途不会再次套用。\n" +
            "为零：该步仅做与角色水平朝向对齐的初始 yaw（仍只在开局/重生时执行一次）。")]
        public Vector3 freeCamInitialViewerOffsetLocalFromCharacter = Vector3.zero;
        public float camYawSpeed = 150f;
        public float camPitchSpeed = 120f;
        public float camPitchMin = -25f;
        public float camPitchMax = 65f;
        public float camFollowSharpness = 12f;
        [Tooltip("自由移动相机 pivot 高度（跟随点的上移，类似人物头部高度）。")]
        public float camPivotHeight = 1.5f;

        [Header("攀爬（基础）")]
        [Tooltip("攀爬基础速度（最终速度还会叠加宠物/翅膀/拖尾等加成）。")]
        public float climbSpeedBase = 2.0f;
        [Tooltip("攀爬动画基础播放倍率（不影响最终攀爬速度）。")]
        public float climbSpeedAnimBase = 1.0f;
        [Tooltip("体力耗尽时攀爬速度 = StatsAggregator 合并后的爬塔速度 × 本系数（仍含地图修正与 Σ 养成；再乘奖励 Buff 倍率）。")]
        [Range(0.01f, 1f)]
        public float climbSpeedWhenStaminaEmptyMultiplier = 0.2f;
        public float staminaMax = 10f;
        public float staminaDrainPerSecond = 1f;

        [Header("摄像机（攀爬独立参数）")]
        public Vector3 climbCamOffset = new Vector3(2.5f, 2.0f, -3.0f);
        public float climbCamFov = 55f;
        public float climbCamYawSpeed = 120f;
        public float climbCamPitchSpeed = 100f;
        public float climbCamPitchMin = -10f;
        public float climbCamPitchMax = 55f;
        public float climbCamFollowSharpness = 14f;
        public float climbCamPivotHeight = 1.5f;

        [Header("摄像机 · 攀爬（高处风力气动抖动）")]
        [Tooltip("攀爬且向上运动时叠加；频率 0 = 关闭。")]
        public float climbWindShakeFrequencyHz = 4.2f;

        public Vector3 climbWindShakePositionAmplitude = new Vector3(0.028f, 0.036f, 0.024f);

        public Vector3 climbWindShakeEulerAmplitude = new Vector3(0.42f, 0.52f, 0.38f);

        [Header("高塔分段（池化）")]
        public float segmentHeight = 10f;
        public float visibleRangeBack = 40f;
        public float visibleRangeForward = 60f;
        public int pooledSegmentCount = 40;

        [Header("滑翔 — 相机（强制跟随）")]
        public Vector3 glideCamOffset = new Vector3(4.0f, 2.5f, -6.0f);
        public float glideCamFov = 65f;
        [Tooltip("滑翔相机 pivot 高度（跟随点的上移）。")]
        public float glideCamPivotHeight = 1.5f;

        [Header("滑翔 — 路径规划")]
        [Tooltip("相对基准 4 的倍率，乘在「滑翔段」加速度与最大速度的整体倍率上，影响接地前滑行距离与手感；不直接指定米数。")]
        public float glideHorizontalDistanceMultiplier = 4f;
        [Tooltip("异常未接地时的兜底超时（秒），到点强制结算。正常为跑道接地后结算。")]
        public float glideFailsafeTimeoutSeconds = 600f;

        [Header("滑翔 — 坠落段（运动方向偏向下，速度沿路径积分）")]
        [Tooltip("坠落段沿路径加速度（m/s²）。仅读配置，不受养成「滑翔段」加速度加成影响。")]
        public float glideFallAccel = 20f;
        [Tooltip("坠落段沿路径最大速度（m/s）。")]
        public float glideFallMaxSpeed = 80f;

        [Header("滑翔 — 滑翔段（运动方向偏向前，速度沿路径积分）")]
        [Tooltip("滑翔段沿路径加速度（m/s²）。仅配置表，不受养成 StatModifiers 影响。")]
        public float glideForwardAccel = 30f;
        [Tooltip("滑翔段沿路径最大速度（m/s）。仅配置表，不受养成 StatModifiers 影响。")]
        public float glideForwardMaxSpeed = 22f;

        [Header("滑翔 — 相机抖动（坠落段）")]
        [Tooltip("主频率（Hz），0 表示关闭该段位置/旋转抖动。")]
        public float glideFallShakeFrequencyHz = 8f;
        [Tooltip("位置抖动幅度（米，各轴）。")]
        public Vector3 glideFallShakePositionAmplitude = new Vector3(0.08f, 0.065f, 0.055f);
        [Tooltip("旋转抖动幅度（度，各轴）。")]
        public Vector3 glideFallShakeEulerAmplitude = new Vector3(0.65f, 0.42f, 0.38f);

        [Header("滑翔 — 相机抖动（滑翔段）")]
        [Tooltip("主频率（Hz），0 表示关闭该段位置/旋转抖动。")]
        public float glideGlideShakeFrequencyHz = 7f;
        [Tooltip("位置抖动幅度（米，各轴）。")]
        public Vector3 glideGlideShakePositionAmplitude = new Vector3(0.07f, 0.055f, 0.045f);
        [Tooltip("旋转抖动幅度（度，各轴）。")]
        public Vector3 glideGlideShakeEulerAmplitude = new Vector3(0.55f, 0.35f, 0.3f);

        [Header("滑翔跑道（Z轴无限分段）")]
        public float runwaySegmentLength = 12f;
        public float runwayVisibleRangeBack = 60f;
        public float runwayVisibleRangeForward = 120f;
        public int runwayPooledSegmentCount = 60;

        [Header("结算相机（强制跟随）")]
        [Tooltip("结算相机偏移（相对玩家的局部坐标）。")]
        public Vector3 settlementCamOffset = new Vector3(4.0f, 2.5f, -6.0f);
        [Tooltip("结算相机FOV。")]
        public float settlementCamFov = 65f;
        [Tooltip("结算相机pivot高度（跟随点的上移）。")]
        public float settlementCamPivotHeight = 1.5f;

        [Header("结算奖励")]
        [Tooltip(
            "滑翔结算基础金币：金币 = floor((本值 + ΣcoinsAdd) × max(0, 1 + ΣcoinsMul + 地图金币修正) × 水平滑翔距离 ÷ glideSettlementCoinDistanceDivisor)。")]
        public float glideSettlementBaseCoins = 500f;
        [Tooltip(
            "滑翔结算金币公式中的距离除数（米→币的缩放分母）。越大同距离金币越少；≤0 或未配置时运行时使用 1000。")]
        [Min(0f)]
        public float glideSettlementCoinDistanceDivisor = 1000f;
        [Tooltip("已弃用：金币改由 glideSettlementBaseCoins + 地图/养成公式计算。")]
        public float coinsPerMeter = 1f;
        [Tooltip("滑翔结算钻石：floor(水平滑翔距离米 ÷ 1000 × 本系数)。默认 1 ≈ 每 1000 米滑翔 1 钻。金币系数见 glideSettlementCoinDistanceDivisor。")]
        public float gemsPer1000m = 1f;

        [Header("宠物背包 · 扩充金币")]
        [Tooltip(
            "可携带栏位：从「初始 1 格」起每次 +1 的消耗。索引 0 = 扩到第 2 格，索引 1 = 扩到第 3 格；长度不足或为空时回退 PetBackpackEconomy 常量。")]
        public long[] petCarrySlotExpandGoldByTier = { 6000, 6000 };

        [Tooltip(
            "仓库容量：每次扩充 +10 的消耗（与 BackpackExpandStep 对齐）。索引按已扩充次数 0..N-1；长度不足时回退 PetBackpackEconomy 常量。")]
        public long[] petBackpackExpandGoldByTier = { 5000, 5000, 5000, 5000, 5000, 5000 };

        [Header("奖励 Buff")]
        [Tooltip("立即领取后「攀爬速度翻倍」「滑翔结算金币翻倍」的持续秒数。")]
        public float rewardBuffDurationSeconds = 120f;

        [Header("免费获取金币/钻石")]
        [Tooltip(
            "按 MapDatabase 中已解锁地图数量分档（与地图顺序无关，只计数量）。\n" +
            "索引 0 = 仅解锁 1 张（默认场景），索引 1 = 解锁 2 张，索引 2 = 解锁 3 张；超出取最后一档。\n" +
            "为空时回退 5000。")]
        public long[] rewardFreeCoinsByUnlockedMapCount = { 5000, 50000, 500000 };

        public int rewardFreeGems = 100;

        /// <summary>
        /// 根据 <see cref="MapDatabase"/> 中已解锁地图数量解析免费金币。
        /// </summary>
        public long ResolveRewardFreeCoins(int unlockedMapCountInCatalog)
        {
            if (unlockedMapCountInCatalog < 1)
                unlockedMapCountInCatalog = 1;

            if (rewardFreeCoinsByUnlockedMapCount == null || rewardFreeCoinsByUnlockedMapCount.Length == 0)
                return 5000;

            var tier = unlockedMapCountInCatalog - 1;
            if (tier >= rewardFreeCoinsByUnlockedMapCount.Length)
                tier = rewardFreeCoinsByUnlockedMapCount.Length - 1;

            return rewardFreeCoinsByUnlockedMapCount[tier];
        }

        [Header("跑步机")]
        [Tooltip("每累计该秒数发放一次爬塔速度永久加成。")]
        public float treadmillIntervalSeconds = 10f;
        public float treadmillTier1BonusPerInterval = 0.01f;
        public float treadmillTier2BonusPerInterval = 0.02f;
        public float treadmillTier3BonusPerInterval = 0.03f;
        [Tooltip("第三档钻石解锁消耗。")]
        public long treadmillTier3GemUnlockCost = 50;
        [Tooltip("第三档免费解锁所需累计次数。")]
        public int treadmillTier3UnlockRequired = 3;
        [Tooltip("跑步机动画用的假地面速度（m/s），角色位置保持不动。")]
        public float treadmillRunAnimSpeed = 5f;
        [Tooltip("立即领取：等效于在当前档位跑步机上锻炼的分钟数。")]
        [Min(0.1f)]
        public float treadmillInstantRewardMinutes = 15f;
        [Tooltip("走向跑步点时使用的移动速度（m/s）；≤0 时使用 moveSpeed。")]
        public float treadmillApproachMoveSpeed = 0f;

        [Header("钓鱼 — 力量与收杆")]
        [Tooltip("新档默认基础力量（训练可提升）。")]
        public float fishingDefaultBaseStrength = 1000f;
        [Tooltip("（已废弃，不再参与计算）旧版快速上鱼全局系数，保留仅为兼容旧资源。")]
        public float fishingPowerMulScale = 1f;
        [Tooltip("一次性增加的基础力量。")]
        public float fishingFreeStrengthBonus = 1000f;
        [Tooltip("ΣfishingSpecialCatchBonus 超过此值时使用高级九格概率表。")]
        public float fishingSpecialCatchThreshold = 0.001f;
        [Tooltip("收杆单次进度 = (总力量 + ΣfishingProgressAdd) × 本系数。")]
        public float fishingReelProgressScale = 1f;

        [Header("钓鱼 — 自动钓鱼（免费通行证）")]
        [Tooltip("每次立即领取解锁的自动钓鱼时长（秒）；默认 30 分钟。")]
        public float autoFishingPassDurationSeconds = 1800f;

        [Header("钓鱼 — 分区解锁免费奖励（CoinUnlockAreaTrigger.saveKey）")]
        [Tooltip("按列表顺序统计已解锁分区数量决定档位；0 区解锁用第 0 档，解锁第 1 个 key 用第 1 档，以此类推。")]
        public FishingWorldAreaRewardTier[] fishingWorldAreaRewardTiers;

        [Header("摧毁城市 — 免费金币")]
        [Tooltip("按核弹等级（nukeLevel - 1）与已解锁燃料生产器数量取较大值作为进度，匹配 minProgress 后取最高档。")]
        public ProgressRewardCoinTier[] destroyCityRewardCoinTiers;

        [Header("躲避虫子 — 免费金币")]
        [Tooltip("按历史最高层数 bestFloorsReached 匹配 minProgress 后取最高档。")]
        public ProgressRewardCoinTier[] wormDodgeRewardCoinTiers;

        [Header("力量训练池")]
        [Tooltip("全局训练 tick 间隔（秒）；各训练场加成见 StrengthTrainingGroundDatabase。")]
        public float strengthTrainingIntervalSeconds = 10f;
        [Tooltip("未在训练场配置 animSpeedOverride 时使用的默认 Velocity。")]
        public float strengthTrainingAnimSpeed = 5f;
        public float strengthTrainingApproachMoveSpeed = 0f;

        [Header("力量训练池（旧版兜底，无 GroundDatabase 条目时使用）")]
        public float strengthTrainingTier1BonusPerInterval = 0.2f;
        public float strengthTrainingTier2BonusPerInterval = 0.5f;
        public float strengthTrainingTier3BonusPerInterval = 1f;
        public long strengthTrainingTier3GemUnlockCost = 50;
        public int strengthTrainingTier3UnlockRequired = 3;

        [Header("摧毁城市 — 燃料与核弹")]
        [Tooltip("新档默认基础燃料产出率（挂机可提升）。")]
        public float destroyCityDefaultBaseFuelRate = 100f;
        [Tooltip("燃料生产台自动产出周期（秒）。")]
        public float destroyCityFuelIntervalSeconds = 10f;
        [Tooltip("手动点击生产燃料 = 单次自动产出 × 本系数。")]
        public float destroyCityManualTapFuelScale = 1f;
        [Tooltip("摧毁比例上限（爆炸半径/城市规模后再 clamp）。")]
        [Range(0.1f, 2f)]
        public float destroyCityMaxDestructionRatio = 1f;
        [Tooltip("无 FuelProducerDatabase 条目时的兜底每周期产出。")]
        public float destroyCityFallbackFuelPerInterval = 1f;
        [Tooltip("无 FuelProducerDatabase 条目时的兜底升级金币。")]
        public long destroyCityFallbackStationUpgradeCost = 500L;
        [Tooltip("无 NukeDatabase 条目时的兜底升级燃料经验。")]
        public float destroyCityFallbackNukeUpgradeXp = 100f;
        [Tooltip("无 NukeDatabase 条目时的兜底爆炸半径（米）。")]
        public float destroyCityFallbackExplosionRadius = 50f;
        [Tooltip("无 FuelDatabase 条目时的兜底单级升级经验。")]
        public float destroyCityFallbackFuelXpToNext = 100f;

        [Tooltip(
            "开启后：运行时可显示「装备属性调试」覆盖层（由场景中的 EquipmentStatDebugOverlay 或菜单 Game/Debug/Create… 创建），用于覆盖翅膀/角色/拖尾/宠物的部分养成数值参与 StatsAggregator 计算。")]
        public bool debugEnableEquipmentStatPanel;
    }
}


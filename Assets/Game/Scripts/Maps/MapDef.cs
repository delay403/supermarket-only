using UnityEngine;

namespace Game.Maps
{
    [CreateAssetMenu(menuName = "Game/Maps/MapDef", fileName = "MapDef")]
    public sealed class MapDef : ScriptableObject
    {
        [Header("Identity")]
        public string mapId = "map_1";
        public string displayName = "Map 1";

        [Header("Climb")]
        [Tooltip("已弃用：爬塔速度改由 climbSpeedMapCorrection 与 StatsAggregator 新公式计算。保留仅为旧数据兼容。")]
        public float climbSpeedMultiplier = 1f;

        [Tooltip("地图速度修正（米/秒量纲，从「基础+养成加法」一侧扣除）：参与 max(基础, 基础+ΣclimbSpeedAdd-本值)。再 × (1+ΣclimbSpeedMul)。")]
        public float climbSpeedMapCorrection = 0f;

        [Tooltip("0 or negative = infinite.")]
        public float maxTowerHeight = 0f;

        [Header("Glide 结算（本图）")]
        [Tooltip("地图金币修正：参与 (基础金币+养成coinsAdd)×(1+养成coinsMul之和+本值)×水平滑翔距离÷GameBalanceConfig.glideSettlementCoinDistanceDivisor。")]
        public float glideCoinsMapCorrection = 0f;

        [Header("Glide / medals")]
        [Tooltip("攀爬到本地图配置的最高塔高（相对基底）并进入滑翔时，发放的「全局奖牌」数量（≤0 视为不发）。")]
        public int medalsAwardOnTowerClear = 1;

        [Header("Presentation")]
        [Tooltip("Build Settings 中的 gameplay 场景名；场景文件建议放在固定目录（例如 Assets/Game/Scenes/Gameplay/）。")]
        public string gameplaySceneName = "";

        [Tooltip("地图选择界面列表项 / 解锁弹窗等处使用的图标。")]
        public Sprite mapIcon;

        [Header("Unlock")]
        [Tooltip("首次解锁本地图消耗的「全局奖牌」数量（与金币、钻石同类公共资源）。已解锁后不再扣除。")]
        public int medalsRequiredToUnlock = 1;

        [Header("Unlockable pools (optional)")]
        public string[] unlockablePetIds;
        public string[] unlockableWingIds;
        public string[] unlockableTrailIds;
    }
}

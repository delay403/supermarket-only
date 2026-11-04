using UnityEngine;

namespace Game.DestroyCity.Config
{
    [CreateAssetMenu(menuName = "Game/DestroyCity/DestroyCityMapDef", fileName = "DestroyCityMapDef")]
    public sealed class DestroyCityMapDef : ScriptableObject
    {
        public string mapId = "destroy_city_map_1";
        public string displayName = "City Alpha";
        public int mapLevel = 1;

        [Tooltip("Unity 场景名（须在 Build Settings 中）。")]
        public string gameplaySceneName = "DestroyCityScene";

        [Tooltip("城市规模基准（米），与核弹爆炸半径比值得出摧毁比例。")]
        [Min(1f)]
        public float cityScaleMeters = 200f;

        [Tooltip("完全摧毁时的基础金币奖励。")]
        [Min(1)]
        public long baseCoinReward = 1000;

        [Tooltip("解锁所需金币；0 表示默认解锁。")]
        [Min(0)]
        public long unlockCoinCost;

        [Tooltip("HUD 地图标题图标（可选）。")]
        public Sprite mapIcon;
    }
}

using UnityEngine;
using Game.Collectibles.Stats;
using Game.Collectibles;

namespace Game.Collectibles.Wings
{
    [CreateAssetMenu(menuName = "Game/Collectibles/WingDef", fileName = "WingDef")]
    public sealed class WingDef : ScriptableObject
    {
        public string wingId = "wing_1";
        public string displayName = "Wing";
        public int level = 1;

        [Tooltip("商店列表/详情等展示用品质档位（与拖动、宠物公用六级）。")]
        public CosmeticQuality quality = CosmeticQuality.Common;

        public int priceCoins = 1000;
        public int requiredUnlockedLevel = 0;

        public GameObject prefab;

        [Tooltip("商店格子与右侧预览用小图标（可选）；底图请在 QualityVisualLibrary 里按品质配置。")]
        public Sprite shopIcon;
        public StatModifiers modifiers;
    }
}


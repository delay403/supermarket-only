using UnityEngine;
using Game.Collectibles;
using Game.Collectibles.Stats;

namespace Game.Collectibles.Trails
{
    [CreateAssetMenu(menuName = "Game/Collectibles/TrailDef", fileName = "TrailDef")]
    public sealed class TrailDef : ScriptableObject
    {
        public string trailId = "trail_1";
        public string displayName = "Trail";
        public int level = 1;

        [Tooltip("商店列表/详情等展示用品质档位（与翅膀、宠物公用六级）。")]
        public CosmeticQuality quality = CosmeticQuality.Common;

        public int priceCoins = 500;
        public int requiredUnlockedLevel = 0;

        public GameObject prefab;

        [Tooltip("商店格子与右侧预览用小图标（可选）；品质底图在 QualityVisualLibrary。")]
        public Sprite shopIcon;

        public StatModifiers modifiers;
    }
}

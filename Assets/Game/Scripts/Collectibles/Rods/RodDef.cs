using UnityEngine;
using Game.Collectibles.Stats;
using Game.Collectibles;

namespace Game.Collectibles.Rods
{
    [CreateAssetMenu(menuName = "Game/Collectibles/RodDef", fileName = "RodDef")]
    public sealed class RodDef : ScriptableObject
    {
        public string rodId = "rod_1";
        public string displayName = "Rod";
        public int level = 1;

        [Tooltip("商店列表/详情等展示用品质档位。")]
        public CosmeticQuality quality = CosmeticQuality.Common;

        public int priceCoins = 1000;
        public int requiredUnlockedLevel = 0;

        public GameObject prefab;

        [Tooltip("商店格子与右侧预览用小图标（可选）。")]
        public Sprite shopIcon;

        public StatModifiers modifiers;
    }
}

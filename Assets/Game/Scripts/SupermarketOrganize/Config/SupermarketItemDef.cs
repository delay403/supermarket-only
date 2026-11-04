using System;
using UnityEngine;

namespace Game.SupermarketOrganize.Config
{
    [CreateAssetMenu(menuName = "Game/Supermarket Organize/Item Def", fileName = "SupermarketItemDef")]
    public sealed class SupermarketItemDef : ScriptableObject
    {
        public string itemId = "item_apple";

        public string displayName = "苹果";

        [Tooltip("场景实例预制体（需含 Collider 与 SupermarketItem）。")]
        public GameObject worldPrefab;

        [Min(1)]
        public int minSpawnCount = 3;

        [Min(1)]
        public int maxSpawnCount = 15;

        [Tooltip("生成时是否关闭阴影投射（大量小物体建议开启）。")]
        public bool disableShadows = true;

        public int ResolveSpawnCount(System.Random rng)
        {
            var min = Mathf.Max(1, minSpawnCount);
            var max = Mathf.Max(min, maxSpawnCount);
            return rng.Next(min, max + 1);
        }
    }
}

using UnityEngine;

namespace Game.SupermarketOrganize.Config
{
    [CreateAssetMenu(menuName = "Game/Supermarket Organize/Map Def", fileName = "SupermarketMapDef")]
    public sealed class SupermarketMapDef : ScriptableObject
    {
        public string mapId = "supermarket_map_1";
        public string displayName = "超市收纳 1";

        [Tooltip("Unity 场景名（须在 Build Settings 中）。")]
        public string gameplaySceneName = "SupermarketOrganizeScene";
    }
}

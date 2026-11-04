using System;
using UnityEngine;

namespace Game.Fishing.Config
{
    [Serializable]
    public struct FishingZoneFishEntry
    {
        [Tooltip("直接拖入 FishDef 资产（推荐）。")]
        public FishDef fish;

        [Tooltip("兼容旧资源：fish 为空时按 id 查 FishDatabase。")]
        public string fishId;

        [Tooltip("权重；为 0 时视为 1。实际抽鱼概率仍可由 FishingCatchProbability 九格表覆盖。")]
        [Min(0)]
        public int weight;
    }

    [CreateAssetMenu(menuName = "Game/Fishing/FishingZoneDef", fileName = "FishingZoneDef")]
    public sealed class FishingZoneDef : ScriptableObject
    {
        public string zoneId = "fishing_zone_1";
        public string displayName = "Fishing Zone";
        public int zoneLevel = 1;

        [Tooltip("该区域钓鱼进度条最大值；不同等级区域可配置不同总值。")]
        [Min(1f)]
        public float progressMax = 100f;

        [Tooltip("本区域鱼池（通常 9 种；可与其他区域交叉）。")]
        public FishingZoneFishEntry[] fishPool = Array.Empty<FishingZoneFishEntry>();

        [Header("图鉴 UI")]
        [Tooltip("图鉴顶部 Tab 图标；配置后不再显示文字标签。")]
        public Sprite codexTabIcon;
    }
}

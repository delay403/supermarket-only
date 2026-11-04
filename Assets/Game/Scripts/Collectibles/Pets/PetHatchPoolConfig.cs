using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Collectibles.Pets
{
    public enum HatchSpendCurrency
    {
        Coins,
        Gems,
    }

    /// <summary>单个孵蛋卡池内的一只宠物及其抽取权重。</summary>
    [Serializable]
    public sealed class PetHatchPoolEntry
    {
        [Tooltip("从 Project 拖入宠物 PetDef 资源（须填写有效的 petId）。")]
        public PetDef pet;

        [Min(1)] public int weight = 100;
    }

    /// <summary>
    /// 孵蛋卡池配置（宠物列表 + 权重、消耗货币类型与单价）。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Pets/Pet Hatch Pool", fileName = "PetHatchPool")]
    public sealed class PetHatchPoolConfig : ScriptableObject
    {
        public string poolId = "default";
        public string displayName = "普通孵蛋";

        public HatchSpendCurrency currency = HatchSpendCurrency.Coins;

        [Min(0)] public long singleDrawCost = 100;

        [Tooltip("为 0 时使用 singleDrawCost × 5。")]
        [Min(0)] public long fiveDrawCost;

        [Tooltip("孵化按钮上方的货币图标；为空则由面板上的兜底 Sprite 按Coins/Gems显示。")]
        public Sprite currencyIcon;

        [Tooltip("孵蛋主界面中央蛋体图片；为空则使用面板上的 defaultEggSprite。")]
        public Sprite eggIcon;

        public List<PetHatchPoolEntry> entries = new();

        public long GetFiveDrawCost() => fiveDrawCost > 0 ? fiveDrawCost : singleDrawCost * 5L;
    }
}

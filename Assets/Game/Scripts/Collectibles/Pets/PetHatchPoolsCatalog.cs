using System.Collections.Generic;
using UnityEngine;

namespace Game.Collectibles.Pets
{
    /// <summary>
    /// 按顺序排列的多个孵蛋卡池（面板左右切换）。
    /// 单独脚本文件便于 Unity 识别 ScriptableObject 资产。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Pets/Pet Hatch Pools Catalog", fileName = "PetHatchPoolsCatalog")]
    public sealed class PetHatchPoolsCatalog : ScriptableObject
    {
        public List<PetHatchPoolConfig> pools = new();
    }
}

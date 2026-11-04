using UnityEngine;
using Game.Collectibles.Stats;

namespace Game.Collectibles.Pets
{
    [CreateAssetMenu(menuName = "Game/Collectibles/PetDef", fileName = "PetDef")]
    public sealed class PetDef : ScriptableObject
    {
        [Header("Identity")]
        public string petId = "pet_1";
        public string displayName = "Pet";
        public PetQuality quality = PetQuality.Green;
        public int level = 1;

        [Header("Gacha")]
        public int hatchCostCoins = 100;
        public int hatchWeight = 100;

        [Header("Fusion")]
        [Tooltip("三只本宠物合成下一级时的成功概率（0~1），失败返还 2 只材料宠。")]
        [Range(0f, 1f)]
        public float fusionSuccessChance01 = 0.6f;
        [Tooltip("合成产出的下一级宠物 id。留空时：若库中 level=本级+1 的条目唯一则自动选用；否则回退为「同 PetQuality 且 level+1」匹配。")]
        public string fusionTargetPetId;

        [Header("Visual")]
        public GameObject prefab;
        [Tooltip("背包 / 孵化结算等 UI 用的小图标（可选）。")]
        public Sprite inventoryIcon;

        [Header("Stats")]
        public StatModifiers modifiers;

        [Header("钓鱼 — 出售")]
        [Tooltip("钓鱼模式下出售该宠物获得的金币；≤0 时使用 modifiers.coinsAdd 或 hatchCostCoins 作为默认出售价。")]
        public long fishingSellPriceCoins;
    }
}


using System;
using UnityEngine;

namespace Game.Collectibles.Stats
{
    [Serializable]
    public struct StatModifiers
    {
        [Header("攀爬")]
        [Tooltip("攀爬速度加法（m/s），与基础速度相加后再乘地图/倍率项。")]
        public float climbSpeedAdd;
        [Tooltip("攀爬速度倍率：多件养成相加后参与 (1+之和)，再减地图速度修正。")]
        public float climbSpeedMul;

        [Header("金币（滑翔结算）")]
        [Tooltip("金币倍率：多件养成相加后参与 (1 + coinsMul之和 + 地图金币修正)。")]
        public float coinsMul;
        [Tooltip("金币加法：与基础金币相加后再乘倍率与距离。")]
        public float coinsAdd;

        [Header("钓鱼")]
        [Tooltip("钓鱼力量加法：与基础力量、训练加成线性相加后再乘倍率。")]
        public float fishingPowerAdd;
        [Tooltip("快速上鱼：按角色基础力量百分比加成，ΣfishingPowerMul 与 baseStrength 相乘后计入总力量（0.1=+10%）。")]
        public float fishingPowerMul;
        [Tooltip("收杆单次进度额外加成（与总力量相乘前叠加）。")]
        public float fishingProgressAdd;
        [Tooltip("钓获鱼金币倍率：多件养成相加后参与 (1 + 之和)。")]
        public float fishingCoinMul;
        [Tooltip("力量训练：每秒增量 = 基础值 × (1 + 训练速度倍率之和)。")]
        public float fishingTrainSpeedMul;
        [Tooltip("海洋眷属：设为 1 时启用高级九格概率表（任一已装备养成拥有即生效，不累加）。")]
        public float fishingSpecialCatchBonus;

        [Header("躲避虫子")]
        [Tooltip("移动速度加法（m/s）。")]
        public float wormDodgeMoveSpeedAdd;
        [Tooltip("移动速度倍率：多件养成相加后参与 (1 + 之和)。")]
        public float wormDodgeMoveSpeedMul;
        [Tooltip("结算金币倍率：多件养成相加后参与 (1 + 之和)。")]
        public float wormDodgeCoinMul;
    }
}

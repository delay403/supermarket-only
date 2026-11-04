using System;
using UnityEngine;

namespace Game.Features.StrengthTraining
{
    [Serializable]
    public sealed class StrengthTrainingGroundDef
    {
        [Tooltip("与场景中 StrengthTrainingZone.groundId 一致，全局唯一。")]
        public string groundId = "training_basic";

        public string displayName = "基础训练场";

        [Tooltip("面板展示图标；场景中 Zone 可覆盖。")]
        public Sprite panelIcon;

        [Min(0f)]
        public float bonusPerInterval = 1f;

        public StrengthTrainingUnlockPolicy unlockPolicy = StrengthTrainingUnlockPolicy.AlwaysOpen;

        [Min(0)]
        public long gemUnlockCost = 50;

        [Min(1)]
        public int claimsRequired = 3;

        [Tooltip("0 = 使用 GameBalanceConfig.strengthTrainingAnimSpeed。")]
        [Min(0f)]
        public float animSpeedOverride;
    }
}

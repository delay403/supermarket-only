using Game.Core.Config;

namespace Game.Debugging
{
    /// <summary>
    /// runtime 调试覆盖：<see cref="ConfigRegistry.Balance"/> 中 <c>debugEnableEquipmentStatPanel</c> 开启且本类 <see cref="OverrideEnabled"/> 为 true 时，
    /// <see cref="Game.Collectibles.Stats.StatsAggregator"/> 用此处数值替换对应装备项（与原版同一合并公式，仅替换指定维度）。
    /// </summary>
    public static class EquipmentStatDebugState
    {
        /// <summary>参与 ΣclimbSpeedMul：替代已装备翅膀的 <c>climbSpeedMul</c>（翅膀的 climbSpeedAdd / 金币项仍用配置）。</summary>
        public static float WingClimbSpeedMul;

        /// <summary>参与 ΣcoinsMul：替代已装备角色的 <c>coinsMul</c>（角色的 climbSpeedAdd / coinsAdd 等仍用配置）。</summary>
        public static float CharacterCoinsMul;

        /// <summary>参与 ΣclimbSpeedMul：替代已装备角色的 <c>climbSpeedMul</c>。</summary>
        public static float CharacterClimbSpeedMul;

        /// <summary>参与 ΣcoinsMul：替代已装备拖尾的 <c>coinsMul</c>（拖尾其它项仍用配置）。</summary>
        public static float TrailCoinsMul;

        public static float PetSlot1CoinsAdd;
        public static float PetSlot2CoinsAdd;
        public static float PetSlot3CoinsAdd;

        public static bool OverrideEnabled;

        public static bool ShouldApplyOverrides =>
            OverrideEnabled &&
            ConfigRegistry.Balance != null &&
            ConfigRegistry.Balance.debugEnableEquipmentStatPanel;

        public static float GetPetCoinsAdd(int equipSlotIndex)
        {
            return equipSlotIndex switch
            {
                0 => PetSlot1CoinsAdd,
                1 => PetSlot2CoinsAdd,
                2 => PetSlot3CoinsAdd,
                _ => 0f,
            };
        }
    }
}

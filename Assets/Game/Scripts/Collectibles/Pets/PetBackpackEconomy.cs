namespace Game.Collectibles.Pets
{
    /// <summary>宠物背包扩充与栏位扩充经济参数（可先写常量，日后可迁至配置表）。</summary>
    public static class PetBackpackEconomy
    {
        public const int EquipSlotsMax = 3;
        public const int EquipSlotsInitial = 1;
        public const int BackpackCapacityMax = 80;
        public const int BackpackCapacityInitial = 20;
        public const int BackpackExpandStep = 10;

        public const int EquipSlotExpandCostCoins = 6000;
        public const int BackpackExpandCostCoins = 5000;
    }
}

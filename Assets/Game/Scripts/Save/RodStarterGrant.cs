namespace Game.Save
{
    /// <summary>
    /// 兼容旧调用点：改为走 <see cref="FreeCatalogGrant"/>（价格为 0 的钓竿默认解锁并装备）。
    /// </summary>
    public static class RodStarterGrant
    {
        public static void TryGrantStarterRodIfNeeded(PlayerSaveData data) =>
            FreeCatalogGrant.EnsureFreeItemsOwnedAndEquipped(data);
    }
}

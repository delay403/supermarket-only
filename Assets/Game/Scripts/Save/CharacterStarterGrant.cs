namespace Game.Save
{
    /// <summary>
    /// 兼容旧调用点：改为走 <see cref="FreeCatalogGrant"/>（价格为 0 的角色默认解锁并装备）。
    /// </summary>
    public static class CharacterStarterGrant
    {
        public static void TryGrantStarterCharacterIfNeeded(PlayerSaveData data) =>
            FreeCatalogGrant.EnsureFreeItemsOwnedAndEquipped(data);
    }
}

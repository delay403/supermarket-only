namespace Game.UI.Framework
{
    /// <summary>
    /// 打开宠物孵蛋界面时可选指定卡池（按目录顺序索引或 poolId）。
    /// </summary>
    public sealed class PetHatchOpenPayload
    {
        /// <summary>目录中第几个卡池（从 0 开始）。负数表示不通过索引指定。</summary>
        public int CatalogIndex = -1;

        /// <summary>与 <see cref="Game.Collectibles.Pets.PetHatchPoolConfig.poolId"/> 一致时优先于 <see cref="CatalogIndex"/>。</summary>
        public string PoolId;
    }
}

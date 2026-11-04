using Game.UI.ViewModels;

namespace Game.UI
{
    /// <summary>
    /// 拖尾商店：与 <see cref="WingShopPanel"/> 共用界面与逻辑，仅商品目录为 <see cref="CosmeticKind.Trail"/>。
    /// </summary>
    public sealed class TrailShopPanel : WingShopPanel
    {
        protected override CosmeticKind CatalogKind => CosmeticKind.Trail;
    }
}

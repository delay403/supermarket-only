using Game.Core;
using Game.Economy.Config;

namespace Game.Economy
{
    /// <summary>玩法模块与限时优惠作用域的映射。</summary>
    static class DiscountOfferScopeUtility
    {
        public static bool TryMapModuleToScope(GameplayModule module, out DiscountOfferProductScope scope)
        {
            scope = default;
            switch (module)
            {
                case GameplayModule.Fishing:
                    scope = DiscountOfferProductScope.Fishing;
                    return true;
                case GameplayModule.DestroyCity:
                    scope = DiscountOfferProductScope.DestroyCity;
                    return true;
                case GameplayModule.WormDodge:
                    scope = DiscountOfferProductScope.WormDodge;
                    return true;
                case GameplayModule.SupermarketOrganize:
                    scope = DiscountOfferProductScope.SupermarketOrganize;
                    return true;
                case GameplayModule.Tower:
                    scope = DiscountOfferProductScope.Tower;
                    return true;
                default:
                    return false;
            }
        }

        public static bool ModuleHasDiscountOffers(GameplayModule module)
        {
            switch (module)
            {
                case GameplayModule.Fishing:
                    return GameProductSettings.IsFishingContentEnabled;
                case GameplayModule.DestroyCity:
                    return GameProductSettings.IsDestroyCityContentEnabled;
                case GameplayModule.WormDodge:
                    return GameProductSettings.IsWormDodgeContentEnabled;
                case GameplayModule.SupermarketOrganize:
                    return GameProductSettings.IsSupermarketOrganizeContentEnabled;
                case GameplayModule.Tower:
                    return GameProductSettings.IsTowerContentEnabled;
                default:
                    return false;
            }
        }

        public static bool TryGetActiveScope(out DiscountOfferProductScope scope)
        {
            scope = default;
            var module = GameplayModuleContext.ActiveModule;
            if (module == GameplayModule.None)
                return false;

            if (!GameplayModuleContext.IsModuleActive(module))
                return false;

            if (!ModuleHasDiscountOffers(module))
                return false;

            return TryMapModuleToScope(module, out scope);
        }
    }
}

using Game.Core.Content;
using UnityEngine;

namespace Game.Core.Config
{
    public static class ConfigRegistry
    {
        public struct Input
        {
            public GameBalanceConfig balance;
            public SupermarketOrganize.Config.SupermarketOrganizeConfig supermarketOrganize;
            public SupermarketOrganize.Config.SupermarketItemDatabase supermarketItems;
            public SupermarketOrganize.Config.SupermarketMapDatabase supermarketMaps;
            public Economy.Config.DiamondCoinsExchangeDatabase diamondCoinsExchange;
        }

        public static GameBalanceConfig Balance { get; private set; }
        public static Economy.Config.DiamondCoinsExchangeDatabase DiamondCoinsExchange { get; private set; }

        static SupermarketOrganize.Config.SupermarketOrganizeConfig s_supermarketConfig;
        static SupermarketOrganize.Config.SupermarketItemDatabase s_supermarketItems;
        static SupermarketOrganize.Config.SupermarketMapDatabase s_supermarketMaps;

        public static SupermarketOrganize.Config.SupermarketOrganizeConfig SupermarketConfig =>
            ResolveLazy(ref s_supermarketConfig);

        public static SupermarketOrganize.Config.SupermarketItemDatabase SupermarketItems =>
            ResolveLazy(ref s_supermarketItems);

        public static SupermarketOrganize.Config.SupermarketMapDatabase SupermarketMaps =>
            ResolveLazy(ref s_supermarketMaps);

        public static void Initialize(Input input)
        {
            Balance = input.balance != null
                ? input.balance
                : Resources.Load<GameBalanceConfig>("GameBalanceConfig");
            s_supermarketConfig = Resolve(input.supermarketOrganize);
            s_supermarketItems = Resolve(input.supermarketItems);
            s_supermarketMaps = Resolve(input.supermarketMaps);
            DiamondCoinsExchange = Resolve(input.diamondCoinsExchange);
        }

        public static void ApplySceneBinder(GameModeConfigBinder binder)
        {
            if (binder == null)
                return;

            GameContentService.RegisterBinderAssets(binder);
            var patch = binder.BuildConfigRegistryInput(default);
            Balance = patch.balance != null ? patch.balance : Balance;
            s_supermarketConfig = Coalesce(s_supermarketConfig, patch.supermarketOrganize);
            s_supermarketItems = Coalesce(s_supermarketItems, patch.supermarketItems);
            s_supermarketMaps = Coalesce(s_supermarketMaps, patch.supermarketMaps);
            DiamondCoinsExchange = Coalesce(DiamondCoinsExchange, patch.diamondCoinsExchange);
        }

        static T Resolve<T>(T direct) where T : Object =>
            direct != null ? direct : GameContentService.Resolve<T>();

        static T ResolveLazy<T>(ref T cache) where T : Object
        {
            if (cache == null)
                cache = GameContentService.Resolve<T>();
            return cache;
        }

        static T Coalesce<T>(T current, T fromBinder) where T : Object =>
            fromBinder != null ? fromBinder : current ?? GameContentService.Resolve<T>();
    }
}

using UnityEngine;
using Game.Core;
using Game.Core.Content;

namespace Game.Core.Config
{
    public static class ConfigRegistry
    {
        public struct Input
        {
            public GameBalanceConfig balance;
            public Maps.MapDatabase maps;
            public Collectibles.Pets.PetDatabase pets;
            public Collectibles.Wings.WingDatabase wings;
            public Collectibles.Trails.TrailDatabase trails;
            public Collectibles.Characters.CharacterDatabase characters;
            public Collectibles.Rods.RodDatabase rods;
            public Fishing.Config.FishDatabase fish;
            public Fishing.Config.FishingZoneDatabase fishingZones;
            public DailySignIn.SignInDatabase signIn;
            public Features.LuckyWheel.WheelDatabase wheel;
            public Features.OnlineReward.OnlineRewardDatabase onlineReward;
            public Collectibles.QualityVisualLibrary qualityVisuals;
            public Features.StrengthTraining.StrengthTrainingGroundDatabase strengthTrainingGrounds;
            public Economy.Config.DiscountOfferDatabase discountOffers;
            public Economy.Config.DiamondCoinsExchangeDatabase diamondCoinsExchange;
            public DestroyCity.Config.NukeDatabase destroyCityNukes;
            public DestroyCity.Config.DestroyCityMapDatabase destroyCityMaps;
            public DestroyCity.Config.FuelDatabase destroyCityFuels;
            public DestroyCity.Config.FuelProducerDatabase destroyCityFuelProducers;
            public DestroyCity.Config.DestroyCityBuildingDatabase destroyCityBuildings;
            public SupermarketOrganize.Config.SupermarketOrganizeConfig supermarketOrganize;
            public SupermarketOrganize.Config.SupermarketItemDatabase supermarketItems;
            public SupermarketOrganize.Config.SupermarketMapDatabase supermarketMaps;
            public AnimalHospital.Config.AnimalHospitalConfig animalHospital;
            public Game.WormDodge.Pickups.WormDodgePickupConfig wormDodgePickup;
        }

        public static GameBalanceConfig Balance { get; private set; }
        public static Maps.MapDatabase Maps { get; private set; }
        public static Collectibles.Pets.PetDatabase Pets { get; private set; }
        public static Collectibles.Wings.WingDatabase Wings { get; private set; }
        public static Collectibles.Trails.TrailDatabase Trails { get; private set; }
        public static Collectibles.Characters.CharacterDatabase Characters { get; private set; }
        public static Collectibles.Rods.RodDatabase Rods { get; private set; }
        public static Fishing.Config.FishDatabase Fish { get; private set; }
        public static Fishing.Config.FishingZoneDatabase FishingZones { get; private set; }
        public static DailySignIn.SignInDatabase SignIn { get; private set; }
        public static Features.LuckyWheel.WheelDatabase Wheel { get; private set; }
        public static Features.OnlineReward.OnlineRewardDatabase OnlineReward { get; private set; }
        public static Collectibles.QualityVisualLibrary QualityVisuals { get; private set; }
        public static Features.StrengthTraining.StrengthTrainingGroundDatabase StrengthTrainingGrounds { get; private set; }
        public static Economy.Config.DiscountOfferDatabase DiscountOffers { get; private set; }
        public static Economy.Config.DiamondCoinsExchangeDatabase DiamondCoinsExchange { get; private set; }
        public static DestroyCity.Config.NukeDatabase DestroyCityNukes { get; private set; }
        public static DestroyCity.Config.DestroyCityMapDatabase DestroyCityMaps { get; private set; }
        public static DestroyCity.Config.FuelDatabase DestroyCityFuels { get; private set; }
        public static DestroyCity.Config.FuelProducerDatabase DestroyCityFuelProducers { get; private set; }
        public static DestroyCity.Config.DestroyCityBuildingDatabase DestroyCityBuildings { get; private set; }
        public static SupermarketOrganize.Config.SupermarketOrganizeConfig SupermarketConfig =>
            ResolveLazy(ref s_supermarketConfig);

        public static SupermarketOrganize.Config.SupermarketItemDatabase SupermarketItems =>
            ResolveLazy(ref s_supermarketItems);

        public static SupermarketOrganize.Config.SupermarketMapDatabase SupermarketMaps =>
            ResolveLazy(ref s_supermarketMaps);

        public static AnimalHospital.Config.AnimalHospitalConfig AnimalHospitalConfig =>
            ResolveLazy(ref s_animalHospitalConfig);

        public static Game.WormDodge.Pickups.WormDodgePickupConfig WormDodgePickupConfig =>
            ResolveLazy(ref s_wormDodgePickupConfig);

        static SupermarketOrganize.Config.SupermarketOrganizeConfig s_supermarketConfig;
        static SupermarketOrganize.Config.SupermarketItemDatabase s_supermarketItems;
        static SupermarketOrganize.Config.SupermarketMapDatabase s_supermarketMaps;
        static AnimalHospital.Config.AnimalHospitalConfig s_animalHospitalConfig;
        static Game.WormDodge.Pickups.WormDodgePickupConfig s_wormDodgePickupConfig;

        public static GameBalanceConfig ResolveBalance() =>
            Balance ?? Resources.Load<GameBalanceConfig>("GameBalanceConfig");

        public static float ResolveFishingDefaultBaseStrength()
        {
            var bal = ResolveBalance();
            return bal != null ? Mathf.Max(0f, bal.fishingDefaultBaseStrength) : 0f;
        }

        [System.Obsolete("快速上鱼已改为 baseStrength × ΣfishingPowerMul，不再使用全局系数。")]
        public static float ResolveFishingPowerMulScale()
        {
            var bal = ResolveBalance();
            return bal != null ? Mathf.Max(0f, bal.fishingPowerMulScale) : 0f;
        }

        public static float ResolveFishingFreeStrengthBonus()
        {
            var bal = ResolveBalance();
            return bal != null ? Mathf.Max(0f, bal.fishingFreeStrengthBonus) : 0f;
        }

        public static float ResolveDestroyCityDefaultBaseFuelRate()
        {
            var bal = ResolveBalance();
            return bal != null ? Mathf.Max(0f, bal.destroyCityDefaultBaseFuelRate) : 0f;
        }

        private static bool s_inited;

        public static void Initialize(Input input)
        {
            if (s_inited) return;
            s_inited = true;

            var settings = GameProductSettings.ResolveActive();

            Balance = ResolveCoreConfig(input.balance, "GameBalanceConfig");
            Maps = ShouldLoadTower()
                ? ResolveModeConfig(input.maps)
                : input.maps;
            Pets = ShouldLoadCosmetics(settings)
                ? ResolveModeConfig(input.pets)
                : input.pets;
            Wings = ShouldLoadCosmetics(settings)
                ? ResolveModeConfig(input.wings)
                : input.wings;
            Trails = ShouldLoadCosmetics(settings)
                ? ResolveModeConfig(input.trails)
                : input.trails;
            Characters = ShouldLoadCosmetics(settings)
                ? ResolveModeConfig(input.characters)
                : input.characters;
            Rods = ShouldLoadFishing()
                ? ResolveModeConfig(input.rods)
                : input.rods;
            Fish = ShouldLoadFishing()
                ? ResolveModeConfig(input.fish)
                : input.fish;
            FishingZones = ShouldLoadFishing()
                ? ResolveModeConfig(input.fishingZones)
                : input.fishingZones;
            SignIn = ShouldLoadActivity(settings)
                ? ResolveModeConfig(input.signIn)
                : input.signIn;
            Wheel = ShouldLoadActivity(settings)
                ? ResolveModeConfig(input.wheel)
                : input.wheel;
            OnlineReward = ShouldLoadActivity(settings)
                ? ResolveModeConfig(input.onlineReward)
                : input.onlineReward;
            QualityVisuals = ShouldLoadCosmetics(settings)
                ? ResolveModeConfig(input.qualityVisuals)
                : input.qualityVisuals;
            StrengthTrainingGrounds = ShouldLoadFishing()
                ? ResolveModeConfig(input.strengthTrainingGrounds)
                : input.strengthTrainingGrounds;
            DiscountOffers = ShouldLoadEconomy(settings)
                ? ResolveModeConfig(input.discountOffers)
                : input.discountOffers;
            DiamondCoinsExchange = ShouldLoadEconomy(settings)
                ? ResolveModeConfig(input.diamondCoinsExchange)
                : input.diamondCoinsExchange;
            DestroyCityNukes = ShouldLoadDestroyCity()
                ? ResolveModeConfig(input.destroyCityNukes)
                : input.destroyCityNukes;
            DestroyCityNukes?.EnsureReady();
            DestroyCityMaps = ShouldLoadDestroyCity()
                ? ResolveModeConfig(input.destroyCityMaps)
                : input.destroyCityMaps;
            DestroyCityFuels = ShouldLoadDestroyCity()
                ? ResolveModeConfig(input.destroyCityFuels)
                : input.destroyCityFuels;
            DestroyCityFuelProducers = ShouldLoadDestroyCity()
                ? ResolveModeConfig(input.destroyCityFuelProducers)
                : input.destroyCityFuelProducers;
            DestroyCityBuildings = ShouldLoadDestroyCity()
                ? ResolveModeConfig(input.destroyCityBuildings)
                : input.destroyCityBuildings;
            s_supermarketConfig = ShouldLoadSupermarket()
                ? ResolveModeConfig(input.supermarketOrganize)
                : input.supermarketOrganize;
            s_supermarketItems = ShouldLoadSupermarket()
                ? ResolveModeConfig(input.supermarketItems)
                : input.supermarketItems;
            s_supermarketMaps = ShouldLoadSupermarket()
                ? ResolveModeConfig(input.supermarketMaps)
                : input.supermarketMaps;
            s_animalHospitalConfig = ShouldLoadAnimalHospital()
                ? ResolveModeConfig(input.animalHospital)
                : input.animalHospital;
            s_wormDodgePickupConfig = ShouldLoadWormDodge()
                ? ResolveModeConfig(input.wormDodgePickup)
                : input.wormDodgePickup;

            if (Balance == null)
                Debug.LogWarning("GameBalanceConfig not found. Place it under Game/Core/Resources/GameBalanceConfig.asset.");
            if (ShouldLoadCosmetics(settings) && Characters == null)
                Debug.LogWarning(
                    "CharacterDatabase not wired. Assign via GameBootstrapper or GameModeConfigBinder from Content/Configs.");
        }

        static bool ShouldLoadTower() =>
            GameProductSettings.HasModule(GameplayModule.Tower);

        static bool ShouldLoadFishing() =>
            GameProductSettings.HasModule(GameplayModule.Fishing);

        static bool ShouldLoadDestroyCity() =>
            GameProductSettings.HasModule(GameplayModule.DestroyCity);

        static bool ShouldLoadSupermarket() =>
            GameProductSettings.HasModule(GameplayModule.SupermarketOrganize);

        static bool ShouldLoadAnimalHospital() =>
            GameProductSettings.HasModule(GameplayModule.AnimalHospital);

        static bool ShouldLoadWormDodge() =>
            GameProductSettings.HasModule(GameplayModule.WormDodge);

        static bool ShouldLoadCosmetics(GameProductSettings settings) =>
            settings == null || settings.NeedsSharedCosmeticsResources();

        static bool ShouldLoadActivity(GameProductSettings settings) =>
            GameProductSettings.HasModule(GameplayModule.Tower)
            || (settings != null && settings.NeedsSharedActivityResources());

        static bool ShouldLoadEconomy(GameProductSettings settings) =>
            settings == null || settings.NeedsSharedEconomyResources();

        static T ResolveCoreConfig<T>(T overrideAsset, string resourcesPath) where T : Object =>
            overrideAsset != null ? overrideAsset : Resources.Load<T>(resourcesPath);

        static T ResolveModeConfig<T>(T overrideAsset) where T : Object
        {
            if (overrideAsset != null)
                return overrideAsset;

            return GameContentService.Resolve<T>();
        }

        static T ResolveLazy<T>(ref T cache) where T : Object
        {
            if (cache != null)
                return cache;

            cache = GameContentService.Resolve<T>();
            return cache;
        }

        /// <summary>
        /// 玩法场景加载后，将场景内 <see cref="GameModeConfigBinder"/> 的 SO 合并进注册表。
        /// 首场景 Bootstrap 时 <see cref="Initialize"/> 只执行一次，因此需在切场景时补注册。
        /// </summary>
        public static void ApplySceneBinder(GameModeConfigBinder binder)
        {
            if (binder == null)
                return;

            GameContentService.RegisterBinderAssets(binder);

            var patch = binder.BuildConfigRegistryInput(default);

            if (ShouldLoadTower())
                Maps = Coalesce(Maps, patch.maps);

            // 共享配置：场景 Binder 的直接引用即表示该资源应进入包体，不再受 includeShared* 开关阻挡。
            Pets = Coalesce(Pets, patch.pets);
            Wings = Coalesce(Wings, patch.wings);
            Trails = Coalesce(Trails, patch.trails);
            Characters = Coalesce(Characters, patch.characters);
            QualityVisuals = Coalesce(QualityVisuals, patch.qualityVisuals);
            SignIn = Coalesce(SignIn, patch.signIn);
            Wheel = Coalesce(Wheel, patch.wheel);
            OnlineReward = Coalesce(OnlineReward, patch.onlineReward);
            DiscountOffers = Coalesce(DiscountOffers, patch.discountOffers);
            DiamondCoinsExchange = Coalesce(DiamondCoinsExchange, patch.diamondCoinsExchange);

            if (ShouldLoadFishing())
            {
                Rods = Coalesce(Rods, patch.rods);
                Fish = Coalesce(Fish, patch.fish);
                FishingZones = Coalesce(FishingZones, patch.fishingZones);
                StrengthTrainingGrounds = Coalesce(StrengthTrainingGrounds, patch.strengthTrainingGrounds);
            }

            if (ShouldLoadDestroyCity())
            {
                DestroyCityNukes = Coalesce(DestroyCityNukes, patch.destroyCityNukes);
                DestroyCityNukes?.EnsureReady();
                DestroyCityMaps = Coalesce(DestroyCityMaps, patch.destroyCityMaps);
                DestroyCityFuels = Coalesce(DestroyCityFuels, patch.destroyCityFuels);
                DestroyCityFuelProducers = Coalesce(DestroyCityFuelProducers, patch.destroyCityFuelProducers);
                DestroyCityBuildings = Coalesce(DestroyCityBuildings, patch.destroyCityBuildings);
            }

            if (ShouldLoadSupermarket())
            {
                s_supermarketConfig = Coalesce(s_supermarketConfig, patch.supermarketOrganize);
                s_supermarketItems = Coalesce(s_supermarketItems, patch.supermarketItems);
                s_supermarketMaps = Coalesce(s_supermarketMaps, patch.supermarketMaps);
            }

            if (ShouldLoadAnimalHospital())
                s_animalHospitalConfig = Coalesce(s_animalHospitalConfig, patch.animalHospital);

            if (ShouldLoadWormDodge())
                s_wormDodgePickupConfig = Coalesce(s_wormDodgePickupConfig, patch.wormDodgePickup);
        }

        static T Coalesce<T>(T current, T fromBinder) where T : Object
        {
            if (fromBinder != null)
                return fromBinder;

            return current != null ? current : GameContentService.Resolve<T>();
        }

        public static void SetSignInOverride(DailySignIn.SignInDatabase database)
        {
            if (database != null)
                SignIn = database;
        }

        public static void SetWheelOverride(Features.LuckyWheel.WheelDatabase database)
        {
            if (database != null)
                Wheel = database;
        }

        public static void SetOnlineRewardOverride(Features.OnlineReward.OnlineRewardDatabase database)
        {
            if (database != null)
                OnlineReward = database;
        }
    }
}

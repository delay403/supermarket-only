using System;
using System.Collections.Generic;
using Game.Collectibles.Rods;
using Game.Core.Config;
using Game.DestroyCity.Config;
using Game.Features.StrengthTraining;
using Game.Fishing.Config;
using Game.AnimalHospital.Config;
using Game.SupermarketOrganize.Config;
using Game.WormDodge.Pickups;
using UnityEngine;

namespace Game.Core.Content
{
    /// <summary>
    /// 挂在玩法场景上，将模式配置 SO 注册到 <see cref="GameContentService"/>。
    /// 配置资产应位于 <c>Modes/{Mode}/Content/Configs/</c>，通过直接引用进入 Build 引用链。
    /// </summary>
    public sealed class GameModeConfigBinder : MonoBehaviour
    {
        [Header("Animal Hospital")]
        [SerializeField] AnimalHospitalConfig animalHospital;

        [Header("Worm Dodge")]
        [SerializeField] WormDodgePickupConfig wormDodgePickup;

        [Header("Supermarket Organize")]
        [SerializeField] SupermarketOrganizeConfig supermarketOrganize;
        [SerializeField] SupermarketItemDatabase supermarketItems;
        [SerializeField] SupermarketMapDatabase supermarketMaps;
        [SerializeField] SupermarketOrganizeProgressionConfig supermarketProgression;
        [SerializeField] SupermarketOrganizeUpgradeUiConfig supermarketUpgradeUi;
        [SerializeField] SupermarketOrganizeHomeConfig supermarketHome;

        [Header("Fishing")]
        [SerializeField] FishDatabase fishDatabase;
        [SerializeField] FishingZoneDatabase fishingZoneDatabase;
        [SerializeField] RodDatabase rodDatabase;
        [SerializeField] StrengthTrainingGroundDatabase strengthTrainingGrounds;

        [Header("Tower / Activity")]
        [SerializeField] Maps.MapDatabase mapDatabase;
        [SerializeField] DailySignIn.SignInDatabase signInDatabase;
        [SerializeField] Features.LuckyWheel.WheelDatabase wheelDatabase;
        [SerializeField] Features.OnlineReward.OnlineRewardDatabase onlineRewardDatabase;

        [Header("Destroy City")]
        [SerializeField] NukeDatabase destroyCityNukes;
        [SerializeField] DestroyCityMapDatabase destroyCityMaps;
        [SerializeField] FuelDatabase destroyCityFuels;
        [SerializeField] FuelProducerDatabase destroyCityFuelProducers;
        [SerializeField] DestroyCityBuildingDatabase destroyCityBuildings;

        [Header("Shared Cosmetics")]
        [SerializeField] Collectibles.Pets.PetDatabase petDatabase;
        [SerializeField] Collectibles.Wings.WingDatabase wingDatabase;
        [SerializeField] Collectibles.Trails.TrailDatabase trailDatabase;
        [SerializeField] Collectibles.Characters.CharacterDatabase characterDatabase;
        [SerializeField] Collectibles.QualityVisualLibrary qualityVisualLibrary;

        [Header("Shared Economy")]
        [SerializeField] Economy.Config.DiscountOfferDatabase discountOffers;
        [SerializeField] Economy.Config.DiamondCoinsExchangeDatabase diamondCoinsExchange;

        void Awake()
        {
            GameContentService.RegisterBinderAssets(this);
        }

        internal void PopulateRegistrations(IDictionary<Type, UnityEngine.Object> registry)
        {
            RegisterIfNotNull(registry, animalHospital);
            RegisterIfNotNull(registry, wormDodgePickup);
            RegisterIfNotNull(registry, supermarketOrganize);
            RegisterIfNotNull(registry, supermarketItems);
            RegisterIfNotNull(registry, supermarketMaps);
            RegisterIfNotNull(registry, supermarketProgression);
            RegisterIfNotNull(registry, supermarketUpgradeUi);
            RegisterIfNotNull(registry, supermarketHome);
            RegisterIfNotNull(registry, fishDatabase);
            RegisterIfNotNull(registry, fishingZoneDatabase);
            RegisterIfNotNull(registry, rodDatabase);
            RegisterIfNotNull(registry, strengthTrainingGrounds);
            RegisterIfNotNull(registry, mapDatabase);
            RegisterIfNotNull(registry, signInDatabase);
            RegisterIfNotNull(registry, wheelDatabase);
            RegisterIfNotNull(registry, onlineRewardDatabase);
            RegisterIfNotNull(registry, destroyCityNukes);
            RegisterIfNotNull(registry, destroyCityMaps);
            RegisterIfNotNull(registry, destroyCityFuels);
            RegisterIfNotNull(registry, destroyCityFuelProducers);
            RegisterIfNotNull(registry, destroyCityBuildings);
            RegisterIfNotNull(registry, petDatabase);
            RegisterIfNotNull(registry, wingDatabase);
            RegisterIfNotNull(registry, trailDatabase);
            RegisterIfNotNull(registry, characterDatabase);
            RegisterIfNotNull(registry, qualityVisualLibrary);
            RegisterIfNotNull(registry, discountOffers);
            RegisterIfNotNull(registry, diamondCoinsExchange);
        }

        public ConfigRegistry.Input BuildConfigRegistryInput(ConfigRegistry.Input baseline)
        {
            baseline.animalHospital = animalHospital ?? baseline.animalHospital;
            baseline.wormDodgePickup = wormDodgePickup ?? baseline.wormDodgePickup;
            baseline.supermarketOrganize = supermarketOrganize ?? baseline.supermarketOrganize;
            baseline.supermarketItems = supermarketItems ?? baseline.supermarketItems;
            baseline.supermarketMaps = supermarketMaps ?? baseline.supermarketMaps;
            baseline.fish = fishDatabase ?? baseline.fish;
            baseline.fishingZones = fishingZoneDatabase ?? baseline.fishingZones;
            baseline.rods = rodDatabase ?? baseline.rods;
            baseline.strengthTrainingGrounds = strengthTrainingGrounds ?? baseline.strengthTrainingGrounds;
            baseline.maps = mapDatabase ?? baseline.maps;
            baseline.signIn = signInDatabase ?? baseline.signIn;
            baseline.wheel = wheelDatabase ?? baseline.wheel;
            baseline.onlineReward = onlineRewardDatabase ?? baseline.onlineReward;
            baseline.destroyCityNukes = destroyCityNukes ?? baseline.destroyCityNukes;
            baseline.destroyCityMaps = destroyCityMaps ?? baseline.destroyCityMaps;
            baseline.destroyCityFuels = destroyCityFuels ?? baseline.destroyCityFuels;
            baseline.destroyCityFuelProducers = destroyCityFuelProducers ?? baseline.destroyCityFuelProducers;
            baseline.destroyCityBuildings = destroyCityBuildings ?? baseline.destroyCityBuildings;
            baseline.pets = petDatabase ?? baseline.pets;
            baseline.wings = wingDatabase ?? baseline.wings;
            baseline.trails = trailDatabase ?? baseline.trails;
            baseline.characters = characterDatabase ?? baseline.characters;
            baseline.qualityVisuals = qualityVisualLibrary ?? baseline.qualityVisuals;
            baseline.discountOffers = discountOffers ?? baseline.discountOffers;
            baseline.diamondCoinsExchange = diamondCoinsExchange ?? baseline.diamondCoinsExchange;
            return baseline;
        }

        static void RegisterIfNotNull<T>(IDictionary<Type, UnityEngine.Object> registry, T asset) where T : UnityEngine.Object
        {
            if (asset != null)
                registry[typeof(T)] = asset;
        }
    }
}

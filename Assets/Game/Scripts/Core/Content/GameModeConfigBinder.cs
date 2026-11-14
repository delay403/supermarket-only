using System;
using System.Collections.Generic;
using Game.Core.Config;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.Core.Content
{
    public sealed class GameModeConfigBinder : MonoBehaviour
    {
        [Header("Supermarket Organize")]
        [SerializeField] SupermarketOrganizeConfig supermarketOrganize;
        [SerializeField] SupermarketItemDatabase supermarketItems;
        [SerializeField] SupermarketMapDatabase supermarketMaps;
        [SerializeField] SupermarketOrganizeProgressionConfig supermarketProgression;
        [SerializeField] SupermarketOrganizeUpgradeUiConfig supermarketUpgradeUi;
        [SerializeField] SupermarketOrganizeHomeConfig supermarketHome;

        [Header("Economy")]
        [SerializeField] Economy.Config.DiamondCoinsExchangeDatabase diamondCoinsExchange;

        void Awake() => GameContentService.RegisterBinderAssets(this);

        internal void PopulateRegistrations(IDictionary<Type, UnityEngine.Object> registry)
        {
            RegisterIfNotNull(registry, supermarketOrganize);
            RegisterIfNotNull(registry, supermarketItems);
            RegisterIfNotNull(registry, supermarketMaps);
            RegisterIfNotNull(registry, supermarketProgression);
            RegisterIfNotNull(registry, supermarketUpgradeUi);
            RegisterIfNotNull(registry, supermarketHome);
            RegisterIfNotNull(registry, diamondCoinsExchange);
        }

        public ConfigRegistry.Input BuildConfigRegistryInput(ConfigRegistry.Input baseline)
        {
            baseline.supermarketOrganize = supermarketOrganize ?? baseline.supermarketOrganize;
            baseline.supermarketItems = supermarketItems ?? baseline.supermarketItems;
            baseline.supermarketMaps = supermarketMaps ?? baseline.supermarketMaps;
            baseline.diamondCoinsExchange = diamondCoinsExchange ?? baseline.diamondCoinsExchange;
            return baseline;
        }

        static void RegisterIfNotNull<T>(IDictionary<Type, UnityEngine.Object> registry, T asset)
            where T : UnityEngine.Object
        {
            if (asset != null)
                registry[typeof(T)] = asset;
        }
    }
}

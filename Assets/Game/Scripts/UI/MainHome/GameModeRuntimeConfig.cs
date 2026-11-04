using Game.Core;

namespace Game.UI.MainHome
{
    /// <summary>运行时解析当前玩法模式在 GameProductSettings 中的 HUD / 装备配置。</summary>
    public static class GameModeRuntimeConfig
    {
        public static GameModeHudVisibility ResolveHudVisibility()
        {
            var entry = GameplayModuleContext.ActiveEntry;
            if (entry != null)
                return entry.hudVisibility;

            return GameModeHudVisibility.ForGameplayKind(InferGameplayKind(GameplayModuleContext.ActiveModule));
        }

        public static GameModeEquipmentBonuses ResolveEquipmentBonuses()
        {
            var entry = GameplayModuleContext.ActiveEntry;
            if (entry != null)
                return entry.equipmentBonuses;

            return GameModeEquipmentBonuses.ForGameplayKind(InferGameplayKind(GameplayModuleContext.ActiveModule));
        }

        public static bool AppliesPetBonuses => ResolveEquipmentBonuses().pet;
        public static bool AppliesWingBonuses => ResolveEquipmentBonuses().wing;
        public static bool AppliesTrailBonuses => ResolveEquipmentBonuses().trail;
        public static bool AppliesCharacterBonuses => ResolveEquipmentBonuses().character;
        public static bool AppliesRodBonuses => ResolveEquipmentBonuses().rod;

        public static float StatMultiplier
        {
            get
            {
                var mul = ResolveEquipmentBonuses().statMultiplier;
                return mul > 0f ? mul : 1f;
            }
        }

        public static MainHomeGameModeEntry FindEntryForModule(GameplayModule module)
        {
            if (module == GameplayModule.None)
                return null;

            var settings = GameProductSettings.ResolveActive();
            if (settings?.modes == null)
                return null;

            for (var i = 0; i < settings.modes.Count; i++)
            {
                var entry = settings.modes[i];
                if (entry != null && entry.enabled && entry.RequiredModule == module)
                    return entry;
            }

            return null;
        }

        static MainHomeGameplayKind InferGameplayKind(GameplayModule module) => module switch
        {
            GameplayModule.Fishing => MainHomeGameplayKind.Fishing,
            GameplayModule.DestroyCity => MainHomeGameplayKind.DestroyCity,
            GameplayModule.WormDodge => MainHomeGameplayKind.WormDodge,
            GameplayModule.SupermarketOrganize => MainHomeGameplayKind.SupermarketOrganize,
            GameplayModule.AnimalHospital => MainHomeGameplayKind.AnimalHospital,
            GameplayModule.Tower => MainHomeGameplayKind.Tower,
            _ => MainHomeGameplayKind.Tower,
        };
    }
}

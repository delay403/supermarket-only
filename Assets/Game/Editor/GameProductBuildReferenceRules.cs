#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Maps;
using Game.VFX;

namespace Game.Editor
{
    static class GameProductBuildReferenceRules
    {
        public static GameplayModule ResolveModules(GameProductSettings settings) =>
            GameProductSettings.ResolveEnabledModules(settings);

        public static bool ShouldLoadCosmetics(GameProductSettings settings) =>
            settings == null || settings.NeedsSharedCosmeticsResources();

        public static bool ShouldLoadActivity(GameProductSettings settings) =>
            GameProductSettings.HasModule(GameplayModule.Tower)
            || (settings != null && settings.NeedsSharedActivityResources());

        public static bool ShouldLoadEconomy(GameProductSettings settings) =>
            settings == null || settings.NeedsSharedEconomyResources();

        public static bool NeedsFguiGameHud(GameplayModule modules) =>
            (modules & (GameplayModule.Tower
                         | GameplayModule.Fishing
                         | GameplayModule.DestroyCity
                         | GameplayModule.WormDodge)) != 0;

        public static bool ShouldStripFguiHomePackage(GameProductSettings settings)
        {
            if (settings == null)
                return false;

            var nav = SceneNavigationSettings.TryLoadFromResources();
            if (nav == null || !nav.useMainHomeScene)
                return true;

            return settings.BuildAvailableModes().Count <= 1;
        }

        public static bool ShouldStripFguiPackage(string packageName, GameProductSettings settings, GameplayModule modules)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                return false;

            switch (packageName)
            {
                case "Home":
                    return ShouldStripFguiHomePackage(settings);
                case "Game":
                    return !NeedsFguiGameHud(modules);
                case "Setting":
                    return true;
                default:
                    return false;
            }
        }

        public static HashSet<int> ResolveAllowedVfxKinds(GameplayModule modules)
        {
            var allowed = new HashSet<int>();

            if ((modules & GameplayModule.Tower) != 0)
            {
                allowed.Add((int)VfxKind.AerialFallWind);
                allowed.Add((int)VfxKind.AerialGlideWind);
                allowed.Add((int)VfxKind.LandingDustStrong);
                allowed.Add((int)VfxKind.LandingDustWeak);
                allowed.Add((int)VfxKind.LandingGroundSlam);
                allowed.Add((int)VfxKind.SettlementVictory);
            }

            if ((modules & GameplayModule.Fishing) != 0)
            {
                allowed.Add((int)VfxKind.FishingWaterSplash);
                allowed.Add((int)VfxKind.FishingCatchCelebrate);
                allowed.Add((int)VfxKind.FishingWaterRipple);
            }

            if ((modules & GameplayModule.DestroyCity) != 0)
            {
                allowed.Add((int)VfxKind.DestroyCityFuelUpgrade);
                allowed.Add((int)VfxKind.DestroyCityNukeUpgrade);
                allowed.Add((int)VfxKind.DestroyCityMissileLaunch);
                allowed.Add((int)VfxKind.DestroyCityMissileExplosion);
            }

            if ((modules & GameplayModule.WormDodge) != 0)
            {
                allowed.Add((int)VfxKind.WormDodgeSpeedBoost);
                allowed.Add((int)VfxKind.WormDodgeSuperJump);
                allowed.Add((int)VfxKind.WormDodgeCoin);
            }

            if ((modules & GameplayModule.SupermarketOrganize) != 0)
            {
                allowed.Add((int)VfxKind.SupermarketOrganizePickUp);
                allowed.Add((int)VfxKind.SupermarketOrganizePlace);
                allowed.Add((int)VfxKind.SupermarketOrganizeVictory);
                allowed.Add((int)VfxKind.SupermarketOrganizeShelfComplete);
            }

            return allowed;
        }

        public static HashSet<int> ResolveAllowedSfxIds(GameplayModule modules, GameProductSettings settings)
        {
            var allowed = new HashSet<int>
            {
                (int)SfxId.UiButtonClick,
                (int)SfxId.ResourceCollect,
                (int)SfxId.ResourceDeduct,
                (int)SfxId.Interact,
                (int)SfxId.UiPanelOpen,
                (int)SfxId.UiSelect,
                (int)SfxId.UiError,
                (int)SfxId.Move,
                (int)SfxId.Jump,
                (int)SfxId.UiSliderChange,
            };

            if ((modules & GameplayModule.Tower) != 0)
            {
                allowed.Add((int)SfxId.AmbientWind);
                allowed.Add((int)SfxId.GlideLand);
                allowed.Add((int)SfxId.SettlementWin);
                allowed.Add((int)SfxId.GlideDistanceMilestone);
                allowed.Add((int)SfxId.AreaUnlock);
            }

            if (ShouldLoadCosmetics(settings))
            {
                allowed.Add((int)SfxId.HatchStart);
                allowed.Add((int)SfxId.PetObtainHatch);
                allowed.Add((int)SfxId.PetObtainMerge);
            }

            if ((modules & GameplayModule.Fishing) != 0)
            {
                allowed.Add((int)SfxId.FishingWaterSplash);
                allowed.Add((int)SfxId.FishingCatchSettle);
                allowed.Add((int)SfxId.UiFloatPopup);
                allowed.Add((int)SfxId.FishingProgressGain);
                allowed.Add((int)SfxId.FishingReelPress1);
                allowed.Add((int)SfxId.FishingReelPress2);
                allowed.Add((int)SfxId.FishingReelPress3);
                allowed.Add((int)SfxId.FishingCatchCelebrate1);
                allowed.Add((int)SfxId.FishingCatchCelebrate2);
                allowed.Add((int)SfxId.FishingCatchCelebrate3);
                allowed.Add((int)SfxId.FishingCatchCelebrate4);
            }

            if ((modules & GameplayModule.DestroyCity) != 0)
            {
                for (var id = (int)SfxId.DestroyCityFuelUpgrade; id <= (int)SfxId.DestroyCityMissileFlight; id++)
                    allowed.Add(id);
            }

            if ((modules & GameplayModule.WormDodge) != 0)
            {
                allowed.Add((int)SfxId.WormMoveLoop);
                allowed.Add((int)SfxId.WormInsideWallLoop);
            }

            if ((modules & GameplayModule.SupermarketOrganize) != 0)
            {
                allowed.Add((int)SfxId.SupermarketOrganizePickUp);
                allowed.Add((int)SfxId.SupermarketOrganizePlace);
                allowed.Add((int)SfxId.SupermarketOrganizeSelect);
                allowed.Add((int)SfxId.SupermarketOrganizePlaceWrong);
                allowed.Add((int)SfxId.SupermarketOrganizeShelfComplete);
            }

            return allowed;
        }

        public static IEnumerable<string> EnumerateBootstrapperOverrideFields(
            GameplayModule modules,
            GameProductSettings settings)
        {
            if ((modules & GameplayModule.Tower) == 0)
            {
                yield return "mapDbOverride";
                if (!ShouldLoadActivity(settings))
                {
                    yield return "signInDbOverride";
                    yield return "wheelDbOverride";
                    yield return "onlineRewardDbOverride";
                }
            }
            else if (!ShouldLoadActivity(settings))
            {
                yield return "signInDbOverride";
                yield return "wheelDbOverride";
                yield return "onlineRewardDbOverride";
            }

            if (!ShouldLoadCosmetics(settings))
            {
                yield return "petDbOverride";
                yield return "wingDbOverride";
                yield return "trailDbOverride";
                yield return "characterDbOverride";
                yield return "qualityVisualsOverride";
            }

            if ((modules & GameplayModule.Fishing) == 0)
            {
                yield return "rodDbOverride";
                yield return "fishDbOverride";
                yield return "fishingZoneDbOverride";
                yield return "strengthTrainingGroundsOverride";
            }

            if ((modules & GameplayModule.DestroyCity) == 0)
            {
                yield return "destroyCityNukesOverride";
                yield return "destroyCityMapsOverride";
                yield return "destroyCityFuelsOverride";
                yield return "destroyCityFuelProducersOverride";
                yield return "destroyCityBuildingsOverride";
            }

            if ((modules & GameplayModule.SupermarketOrganize) == 0)
            {
                yield return "supermarketOrganizeOverride";
                yield return "supermarketItemsOverride";
                yield return "supermarketMapsOverride";
            }

            if (!ShouldLoadEconomy(settings))
            {
                // ConfigRegistry loads these from Resources; overrides are rare but clear for safety.
            }
        }
    }
}
#endif

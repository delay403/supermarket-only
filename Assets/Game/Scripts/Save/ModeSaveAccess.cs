using System.Collections.Generic;
using Game.Core;

namespace Game.Save
{
    /// <summary>融合包体下各玩法独立存档与根 <see cref="PlayerSaveData"/> 的切换桥接。</summary>
    public static class ModeSaveAccess
    {
        public static long Coins => SaveService.Data?.coins ?? 0L;
        public static long Gems => SaveService.Data?.gems ?? 0L;
        public static long Medals => SaveService.Data?.medals ?? 0L;

        public static void AddCoins(long delta)
        {
            if (delta == 0 || SaveService.Data == null)
                return;

            SaveService.Data.coins += delta;
        }

        public static void AddGems(long delta)
        {
            if (delta == 0 || SaveService.Data == null)
                return;

            SaveService.Data.gems += delta;
        }

        public static void SwitchActiveModule(GameplayModule fromModule, GameplayModule toModule)
        {
            var data = SaveService.Data;
            if (data == null || !GameplayModuleContext.IsFusionProduct())
                return;

            if (fromModule != GameplayModule.None && fromModule != toModule)
                PersistRootToProfile(data, fromModule);

            if (toModule != GameplayModule.None)
                LoadProfileIntoRoot(data, toModule);
        }

        public static void PersistRootToProfile(PlayerSaveData data, GameplayModule module)
        {
            var profile = GetOrCreateProfile(data, module);
            if (profile == null)
                return;

            profile.coins = data.coins;
            // gems 为全模式共享，根存档为准；此处仅镜像便于调试/兼容，切模式时不回读。
            profile.gems = data.gems;
            profile.signInDayIndex = data.signInDayIndex;
            profile.lastSignInDateIso = data.lastSignInDateIso ?? "";
            profile.signedInToday = data.signedInToday;
            profile.luckyWheelGameDayId = data.luckyWheelGameDayId ?? "";
            profile.luckyWheelFreeSpinsToday = data.luckyWheelFreeSpinsToday;

            profile.petInstances = CloneList(data.petInstances);
            profile.equippedPetIds = data.equippedPetIds != null
                ? new List<string>(data.equippedPetIds)
                : new List<string>();
            profile.petUnlockedEquipSlots = data.petUnlockedEquipSlots;
            profile.petBackpackCapacity = data.petBackpackCapacity;

            profile.ownedWingIds = data.ownedWingIds != null ? new List<string>(data.ownedWingIds) : new List<string>();
            profile.equippedWingId = data.equippedWingId ?? "";
            profile.ownedTrailIds = data.ownedTrailIds != null ? new List<string>(data.ownedTrailIds) : new List<string>();
            profile.equippedTrailId = data.equippedTrailId ?? "";
            profile.ownedRodIds = data.ownedRodIds != null ? new List<string>(data.ownedRodIds) : new List<string>();
            profile.equippedRodId = data.equippedRodId ?? "";
            profile.ownedCharacters = CloneList(data.ownedCharacters);
            profile.equippedCharacterId = data.equippedCharacterId ?? "";
            profile.seededFromShared = true;
        }

        public static void LoadProfileIntoRoot(PlayerSaveData data, GameplayModule module)
        {
            var profile = GetOrCreateProfile(data, module);
            if (profile == null)
                return;

            if (!profile.seededFromShared)
                profile.SeedFromShared(data);

            data.coins = profile.coins;
            // 钻石跨模式共享：切玩法时不覆盖根存档 gems。
            data.signInDayIndex = profile.signInDayIndex;
            data.lastSignInDateIso = profile.lastSignInDateIso ?? "";
            data.signedInToday = profile.signedInToday;
            data.luckyWheelGameDayId = profile.luckyWheelGameDayId ?? "";
            data.luckyWheelFreeSpinsToday = profile.luckyWheelFreeSpinsToday;

            data.petInstances = CloneList(profile.petInstances);
            data.equippedPetIds = profile.equippedPetIds != null
                ? new List<string>(profile.equippedPetIds)
                : new List<string>();
            data.petUnlockedEquipSlots = profile.petUnlockedEquipSlots;
            data.petBackpackCapacity = profile.petBackpackCapacity;
            data.SyncLegacyEquippedPetId();

            data.ownedWingIds = profile.ownedWingIds != null ? new List<string>(profile.ownedWingIds) : new List<string>();
            data.equippedWingId = profile.equippedWingId ?? "";
            data.ownedTrailIds = profile.ownedTrailIds != null ? new List<string>(profile.ownedTrailIds) : new List<string>();
            data.equippedTrailId = profile.equippedTrailId ?? "";
            data.ownedRodIds = profile.ownedRodIds != null ? new List<string>(profile.ownedRodIds) : new List<string>();
            data.equippedRodId = profile.equippedRodId ?? "";
            data.ownedCharacters = CloneList(profile.ownedCharacters);
            data.equippedCharacterId = profile.equippedCharacterId ?? "";

            // 价格为 0 的初始物品在每个模式 profile 上补齐并装备，避免空档/空种子导致不显示。
            FreeCatalogGrant.EnsureFreeItemsOwnedAndEquipped(data);
            PersistRootToProfile(data, module);
        }

        static ModeScopedProfile GetOrCreateProfile(PlayerSaveData data, GameplayModule module)
        {
            if (data == null || module == GameplayModule.None)
                return null;

            switch (module)
            {
                case GameplayModule.Tower:
                    if (data.towerProfile == null)
                        data.towerProfile = new ModeScopedProfile();
                    return data.towerProfile;
                case GameplayModule.Fishing:
                    data.EnsureFishingSectionInitialized();
                    if (data.fishing.profile == null)
                        data.fishing.profile = new ModeScopedProfile();
                    return data.fishing.profile;
                case GameplayModule.DestroyCity:
                    data.EnsureDestroyCitySectionInitialized();
                    if (data.destroyCity.profile == null)
                        data.destroyCity.profile = new ModeScopedProfile();
                    return data.destroyCity.profile;
                case GameplayModule.WormDodge:
                    data.EnsureWormDodgeSectionInitialized();
                    if (data.wormDodge.profile == null)
                        data.wormDodge.profile = new ModeScopedProfile();
                    return data.wormDodge.profile;
                case GameplayModule.SupermarketOrganize:
                    data.EnsureSupermarketOrganizeSectionInitialized();
                    if (data.supermarketOrganize.profile == null)
                        data.supermarketOrganize.profile = new ModeScopedProfile();
                    return data.supermarketOrganize.profile;
                case GameplayModule.AnimalHospital:
                    data.EnsureAnimalHospitalSectionInitialized();
                    if (data.animalHospital.profile == null)
                        data.animalHospital.profile = new ModeScopedProfile();
                    return data.animalHospital.profile;
                default:
                    return null;
            }
        }

        static List<T> CloneList<T>(List<T> source) where T : struct =>
            source != null ? new List<T>(source) : new List<T>();
    }
}

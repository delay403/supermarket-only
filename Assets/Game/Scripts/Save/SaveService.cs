using System;
using Game.Core;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// 按 <see cref="GameProductSettings.ActiveMode"/> 使用独立 PlayerPrefs 键存整份 <see cref="PlayerSaveData"/> JSON。
    /// 全平台统一走 PlayerPrefs（WebGL IndexedDB、小游戏宿主本地存储等），不使用磁盘文件读写。
    /// </summary>
    public static class SaveService
    {
        /// <summary>旧版仅爬塔存档键（TowerOnly 首次启动会尝试迁移）。</summary>
        public const string LegacyTowerOnlyKey = "RobloxPata.PlayerSaveData.json.v1";

        public const string TowerOnlyKey = "RobloxPata.Tower.PlayerSaveData.json.v1";
        public const string FishingOnlyKey = "RobloxPata.Fishing.PlayerSaveData.json.v1";
        public const string HybridKey = "RobloxPata.Hybrid.PlayerSaveData.json.v1";
        public const string DestroyCityOnlyKey = "RobloxPata.DestroyCity.PlayerSaveData.json.v1";
        public const string WormDodgeOnlyKey = "RobloxPata.WormDodge.PlayerSaveData.json.v1";
        public const string SupermarketOrganizeOnlyKey = "RobloxPata.SupermarketOrganize.PlayerSaveData.json.v1";
        public const string AnimalHospitalOnlyKey = "RobloxPata.AnimalHospital.PlayerSaveData.json.v1";

        private const string LegacySaveFileName = "save.json";

#if !UNITY_WEBGL
        private const string ObsoleteSaveFolderName = "saves";
#endif

        public static PlayerSaveData Data { get; private set; }

        public static GameProductMode ActiveProductMode { get; private set; } = GameProductMode.TowerOnly;

        static bool s_inited;

        public static string GetSaveKeyForMode(GameProductMode mode) => mode switch
        {
            GameProductMode.FishingOnly => FishingOnlyKey,
            GameProductMode.Hybrid => HybridKey,
            GameProductMode.DestroyCityOnly => DestroyCityOnlyKey,
            GameProductMode.WormDodgeOnly => WormDodgeOnlyKey,
            GameProductMode.SupermarketOrganizeOnly => SupermarketOrganizeOnlyKey,
            GameProductMode.AnimalHospitalOnly => AnimalHospitalOnlyKey,
            _ => TowerOnlyKey,
        };

        public static string ActiveSaveKey => GetSaveKeyForMode(ActiveProductMode);

        public enum SavePersistenceLocation
        {
            None = 0,
            PlayerPrefs,
            LegacyPlayerPrefsKey,
            RuntimeMemory,
        }

        /// <summary>读取指定产品形态下已持久化到 PlayerPrefs 的存档 JSON。</summary>
        public static bool TryReadPersistedSaveJson(
            GameProductMode mode,
            out string json,
            out SavePersistenceLocation location)
        {
            json = null;
            location = SavePersistenceLocation.None;

            try
            {
                var key = GetSaveKeyForMode(mode);
                if (PlayerPrefs.HasKey(key))
                {
                    var fromPrefs = PlayerPrefs.GetString(key, string.Empty);
                    if (!string.IsNullOrWhiteSpace(fromPrefs))
                    {
                        json = fromPrefs;
                        location = SavePersistenceLocation.PlayerPrefs;
                        return true;
                    }
                }

                if (mode != GameProductMode.TowerOnly || !PlayerPrefs.HasKey(LegacyTowerOnlyKey))
                    return false;

                var legacyPrefs = PlayerPrefs.GetString(LegacyTowerOnlyKey, string.Empty);
                if (string.IsNullOrWhiteSpace(legacyPrefs))
                    return false;

                json = legacyPrefs;
                location = SavePersistenceLocation.LegacyPlayerPrefsKey;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Read persisted save failed ({mode}): {e.Message}");
                return false;
            }
        }

        public static void Initialize()
        {
            if (s_inited) return;
            s_inited = true;
            ActiveProductMode = GameProductSettings.ActiveMode;
        }

        public static void LoadOrCreate()
        {
            if (!s_inited) Initialize();
            ActiveProductMode = GameProductSettings.ActiveMode;

            Data = TryLoadForActiveMode() ?? new PlayerSaveData();
            Data.EnsurePetLoadoutMigrated();
            Data.MigrateToV2IfNeeded();
            Data.MigrateToV3MedalsGlobalIfNeeded();
            Data.MigrateToV4TreadmillIfNeeded();
            Data.MigrateToV5FishingIfNeeded();
            Data.MigrateToV6FishCodexIfNeeded();
            Data.MigrateToV7DestroyCityIfNeeded();
            Data.MigrateToV8DestroyCityFuelIfNeeded();
            Data.MigrateToV9WormDodgeIfNeeded();
            Data.MigrateToV10WormDodgeStaminaIfNeeded();
            Data.MigrateToV11ModeScopedProfilesIfNeeded();
            Data.MigrateToV12TowerProfileIfNeeded();
            Data.MigrateToV13SupermarketOrganizeIfNeeded();
            Data.MigrateToV14SupermarketProgressionIfNeeded();
            Data.MigrateToV15AnimalHospitalIfNeeded();
            Data.MigrateToV16MainHomeModeUnlocksIfNeeded();
            Data.MigrateToV17SharedGemsAndOnlineRewardIfNeeded();
            Data.EnsureFishingSectionInitialized();
            Data.EnsureDestroyCitySectionInitialized();
            Data.EnsureWormDodgeSectionInitialized();
            Data.EnsureSupermarketOrganizeSectionInitialized();
            Data.EnsureAnimalHospitalSectionInitialized();

            if (GameProductSettings.IsTowerContentEnabled
                || GameProductSettings.IsFishingContentEnabled
                || GameProductSettings.IsDestroyCityContentEnabled
                || GameProductSettings.IsWormDodgeContentEnabled)
            {
                PetStarterGrant.TryGrantStarterPetIfInventoryEmpty(Data);
                FreeCatalogGrant.EnsureFreeItemsOwnedAndEquipped(Data);
            }

            Save();
        }

        public static void Save()
        {
            if (Data == null) return;

            if (GameplayModuleContext.IsFusionProduct()
                && GameplayModuleContext.ActiveModule != GameplayModule.None)
            {
                ModeSaveAccess.PersistRootToProfile(Data, GameplayModuleContext.ActiveModule);
            }

            if (TryPersist(Data.CreatePersistenceSnapshot(ActiveProductMode)))
                return;

            if (TryPersist(Data.CreateMinimalPersistenceSnapshot(ActiveProductMode)))
                return;

            Debug.LogError(
                $"[SaveService] Save failed ({ActiveSaveKey}): storage quota may be exceeded; in-memory progress kept.");
        }

        static bool TryPersist(PlayerSaveData snapshot)
        {
            if (snapshot == null)
                return false;

            try
            {
                var json = JsonUtility.ToJson(snapshot, false);
                if (string.IsNullOrEmpty(json))
                    return false;

                PlayerPrefs.SetString(ActiveSaveKey, json);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Save attempt failed ({ActiveSaveKey}): {e.Message}");
                return false;
            }
        }

        /// <summary>删除当前产品形态下的存档（下次 LoadOrCreate 会新建默认档）。</summary>
        public static void DeleteSave()
        {
            ClearPlayerPrefsEntry(ActiveSaveKey);
            Data = null;
        }

        /// <summary>删除指定产品形态的存档条目（不影响其它形态）。</summary>
        public static void DeleteSaveForMode(GameProductMode mode)
        {
            ClearPlayerPrefsEntry(GetSaveKeyForMode(mode));
            if (mode == GameProductMode.TowerOnly)
                ClearPlayerPrefsEntry(LegacyTowerOnlyKey);
            if (mode == ActiveProductMode)
                Data = null;
        }

        static PlayerSaveData TryLoadForActiveMode()
        {
            try
            {
                if (TryLoadFromPlayerPrefsKey(ActiveSaveKey, out var data))
                    return data;

                if (ActiveProductMode == GameProductMode.TowerOnly)
                {
                    var migrated = TryMigrateLegacyTowerPlayerPrefs();
                    if (migrated != null)
                        return migrated;
                }

#if !UNITY_WEBGL
                var fromObsoleteFile = TryImportObsoleteDiskSave(ActiveProductMode);
                if (fromObsoleteFile != null)
                {
                    Debug.Log(
                        $"[SaveService] Imported obsolete disk save into PlayerPrefs ({ActiveSaveKey}). Future saves use PlayerPrefs only.");
                    return fromObsoleteFile;
                }
#endif

                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Load failed, will create new save. {e.Message}");
                return null;
            }
        }

        static bool TryLoadFromPlayerPrefsKey(string key, out PlayerSaveData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(key))
                return false;

            var json = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            data = JsonUtility.FromJson<PlayerSaveData>(json);
            return data != null;
        }

        static PlayerSaveData TryMigrateLegacyTowerPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(LegacyTowerOnlyKey))
                return TryMigrateLegacyTowerDiskFile();

            try
            {
                var json = PlayerPrefs.GetString(LegacyTowerOnlyKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                    return TryMigrateLegacyTowerDiskFile();

                var data = JsonUtility.FromJson<PlayerSaveData>(json);
                PlayerPrefs.DeleteKey(LegacyTowerOnlyKey);
                PlayerPrefs.Save();
                Debug.Log($"[SaveService] Migrated legacy tower PlayerPrefs to {TowerOnlyKey}.");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Legacy tower PlayerPrefs migration failed: {e.Message}");
                return TryMigrateLegacyTowerDiskFile();
            }
        }

#if !UNITY_WEBGL
        static PlayerSaveData TryMigrateLegacyTowerDiskFile()
        {
            try
            {
                var legacyPath = System.IO.Path.Combine(Application.persistentDataPath, LegacySaveFileName);
                if (!System.IO.File.Exists(legacyPath))
                    return null;

                var json = System.IO.File.ReadAllText(legacyPath);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var data = JsonUtility.FromJson<PlayerSaveData>(json);
                TryDeleteFile(legacyPath);
                if (data != null)
                    Debug.Log($"[SaveService] Migrated legacy disk save.json into PlayerPrefs ({TowerOnlyKey}).");

                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Legacy save.json migration failed: {e.Message}");
                return null;
            }
        }

        static PlayerSaveData TryImportObsoleteDiskSave(GameProductMode mode)
        {
            try
            {
                var path = GetObsoleteSaveFilePath(mode);
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                    return null;

                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var data = JsonUtility.FromJson<PlayerSaveData>(json);
                TryDeleteFile(path);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Obsolete disk save import failed ({mode}): {e.Message}");
                return null;
            }
        }

        static string GetObsoleteSaveFilePath(GameProductMode mode)
        {
            var fileName = mode switch
            {
                GameProductMode.FishingOnly => "fishing_save.json",
                GameProductMode.Hybrid => "hybrid_save.json",
                GameProductMode.DestroyCityOnly => "destroy_city_save.json",
                GameProductMode.WormDodgeOnly => "worm_dodge_save.json",
                _ => "tower_save.json",
            };

            return System.IO.Path.Combine(Application.persistentDataPath, ObsoleteSaveFolderName, fileName);
        }

        static void TryDeleteFile(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Could not delete obsolete save file: {e.Message}");
            }
        }
#else
        static PlayerSaveData TryMigrateLegacyTowerDiskFile() => null;
#endif

        static void ClearPlayerPrefsEntry(string key)
        {
            if (!PlayerPrefs.HasKey(key))
                return;

            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}

using System;
using Game.Core;
using UnityEngine;

namespace Game.Save
{
    public static class SaveService
    {
        public const string SupermarketOrganizeOnlyKey =
            "RobloxPata.SupermarketOrganize.PlayerSaveData.json.v1";

        public static PlayerSaveData Data { get; private set; }
        public static GameProductMode ActiveProductMode => GameProductMode.SupermarketOrganizeOnly;
        public static string ActiveSaveKey => SupermarketOrganizeOnlyKey;

        public static string GetSaveKeyForMode(GameProductMode mode) => SupermarketOrganizeOnlyKey;
        public static void Initialize() { }

        public static void LoadOrCreate()
        {
            Data = TryLoad() ?? new PlayerSaveData();
            Data.EnsureInitialized();
            Save();
        }

        public static void Save()
        {
            if (Data == null)
                return;

            try
            {
                Data.EnsureInitialized();
                PlayerPrefs.SetString(ActiveSaveKey, JsonUtility.ToJson(Data, false));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Save failed: {e.Message}");
            }
        }

        public static void DeleteSave()
        {
            PlayerPrefs.DeleteKey(ActiveSaveKey);
            PlayerPrefs.Save();
            Data = null;
        }

        public static void DeleteSaveForMode(GameProductMode mode) => DeleteSave();

        static PlayerSaveData TryLoad()
        {
            try
            {
                if (!PlayerPrefs.HasKey(ActiveSaveKey))
                    return null;
                var json = PlayerPrefs.GetString(ActiveSaveKey, string.Empty);
                return string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonUtility.FromJson<PlayerSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Load failed; creating a new save: {e.Message}");
                return null;
            }
        }
    }
}

using System;
using UnityEngine;

namespace Game.Save
{
    [Serializable]
    public sealed class PlayerSaveData
    {
        public int saveVersion = 18;
        public long coins = 500L;
        public long gems;
        public float settingsSfxVolume01 = 1f;
        public float settingsBgmVolume01 = 1f;
        public bool settingsVibrationEnabled = true;
        public SupermarketOrganizeModeSaveData supermarketOrganize = new SupermarketOrganizeModeSaveData();

        public void EnsureInitialized()
        {
            settingsSfxVolume01 = Mathf.Clamp01(settingsSfxVolume01);
            settingsBgmVolume01 = Mathf.Clamp01(settingsBgmVolume01);
            supermarketOrganize ??= new SupermarketOrganizeModeSaveData();
            supermarketOrganize.MigrateDefaultsIfNeeded();
            saveVersion = 18;
        }

        public void EnsureSupermarketOrganizeSectionInitialized()
        {
            supermarketOrganize ??= new SupermarketOrganizeModeSaveData();
            supermarketOrganize.MigrateDefaultsIfNeeded();
        }
    }
}

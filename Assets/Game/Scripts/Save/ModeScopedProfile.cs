using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// 单玩法模式独立经济与养成存档（融合包体各模式互不共享）。
    /// 注意：<see cref="gems"/> 已改为全模式共享（根 <see cref="PlayerSaveData.gems"/>），
    /// 切模式时不再从本字段回读；字段仅作迁移/镜像兼容。
    /// </summary>
    [Serializable]
    public sealed class ModeScopedProfile
    {
        public bool seededFromShared;
        public long coins = 500L;

        /// <summary>已废弃为模式独立资源；请读写根存档 <see cref="PlayerSaveData.gems"/>。</summary>
        public long gems;

        public int signInDayIndex;
        public string lastSignInDateIso = "";
        public bool signedInToday;
        public string luckyWheelGameDayId = "";
        public int luckyWheelFreeSpinsToday;

        public List<PlayerSaveData.PetInstanceEntry> petInstances = new List<PlayerSaveData.PetInstanceEntry>();
        public List<string> equippedPetIds = new List<string>();
        public int petUnlockedEquipSlots = 1;
        public int petBackpackCapacity = 20;

        public List<string> ownedWingIds = new List<string>();
        public string equippedWingId = "";

        public List<string> ownedTrailIds = new List<string>();
        public string equippedTrailId = "";

        public List<string> ownedRodIds = new List<string>();
        public string equippedRodId = "";

        public List<PlayerSaveData.CharacterOwnedEntry> ownedCharacters = new List<PlayerSaveData.CharacterOwnedEntry>();
        public string equippedCharacterId = "";

        public void SeedFromShared(PlayerSaveData root)
        {
            if (root == null || seededFromShared)
                return;

            coins = root.coins;
            gems = root.gems;
            signInDayIndex = root.signInDayIndex;
            lastSignInDateIso = root.lastSignInDateIso ?? "";
            signedInToday = root.signedInToday;
            luckyWheelGameDayId = root.luckyWheelGameDayId ?? "";
            luckyWheelFreeSpinsToday = root.luckyWheelFreeSpinsToday;

            petInstances = CloneList(root.petInstances);
            equippedPetIds = root.equippedPetIds != null
                ? new List<string>(root.equippedPetIds)
                : new List<string>();
            petUnlockedEquipSlots = root.petUnlockedEquipSlots;
            petBackpackCapacity = root.petBackpackCapacity;

            ownedWingIds = root.ownedWingIds != null ? new List<string>(root.ownedWingIds) : new List<string>();
            equippedWingId = root.equippedWingId ?? "";
            ownedTrailIds = root.ownedTrailIds != null ? new List<string>(root.ownedTrailIds) : new List<string>();
            equippedTrailId = root.equippedTrailId ?? "";
            ownedRodIds = root.ownedRodIds != null ? new List<string>(root.ownedRodIds) : new List<string>();
            equippedRodId = root.equippedRodId ?? "";
            ownedCharacters = CloneList(root.ownedCharacters);
            equippedCharacterId = root.equippedCharacterId ?? "";

            seededFromShared = true;
        }

        static List<T> CloneList<T>(List<T> source) where T : struct
        {
            return source != null ? new List<T>(source) : new List<T>();
        }
    }
}

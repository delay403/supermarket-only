using System;
using System.Collections.Generic;
using Game.Collectibles.Pets;
using Game.Core;
using UnityEngine;

namespace Game.Save
{
    [Serializable]
    public sealed partial class PlayerSaveData
    {
        public int saveVersion = 17;

        public long coins = 500L;

        /// <summary>全模式共享钻石（融合包体切玩法时不随 ModeScopedProfile 切换）。</summary>
        public long gems = 0L;

        /// <summary>全局奖牌（与金币、钻石同类公共资源）。旧版按地图累计在 medalsByMapId，读档时迁移汇总到本字段。</summary>
        public long medals;

        public string currentMapId = "map_1";

        // JsonUtility does NOT support Dictionary/HashSet. Use serializable lists.
        public List<string> unlockedMaps = new List<string> { "map_1" };

        /// <summary>金币解锁的世界区域 / 门（与 <see cref="Game.WorldTriggers.CoinUnlockAreaTrigger.saveKey"/> 对应）。</summary>
        public List<string> unlockedWorldAreaIds = new List<string>();

        /// <summary>主页副游戏模式（免费解锁）；主模式不在此列表中。</summary>
        public List<string> unlockedMainHomeModeIds = new List<string>();

        [Tooltip("旧版：每张地图独立奖牌计数；saveVersion&lt;3 时迁移进 medals 后可忽略。")]
        public List<MedalEntry> medalsByMapId = new List<MedalEntry>();

        // Pets: 旧版按类型堆叠；迁移后 petCounts 清空，数据在 petInstances。
        public List<PetCountEntry> petCounts = new List<PetCountEntry>();
        /// <summary>背包内每只宠物唯一实例；装备栏 equippedPetIds 存 instanceId。</summary>
        public List<PetInstanceEntry> petInstances = new List<PetInstanceEntry>();

        /// <summary>兼容旧存档的单栏；与 equippedPetIds[0] 同步，勿单独写入。</summary>
        public string equippedPetId;

        /// <summary>同时携带 3 只宠物，空字符串表示该栏位未装备。</summary>
        public List<string> equippedPetIds = new List<string>();

        public const int PetEquipSlotCount = 3;

        /// <summary>已解锁的可携带栏位数（1~3），旧存档读入为 0 时由迁移逻辑置为 1。</summary>
        public int petUnlockedEquipSlots = 1;

        /// <summary>背包宠物道具总数上限（初始 20，最大 80）。</summary>
        public int petBackpackCapacity = 20;

        // Wings / Trails: id -> owned
        public List<string> ownedWingIds = new List<string>();
        public string equippedWingId;

        public List<string> ownedTrailIds = new List<string>();
        public string equippedTrailId;

        // Rods (fishing): id -> owned
        public List<string> ownedRodIds = new List<string>();
        public string equippedRodId;

        // Characters: 已拥有 + 养成星级（1～3）
        public List<CharacterOwnedEntry> ownedCharacters = new List<CharacterOwnedEntry>();
        public string equippedCharacterId;

        // Daily sign-in
        public int signInDayIndex; // 0..6
        public string lastSignInDateIso; // yyyy-MM-dd
        public bool signedInToday;

        /// <summary>与 <see cref="Game.DailySignIn.GameDayUtility"/> 对齐；用于转盘领取次数每日重置。</summary>
        public string luckyWheelGameDayId = "";

        public int luckyWheelFreeSpinsToday;

        /// <summary>累计在线时长（秒），用于在线奖励阶梯；跨会话累加。</summary>
        public double onlineRewardAccumulatedSeconds;

        /// <summary>已领取的在线奖励档位数（按 OnlineRewardDatabase.tiers 顺序，0 起）。</summary>
        public int onlineRewardClaimedCount;

        /// <summary>历史最远的滑翔距离（水平 XZ，自 <see cref="Game.Tower.TowerManager.towerRoot"/> 起算，米）。</summary>
        public float bestGlideDistanceMeters;

        /// <summary>排行榜上显示的本地玩家名称；空则 UI 使用默认值（如「我」）。</summary>
        public string leaderboardDisplayName = "";

        [Range(0f, 1f)] public float settingsSfxVolume01 = 1f;
        [Range(0f, 1f)] public float settingsBgmVolume01 = 1f;
        public bool settingsVibrationEnabled = true;

        /// <summary>双倍攀爬速度 Buff：UTC Ticks 截止时刻；0 或未到时无效。</summary>
        public long climbBuffEndUtcTicks;

        /// <summary>双倍滑翔金币 Buff：UTC Ticks 截止。</summary>
        public long coinsBuffEndUtcTicks;

        /// <summary>跑步机永久爬塔速度加成（m/s，写入 StatsAggregator 加法项）。</summary>
        public float treadmillPermanentClimbSpeedBonus;

        /// <summary>跑步机每日解锁刷新日（与 <see cref="Game.DailySignIn.GameDayUtility"/> 对齐）。</summary>
        public string treadmillGameDayId = "";

        public bool treadmillTier2UnlockedToday;

        public bool treadmillTier3UnlockedToday;

        /// <summary>第三档累计解锁进度（当日未解锁前累计；解锁或跨日清零）。</summary>
        public int treadmillTier3Progress;

        /// <summary>钓鱼玩法专属进度；仅在融合/仅钓鱼产品形态下读写。</summary>
        public FishingModeSaveData fishing = new FishingModeSaveData();

        /// <summary>摧毁城市玩法专属进度；仅在仅摧毁城市产品形态下读写。</summary>
        public DestroyCityModeSaveData destroyCity = new DestroyCityModeSaveData();

        /// <summary>躲避虫子玩法专属进度；仅在仅躲避虫子产品形态下读写。</summary>
        public WormDodgeModeSaveData wormDodge = new WormDodgeModeSaveData();

        /// <summary>超市收纳玩法专属进度。</summary>
        public SupermarketOrganizeModeSaveData supermarketOrganize = new SupermarketOrganizeModeSaveData();

        /// <summary>动物医院玩法专属进度。</summary>
        public AnimalHospitalModeSaveData animalHospital = new AnimalHospitalModeSaveData();

        /// <summary>爬塔玩法独立养成（融合包体切换模式时使用）。</summary>
        public ModeScopedProfile towerProfile = new ModeScopedProfile();

        [Serializable]
        public struct MedalEntry
        {
            public string mapId;
            public int count;
        }

        [Serializable]
        public struct PetInstanceEntry
        {
            public string instanceId;
            public string petId;
        }

        [Serializable]
        public struct PetCountEntry
        {
            public string petId;
            public int count;
        }

        [Serializable]
        public struct CharacterOwnedEntry
        {
            public string characterId;
            /// <summary>当前养成星级 1～3（与 UI 星星数一致）。</summary>
            public int growthStars;
        }

        public bool HasUnlockedMap(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId)) return false;
            return unlockedMaps != null && unlockedMaps.Contains(mapId);
        }

        public void UnlockMap(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId)) return;
            if (unlockedMaps == null) unlockedMaps = new List<string>();
            if (!unlockedMaps.Contains(mapId)) unlockedMaps.Add(mapId);
        }

        public bool HasUnlockedWorldArea(string areaId)
        {
            if (string.IsNullOrWhiteSpace(areaId)) return false;
            return unlockedWorldAreaIds != null && unlockedWorldAreaIds.Contains(areaId.Trim());
        }

        public void UnlockWorldArea(string areaId)
        {
            if (string.IsNullOrWhiteSpace(areaId)) return;
            if (unlockedWorldAreaIds == null) unlockedWorldAreaIds = new List<string>();
            var key = areaId.Trim();
            if (!unlockedWorldAreaIds.Contains(key))
                unlockedWorldAreaIds.Add(key);
        }

        public void AddMedals(long add)
        {
            if (add <= 0) return;
            medals += add;
        }

        public void MigrateInventoryToPetInstancesIfNeeded()
        {
            if (petInstances == null) petInstances = new List<PetInstanceEntry>();

            var legacyStacks = 0;
            if (petCounts != null)
            {
                for (var i = 0; i < petCounts.Count; i++)
                {
                    var c = petCounts[i].count;
                    if (c > 0) legacyStacks += c;
                }
            }

            if (petInstances.Count > 0)
            {
                if (legacyStacks > 0 && petCounts != null)
                    petCounts.Clear();
                return;
            }

            if (legacyStacks == 0) return;

            for (var pi = 0; pi < petCounts.Count; pi++)
            {
                var e = petCounts[pi];
                if (e.count <= 0) continue;
                for (var k = 0; k < e.count; k++)
                {
                    petInstances.Add(new PetInstanceEntry
                    {
                        instanceId = System.Guid.NewGuid().ToString("D"),
                        petId = e.petId
                    });
                }
            }

            if (petCounts != null)
                petCounts.Clear();

            var queues = new Dictionary<string, Queue<string>>(StringComparer.Ordinal);
            foreach (var inst in petInstances)
            {
                if (!queues.TryGetValue(inst.petId, out var q))
                {
                    q = new Queue<string>();
                    queues[inst.petId] = q;
                }

                q.Enqueue(inst.instanceId);
            }

            if (equippedPetIds != null)
            {
                for (var s = 0; s < equippedPetIds.Count; s++)
                {
                    var v = equippedPetIds[s];
                    if (string.IsNullOrWhiteSpace(v)) continue;
                    if (FindInstanceIndex(v) >= 0) continue;
                    var pid = v.Trim();
                    if (queues.TryGetValue(pid, out var queue) && queue.Count > 0)
                        equippedPetIds[s] = queue.Dequeue();
                    else
                        equippedPetIds[s] = "";
                }
            }

            SyncLegacyEquippedPetId();
        }

        int FindInstanceIndex(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || petInstances == null) return -1;
            for (var i = 0; i < petInstances.Count; i++)
            {
                if (petInstances[i].instanceId == instanceId) return i;
            }

            return -1;
        }

        public bool TryResolveInstanceToPetId(string instanceId, out string petId)
        {
            petId = null;
            if (string.IsNullOrWhiteSpace(instanceId)) return false;
            var ix = FindInstanceIndex(instanceId);
            if (ix >= 0)
            {
                petId = petInstances[ix].petId;
                return true;
            }

            return false;
        }

        bool IsInstanceIdEquipped(string instanceId)
        {
            if (equippedPetIds == null) return false;
            for (var i = 0; i < equippedPetIds.Count; i++)
            {
                if (equippedPetIds[i] == instanceId) return true;
            }

            return false;
        }

        void RemoveInstanceById(string instanceId)
        {
            if (petInstances == null) return;
            for (var i = petInstances.Count - 1; i >= 0; i--)
            {
                if (petInstances[i].instanceId == instanceId)
                    petInstances.RemoveAt(i);
            }

            if (equippedPetIds == null) return;
            for (var s = 0; s < equippedPetIds.Count; s++)
            {
                if (equippedPetIds[s] == instanceId)
                    equippedPetIds[s] = "";
            }
        }

        public bool RemovePetInstanceById(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || petInstances == null)
                return false;
            var before = petInstances.Count;
            RemoveInstanceById(instanceId.Trim());
            if (petInstances.Count < before)
            {
                SyncLegacyEquippedPetId();
                return true;
            }

            return false;
        }

        public int GetPetCount(string petId)
        {
            if (string.IsNullOrWhiteSpace(petId)) return 0;
            MigrateInventoryToPetInstancesIfNeeded();
            if (petInstances != null && petInstances.Count > 0)
            {
                var n = 0;
                for (var i = 0; i < petInstances.Count; i++)
                {
                    if (petInstances[i].petId == petId) n++;
                }

                return n;
            }

            if (petCounts == null) return 0;
            for (var i = 0; i < petCounts.Count; i++)
            {
                if (petCounts[i].petId == petId) return petCounts[i].count;
            }

            return 0;
        }

        public void AddPet(string petId, int add = 1)
        {
            if (string.IsNullOrWhiteSpace(petId) || add <= 0) return;
            MigrateInventoryToPetInstancesIfNeeded();
            if (petInstances == null) petInstances = new List<PetInstanceEntry>();
            for (var i = 0; i < add; i++)
            {
                petInstances.Add(new PetInstanceEntry
                {
                    instanceId = System.Guid.NewGuid().ToString("D"),
                    petId = petId
                });
            }
        }

        public bool RemovePet(string petId, int remove = 1)
        {
            if (string.IsNullOrWhiteSpace(petId) || remove <= 0) return false;
            MigrateInventoryToPetInstancesIfNeeded();
            if (petInstances == null || petInstances.Count == 0) return false;

            var toRemove = new List<string>();
            CollectRemovableInstances(petId, remove, toRemove);
            if (toRemove.Count < remove) return false;
            foreach (var id in toRemove)
                RemoveInstanceById(id);
            SyncLegacyEquippedPetId();
            return true;
        }

        void CollectRemovableInstances(string petId, int remove, List<string> sink)
        {
            var taken = new HashSet<string>(StringComparer.Ordinal);
            for (var pass = 0; pass < 2 && sink.Count < remove; pass++)
            {
                for (var i = 0; i < petInstances.Count && sink.Count < remove; i++)
                {
                    var e = petInstances[i];
                    if (e.petId != petId) continue;
                    var eq = IsInstanceIdEquipped(e.instanceId);
                    if (pass == 0 && eq) continue;
                    if (pass == 1 && !eq) continue;
                    if (!taken.Add(e.instanceId)) continue;
                    sink.Add(e.instanceId);
                }
            }
        }
        public bool HasWing(string wingId) => !string.IsNullOrWhiteSpace(wingId) && ownedWingIds != null && ownedWingIds.Contains(wingId);
        public void AddWing(string wingId)
        {
            if (string.IsNullOrWhiteSpace(wingId)) return;
            if (ownedWingIds == null) ownedWingIds = new List<string>();
            if (!ownedWingIds.Contains(wingId)) ownedWingIds.Add(wingId);
        }

        public bool HasTrail(string trailId) => !string.IsNullOrWhiteSpace(trailId) && ownedTrailIds != null && ownedTrailIds.Contains(trailId);
        public void AddTrail(string trailId)
        {
            if (string.IsNullOrWhiteSpace(trailId)) return;
            if (ownedTrailIds == null) ownedTrailIds = new List<string>();
            if (!ownedTrailIds.Contains(trailId)) ownedTrailIds.Add(trailId);
        }

        public bool HasRod(string rodId) => !string.IsNullOrWhiteSpace(rodId) && ownedRodIds != null && ownedRodIds.Contains(rodId);

        public void AddRod(string rodId)
        {
            if (string.IsNullOrWhiteSpace(rodId)) return;
            if (ownedRodIds == null) ownedRodIds = new List<string>();
            if (!ownedRodIds.Contains(rodId)) ownedRodIds.Add(rodId);
        }

        public bool TryGetOwnedCharacter(string characterId, out CharacterOwnedEntry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(characterId) || ownedCharacters == null) return false;
            var id = characterId.Trim();
            for (var i = 0; i < ownedCharacters.Count; i++)
            {
                var e = ownedCharacters[i];
                if (string.Equals(e.characterId?.Trim(), id, StringComparison.Ordinal))
                {
                    entry = e;
                    return true;
                }
            }

            return false;
        }

        public bool HasCharacter(string characterId)
        {
            return TryGetOwnedCharacter(characterId ?? string.Empty, out _);
        }

        public IEnumerable<string> EnumerateOwnedCharacterIds()
        {
            if (ownedCharacters == null) yield break;
            foreach (var c in ownedCharacters)
            {
                if (!string.IsNullOrWhiteSpace(c.characterId))
                    yield return c.characterId.Trim();
            }
        }

        public void UpsertOwnedCharacter(string characterId, int growthStars)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return;
            if (ownedCharacters == null) ownedCharacters = new List<CharacterOwnedEntry>();

            var id = characterId.Trim();
            var clamped = Mathf.Clamp(growthStars, 1, 3);

            for (var i = 0; i < ownedCharacters.Count; i++)
            {
                if (!string.Equals(ownedCharacters[i].characterId?.Trim(), id, StringComparison.Ordinal)) continue;
                var e = ownedCharacters[i];
                e.growthStars = clamped;
                ownedCharacters[i] = e;
                return;
            }

            ownedCharacters.Add(new CharacterOwnedEntry { characterId = id, growthStars = clamped });
        }

        /// <summary>保证 equippedPetIds 长度与旧字段迁移；加载存档后应调用一次。</summary>
        public void EnsurePetLoadoutMigrated()
        {
            MigrateInventoryToPetInstancesIfNeeded();

            if (equippedPetIds == null) equippedPetIds = new List<string>();
            while (equippedPetIds.Count < PetEquipSlotCount) equippedPetIds.Add("");
            while (equippedPetIds.Count > PetEquipSlotCount) equippedPetIds.RemoveAt(equippedPetIds.Count - 1);
            for (var i = 0; i < equippedPetIds.Count; i++)
                equippedPetIds[i] = string.IsNullOrWhiteSpace(equippedPetIds[i]) ? "" : equippedPetIds[i].Trim();

            if (!string.IsNullOrWhiteSpace(equippedPetId) && string.IsNullOrWhiteSpace(equippedPetIds[0]))
                equippedPetIds[0] = equippedPetId.Trim();

            EnsurePetEconomyMigrated();
            SyncLegacyEquippedPetId();

            // 未解锁的栏位清空装备
            for (var i = petUnlockedEquipSlots; i < PetEquipSlotCount; i++)
            {
                if (i < equippedPetIds.Count)
                    equippedPetIds[i] = "";
            }
            SyncLegacyEquippedPetId();
        }

        public void EnsurePetEconomyMigrated()
        {
            if (petUnlockedEquipSlots < 1) petUnlockedEquipSlots = 1;
            if (petUnlockedEquipSlots > PetEquipSlotCount) petUnlockedEquipSlots = PetEquipSlotCount;

            if (petBackpackCapacity < 1) petBackpackCapacity = PetBackpackEconomy.BackpackCapacityInitial;
            if (petBackpackCapacity > PetBackpackEconomy.BackpackCapacityMax)
                petBackpackCapacity = PetBackpackEconomy.BackpackCapacityMax;
        }

        /// <summary>旧档 saveVersion&lt;2：补齐音频与振动默认值（新字段 JsonUtility 曾为 0）。</summary>
        public void MigrateToV2IfNeeded()
        {
            if (saveVersion >= 2)
                return;
            settingsSfxVolume01 = 1f;
            settingsBgmVolume01 = 1f;
            settingsVibrationEnabled = true;
            saveVersion = 2;
        }

        /// <summary>saveVersion&lt;3：将 medalsByMapId 累计并入全局 medals。</summary>
        public void MigrateToV3MedalsGlobalIfNeeded()
        {
            if (saveVersion >= 3)
                return;

            if (medalsByMapId != null)
            {
                for (var i = 0; i < medalsByMapId.Count; i++)
                {
                    var n = medalsByMapId[i].count;
                    if (n > 0)
                        AddMedals(n);
                }
            }

            saveVersion = 3;
        }

        /// <summary>saveVersion&lt;4：补齐跑步机存档字段默认值。</summary>
        public void MigrateToV4TreadmillIfNeeded()
        {
            if (saveVersion >= 4)
                return;

            if (string.IsNullOrEmpty(treadmillGameDayId))
                treadmillGameDayId = "";
            treadmillTier2UnlockedToday = false;
            treadmillTier3UnlockedToday = false;
            treadmillTier3Progress = 0;
            treadmillPermanentClimbSpeedBonus = Mathf.Max(0f, treadmillPermanentClimbSpeedBonus);
            saveVersion = 4;
        }

        /// <summary>saveVersion&lt;5：补齐钓竿与钓鱼玩法存档字段。</summary>
        public void MigrateToV5FishingIfNeeded()
        {
            if (saveVersion >= 5)
                return;

            if (ownedRodIds == null) ownedRodIds = new List<string>();
            if (fishing == null) fishing = new FishingModeSaveData();
            fishing.MigrateDefaultsIfNeeded();
            saveVersion = 5;
        }

        /// <summary>saveVersion&lt;6：补齐鱼图鉴存档字段。</summary>
        public void MigrateToV6FishCodexIfNeeded()
        {
            if (saveVersion >= 6)
                return;

            if (fishing == null)
                fishing = new FishingModeSaveData();
            fishing.EnsureFishCodexMigrated();
            saveVersion = 6;
        }

        /// <summary>补齐钓鱼区块默认值；跨形态隔离由独立 PlayerPrefs 键保证，不在此清空其它玩法字段。</summary>
        public void EnsureFishingSectionInitialized()
        {
            MigrateToV5FishingIfNeeded();
            MigrateToV6FishCodexIfNeeded();
            if (fishing == null)
                fishing = new FishingModeSaveData();
            fishing.MigrateDefaultsIfNeeded();
        }

        /// <summary>saveVersion&lt;7：补齐摧毁城市玩法存档字段。</summary>
        public void MigrateToV7DestroyCityIfNeeded()
        {
            if (saveVersion >= 7)
                return;

            if (destroyCity == null)
                destroyCity = new DestroyCityModeSaveData();
            destroyCity.MigrateDefaultsIfNeeded();
            saveVersion = 7;
        }

        /// <summary>saveVersion&lt;8：燃料生产器独立存档、燃料实例、核弹经验。</summary>
        public void MigrateToV8DestroyCityFuelIfNeeded()
        {
            if (saveVersion >= 8)
                return;

            if (destroyCity == null)
                destroyCity = new DestroyCityModeSaveData();
            destroyCity.MigrateDefaultsIfNeeded();

            if (destroyCity.fuelProducers == null)
                destroyCity.fuelProducers = new System.Collections.Generic.List<DestroyCityModeSaveData.FuelProducerSaveEntry>();
            if (destroyCity.fuelInstances == null)
                destroyCity.fuelInstances = new System.Collections.Generic.List<DestroyCityModeSaveData.FuelInstanceSaveEntry>();

            if (destroyCity.fuelProducers.Count == 0)
            {
                destroyCity.fuelProducers.Add(new DestroyCityModeSaveData.FuelProducerSaveEntry
                {
                    producerId = Game.DestroyCity.FuelProducerIds.Manual,
                    level = destroyCity.fuelStationLevel,
                    unlocked = true,
                });
            }

            if (destroyCity.nukeXp <= 0f && destroyCity.fuelXp > 0f)
                destroyCity.nukeXp = destroyCity.fuelXp;

            saveVersion = 8;
        }

        /// <summary>补齐摧毁城市区块默认值。</summary>
        public void EnsureDestroyCitySectionInitialized()
        {
            MigrateToV7DestroyCityIfNeeded();
            MigrateToV8DestroyCityFuelIfNeeded();
            if (destroyCity == null)
                destroyCity = new DestroyCityModeSaveData();
            destroyCity.MigrateDefaultsIfNeeded();
        }

        /// <summary>saveVersion&lt;9：补齐躲避虫子玩法存档字段。</summary>
        public void MigrateToV9WormDodgeIfNeeded()
        {
            if (saveVersion >= 9)
                return;

            if (wormDodge == null)
                wormDodge = new WormDodgeModeSaveData();
            wormDodge.MigrateDefaultsIfNeeded();
            saveVersion = 9;
        }

        /// <summary>补齐躲避虫子区块默认值。</summary>
        public void EnsureWormDodgeSectionInitialized()
        {
            MigrateToV9WormDodgeIfNeeded();
            MigrateToV10WormDodgeStaminaIfNeeded();
            if (wormDodge == null)
                wormDodge = new WormDodgeModeSaveData();
            wormDodge.MigrateDefaultsIfNeeded();
        }

        /// <summary>saveVersion&lt;10：补齐躲避虫子体力与通关时间存档。</summary>
        public void MigrateToV10WormDodgeStaminaIfNeeded()
        {
            if (saveVersion >= 10)
                return;

            if (wormDodge == null)
                wormDodge = new WormDodgeModeSaveData();
            wormDodge.MigrateDefaultsIfNeeded();
            if (wormDodge.stamina <= 0)
                wormDodge.stamina = WormDodgeModeSaveData.DefaultMaxStamina;
            saveVersion = 10;
        }

        /// <summary>saveVersion&lt;11：融合包体为各玩法初始化独立 ModeScopedProfile。</summary>
        public void MigrateToV11ModeScopedProfilesIfNeeded()
        {
            if (saveVersion >= 11)
                return;

            EnsureFishingSectionInitialized();
            EnsureDestroyCitySectionInitialized();
            EnsureWormDodgeSectionInitialized();

            if (Game.Core.GameProductSettings.CountEnabledModules() > 1
                || SaveService.ActiveProductMode == Game.Core.GameProductMode.Hybrid)
            {
                if (Game.Core.GameProductSettings.HasModule(Game.Core.GameplayModule.Fishing))
                    fishing.profile?.SeedFromShared(this);
                if (Game.Core.GameProductSettings.HasModule(Game.Core.GameplayModule.DestroyCity))
                    destroyCity.profile?.SeedFromShared(this);
                if (Game.Core.GameProductSettings.HasModule(Game.Core.GameplayModule.WormDodge))
                    wormDodge.profile?.SeedFromShared(this);
                if (Game.Core.GameProductSettings.HasModule(Game.Core.GameplayModule.SupermarketOrganize))
                    supermarketOrganize?.profile?.SeedFromShared(this);
                if (Game.Core.GameProductSettings.HasModule(Game.Core.GameplayModule.AnimalHospital))
                    animalHospital?.profile?.SeedFromShared(this);
            }

            saveVersion = 11;
        }

        /// <summary>saveVersion&lt;12：融合包体为爬塔初始化独立 ModeScopedProfile。</summary>
        public void MigrateToV12TowerProfileIfNeeded()
        {
            if (saveVersion >= 12)
                return;

            if (towerProfile == null)
                towerProfile = new ModeScopedProfile();

            if ((Game.Core.GameProductSettings.CountEnabledModules() > 1
                 || SaveService.ActiveProductMode == Game.Core.GameProductMode.Hybrid)
                && Game.Core.GameProductSettings.HasModule(Game.Core.GameplayModule.Tower))
                towerProfile.SeedFromShared(this);

            saveVersion = 12;
        }

        public void EnsureSupermarketOrganizeSectionInitialized()
        {
            MigrateToV13SupermarketOrganizeIfNeeded();
            MigrateToV14SupermarketProgressionIfNeeded();
            if (supermarketOrganize == null)
                supermarketOrganize = new SupermarketOrganizeModeSaveData();
            supermarketOrganize.MigrateDefaultsIfNeeded();
        }

        /// <summary>saveVersion&lt;14：补齐超市收纳技能/属性/扫地机器人存档。</summary>
        public void MigrateToV14SupermarketProgressionIfNeeded()
        {
            if (saveVersion >= 14)
                return;

            if (supermarketOrganize == null)
                supermarketOrganize = new SupermarketOrganizeModeSaveData();
            supermarketOrganize.MigrateDefaultsIfNeeded();
            saveVersion = 14;
        }

        /// <summary>saveVersion&lt;13：补齐超市收纳区块。</summary>
        public void MigrateToV13SupermarketOrganizeIfNeeded()
        {
            if (saveVersion >= 13)
                return;

            if (supermarketOrganize == null)
                supermarketOrganize = new SupermarketOrganizeModeSaveData();
            supermarketOrganize.MigrateDefaultsIfNeeded();

            if ((Game.Core.GameProductSettings.CountEnabledModules() > 1
                 || SaveService.ActiveProductMode == Game.Core.GameProductMode.Hybrid)
                && Game.Core.GameProductSettings.HasModule(Game.Core.GameplayModule.SupermarketOrganize))
                supermarketOrganize.profile?.SeedFromShared(this);

            saveVersion = 13;
        }

        public void EnsureAnimalHospitalSectionInitialized()
        {
            MigrateToV15AnimalHospitalIfNeeded();
            if (animalHospital == null)
                animalHospital = new AnimalHospitalModeSaveData();
            animalHospital.MigrateDefaultsIfNeeded();
        }

        /// <summary>saveVersion&lt;15：补齐动物医院区块。</summary>
        public void MigrateToV15AnimalHospitalIfNeeded()
        {
            if (saveVersion >= 15)
                return;

            if (animalHospital == null)
                animalHospital = new AnimalHospitalModeSaveData();
            animalHospital.MigrateDefaultsIfNeeded();

            if ((Game.Core.GameProductSettings.CountEnabledModules() > 1
                 || SaveService.ActiveProductMode == Game.Core.GameProductMode.Hybrid)
                && Game.Core.GameProductSettings.HasModule(Game.Core.GameplayModule.AnimalHospital))
                animalHospital.profile?.SeedFromShared(this);

            saveVersion = 15;
        }

        /// <summary>saveVersion&lt;16：补齐主页副模式解锁列表。</summary>
        public void MigrateToV16MainHomeModeUnlocksIfNeeded()
        {
            if (saveVersion >= 16)
                return;

            if (unlockedMainHomeModeIds == null)
                unlockedMainHomeModeIds = new List<string>();

            saveVersion = 16;
        }

        /// <summary>
        /// saveVersion&lt;17：钻石改为全模式共享；补齐在线奖励字段。
        /// 取根存档与各 ModeScopedProfile 中 gems 的最大值作为共享钻石。
        /// </summary>
        public void MigrateToV17SharedGemsAndOnlineRewardIfNeeded()
        {
            if (saveVersion >= 17)
                return;

            var shared = gems;
            shared = MaxGems(shared, towerProfile);
            if (fishing != null)
                shared = MaxGems(shared, fishing.profile);
            if (destroyCity != null)
                shared = MaxGems(shared, destroyCity.profile);
            if (wormDodge != null)
                shared = MaxGems(shared, wormDodge.profile);
            if (supermarketOrganize != null)
                shared = MaxGems(shared, supermarketOrganize.profile);
            if (animalHospital != null)
                shared = MaxGems(shared, animalHospital.profile);

            gems = shared;
            MirrorSharedGemsToProfile(towerProfile);
            if (fishing != null)
                MirrorSharedGemsToProfile(fishing.profile);
            if (destroyCity != null)
                MirrorSharedGemsToProfile(destroyCity.profile);
            if (wormDodge != null)
                MirrorSharedGemsToProfile(wormDodge.profile);
            if (supermarketOrganize != null)
                MirrorSharedGemsToProfile(supermarketOrganize.profile);
            if (animalHospital != null)
                MirrorSharedGemsToProfile(animalHospital.profile);

            if (onlineRewardAccumulatedSeconds < 0d)
                onlineRewardAccumulatedSeconds = 0d;
            if (onlineRewardClaimedCount < 0)
                onlineRewardClaimedCount = 0;

            saveVersion = 17;
        }

        static long MaxGems(long current, ModeScopedProfile profile) =>
            profile != null ? Math.Max(current, profile.gems) : current;

        void MirrorSharedGemsToProfile(ModeScopedProfile profile)
        {
            if (profile != null)
                profile.gems = gems;
        }

        public bool IsMainHomeModeUnlocked(string modeId)
        {
            if (string.IsNullOrWhiteSpace(modeId))
                return false;

            modeId = modeId.Trim();
            return unlockedMainHomeModeIds != null && unlockedMainHomeModeIds.Contains(modeId);
        }

        public void UnlockMainHomeMode(string modeId)
        {
            if (string.IsNullOrWhiteSpace(modeId))
                return;

            modeId = modeId.Trim();
            if (unlockedMainHomeModeIds == null)
                unlockedMainHomeModeIds = new List<string>();

            if (!unlockedMainHomeModeIds.Contains(modeId))
                unlockedMainHomeModeIds.Add(modeId);
        }

        public void SyncLegacyEquippedPetId()
        {
            if (equippedPetIds == null || equippedPetIds.Count == 0 ||
                string.IsNullOrWhiteSpace(equippedPetIds[0]))
            {
                equippedPetId = "";
                return;
            }

            var s0 = equippedPetIds[0];
            if (TryResolveInstanceToPetId(s0, out var pid) && !string.IsNullOrWhiteSpace(pid))
                equippedPetId = pid;
            else
                equippedPetId = s0;
        }

        /// <summary>
        /// 生成写入 PlayerPrefs 的副本：单玩法包体剔除未启用模块，降低小游戏宿主存储配额压力。
        /// </summary>
        public PlayerSaveData CreatePersistenceSnapshot(GameProductMode mode)
        {
            var json = JsonUtility.ToJson(this);
            var copy = JsonUtility.FromJson<PlayerSaveData>(json);
            if (copy == null)
                return this;

            copy.TrimForPersistence(mode);
            return copy;
        }

        /// <summary>存档仍失败时的极简副本（仅保留经济与当前玩法进度）。</summary>
        public PlayerSaveData CreateMinimalPersistenceSnapshot(GameProductMode mode)
        {
            var copy = new PlayerSaveData
            {
                saveVersion = saveVersion,
                coins = coins,
                gems = gems,
                medals = medals,
                onlineRewardAccumulatedSeconds = onlineRewardAccumulatedSeconds,
                onlineRewardClaimedCount = onlineRewardClaimedCount,
                leaderboardDisplayName = leaderboardDisplayName,
                settingsSfxVolume01 = settingsSfxVolume01,
                settingsBgmVolume01 = settingsBgmVolume01,
                settingsVibrationEnabled = settingsVibrationEnabled,
                equippedCharacterId = equippedCharacterId,
                equippedPetIds = equippedPetIds != null
                    ? new List<string>(equippedPetIds)
                    : new List<string>(),
                equippedWingId = equippedWingId,
                equippedTrailId = equippedTrailId,
            };

            if (mode == GameProductMode.WormDodgeOnly || mode == GameProductMode.Hybrid)
            {
                copy.wormDodge = wormDodge != null
                    ? JsonUtility.FromJson<WormDodgeModeSaveData>(JsonUtility.ToJson(wormDodge))
                    : new WormDodgeModeSaveData();
            }

            if (mode == GameProductMode.FishingOnly || mode == GameProductMode.Hybrid)
            {
                copy.fishing = fishing != null
                    ? JsonUtility.FromJson<FishingModeSaveData>(JsonUtility.ToJson(fishing))
                    : new FishingModeSaveData();
            }

            if (mode == GameProductMode.DestroyCityOnly || mode == GameProductMode.Hybrid)
            {
                copy.destroyCity = destroyCity != null
                    ? JsonUtility.FromJson<DestroyCityModeSaveData>(JsonUtility.ToJson(destroyCity))
                    : new DestroyCityModeSaveData();
            }

            if (mode == GameProductMode.SupermarketOrganizeOnly || mode == GameProductMode.Hybrid)
            {
                copy.supermarketOrganize = supermarketOrganize != null
                    ? JsonUtility.FromJson<SupermarketOrganizeModeSaveData>(JsonUtility.ToJson(supermarketOrganize))
                    : new SupermarketOrganizeModeSaveData();
            }

            if (mode == GameProductMode.AnimalHospitalOnly || mode == GameProductMode.Hybrid)
            {
                copy.animalHospital = animalHospital != null
                    ? JsonUtility.FromJson<AnimalHospitalModeSaveData>(JsonUtility.ToJson(animalHospital))
                    : new AnimalHospitalModeSaveData();
            }

            copy.TrimForPersistence(mode);
            return copy;
        }

        void TrimForPersistence(GameProductMode mode)
        {
            if (mode != GameProductMode.FishingOnly && mode != GameProductMode.Hybrid)
                fishing = new FishingModeSaveData();

            if (mode != GameProductMode.DestroyCityOnly && mode != GameProductMode.Hybrid)
                destroyCity = new DestroyCityModeSaveData();

            if (mode != GameProductMode.WormDodgeOnly && mode != GameProductMode.Hybrid)
                wormDodge = new WormDodgeModeSaveData();

            if (mode != GameProductMode.SupermarketOrganizeOnly && mode != GameProductMode.Hybrid)
                supermarketOrganize = new SupermarketOrganizeModeSaveData();

            if (mode != GameProductMode.AnimalHospitalOnly && mode != GameProductMode.Hybrid)
                animalHospital = new AnimalHospitalModeSaveData();

            if (mode != GameProductMode.TowerOnly && mode != GameProductMode.Hybrid)
            {
                medalsByMapId?.Clear();
                bestGlideDistanceMeters = 0f;
                climbBuffEndUtcTicks = 0L;
                coinsBuffEndUtcTicks = 0L;
                treadmillPermanentClimbSpeedBonus = 0f;
                treadmillGameDayId = string.Empty;
                treadmillTier2UnlockedToday = false;
                treadmillTier3UnlockedToday = false;
                treadmillTier3Progress = 0;
            }

            petCounts?.Clear();
        }
    }
}


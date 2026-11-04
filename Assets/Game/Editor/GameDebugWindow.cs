using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Game.Save;
using Game.Core;
using Game.Core.Config;
using Game.Collectibles.Pets;
using Game.Collectibles.Rods;
using Game.Collectibles.Characters;
using Game.Maps;
using Game.Tower;
using Game.DailySignIn;
using Game.Fishing;
using Game.Features.StrengthTraining;

namespace Game.EditorTools
{
    public sealed class GameDebugWindow : EditorWindow
    {
        private Vector2 _scroll;
        private Vector2 _saveJsonScroll;

        private long _addCoins = 1000;
        private long _addGems = 100;
        private string _grantPetId = "pet_1";
        private string _grantWingId = "wing_1";
        private string _grantTrailId = "trail_1";
        private string _grantRodId = "rod_1";
        private string _grantFishId = "fish_1";
        private string _grantCharacterId = "char_0";
        private float _addStrength = 100f;
        private float _addTreadmillBonus = 1f;

        private GameProductMode _viewSaveMode = GameProductMode.FishingOnly;
        private GameProductMode _runtimeModeOverride = GameProductMode.FishingOnly;
        private bool _prettyJson = true;
        private string _cachedSaveJson = "";
        private string _cachedSaveSummary = "";
        private bool _saveDataDirty = true;

        private bool _foldProductMode = true;
        private bool _foldSaveMgmt = true;
        private bool _foldSaveViewer = true;
        private bool _foldEconomy = true;
        private bool _foldTower;
        private bool _foldFishing = true;
        private bool _foldGrant = true;
        private bool _foldPerf;
        private bool _foldSignIn;

        private float _fps;
        private double _lastTime;
        private int _frameCount;

        private GameProductSettings _runtimeOverrideInstance;

        [MenuItem("Game/Debug/Debug Window")]
        public static void Open()
        {
            GetWindow<GameDebugWindow>("Game Debug");
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            SyncViewModeFromActive();
            _saveDataDirty = true;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
        }

        private void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            _frameCount++;
            var t = EditorApplication.timeSinceStartup;
            if (t - _lastTime >= 0.5f)
            {
                _fps = (float)(_frameCount / (t - _lastTime));
                _frameCount = 0;
                _lastTime = t;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Play Mode", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("IsPlaying", EditorApplication.isPlaying ? "Yes" : "No");
            if (EditorApplication.isPlaying)
                EditorGUILayout.LabelField("FPS (editor)", _fps.ToString("0.0"));

            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawProductModeSection();
            EditorGUILayout.Space(6);
            DrawSaveManagementSection();
            EditorGUILayout.Space(6);
            DrawSaveDataViewerSection();
            EditorGUILayout.Space(6);
            DrawEconomySection();

            if (IsTowerEnabled())
            {
                EditorGUILayout.Space(6);
                DrawTowerSection();
            }

            if (IsFishingEnabled())
            {
                EditorGUILayout.Space(6);
                DrawFishingSection();
            }

            EditorGUILayout.Space(6);
            DrawGrantSection();
            EditorGUILayout.Space(6);
            DrawPerformanceSection();
            EditorGUILayout.Space(6);
            DrawSignInSection();

            EditorGUILayout.EndScrollView();
        }

        private static bool IsTowerEnabled() =>
            ResolveActiveMode() == GameProductMode.TowerOnly || ResolveActiveMode() == GameProductMode.Hybrid;

        private static bool IsFishingEnabled() =>
            ResolveActiveMode() == GameProductMode.FishingOnly || ResolveActiveMode() == GameProductMode.Hybrid;

        private static GameProductMode ResolveActiveMode()
        {
            if (EditorApplication.isPlaying)
                return GameProductSettings.ActiveMode;

            var asset = GameProductSettings.TryLoadFromResources();
            return asset != null ? asset.productMode : GameProductMode.TowerOnly;
        }

        private void SyncViewModeFromActive()
        {
            _viewSaveMode = ResolveActiveMode();
            _runtimeModeOverride = _viewSaveMode;
        }

        private void DrawProductModeSection()
        {
            _foldProductMode = EditorGUILayout.Foldout(_foldProductMode, "Product Mode", true, EditorStyles.foldoutHeader);
            if (!_foldProductMode) return;

            using (new EditorGUI.IndentLevelScope())
            {
                var asset = GameProductSettings.TryLoadFromResources();
                EditorGUILayout.LabelField("Resources asset", asset != null ? asset.productMode.ToString() : "(missing)");
                EditorGUILayout.LabelField("Effective mode", ResolveActiveMode().ToString());
                EditorGUILayout.LabelField("Tower enabled", IsTowerEnabled() ? "Yes" : "No");
                EditorGUILayout.LabelField("Fishing enabled", IsFishingEnabled() ? "Yes" : "No");

                if (GameProductSettings.RuntimeOverride != null)
                    EditorGUILayout.HelpBox(
                        $"RuntimeOverride active: {GameProductSettings.RuntimeOverride.productMode}",
                        MessageType.Info);

                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                {
                    _runtimeModeOverride = (GameProductMode)EditorGUILayout.EnumPopup("Override mode", _runtimeModeOverride);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Apply override + reload save"))
                            ApplyRuntimeModeOverride(_runtimeModeOverride);

                        if (GUILayout.Button("Clear override"))
                            ClearRuntimeModeOverride();
                    }
                }

                if (!EditorApplication.isPlaying)
                    EditorGUILayout.HelpBox("Enter Play Mode to switch product mode at runtime.", MessageType.Info);
            }
        }

        private void ApplyRuntimeModeOverride(GameProductMode mode)
        {
            if (_runtimeOverrideInstance == null)
                _runtimeOverrideInstance = CreateInstance<GameProductSettings>();

            _runtimeOverrideInstance.productMode = mode;
            GameProductSettings.RuntimeOverride = _runtimeOverrideInstance;
            SaveService.Initialize();
            SaveService.LoadOrCreate();
            _viewSaveMode = mode;
            _saveDataDirty = true;
            Debug.Log($"[GameDebug] Runtime product mode override → {mode}, save reloaded.");
        }

        private void ClearRuntimeModeOverride()
        {
            GameProductSettings.RuntimeOverride = null;
            _runtimeOverrideInstance = null;
            if (EditorApplication.isPlaying)
            {
                SaveService.Initialize();
                SaveService.LoadOrCreate();
            }

            SyncViewModeFromActive();
            _saveDataDirty = true;
            Debug.Log("[GameDebug] Runtime product mode override cleared.");
        }

        private void DrawSaveManagementSection()
        {
            _foldSaveMgmt = EditorGUILayout.Foldout(_foldSaveMgmt, "Save Management", true, EditorStyles.foldoutHeader);
            if (!_foldSaveMgmt) return;

            using (new EditorGUI.IndentLevelScope())
            {
                var activeMode = EditorApplication.isPlaying ? SaveService.ActiveProductMode : ResolveActiveMode();
                EditorGUILayout.LabelField("Active PlayerPrefs key", SaveService.GetSaveKeyForMode(activeMode));
                EditorGUILayout.HelpBox(
                    "All platforms persist saves via PlayerPrefs only (WebGL / mini-game local storage).",
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("LoadOrCreate"))
                    {
                        SaveService.Initialize();
                        SaveService.LoadOrCreate();
                        _saveDataDirty = true;
                    }

                    if (GUILayout.Button("Save"))
                    {
                        SaveService.Save();
                        _saveDataDirty = true;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Delete active save"))
                        DeleteSaveForMode(EditorApplication.isPlaying ? SaveService.ActiveProductMode : ResolveActiveMode());
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Per-mode delete", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Tower"))
                        DeleteSaveForMode(GameProductMode.TowerOnly);
                    if (GUILayout.Button("Fishing"))
                        DeleteSaveForMode(GameProductMode.FishingOnly);
                    if (GUILayout.Button("Hybrid"))
                        DeleteSaveForMode(GameProductMode.Hybrid);
                    if (GUILayout.Button("Destroy City"))
                        DeleteSaveForMode(GameProductMode.DestroyCityOnly);
                    if (GUILayout.Button("Delete Worm Dodge save"))
                        DeleteSaveForMode(GameProductMode.WormDodgeOnly);
                }
            }
        }

        private void DeleteSaveForMode(GameProductMode mode)
        {
            if (EditorApplication.isPlaying && mode == SaveService.ActiveProductMode)
            {
                SaveService.DeleteSave();
                SaveService.Initialize();
                SaveService.LoadOrCreate();
            }
            else
            {
                SaveService.DeleteSaveForMode(mode);
            }

            _saveDataDirty = true;
            Debug.Log($"[GameDebug] Deleted save for mode {mode}.");
        }

        private void DrawSaveDataViewerSection()
        {
            _foldSaveViewer = EditorGUILayout.Foldout(_foldSaveViewer, "Save Data Viewer", true, EditorStyles.foldoutHeader);
            if (!_foldSaveViewer) return;

            using (new EditorGUI.IndentLevelScope())
            {
                _viewSaveMode = (GameProductMode)EditorGUILayout.EnumPopup("View mode", _viewSaveMode);
                var key = SaveService.GetSaveKeyForMode(_viewSaveMode);
                var hasPersisted = SaveService.TryReadPersistedSaveJson(
                    _viewSaveMode,
                    out var persistedJson,
                    out var persistenceLocation);
                var hasRuntime = EditorApplication.isPlaying
                    && _viewSaveMode == SaveService.ActiveProductMode
                    && SaveService.Data != null;
                var hasData = hasPersisted || hasRuntime;

                EditorGUILayout.LabelField("PlayerPrefs key", key);
                EditorGUILayout.LabelField("PlayerPrefs exists", PlayerPrefs.HasKey(key) ? "Yes" : "No");
                EditorGUILayout.LabelField("Readable data", hasData ? "Yes" : "No");
                if (hasData)
                    EditorGUILayout.LabelField("Source", DescribeSaveSource(persistenceLocation, hasRuntime));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh"))
                        _saveDataDirty = true;

                    _prettyJson = EditorGUILayout.ToggleLeft("Pretty JSON", _prettyJson, GUILayout.Width(120));

                    using (new EditorGUI.DisabledScope(!hasData))
                    {
                        if (GUILayout.Button("Copy JSON", GUILayout.Width(90)))
                        {
                            RefreshSaveCacheIfNeeded();
                            EditorGUIUtility.systemCopyBuffer = _cachedSaveJson;
                        }

                        if (GUILayout.Button("Export", GUILayout.Width(70)))
                            ExportSaveJson(_viewSaveMode);
                    }
                }

                RefreshSaveCacheIfNeeded();

                if (!hasData)
                {
                    EditorGUILayout.HelpBox("No save data for this mode in PlayerPrefs.", MessageType.Info);
                    return;
                }

                var raw = ResolveViewSaveJson(persistedJson, persistenceLocation, hasRuntime);
                var bytes = Encoding.UTF8.GetByteCount(raw);
                EditorGUILayout.LabelField("JSON size", FormatByteSize(bytes));

                if (!string.IsNullOrEmpty(_cachedSaveSummary))
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.miniBoldLabel);
                    EditorGUILayout.HelpBox(_cachedSaveSummary, MessageType.None);
                }

                EditorGUILayout.LabelField("JSON content", EditorStyles.miniBoldLabel);
                _saveJsonScroll = EditorGUILayout.BeginScrollView(_saveJsonScroll, GUILayout.Height(220));
                EditorGUILayout.TextArea(_cachedSaveJson, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void RefreshSaveCacheIfNeeded()
        {
            if (!_saveDataDirty) return;

            SaveService.TryReadPersistedSaveJson(
                _viewSaveMode,
                out var persistedJson,
                out var persistenceLocation);
            var hasRuntime = EditorApplication.isPlaying
                && _viewSaveMode == SaveService.ActiveProductMode
                && SaveService.Data != null;
            var raw = ResolveViewSaveJson(persistedJson, persistenceLocation, hasRuntime);
            _cachedSaveJson = _prettyJson && !string.IsNullOrEmpty(raw)
                ? JsonPrettyPrint(raw)
                : raw;
            _cachedSaveSummary = BuildSaveSummary(raw);
            _saveDataDirty = false;
        }

        static string ResolveViewSaveJson(
            string persistedJson,
            SaveService.SavePersistenceLocation persistenceLocation,
            bool hasRuntime)
        {
            if (!string.IsNullOrWhiteSpace(persistedJson))
                return persistedJson;

            if (hasRuntime && persistenceLocation == SaveService.SavePersistenceLocation.None)
                return JsonUtility.ToJson(SaveService.Data, false);

            return string.Empty;
        }

        static string DescribeSaveSource(
            SaveService.SavePersistenceLocation persistenceLocation,
            bool hasRuntime)
        {
            if (persistenceLocation != SaveService.SavePersistenceLocation.None)
            {
                return persistenceLocation switch
                {
                    SaveService.SavePersistenceLocation.PlayerPrefs => "PlayerPrefs",
                    SaveService.SavePersistenceLocation.LegacyPlayerPrefsKey => "Legacy PlayerPrefs key",
                    SaveService.SavePersistenceLocation.RuntimeMemory => "Runtime memory",
                    _ => persistenceLocation.ToString(),
                };
            }

            return hasRuntime ? "Runtime memory (not yet persisted)" : "Unknown";
        }

        private static string BuildSaveSummary(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
                return "(empty)";

            try
            {
                var data = JsonUtility.FromJson<PlayerSaveData>(rawJson);
                if (data == null)
                    return "Parse failed: null result.";

                var sb = new StringBuilder();
                sb.AppendLine($"saveVersion={data.saveVersion}");
                sb.AppendLine($"coins={data.coins}, gems={data.gems}, medals={data.medals}");
                sb.AppendLine($"currentMapId={data.currentMapId ?? "(null)"}");
                sb.AppendLine($"pets: {CountOrZero(data.petInstances)} / cap {data.petBackpackCapacity}");
                sb.AppendLine($"wings={CountOrZero(data.ownedWingIds)}, trails={CountOrZero(data.ownedTrailIds)}, rods={CountOrZero(data.ownedRodIds)}");
                sb.AppendLine($"characters={CountOrZero(data.ownedCharacters)}, maps unlocked={CountOrZero(data.unlockedMaps)}");
                sb.AppendLine($"world areas unlocked={CountOrZero(data.unlockedWorldAreaIds)}");

                if (data.fishing != null)
                {
                    var f = data.fishing;
                    sb.AppendLine($"fishing.scene={f.currentFishingSceneId}, baseStrength={f.baseStrength:F1}");
                    sb.AppendLine($"fishing catches={f.totalSuccessfulCatchCount}, codex={CountOrZero(f.fishDiscoveries)}");
                    sb.AppendLine($"fishing offers tracked={CountOrZero(f.discountOfferStates)}, autoFish end ticks={f.autoFishingEndUtcTicks}");
                }
                else
                {
                    sb.AppendLine("fishing: (null)");
                }

                if (data.destroyCity != null)
                {
                    var dc = data.destroyCity;
                    sb.AppendLine(
                        $"destroyCity nuke Lv{dc.nukeLevel} xp={dc.nukeXp:F0}, launches={dc.totalLaunches}, producers={CountOrZero(dc.fuelProducers)}, fuelInstances={CountOrZero(dc.fuelInstances)}");
                }
                else
                {
                    sb.AppendLine("destroyCity: (null)");
                }

                return sb.ToString().TrimEnd();
            }
            catch (Exception e)
            {
                return $"Parse failed: {e.Message}";
            }
        }

        private static int CountOrZero<T>(System.Collections.Generic.List<T> list) => list?.Count ?? 0;

        private static void ExportSaveJson(GameProductMode mode)
        {
            if (!SaveService.TryReadPersistedSaveJson(mode, out var raw, out _))
            {
                if (!EditorApplication.isPlaying
                    || mode != SaveService.ActiveProductMode
                    || SaveService.Data == null)
                {
                    return;
                }

                raw = JsonUtility.ToJson(SaveService.Data, false);
            }

            if (string.IsNullOrWhiteSpace(raw))
                return;

            var dir = Application.persistentDataPath;
            var fileName = $"debug_save_{mode}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, JsonPrettyPrint(raw), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
            Debug.Log($"[GameDebug] Exported save to {path}");
        }

        private void DrawEconomySection()
        {
            _foldEconomy = EditorGUILayout.Foldout(_foldEconomy, "Economy / Data Cheats", true, EditorStyles.foldoutHeader);
            if (!_foldEconomy) return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (SaveService.Data == null)
                {
                    EditorGUILayout.HelpBox("Save not loaded. Click LoadOrCreate (Play Mode).", MessageType.Warning);
                    return;
                }

                EditorGUILayout.LabelField("Coins", SaveService.Data.coins.ToString());
                EditorGUILayout.LabelField("Gems", SaveService.Data.gems.ToString());
                EditorGUILayout.LabelField("Medals (global)", SaveService.Data.medals.ToString());

                _addCoins = EditorGUILayout.LongField("AddCoins", _addCoins);
                if (GUILayout.Button("Add Coins"))
                {
                    SaveService.Data.coins += Math.Max(0, _addCoins);
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                _addGems = EditorGUILayout.LongField("AddGems", _addGems);
                if (GUILayout.Button("Add Gems"))
                {
                    SaveService.Data.gems += Math.Max(0, _addGems);
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                var addMedals = EditorGUILayout.LongField("AddMedals", 10);
                if (GUILayout.Button("Add Medals (global)"))
                {
                    SaveService.Data.AddMedals(Math.Max(0, addMedals));
                    SaveService.Save();
                    _saveDataDirty = true;
                }
            }
        }

        private void DrawTowerSection()
        {
            _foldTower = EditorGUILayout.Foldout(_foldTower, "Tower Debug", true, EditorStyles.foldoutHeader);
            if (!_foldTower) return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (SaveService.Data == null)
                {
                    EditorGUILayout.HelpBox("Save not loaded.", MessageType.Warning);
                    return;
                }

                EditorGUILayout.LabelField("CurrentMapId", SaveService.Data.currentMapId ?? "(null)");
                EditorGUILayout.LabelField("Treadmill climb bonus", SaveService.Data.treadmillPermanentClimbSpeedBonus.ToString("0.###"));

                _addTreadmillBonus = EditorGUILayout.FloatField("Add treadmill bonus", _addTreadmillBonus);
                if (GUILayout.Button("Add treadmill climb speed bonus"))
                {
                    SaveService.Data.treadmillPermanentClimbSpeedBonus += Mathf.Max(0f, _addTreadmillBonus);
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                if (GUILayout.Button("Award tower-clear medals (current MapDef count)"))
                {
                    MapService.AwardTowerClearMedalsForCurrentMap();
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                if (ConfigRegistry.Maps != null)
                {
                    foreach (var map in ConfigRegistry.Maps.maps)
                    {
                        if (map == null) continue;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var unlocked = SaveService.Data.HasUnlockedMap(map.mapId);
                            EditorGUILayout.LabelField($"{map.mapId}", unlocked ? "Unlocked" : "Locked");
                            if (GUILayout.Button("Unlock", GUILayout.Width(80)))
                            {
                                SaveService.Data.UnlockMap(map.mapId);
                                SaveService.Save();
                                _saveDataDirty = true;
                            }

                            if (GUILayout.Button("Select", GUILayout.Width(80)))
                            {
                                MapService.TrySelectMap(map.mapId);
                                SaveService.Save();
                                _saveDataDirty = true;
                            }
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("MapDatabase not loaded (Resources/MapDatabase.asset).", MessageType.Info);
                }
            }
        }

        private void DrawFishingSection()
        {
            _foldFishing = EditorGUILayout.Foldout(_foldFishing, "Fishing Debug", true, EditorStyles.foldoutHeader);
            if (!_foldFishing) return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (SaveService.Data == null)
                {
                    EditorGUILayout.HelpBox("Save not loaded.", MessageType.Warning);
                    return;
                }

                SaveService.Data.EnsureFishingSectionInitialized();
                var fishing = SaveService.Data.fishing;

                EditorGUILayout.LabelField("Scene", fishing.currentFishingSceneId ?? "(null)");
                EditorGUILayout.LabelField("Base strength", StrengthTrainingService.GetBaseStrength().ToString("0.##"));
                EditorGUILayout.LabelField("Equipped rod", string.IsNullOrEmpty(SaveService.Data.equippedRodId) ? "(none)" : SaveService.Data.equippedRodId);
                EditorGUILayout.LabelField("Back fish", string.IsNullOrEmpty(fishing.equippedBackFishId) ? "(none)" : fishing.equippedBackFishId);
                EditorGUILayout.LabelField("Total catches", fishing.totalSuccessfulCatchCount.ToString());
                EditorGUILayout.LabelField("Codex discovered", $"{CountOrZero(fishing.fishDiscoveries)} fish");
                EditorGUILayout.LabelField("Auto fishing pass", AutoFishingService.IsPassActive
                    ? AutoFishingService.FormatRemainingMmSs(false)
                    : "inactive");

                _addStrength = EditorGUILayout.FloatField("Add base strength", _addStrength);
                if (GUILayout.Button("Add base strength"))
                {
                    StrengthTrainingService.AddBaseStrength(Mathf.Max(0f, _addStrength));
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                _grantRodId = EditorGUILayout.TextField("RodId", _grantRodId);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Grant rod"))
                    {
                        SaveService.Data.AddRod(_grantRodId);
                        SaveService.Save();
                        _saveDataDirty = true;
                    }

                    if (GUILayout.Button("Equip rod"))
                    {
                        RodService.Equip(_grantRodId);
                        _saveDataDirty = true;
                    }
                }

                _grantFishId = EditorGUILayout.TextField("FishId", _grantFishId);
                if (GUILayout.Button("Record fish catch (codex)"))
                {
                    var db = ConfigRegistry.Fish;
                    if (db != null && db.TryGet(_grantFishId, out var fishDef) && fishDef != null)
                    {
                        FishCodexService.RecordCatch(fishDef);
                        fishing.totalSuccessfulCatchCount = Mathf.Max(0, fishing.totalSuccessfulCatchCount) + 1;
                        SaveService.Save();
                        _saveDataDirty = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[GameDebug] FishId not found: {_grantFishId}");
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Activate auto-fish pass"))
                    {
                        AutoFishingService.ActivatePass(autoEnableMode: true);
                        _saveDataDirty = true;
                    }

                    if (GUILayout.Button("Reset strength training daily"))
                    {
                        fishing.strengthTrainingGameDayId = "";
                        StrengthTrainingService.RefreshDailyFlags();
                        _saveDataDirty = true;
                    }
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("World areas", EditorStyles.miniBoldLabel);
                var balance = ConfigRegistry.Balance;
                var areaTiers = balance?.fishingWorldAreaRewardTiers;
                if (areaTiers != null && areaTiers.Length > 0)
                {
                    foreach (var entry in areaTiers)
                    {
                        if (string.IsNullOrWhiteSpace(entry.areaSaveKey)) continue;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var unlocked = SaveService.Data.HasUnlockedWorldArea(entry.areaSaveKey);
                            EditorGUILayout.LabelField(entry.areaSaveKey, unlocked ? "Unlocked" : "Locked");
                            if (GUILayout.Button("Unlock", GUILayout.Width(80)))
                            {
                                SaveService.Data.UnlockWorldArea(entry.areaSaveKey);
                                SaveService.Save();
                                _saveDataDirty = true;
                            }
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No fishingWorldAreaRewardTiers in GameBalanceConfig.", MessageType.Info);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Discount offers", EditorStyles.miniBoldLabel);
                if (fishing.discountOfferStates != null && fishing.discountOfferStates.Count > 0)
                {
                    foreach (var state in fishing.discountOfferStates)
                    {
                        EditorGUILayout.LabelField(
                            $"offer {state.offerId}",
                            $"popup={state.popupScheduled}, purchased={state.purchased}, expired={state.expired}");
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("(no offer states yet)");
                }
            }
        }

        private void DrawGrantSection()
        {
            _foldGrant = EditorGUILayout.Foldout(_foldGrant, "Grant / Unlock Items", true, EditorStyles.foldoutHeader);
            if (!_foldGrant) return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (SaveService.Data == null) return;

                _grantPetId = EditorGUILayout.TextField("PetId", _grantPetId);
                if (GUILayout.Button("Grant Pet x1"))
                {
                    PetService.Add(_grantPetId, 1);
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                if (GUILayout.Button("Equip Pet"))
                {
                    PetService.Equip(_grantPetId);
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                EditorGUILayout.Space(4);
                _grantWingId = EditorGUILayout.TextField("WingId", _grantWingId);
                if (GUILayout.Button("Grant Wing"))
                {
                    SaveService.Data.AddWing(_grantWingId);
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                if (GUILayout.Button("Equip Wing"))
                {
                    Collectibles.Wings.WingService.Equip(_grantWingId);
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                EditorGUILayout.Space(4);
                _grantTrailId = EditorGUILayout.TextField("TrailId", _grantTrailId);
                if (GUILayout.Button("Grant Trail"))
                {
                    SaveService.Data.AddTrail(_grantTrailId);
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                if (GUILayout.Button("Equip Trail"))
                {
                    Collectibles.Trails.TrailService.Equip(_grantTrailId);
                    SaveService.Save();
                    _saveDataDirty = true;
                }

                EditorGUILayout.Space(4);
                _grantCharacterId = EditorGUILayout.TextField("CharacterId", _grantCharacterId);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Grant character"))
                    {
                        SaveService.Data.UpsertOwnedCharacter(_grantCharacterId, 1);
                        SaveService.Save();
                        _saveDataDirty = true;
                    }

                    if (GUILayout.Button("Equip character"))
                    {
                        CharacterService.Equip(_grantCharacterId);
                        SaveService.Save();
                        _saveDataDirty = true;
                    }
                }
            }
        }

        private void DrawPerformanceSection()
        {
            _foldPerf = EditorGUILayout.Foldout(_foldPerf, "Performance (Play Mode)", true, EditorStyles.foldoutHeader);
            if (!_foldPerf) return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to see runtime stats.", MessageType.Info);
                    return;
                }

#if UNITY_2023_1_OR_NEWER
                var tower = FindFirstObjectByType<TowerManager>();
#else
                var tower = FindObjectOfType<TowerManager>();
#endif
                if (tower != null)
                {
                    EditorGUILayout.LabelField("Tower ActiveMidCount", tower.ActiveMidCount.ToString());
                    EditorGUILayout.LabelField("Tower PooledMidCount", tower.PooledMidCount.ToString());
                    EditorGUILayout.LabelField("Tower MaxHeight", tower.MaxHeight.ToString("0.0"));
                }
                else if (IsTowerEnabled())
                {
                    EditorGUILayout.HelpBox("No TowerManager found in scene.", MessageType.Warning);
                }
            }
        }

        private void DrawSignInSection()
        {
            _foldSignIn = EditorGUILayout.Foldout(_foldSignIn, "Daily Sign-In", true, EditorStyles.foldoutHeader);
            if (!_foldSignIn) return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (SaveService.Data == null) return;
                EditorGUILayout.LabelField("Today(UTC)", SignInService.TodayIso);
                EditorGUILayout.LabelField("SignedInToday", SaveService.Data.signedInToday ? "Yes" : "No");
                if (GUILayout.Button("Reset Today SignIn Flag"))
                {
                    SaveService.Data.signedInToday = false;
                    SaveService.Data.lastSignInDateIso = "";
                    SaveService.Save();
                    _saveDataDirty = true;
                }
            }
        }

        private static string FormatByteSize(int bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            return $"{bytes / 1024f:0.##} KB ({bytes} B)";
        }

        private static string JsonPrettyPrint(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            var sb = new StringBuilder(json.Length + 64);
            var indent = 0;
            var inString = false;
            var escape = false;

            for (var i = 0; i < json.Length; i++)
            {
                var c = json[i];

                if (inString)
                {
                    sb.Append(c);
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                        escape = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        sb.Append(c);
                        break;
                    case '{':
                    case '[':
                        sb.Append(c);
                        sb.Append('\n');
                        indent++;
                        sb.Append(' ', indent * 2);
                        break;
                    case '}':
                    case ']':
                        sb.Append('\n');
                        indent = Math.Max(0, indent - 1);
                        sb.Append(' ', indent * 2);
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        sb.Append('\n');
                        sb.Append(' ', indent * 2);
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        if (!char.IsWhiteSpace(c))
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}

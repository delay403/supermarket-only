# Supermarket-Only Project Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Physically remove every gameplay mode and unused shared feature except Supermarket Organize while preserving compilation, Addressables builds, save compatibility, and the complete supermarket gameplay loop.

**Architecture:** First establish an executable validator and remove serialized cross-mode components from the retained supermarket scene while all legacy types still compile. Because this project has no gameplay asmdefs and all scripts compile together, perform the source/API reduction as one coordinated cut: replace shared core/save/UI APIs and delete their old consumers before the next Unity compile. After that compile gate passes, delete non-supermarket assets, clean Addressables/generated output, and complete build/runtime verification.

**Tech Stack:** Unity 2021.3.11f1c2, C#, UGUI, FairyGUI runtime, Addressables 1.19.19, PlayerPrefs/JsonUtility, PowerShell, Git.

---

## Retained runtime boundary

Keep these functional areas:

- `Assets/Game/Modes/SupermarketOrganize`
- `Assets/Game/Scripts/SupermarketOrganize`
- player movement/camera components used by `SupermarketOrganizeScene`
- bootstrap, supermarket config registration, PlayerPrefs save, settings, coins/gems, CoinsHub, audio, VFX, cutscene, reward gate, UI sliding infrastructure
- supermarket editor tools and generic import/rename/material tools they use
- `Remote_SupermarketOrganize` and required local Addressables groups

Delete these functional areas:

- AnimalHospital, DestroyCity, Fishing, Tower, WormDodge, EscapeSnake, Glide
- MainHome and multi-product selection/packaging
- pets, wings, trails, characters, rods, sign-in, lucky wheel, online reward, treadmill, strength training, leaderboard and unrelated AI
- discount offers unless a retained supermarket dependency scan proves a live caller; keep diamond-to-coins exchange because supermarket upgrades call CoinsHub
- all non-supermarket Addressables groups, bundles, catalogs and backups

## Task 1: Establish the baseline and executable validator

**Files:**
- Create: `Assets/Game/Editor/SupermarketOnlyProjectValidator.cs`
- Unity creates: `Assets/Game/Editor/SupermarketOnlyProjectValidator.cs.meta`

- [ ] **Step 1: Record the current compile baseline**

```powershell
git status --short
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'D:\code\roblox_gamecenter' -logFile 'D:\code\roblox_gamecenter\Logs\supermarket-baseline.log'
Select-String -Path 'Logs\supermarket-baseline.log' -Pattern 'error CS|Scripts have compiler errors|Aborting batchmode'
```

Expected: Unity exits `0`; the final command returns no compiler-error lines. Close any interactive Unity instance holding the project lock before retrying.

- [ ] **Step 2: Create a validator that encodes the final contract**

Create this file exactly:

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class SupermarketOnlyProjectValidator
    {
        static readonly string[] ForbiddenRoots =
        {
            "Assets/Game/Modes/AnimalHospital", "Assets/Game/Modes/DestroyCity",
            "Assets/Game/Modes/Fishing", "Assets/Game/Modes/Tower",
            "Assets/Game/Modes/WormDodge", "Assets/Game/Scripts/AnimalHospital",
            "Assets/Game/Scripts/DestroyCity", "Assets/Game/Scripts/Fishing",
            "Assets/Game/Scripts/Tower", "Assets/Game/Scripts/WormDodge",
            "Assets/Game/Scripts/EscapeSnake", "Assets/Game/Scripts/Glide",
            "Assets/Game/Scripts/Features/Treadmill",
            "Assets/Game/Scripts/Features/StrengthTraining",
            "Assets/Game/Scripts/DailySignIn", "Assets/Game/Scripts/Collectibles",
            "Assets/Game/Scripts/AI", "Assets/Game/Scripts/Leaderboard",
            "Assets/Game/Core/Scenes/MainHome.unity",
            "Assets/Game/Core/Scenes/SceneTransition.unity",
        };

        static readonly string[] ForbiddenTokens =
        {
            "Game.Tower", "Game.Fishing", "Game.DestroyCity", "Game.WormDodge",
            "Game.EscapeSnake", "Game.AnimalHospital", "MainHome", "FishScene",
            "DestroyCityScene", "EscapeWormScene", "AnimalHospitalScene",
        };

        static readonly string[] AllowedScenes =
        {
            "Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity",
        };

        static readonly HashSet<string> AllowedGroups = new HashSet<string>(StringComparer.Ordinal)
        {
            "Built In Data", "Default Local Group", "Main_Local", "Remote_SupermarketOrganize",
        };

        [MenuItem("Game/Supermarket Organize/Validate Supermarket-Only Project")]
        public static void ValidateMenu() => Validate(false);

        public static void ValidateBatch() => Validate(true);

        static void Validate(bool throwOnFailure)
        {
            var errors = new List<string>();
            foreach (var root in ForbiddenRoots)
                if (AssetDatabase.IsValidFolder(root) || File.Exists(root))
                    errors.Add("Forbidden path still exists: " + root);

            foreach (var path in Directory.GetFiles("Assets/Game", "*.cs", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.EndsWith(nameof(SupermarketOnlyProjectValidator) + ".cs", StringComparison.Ordinal))
                    continue;
                var source = File.ReadAllText(path);
                foreach (var token in ForbiddenTokens)
                    if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                        errors.Add($"Forbidden token '{token}': {normalized}");
            }

            var scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
            if (!scenes.SequenceEqual(AllowedScenes))
                errors.Add("Build Settings must contain only SupermarketOrganizeScene.");

            foreach (var path in Directory.GetFiles(
                         "Assets/AddressableAssetsData/AssetGroups", "*.asset", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("Remote_", StringComparison.Ordinal) && !AllowedGroups.Contains(name))
                    errors.Add("Forbidden Addressables group: " + name);
            }

            const string scenePath = "Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                CollectMissingScripts(root, errors);

            ValidateSaveCompatibility(errors);
            if (errors.Count == 0)
            {
                Debug.Log("[SupermarketOnlyProjectValidator] PASS");
                return;
            }

            var message = "[SupermarketOnlyProjectValidator] FAIL\n" + string.Join("\n", errors.Distinct());
            Debug.LogError(message);
            if (throwOnFailure)
                throw new BuildFailedException(message);
        }

        static void CollectMissingScripts(GameObject root, ICollection<string> errors)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) > 0)
                errors.Add("Missing script on scene object: " + GetPath(root.transform));
            foreach (Transform child in root.transform)
                CollectMissingScripts(child.gameObject, errors);
        }

        static string GetPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        static void ValidateSaveCompatibility(ICollection<string> errors)
        {
            const string json = "{\"saveVersion\":17,\"coins\":777,\"gems\":9," +
                                "\"settingsSfxVolume01\":0.4,\"settingsBgmVolume01\":0.6," +
                                "\"settingsVibrationEnabled\":true,\"fishing\":{\"level\":99}," +
                                "\"supermarketOrganize\":{\"totalRuns\":3,\"scannerSkillLevel\":2}}";
            var data = JsonUtility.FromJson<Game.Save.PlayerSaveData>(json);
            if (data == null || data.coins != 777 || data.gems != 9 ||
                data.supermarketOrganize == null || data.supermarketOrganize.totalRuns != 3 ||
                data.supermarketOrganize.scannerSkillLevel != 2)
                errors.Add("Legacy supermarket save JSON is not compatible with the retained save model.");
        }
    }
}
#endif
```

- [ ] **Step 3: Observe the validator fail for the intended red-state reasons**

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'D:\code\roblox_gamecenter' -executeMethod Game.Editor.SupermarketOnlyProjectValidator.ValidateBatch -logFile 'D:\code\roblox_gamecenter\Logs\supermarket-validator-red.log'
```

Expected: non-zero exit; the log contains `Forbidden path still exists` and forbidden-token messages. It must not contain C# compiler errors or the save-compatibility error.

- [ ] **Step 4: Commit the validator**

```powershell
git add Assets/Game/Editor/SupermarketOnlyProjectValidator.cs Assets/Game/Editor/SupermarketOnlyProjectValidator.cs.meta
git commit -m "test: add supermarket-only project validator"
```

## Task 2: Lock product data and return flow to supermarket

**Files:**
- Modify: `Assets/Game/Core/Resources/GameProductSettings.asset`
- Modify: `Assets/Game/Scripts/UI/SettingsPanel.cs`
- Modify: `Assets/Game/Scripts/SupermarketOrganize/SupermarketOrganizeSettingsFeatureBridge.cs`

- [ ] **Step 1: Reduce the serialized product asset to one mode**

Keep only the existing `SupermarketOrganize` element in `modes`. Remove AnimalHospital, EscapeWorm, DestroyCity, Fish, and any Tower entry. Set:

```yaml
  productMode: 5
  useGameplayModuleFlags: 1
  enabledModules: 16
  autoSyncBuildScenes: 0
  includeSharedCosmeticsResources: 0
  includeSharedActivityResources: 0
  includeSharedEconomyResources: 0
```

Do not yet replace `GameProductSettings.cs`; its legacy API is still required until old consumers are removed in Task 4.

- [ ] **Step 2: Make the settings button always return in-scene**

In `SettingsPanel.OnReturnHomeClicked`, preserve `returnHomeOverride`; replace the fallback `GameSceneLoader.NavigateToMainHome()` with:

```csharp
var director = Game.SupermarketOrganize.SupermarketOrganizeDirector.Instance
               ?? UnityEngine.Object.FindObjectOfType<Game.SupermarketOrganize.SupermarketOrganizeDirector>();
director?.ShowHome();
```

Replace `ApplyReturnHomeButtonVisibility` with:

```csharp
void ApplyReturnHomeButtonVisibility()
{
    if (returnHomeButton != null)
        returnHomeButton.gameObject.SetActive(true);
    var layoutRoot = footerButtonsLayoutRoot;
    if (layoutRoot == null && continueGameButton != null)
        layoutRoot = continueGameButton.transform.parent as RectTransform;
    if (layoutRoot != null)
        LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
}
```

In `SupermarketOrganizeSettingsFeatureBridge`, remove `using Game.Maps;`, remove the `SceneNavigationSettings` condition, and call `panel.SetReturnHomeOverride(OnSettingsReturnHome)` unconditionally.

- [ ] **Step 3: Compile and commit**

Run the Task 1 compile command. Expected: exit `0` and no `error CS` lines.

```powershell
git add Assets/Game/Core/Resources/GameProductSettings.asset Assets/Game/Scripts/UI/SettingsPanel.cs Assets/Game/Scripts/SupermarketOrganize/SupermarketOrganizeSettingsFeatureBridge.cs
git commit -m "refactor: lock product flow to supermarket"
```

## Task 3: Remove legacy components from retained scene and prefabs

**Files:**
- Create temporarily: `Assets/Game/Editor/SupermarketOnlySceneCleanupUtility.cs`
- Modify through Unity: `Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity`
- Modify through Unity: scene-dependent prefabs that contain old-mode components
- Modify: `Assets/Game/Scripts/SupermarketOrganize/SupermarketOrganizePlayerSetup.cs`

- [ ] **Step 1: Create the one-time cleanup utility**

Its `CleanBatch` must open the supermarket scene, walk all scene objects, remove components whose namespaces match the predicate below, remove missing scripts, and save. It must then call `AssetDatabase.GetDependencies(scenePath, true)`, load every dependent `.prefab`, perform the same cleanup, and save each changed prefab.

```csharp
static bool IsRemovedModeComponent(Component component)
{
    if (component == null || component is Transform)
        return false;
    var ns = component.GetType().Namespace ?? string.Empty;
    return ns.StartsWith("Game.Tower", StringComparison.Ordinal)
           || ns.StartsWith("Game.Fishing", StringComparison.Ordinal)
           || ns.StartsWith("Game.DestroyCity", StringComparison.Ordinal)
           || ns.StartsWith("Game.WormDodge", StringComparison.Ordinal)
           || ns.StartsWith("Game.EscapeSnake", StringComparison.Ordinal)
           || ns.StartsWith("Game.AnimalHospital", StringComparison.Ordinal);
}
```

For each GameObject, enumerate `GetComponents<Component>()`, call `UnityEngine.Object.DestroyImmediate(component, true)` for matching components, then call `GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject)`.

- [ ] **Step 2: Execute the cleanup while legacy types still compile**

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'D:\code\roblox_gamecenter' -executeMethod Game.Editor.SupermarketOnlySceneCleanupUtility.CleanBatch -logFile 'D:\code\roblox_gamecenter\Logs\supermarket-scene-cleanup.log'
```

Expected: exit `0`; supermarket director, home/difficulty panels, player motor, first-person camera, supermarket HUD, settings, economy, audio and VFX remain.

- [ ] **Step 3: Remove compile-time suppression dependencies from supermarket setup**

Remove imports for `Game.Collectibles.Stats`, `Game.DestroyCity`, `Game.EscapeSnake`, `Game.WormDodge`, and `Game.WormDodge.Pickups`. Delete all cross-mode `Suppress*` calls and `SuppressWormDodgeComponents`. `ApplyPlayerComponents` must begin directly with supermarket component creation:

```csharp
if (player.GetComponent<SupermarketOrganizeStatsAggregator>() == null)
    player.AddComponent<SupermarketOrganizeStatsAggregator>();
if (player.GetComponent<PlayerSupermarketOrganizeController>() == null)
    player.AddComponent<PlayerSupermarketOrganizeController>();
if (player.GetComponent<SupermarketItemHighlightController>() == null)
    player.AddComponent<SupermarketItemHighlightController>();
if (player.GetComponent<SupermarketCrosshairInteractor>() == null)
    player.AddComponent<SupermarketCrosshairInteractor>();
if (player.GetComponent<SupermarketOrganizeSkillController>() == null)
    player.AddComponent<SupermarketOrganizeSkillController>();
if (player.GetComponent<SupermarketOrganizeAttributeApplicator>() == null)
    player.AddComponent<SupermarketOrganizeAttributeApplicator>();
if (player.GetComponent<SupermarketOrganizePlayerMotorBoost>() == null)
    player.AddComponent<SupermarketOrganizePlayerMotorBoost>();
```

- [ ] **Step 4: Clear known stale scene references and save**

Clear `balanceOverride` GUID `45dab4ba6a418ec489319c3031ea91f3` and `sceneNavigationSettingsOverride` GUID `8c7297b0c7681374b921521ad3289487`. The final bootstrap no longer owns the navigation field; balance falls back to Resources if required.

- [ ] **Step 5: Compile and commit the serialized migration**

Expected: Unity compile exits `0`; validator may still fail on forbidden roots/tokens but must not report missing scripts in the supermarket scene.

```powershell
git add Assets/Game/Editor/SupermarketOnlySceneCleanupUtility.cs Assets/Game/Editor/SupermarketOnlySceneCleanupUtility.cs.meta Assets/Game/Modes/SupermarketOrganize Assets/Game/Scripts/SupermarketOrganize/SupermarketOrganizePlayerSetup.cs
git commit -m "chore: detach supermarket scene from legacy modes"
```

## Task 4: Perform the coordinated runtime source cut

This task is one compile unit. Do not run Unity between Steps 1–7: intermediate states intentionally reference APIs being replaced in later steps. Do not commit until Step 8 passes.

**Files replaced:**
- `Assets/Game/Scripts/Core/GameProductMode.cs`
- `Assets/Game/Scripts/Core/GameProductSettings.cs`
- `Assets/Game/Scripts/Core/GameplayModuleContext.cs`
- `Assets/Game/Scripts/Core/GameBootstrapper.cs`
- `Assets/Game/Scripts/Core/Config/ConfigRegistry.cs`
- `Assets/Game/Scripts/Core/Content/GameModeConfigBinder.cs`
- `Assets/Game/Scripts/Save/PlayerSaveData.cs`
- `Assets/Game/Scripts/Save/SaveService.cs`
- `Assets/Game/Scripts/Save/ModeSaveAccess.cs`
- `Assets/Game/Scripts/UI/Framework/UiPanelId.cs`
- `Assets/Game/Scripts/UI/Framework/UiOpenPayload.cs`
- `Assets/Game/Scripts/UI/Framework/UiCoordinator.cs`

**Files deleted:**
- `Assets/Game/Core/Resources/GameProductSettings.asset` and `.meta` after the fixed runtime API replaces the ScriptableObject

**Files modified:**
- `Assets/Game/Scripts/Save/SupermarketOrganizeModeSaveData.cs`
- `Assets/Game/Scripts/Hud/GameHudUguiPanel.cs`
- `Assets/Game/Scripts/Player/PlayerMotor.cs`
- retained Audio, Economy, Reward, UI and Player files reported by the banned-token scan

- [ ] **Step 1: Replace product identity with fixed supermarket values**

Use:

```csharp
namespace Game.Core
{
    public enum GameProductMode { SupermarketOrganizeOnly = 5 }
    [System.Flags]
    public enum GameplayModule { None = 0, SupermarketOrganize = 1 << 4 }

    public static class GameProductSettings
    {
        public static GameProductMode ActiveMode => GameProductMode.SupermarketOrganizeOnly;
        public static bool IsSupermarketOrganizeContentEnabled => true;
        public static GameplayModule ResolveEnabledModules() => GameplayModule.SupermarketOrganize;
        public static bool HasModule(GameplayModule module) => module == GameplayModule.SupermarketOrganize;
        public static int CountEnabledModules() => 1;
    }
}
```

`GameplayModuleContext` must expose `ActiveModule => SupermarketOrganize`, `IsFusionProduct() => false`, and `IsModuleActive(module)` true only for `None` and `SupermarketOrganize`. Keep the existing `ModuleChanged` event signature so retained supermarket HUD listeners compile. `ApplyLoadedScene` and `ClearExplicit` become no-ops.

Delete `Assets/Game/Core/Resources/GameProductSettings.asset` and its `.meta` in this same coordinated cut. Once `GameProductSettings` is a fixed static runtime API, the old ScriptableObject would otherwise become an invalid serialized asset with a missing script.

- [ ] **Step 2: Replace config registration and bootstrap**

`ConfigRegistry.Input` and `GameModeConfigBinder` retain only:

```csharp
public GameBalanceConfig balance;
public SupermarketOrganize.Config.SupermarketOrganizeConfig supermarketOrganize;
public SupermarketOrganize.Config.SupermarketItemDatabase supermarketItems;
public SupermarketOrganize.Config.SupermarketMapDatabase supermarketMaps;
public Economy.Config.DiamondCoinsExchangeDatabase diamondCoinsExchange;
```

The binder additionally serializes/registers `SupermarketOrganizeProgressionConfig`, `SupermarketOrganizeUpgradeUiConfig`, and `SupermarketOrganizeHomeConfig`. `ConfigRegistry.Initialize` resolves only those retained objects. `GameBootstrapper` keeps balance, the three supermarket databases, VFX bindings, SFX library and BGM; it initializes save/audio/VFX and refreshes only supermarket runtime/config binders. Remove collectible grants, online rewards and every old player setup call.

- [ ] **Step 3: Replace the save root and service**

Use this root model:

```csharp
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
        settingsSfxVolume01 = UnityEngine.Mathf.Clamp01(settingsSfxVolume01);
        settingsBgmVolume01 = UnityEngine.Mathf.Clamp01(settingsBgmVolume01);
        supermarketOrganize ??= new SupermarketOrganizeModeSaveData();
        supermarketOrganize.MigrateDefaultsIfNeeded();
        saveVersion = 18;
    }
}
```

`SaveService` retains the exact key `RobloxPata.SupermarketOrganize.PlayerSaveData.json.v1`; `LoadOrCreate` reads it with `JsonUtility`, ignores unknown old JSON fields, calls `EnsureInitialized`, and saves. `ModeSaveAccess` contains only root `Coins`, `Gems`, `AddCoins`, and `AddGems`. Remove `ModeScopedProfile profile` from `SupermarketOrganizeModeSaveData`.

- [ ] **Step 4: Replace UI routing with retained panel values**

Keep serialized numeric values:

```csharp
public enum UiPanelId
{
    None = 0, Settings = 10, CoinsHub = 27,
    SupermarketOrganizeUpgrade = 33, SupermarketOrganizeSweepRobot = 34,
    SupermarketOrganizeHome = 35, SupermarketOrganizeStamina = 36,
    SupermarketOrganizeResult = 37,
}
```

`UiOpenPayload` retains `CoinsHubOpenPayload CoinsHub` and `SupermarketOrganizeResultViewModel SupermarketOrganizeResult`. `UiCoordinator` retains panel registration/open/close plus routes for Settings, CoinsHub, and the five supermarket panels. `Open` plays `UiPanelOpen` except for the result panel.

- [ ] **Step 5: Reduce HUD, player and economy support**

In `GameHudUguiPanel`, retain settings, coins, gems, coin/gem add buttons, and `SupermarketOrganizeHudSection`. Remove fields and methods for tower, fishing, worm, destroy city, animal hospital, pets, wings, trails, rods, character, map, sign-in, lucky wheel, online reward, treadmill and glide. Retain economy refresh from `SaveService.Data` and `UiRouter.OpenSettings/OpenCoinsHub`.

In `PlayerMotor`, retain ordinary movement plus `SetSupermarketMoveSpeedBonus`; remove climb/glide/fishing/worm/destroy-city mode branches. For every retained source reported by:

```powershell
rg -l 'Game\.(Tower|Fishing|DestroyCity|WormDodge|EscapeSnake|AnimalHospital)' Assets/Game/Scripts
```

remove the old-mode branch or delete the file if it has no supermarket/generic responsibility. Keep diamond exchange and generic economy display/coin-grant code; delete discount offers and mode reward resolver.

- [ ] **Step 6: Delete old runtime source consumers**

After verifying every resolved absolute target starts with `D:\code\roblox_gamecenter\`, delete these roots and sibling `.meta` files:

```text
Assets/Game/Scripts/AnimalHospital
Assets/Game/Scripts/DestroyCity
Assets/Game/Scripts/Fishing
Assets/Game/Scripts/Tower
Assets/Game/Scripts/WormDodge
Assets/Game/Scripts/EscapeSnake
Assets/Game/Scripts/Glide
Assets/Game/Scripts/Features/Treadmill
Assets/Game/Scripts/Features/StrengthTraining
Assets/Game/Scripts/DailySignIn
Assets/Game/Scripts/Collectibles
Assets/Game/Scripts/AI
Assets/Game/Scripts/Leaderboard
```

Delete old save files except `PlayerSaveData`, `SaveService`, `ModeSaveAccess`, and `SupermarketOrganizeModeSaveData`. Delete UI files/folders for MainHome, Fishing, WormDodge, AnimalHospital, cosmetics, rods, map selection, activities, treadmill, strength training, settlement and discount offers. Keep only files in the scene dependency closure plus Settings, CoinsHub, supermarket panels, and framework primitives.

- [ ] **Step 7: Remove obsolete navigation runtime and scenes**

Delete `Assets/Game/Scripts/Maps`, `Assets/Game/Core/Scenes/MainHome.unity`, `SceneTransition.unity`, and `Assets/Game/Core/Resources/SceneNavigationSettings.asset` with their `.meta` files. Set `ProjectSettings/EditorBuildSettings.asset` to the supermarket scene only:

```yaml
  m_Scenes:
  - enabled: 1
    path: Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity
    guid: e9a62e7d08da65949a7ecf7adee4a05a
```

- [ ] **Step 8: Compile the coordinated cut and fix only evidence-backed blockers**

Run the Task 1 compile command. For each compiler error, either remove the old-mode-only source file or remove the old-mode branch from a retained generic file. Do not restore deleted compatibility APIs. Repeat until Unity exits `0` and:

```powershell
rg -n 'Game\.(Tower|Fishing|DestroyCity|WormDodge|EscapeSnake|AnimalHospital)|MainHome|FishScene|DestroyCityScene|EscapeWormScene|AnimalHospitalScene' Assets/Game/Scripts --glob '*.cs'
```

returns no matches.

- [ ] **Step 9: Commit the compiling runtime cut**

```powershell
git add -A Assets/Game/Scripts Assets/Game/Core ProjectSettings/EditorBuildSettings.asset
git commit -m "refactor: reduce runtime to supermarket only"
```

## Task 5: Remove non-supermarket editor tooling

**Files:**
- Delete mode-specific and multi-product files under `Assets/Game/Editor`
- Retain supermarket tools, validator, Addressables utility added in Task 6, and generic utilities that compile without old types

- [ ] **Step 1: Delete mode-specific editor families**

Delete these exact basename families and `.meta` files:

```text
AnimalHospital*
DestroyCity*
FishDefBatchGenerator*
FishingLine*
WormDodge*
WormPathEditor*
MainHome*
GameProductModeMediaUtility*
GameProductSceneReferenceSyncUtility*
VirtualPlayerDatabaseGenerator*
EquipmentStatDebugOverlayCreator*
OnlineRewardDatabaseCreator*
SceneTransitionSceneBuilder*
```

- [ ] **Step 2: Delete multi-product generators/preprocessors**

Delete these files and `.meta` unless they have been rewritten to supermarket-only behavior with no banned token:

```text
GameAssetGenerator*
GameDebugWindow*
GameModeBootstrapWiringUtility*
GameModeContentLayout*
GameModeContentSyncUtility*
GameModeFolderLayoutUtility*
GameProductBuildAuditUtility*
GameProductBuildAuditWindow*
GameProductBuildReferenceRules*
GameProductBuildSceneReferenceProcessor*
GameProductFusionBuildTools*
GameProductPackagingUtility*
GameProductPackagingWindow*
GameProductSettingsEditor*
ReskinSettingsWindow*
UIPrefabGenerator*
```

- [ ] **Step 3: Compile and commit**

Expected: batch compile exits `0`; banned-token scan of `Assets/Game/Editor` returns matches only inside the validator's explicit token list.

```powershell
git add -A Assets/Game/Editor
git commit -m "chore: remove legacy mode editor tooling"
```

## Task 6: Delete old assets, clean Addressables, bundles and backups

**Files:**
- Create: `Assets/Game/Editor/SupermarketOnlyAddressablesUtility.cs`
- Delete: five old `Assets/Game/Modes` roots
- Delete: unused shared cosmetics/activity/MainHome assets
- Modify: Addressables settings through API
- Delete/rebuild: `ServerData/Android`, `ServerData/WebGL`
- Delete: `LBT-AdFrameWork_Backups`

- [ ] **Step 1: Add Addressables cleanup/build utility**

`CleanAndBuildBatch` must use:

```csharp
var settings = AddressableAssetSettingsDefaultObject.Settings;
var removed = new HashSet<string>
{
    "Remote_AnimalHospital", "Remote_DestroyCity", "Remote_Fishing",
    "Remote_Tower", "Remote_WormDodge",
};
foreach (var group in settings.groups.Where(x => x != null).ToArray())
    if (removed.Contains(group.Name))
        settings.RemoveGroup(group);
foreach (var label in new[] { "AnimalHospital", "DestroyCity", "Fishing", "Tower", "WormDodge" })
    if (settings.GetLabels().Contains(label))
        settings.RemoveLabel(label);
EditorUtility.SetDirty(settings);
AssetDatabase.SaveAssets();
AddressableAssetSettings.BuildPlayerContent(out var result);
if (!string.IsNullOrEmpty(result.Error))
    throw new BuildFailedException(result.Error);
```

Import `UnityEditor.AddressableAssets.Settings`, `UnityEditor.Build`, `System.Collections.Generic`, and `System.Linq`.

- [ ] **Step 2: Delete old mode asset roots safely**

Resolve and verify each path begins with the workspace root, then delete the directory and sibling `.meta`:

```text
Assets/Game/Modes/AnimalHospital
Assets/Game/Modes/DestroyCity
Assets/Game/Modes/Fishing
Assets/Game/Modes/Tower
Assets/Game/Modes/WormDodge
```

- [ ] **Step 3: Delete unused shared assets by protected dependency closure**

Build a protected set from `AssetDatabase.GetDependencies` for the supermarket scene, retained Resources assets, and retained Addressables entries. Delete a known root only if no protected path is equal to or below it. Candidate roots:

```text
Assets/Game/Shared/Cosmetics
Assets/Game/Core/Art/UI/Char
Assets/Game/Core/Art/UI/Pet
Assets/Game/Core/Art/UI/Trail
Assets/Game/Core/Art/UI/Wing
Assets/Game/Core/Art/UI/Prefabs/MainHomeGameModeItemView.prefab
```

Within `Assets/Game/Core/Art/UI/Mode`, retain supermarket `csfj1.png`, `csfj2.png`, CoinsHub diamond/economy art and protected dependencies; delete `dwyy*`, `dbcz*`, `chcs*`, and `dyds*` with `.meta`.

Delete FairyGUI Home package files only after `rg -n 'UIPackage\.AddPackage\("Home"|"Home"' Assets/Game/Scripts` returns no retained loader and they are absent from the protected set.

- [ ] **Step 4: Handle fishing-named VFX by GUID, not filename**

Check scene/dependency references to:

```text
Assets/Game/VFX/Prefabs/特效/diaoyutexiao/shuihua_xiao.prefab
Assets/Game/VFX/Prefabs/特效/diaoyutexiao/texiao4.prefab
Assets/Game/VFX/Prefabs/FishingWaterRipple.prefab
```

Keep a referenced prefab even though its name is legacy. Delete it and orphaned dependencies only when it is absent from the protected set.

- [ ] **Step 5: Delete generated output and backup directories with containment checks**

Verify these resolve inside `D:\code\roblox_gamecenter`, then delete them:

```text
ServerData/Android
ServerData/WebGL
LBT-AdFrameWork_Backups
```

Do not modify the separate untracked root `supermarket/` nested Git repository.

- [ ] **Step 6: Build retained Addressables and validate**

Run `SupermarketOnlyAddressablesUtility.CleanAndBuildBatch` after switching once to Android and once to WebGL. Then run `SupermarketOnlyProjectValidator.ValidateBatch`.

Expected:

- only `Remote_SupermarketOrganize`, `Main_Local`, `Built In Data`, and `Default Local Group` remain;
- no old-mode bundle filename exists under `ServerData`;
- Addressables builds return no error;
- validator reports no forbidden path/group/build-scene/missing-script/save errors.

- [ ] **Step 7: Commit**

```powershell
git add -A Assets/Game/Modes Assets/Game/Shared Assets/Game/Core/Art/UI Assets/Game/VFX Assets/AddressableAssetsData ServerData Assets/Game/Editor/SupermarketOnlyAddressablesUtility.cs Assets/Game/Editor/SupermarketOnlyAddressablesUtility.cs.meta
git commit -m "chore: remove non-supermarket assets and bundles"
```

`LBT-AdFrameWork_Backups` is ignored and untracked, so its physical deletion is verified on disk but is not part of the Git index.

## Task 7: Final verification and migration-tool removal

**Files:**
- Delete: `Assets/Game/Editor/SupermarketOnlySceneCleanupUtility.cs` and `.meta`
- Retain: `SupermarketOnlyProjectValidator.cs`
- Retain: `SupermarketOnlyAddressablesUtility.cs`

- [ ] **Step 1: Delete the one-time scene cleanup utility**

Remove the temporary utility and `.meta`; it must not remain in production editor tooling.

- [ ] **Step 2: Run static final checks**

```powershell
rg -n 'Game\.(Tower|Fishing|DestroyCity|WormDodge|EscapeSnake|AnimalHospital)|MainHome|FishScene|DestroyCityScene|EscapeWormScene|AnimalHospitalScene' Assets ProjectSettings --glob '!Assets/Game/Editor/SupermarketOnlyProjectValidator.cs'
rg -n 'remote_(animalhospital|destroycity|fishing|tower|wormdodge)' ServerData Assets/AddressableAssetsData
git diff --check
```

Expected: no matches and no whitespace errors.

- [ ] **Step 3: Run final Unity compile and validator**

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'D:\code\roblox_gamecenter' -logFile 'D:\code\roblox_gamecenter\Logs\supermarket-final-compile.log'
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'D:\code\roblox_gamecenter' -executeMethod Game.Editor.SupermarketOnlyProjectValidator.ValidateBatch -logFile 'D:\code\roblox_gamecenter\Logs\supermarket-final-validator.log'
```

Expected: both exit `0`; the validator logs `PASS`; no compiler, Missing Script, Missing Reference, Addressables or scene-load errors.

- [ ] **Step 4: Build Addressables and a development player**

Run the retained Addressables utility for the active target. Run the project's retained player-build entry or a minimal `BuildPipeline.BuildPlayer` method targeting `Builds/SupermarketOnly`. Expected: `BuildResult.Succeeded` and exactly one scene in the build report.

- [ ] **Step 5: Perform the manual runtime smoke test**

1. Play starts directly on the supermarket in-scene home.
2. Start “杂货店”; pick up and place an item.
3. Open settings; “返回主页” returns without scene loading.
4. Start “超级市场”; open the skills and sweep-robot panels.
5. Complete/trigger settlement; return to the in-scene home.
6. Restart Play Mode; coins, settings and supermarket progression persist.
7. Console has no Missing Script, Missing Reference, Addressables or scene-load errors.

- [ ] **Step 6: Commit final verification state**

```powershell
git add -A Assets ProjectSettings Packages
git commit -m "test: verify supermarket-only project"
git status --short
```

Expected: only the pre-existing untracked `supermarket/` nested repository may remain.

---

## Plan self-review result

- The no-asmdef compile coupling is handled explicitly: Task 4 is the only atomic source cut, and Unity is not run during its intentionally inconsistent intermediate steps.
- Enum values, supermarket panel IDs, supermarket scene GUID and the PlayerPrefs save key remain stable.
- Physical deletion is ordered after retained scene/prefab components are removed while their legacy types can still be identified.
- Every destructive root is preceded by a workspace-containment check; the unrelated nested `supermarket/` repository is excluded.
- The validator starts red, asserts old-save JSON compatibility, and becomes the final green acceptance gate.

# Settings UI Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the supermarket settings UI artwork and spacing without restoring any lucky-wheel UI or functionality.

**Architecture:** Extend the existing supermarket-only validator with settings-specific asset, binding, layout, and exclusion checks. Add one idempotent editor repair utility that recreates the retained `Main` sprite atlas from the surviving `UI/Main` folder, rebinds the settings prefab, and normalizes the scene settings button. Protect that dependency closure in the cleanup utility so a future cleanup cannot recreate the regression.

**Tech Stack:** Unity 2021.3, C#, UGUI, Unity SpriteAtlas, UnityEditor scene/prefab APIs, existing batch validator and Addressables build utilities.

---

## File Structure

- `Assets/Game/Editor/SupermarketOnlyProjectValidator.cs`: validates settings assets, prefab bindings, scene layout, missing scripts, and wheel exclusion.
- `Assets/Game/Editor/SupermarketSettingsUiRepairUtility.cs`: idempotently creates/repairs the settings atlas, scene button, and settings prefab bindings.
- `Assets/Game/Core/Art/UI/Main.spriteatlas`: packs only the surviving shared UI sprites in `Assets/Game/Core/Art/UI/Main`.
- `Assets/Game/Editor/SupermarketOnlyAddressablesUtility.cs`: seeds the settings atlas and prefab before computing the protected dependency closure.
- `Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity`: receives the normalized 48×48 settings button and explicit gear sprite reference.
- `Assets/Game/Core/Art/UI/Prefabs/Panel/SettingPanel.prefab`: receives refreshed settings artwork bindings.

### Task 1: Add a failing settings UI integrity check

**Files:**
- Modify: `Assets/Game/Editor/SupermarketOnlyProjectValidator.cs`

- [ ] **Step 1: Add settings and wheel constants**

Add the following imports and constants:

```csharp
using Game.UI;
using UnityEditor.U2D;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;

const string ScenePath = "Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity";
const string SettingsPrefabPath = "Assets/Game/Core/Art/UI/Prefabs/Panel/SettingPanel.prefab";
const string SettingsAtlasPath = "Assets/Game/Core/Art/UI/Main.spriteatlas";
const string SharedUiFolder = "Assets/Game/Core/Art/UI/Main";

static readonly string[] RequiredSettingsSprites =
{
    SharedUiFolder + "/shezhi.png",
    SharedUiFolder + "/shezhi1.png",
    SharedUiFolder + "/guanbi.png",
    SharedUiFolder + "/tanchuangkuang.png",
    SharedUiFolder + "/yinliangtiao.png",
    SharedUiFolder + "/yinliangtiao1.png",
    SharedUiFolder + "/yinliangjian.png",
    SharedUiFolder + "/yinxiao1.png",
    SharedUiFolder + "/yinxiao2.png",
    SharedUiFolder + "/yinyue1.png",
    SharedUiFolder + "/yinyue2.png",
};
```

Append these exact paths to `ForbiddenRoots`:

```csharp
"Assets/Game/Core/Art/UI/Main/lunpan.png",
"Assets/Game/Core/Art/UI/Prefabs/LuckyWheelSlot.prefab",
"Assets/Game/Core/Art/UI/Prefabs/Panel/LuckyWheelPanel.prefab",
"Assets/Game/Scripts/Features/LuckyWheel",
"Assets/Game/Scripts/UI/LuckyWheelPanel.cs",
"Assets/Game/Scripts/UI/WheelRewardSlotView.cs",
```

- [ ] **Step 2: Add scene, prefab, and atlas validation**

Call `ValidateSettingsUi(scene, errors);` after `CollectMissingScripts` and add:

```csharp
static void ValidateSettingsUi(Scene scene, ICollection<string> errors)
{
    foreach (var path in RequiredSettingsSprites)
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
            errors.Add("Required settings Sprite is missing or not imported as Sprite: " + path);

    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(SettingsAtlasPath);
    if (atlas == null)
        errors.Add("Settings UI atlas is missing: " + SettingsAtlasPath);
    else
    {
        var packsSharedFolder = SpriteAtlasExtensions.GetPackables(atlas)
            .Any(x => string.Equals(AssetDatabase.GetAssetPath(x), SharedUiFolder, StringComparison.Ordinal));
        if (!packsSharedFolder)
            errors.Add("Settings UI atlas must pack: " + SharedUiFolder);
    }

    var setting = FindScenePath(scene, "Canvas/Setting");
    if (setting == null)
    {
        errors.Add("Scene settings entry is missing: Canvas/Setting");
    }
    else
    {
        var image = setting.GetComponent<Image>();
        if (image == null || image.sprite == null)
            errors.Add("Canvas/Setting must render a non-null gear Sprite.");

        var rect = setting as RectTransform;
        if (rect == null || Vector2.Distance(rect.sizeDelta, new Vector2(48f, 48f)) > 0.01f)
            errors.Add("Canvas/Setting size must be 48x48.");
        if (rect == null || Vector2.Distance(rect.anchoredPosition, new Vector2(42f, -42f)) > 0.01f)
            errors.Add("Canvas/Setting must use an 18px top-left safe margin.");
    }

    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
    var panel = prefab != null ? prefab.GetComponentInChildren<SettingsPanel>(true) : null;
    if (panel == null)
    {
        errors.Add("SettingsPanel prefab/controller is missing: " + SettingsPrefabPath);
        return;
    }

    if (panel.mainPanel == null || panel.mainPanel.GetComponent<Image>()?.sprite == null)
        errors.Add("SettingsPanel main frame Sprite is missing.");
    if (panel.closeButton == null || (panel.closeButton.targetGraphic as Image)?.sprite == null)
        errors.Add("SettingsPanel close-button Sprite is missing.");
    if (panel.sfxSlider == null || panel.bgmSlider == null)
        errors.Add("SettingsPanel volume sliders are not bound.");
    if (panel.sfxIconNormal == null || panel.sfxIconMuted == null ||
        panel.bgmIconNormal == null || panel.bgmIconMuted == null)
        errors.Add("SettingsPanel sound/music icon states are incomplete.");
}

static Transform FindScenePath(Scene scene, string path)
{
    var split = path.IndexOf('/');
    var rootName = split >= 0 ? path.Substring(0, split) : path;
    var childPath = split >= 0 ? path.Substring(split + 1) : string.Empty;
    var root = scene.GetRootGameObjects().FirstOrDefault(x => x.name == rootName);
    return root == null ? null : childPath.Length == 0 ? root.transform : root.transform.Find(childPath);
}
```

- [ ] **Step 3: Run the validator and verify RED**

Run:

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace' `
  -executeMethod Game.Editor.SupermarketOnlyProjectValidator.ValidateBatch `
  -logFile 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace\Temp\settings-ui-red.log'
```

Expected: Unity exits with validation failure containing both:

```text
Settings UI atlas is missing
Canvas/Setting size must be 48x48
```

- [ ] **Step 4: Commit the failing validation**

```powershell
git add Assets/Game/Editor/SupermarketOnlyProjectValidator.cs
git commit -m "test: detect broken settings UI"
```

### Task 2: Repair the settings atlas, scene entry, and prefab bindings

**Files:**
- Create: `Assets/Game/Editor/SupermarketSettingsUiRepairUtility.cs`
- Create: `Assets/Game/Editor/SupermarketSettingsUiRepairUtility.cs.meta` (Unity-generated)
- Create: `Assets/Game/Core/Art/UI/Main.spriteatlas`
- Create: `Assets/Game/Core/Art/UI/Main.spriteatlas.meta` (Unity-generated)
- Modify: `Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity`
- Modify: `Assets/Game/Core/Art/UI/Prefabs/Panel/SettingPanel.prefab`

- [ ] **Step 1: Create the idempotent editor repair utility**

Create `SupermarketSettingsUiRepairUtility` with the following public entry and helpers:

```csharp
#if UNITY_EDITOR
using System;
using System.Linq;
using Game.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class SupermarketSettingsUiRepairUtility
    {
        const string ScenePath = "Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity";
        const string PrefabPath = "Assets/Game/Core/Art/UI/Prefabs/Panel/SettingPanel.prefab";
        const string AtlasPath = "Assets/Game/Core/Art/UI/Main.spriteatlas";
        const string UiFolder = "Assets/Game/Core/Art/UI/Main";

        [MenuItem("Game/Supermarket Organize/Repair Settings UI")]
        public static void RepairMenu() => RepairBatch();

        public static void RepairBatch()
        {
            RepairAtlas();
            RepairSceneButton();
            RepairSettingsPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SupermarketSettingsUiRepairUtility] PASS");
        }

        static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new BuildFailedException("Required settings UI asset is missing: " + path);
            return asset;
        }

        static Sprite Sprite(string file) => LoadRequired<Sprite>(UiFolder + "/" + file);

        static void RepairAtlas()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, AtlasPath);
            }

            var current = SpriteAtlasExtensions.GetPackables(atlas);
            if (current.Length > 0)
                SpriteAtlasExtensions.Remove(atlas, current);
            SpriteAtlasExtensions.Add(atlas, new[] { LoadRequired<UnityEngine.Object>(UiFolder) });
            SpriteAtlasExtensions.SetIncludeInBuild(atlas, true);
            SpriteAtlasExtensions.SetPackingSettings(atlas, new SpriteAtlasPackingSettings
            {
                padding = 4,
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = true,
            });
            SpriteAtlasExtensions.SetTextureSettings(atlas, new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
            });
            EditorUtility.SetDirty(atlas);
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, BuildTarget.StandaloneWindows64);
        }

        static void RepairSceneButton()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "Canvas");
            var setting = canvas != null ? canvas.transform.Find("Setting") as RectTransform : null;
            if (setting == null)
                throw new BuildFailedException("Canvas/Setting was not found.");

            setting.anchorMin = setting.anchorMax = new Vector2(0f, 1f);
            setting.pivot = new Vector2(0.5f, 0.5f);
            setting.sizeDelta = new Vector2(48f, 48f);
            setting.anchoredPosition = new Vector2(42f, -42f);
            var image = setting.GetComponent<Image>();
            if (image == null)
                throw new BuildFailedException("Canvas/Setting has no Image component.");
            image.sprite = Sprite("shezhi.png");
            image.preserveAspect = true;
            EditorUtility.SetDirty(setting);
            EditorUtility.SetDirty(image);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void RepairSettingsPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel = root.GetComponentInChildren<SettingsPanel>(true);
                if (panel == null)
                    throw new BuildFailedException("SettingPanel prefab has no SettingsPanel controller.");

                panel.sfxIconNormal = Sprite("yinxiao1.png");
                panel.sfxIconMuted = Sprite("yinxiao2.png");
                panel.bgmIconNormal = Sprite("yinyue1.png");
                panel.bgmIconMuted = Sprite("yinyue2.png");
                if (panel.sfxIcon != null) panel.sfxIcon.sprite = panel.sfxIconNormal;
                if (panel.bgmIcon != null) panel.bgmIcon.sprite = panel.bgmIconNormal;

                var frame = panel.mainPanel != null ? panel.mainPanel.GetComponent<Image>() : null;
                if (frame != null) frame.sprite = Sprite("tanchuangkuang.png");
                if (panel.closeButton?.targetGraphic is Image close)
                    close.sprite = Sprite("guanbi.png");

                RepairSlider(panel.sfxSlider);
                RepairSlider(panel.bgmSlider);
                EditorUtility.SetDirty(panel);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void RepairSlider(Slider slider)
        {
            if (slider == null)
                throw new BuildFailedException("SettingsPanel slider binding is missing.");
            var background = slider.transform.Find("Background")?.GetComponent<Image>();
            var fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            var handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            if (background == null || fill == null || handle == null)
                throw new BuildFailedException("SettingsPanel slider artwork bindings are incomplete.");
            background.sprite = Sprite("yinliangtiao.png");
            fill.sprite = Sprite("yinliangtiao1.png");
            handle.sprite = Sprite("yinliangjian.png");
        }
    }
}
#endif
```

- [ ] **Step 2: Run the repair utility**

Run:

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace' `
  -executeMethod Game.Editor.SupermarketSettingsUiRepairUtility.RepairBatch `
  -logFile 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace\Temp\settings-ui-repair.log'
```

Expected log:

```text
[SupermarketSettingsUiRepairUtility] PASS
Exiting batchmode successfully now!
```

- [ ] **Step 3: Re-run the validator and verify GREEN**

Run:

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace' `
  -executeMethod Game.Editor.SupermarketOnlyProjectValidator.ValidateBatch `
  -logFile 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace\Temp\settings-ui-green.log'
```

Expected:

```text
[SupermarketOnlyProjectValidator] PASS
```

- [ ] **Step 4: Commit the repair**

```powershell
git add Assets/Game/Editor/SupermarketSettingsUiRepairUtility.cs `
  Assets/Game/Editor/SupermarketSettingsUiRepairUtility.cs.meta `
  Assets/Game/Core/Art/UI/Main.spriteatlas `
  Assets/Game/Core/Art/UI/Main.spriteatlas.meta `
  Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity `
  Assets/Game/Core/Art/UI/Prefabs/Panel/SettingPanel.prefab
git commit -m "fix: restore supermarket settings UI"
```

### Task 3: Protect settings UI during future cleanup

**Files:**
- Modify: `Assets/Game/Editor/SupermarketOnlyAddressablesUtility.cs`

- [ ] **Step 1: Add protected settings seeds**

Immediately after the existing core config seeds in `BuildProtectedAssetSet`, add:

```csharp
seeds.Add("Assets/Game/Core/Art/UI/Main.spriteatlas");
seeds.Add("Assets/Game/Core/Art/UI/Prefabs/Panel/SettingPanel.prefab");
```

This keeps the atlas, prefab, and all dependencies returned by `AssetDatabase.GetDependencies`.

- [ ] **Step 2: Run cleanup and verify the settings assets survive**

Run:

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace' `
  -executeMethod Game.Editor.SupermarketOnlyAddressablesUtility.CleanLegacyAssetsBatch `
  -logFile 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace\Temp\settings-ui-cleanup.log'
```

Then run:

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace' `
  -executeMethod Game.Editor.SupermarketOnlyProjectValidator.ValidateBatch `
  -logFile 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace\Temp\settings-ui-post-cleanup.log'
```

Expected: cleanup exits successfully and the second command reports `[SupermarketOnlyProjectValidator] PASS`.

- [ ] **Step 3: Commit cleanup protection**

```powershell
git add Assets/Game/Editor/SupermarketOnlyAddressablesUtility.cs
git commit -m "fix: protect retained settings UI assets"
```

### Task 4: Final compile, content, player, and visual verification

**Files:**
- Verify only; commit Unity-generated tracked changes if any are necessary and directly related.

- [ ] **Step 1: Compile and run the final validator**

Run:

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace' `
  -logFile 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace\Temp\settings-ui-final-compile.log'

& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace' `
  -executeMethod Game.Editor.SupermarketOnlyProjectValidator.ValidateBatch `
  -logFile 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace\Temp\settings-ui-final-validator.log'
```

Expected: no `error CS`, no missing-script report, and `[SupermarketOnlyProjectValidator] PASS`.

- [ ] **Step 2: Rebuild Addressables**

Run:

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace' `
  -executeMethod Game.Editor.SupermarketOnlyAddressablesUtility.CleanAndBuildBatch `
  -logFile 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace\Temp\settings-ui-final-addressables.log'
```

Expected:

```text
Addressable content successfully built
```

- [ ] **Step 3: Build the Windows development Player**

Run:

```powershell
& 'D:\unity\2021.3.11f1c2\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace' `
  -executeMethod Game.Editor.SupermarketOnlyAddressablesUtility.BuildDevelopmentPlayerBatch `
  -logFile 'C:\Users\QAQ\.codex\worktrees\supermarket-only\roblox_gamecenter\workspace\Temp\settings-ui-final-player.log'
```

Expected:

```text
Player build succeeded: Builds/SupermarketOnly/SupermarketOnly.exe; scenes=1
```

- [ ] **Step 4: Perform a visible UI smoke test**

Open the Player at the reference 16:9 aspect ratio and capture these states:

1. supermarket home;
2. difficulty popup;
3. gameplay HUD;
4. settings panel.

Confirm: no white placeholder blocks, the blue gear appears at the 18px safe margin, sliders and close icon render, progress HUD does not overlap, and no wheel icon or wheel panel appears.

- [ ] **Step 5: Check repository integrity and commit final generated changes**

Run:

```powershell
git diff --check
git status --short
rg -n "LuckyWheel|WheelDatabase|LuckyWheelPanel|lunpan\.png" Assets ProjectSettings
```

Expected: `git diff --check` has no output; the wheel scan has no runtime/scene/asset matches other than validator exclusion strings; the worktree is clean after committing any directly related Unity serialization changes.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class SupermarketOnlyProjectValidator
    {
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
            "Assets/Game/Core/Art/UI/Main/lunpan.png",
            "Assets/Game/Core/Art/UI/Prefabs/LuckyWheelSlot.prefab",
            "Assets/Game/Core/Art/UI/Prefabs/Panel/LuckyWheelPanel.prefab",
            "Assets/Game/Scripts/Features/LuckyWheel",
            "Assets/Game/Scripts/UI/LuckyWheelPanel.cs",
            "Assets/Game/Scripts/UI/WheelRewardSlotView.cs",
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
            {
                if (AssetDatabase.IsValidFolder(root) || File.Exists(root))
                    errors.Add("Forbidden path still exists: " + root);
            }

            foreach (var path in Directory.GetFiles("Assets/Game", "*.cs", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.EndsWith(nameof(SupermarketOnlyProjectValidator) + ".cs", StringComparison.Ordinal))
                    continue;
                var source = File.ReadAllText(path);
                foreach (var token in ForbiddenTokens)
                {
                    if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                        errors.Add($"Forbidden token '{token}': {normalized}");
                }
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

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                CollectMissingScripts(root, errors);

            ValidateSettingsUi(scene, errors);
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

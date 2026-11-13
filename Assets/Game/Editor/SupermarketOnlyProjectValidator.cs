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

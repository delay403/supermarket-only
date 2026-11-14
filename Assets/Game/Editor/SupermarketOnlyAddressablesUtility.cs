#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FairyGUI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class SupermarketOnlyAddressablesUtility
    {
        const string ScenePath = "Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity";

        public static void CleanLegacyAssetsBatch()
        {
            RemoveLegacyFguiComponents();
            RemoveLegacySceneObjects();

            var protectedAssets = BuildProtectedAssetSet();
            foreach (var root in new[]
                     {
                         "Assets/Game/Shared/Cosmetics",
                         "Assets/Game/Shared/Economy",
                         "Assets/Game/Shared",
                         "Assets/Game/Core/Art",
                         "Assets/Game/Core/Audio",
                         "Assets/Game/Core/Prefabs",
                         "Assets/Game/Core/Resources",
                         "Assets/Game/Prefabs",
                         "Assets/Game/VFX",
                         "Assets/Game/Core/Resources/Activity",
                         "Assets/Game/Core/Art/UI/Char",
                         "Assets/Game/Core/Art/UI/Pet",
                         "Assets/Game/Core/Art/UI/Trail",
                         "Assets/Game/Core/Art/UI/Wing",
                         "Assets/Game/Core/Art/UI/Prefabs",
                         "Assets/Game/Core/Art/UI/Mode",
                         "Assets/Game/Core/Scenes",
                         "Assets/Game/UI/FGUI",
                     })
                DeleteUnprotectedAssets(root, protectedAssets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void CleanAndBuildBatch()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new BuildFailedException("Addressables settings were not found.");

            var removed = new HashSet<string>
            {
                "Remote_AnimalHospital",
                "Remote_DestroyCity",
                "Remote_Fishing",
                "Remote_Tower",
                "Remote_WormDodge",
            };
            foreach (var group in settings.groups.Where(x => x != null).ToArray())
                if (removed.Contains(group.Name))
                    settings.RemoveGroup(group);

            foreach (var label in new[]
                     {
                         "AnimalHospital", "DestroyCity", "Fishing", "Tower", "WormDodge",
                     })
                if (settings.GetLabels().Contains(label))
                    settings.RemoveLabel(label);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error))
                throw new BuildFailedException(result.Error);
        }

        static void RemoveLegacyFguiComponents()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var panel in root.GetComponentsInChildren<UIPanel>(true))
                {
                    UnityEngine.Object.DestroyImmediate(panel, true);
                    removed++;
                }

                foreach (var scaler in root.GetComponentsInChildren<UIContentScaler>(true))
                {
                    UnityEngine.Object.DestroyImmediate(scaler, true);
                    removed++;
                }

                foreach (var stageCamera in root.GetComponentsInChildren<StageCamera>(true))
                {
                    UnityEngine.Object.DestroyImmediate(stageCamera, true);
                    removed++;
                }
            }

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[SupermarketOnlyAddressablesUtility] Removed {removed} legacy FGUI components.");
        }

        static void RemoveLegacySceneObjects()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var removed = 0;
            foreach (var path in new[]
                     {
                         "Canvas/LuckyWheelPanel",
                         "Canvas/SignInPanel",
                         "AdCanvas/FreeCurrencyOfferPanel - Gem",
                     })
            {
                var slash = path.IndexOf('/');
                var rootName = slash >= 0 ? path.Substring(0, slash) : path;
                var childPath = slash >= 0 ? path.Substring(slash + 1) : string.Empty;
                var root = scene.GetRootGameObjects().FirstOrDefault(x => x.name == rootName);
                var target = root != null && childPath.Length > 0 ? root.transform.Find(childPath) : root?.transform;
                if (target == null)
                    continue;
                UnityEngine.Object.DestroyImmediate(target.gameObject, true);
                removed++;
            }

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[SupermarketOnlyAddressablesUtility] Removed {removed} legacy scene objects.");
        }

        static HashSet<string> BuildProtectedAssetSet()
        {
            var seeds = new HashSet<string>(StringComparer.Ordinal);
            seeds.Add(ScenePath);
            seeds.Add("Assets/Game/Core/Resources/GameBalanceConfig.asset");
            seeds.Add("Assets/Game/Core/Resources/PlayerJumpConfig.asset");

            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { "Assets/Game/Modes/SupermarketOrganize" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                    seeds.Add(path);
            }

            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { "Assets/Game/Core/Resources/URP" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                    seeds.Add(path);
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                var allowed = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Built In Data", "Default Local Group", "Main_Local", "Remote_SupermarketOrganize",
                };
                foreach (var group in settings.groups.Where(x => x != null && allowed.Contains(x.Name)))
                    foreach (var entry in group.entries)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                        if (!string.IsNullOrEmpty(path))
                            seeds.Add(path);
                    }
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var seed in seeds)
                foreach (var dependency in AssetDatabase.GetDependencies(seed, true))
                    result.Add(dependency.Replace('\\', '/'));
            return result;
        }

        static void DeleteUnprotectedAssets(string root, ISet<string> protectedAssets)
        {
            if (!AssetDatabase.IsValidFolder(root))
                return;

            var deleted = 0;
            var paths = AssetDatabase.FindAssets(string.Empty, new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                .Distinct()
                .ToArray();
            foreach (var path in paths)
            {
                var normalized = path.Replace('\\', '/');
                if (protectedAssets.Contains(normalized))
                    continue;
                if (AssetDatabase.DeleteAsset(normalized))
                    deleted++;
            }

            DeleteEmptyFolders(root);
            Debug.Log($"[SupermarketOnlyAddressablesUtility] Deleted {deleted} unprotected assets below {root}.");
        }

        static void DeleteEmptyFolders(string root)
        {
            if (!AssetDatabase.IsValidFolder(root))
                return;
            var folders = AssetDatabase.FindAssets("t:DefaultAsset", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(AssetDatabase.IsValidFolder)
                .OrderByDescending(path => path.Count(c => c == '/'))
                .ToList();
            folders.Add(root);
            foreach (var folder in folders.Distinct())
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;
                var absolute = Path.GetFullPath(folder);
                if (Directory.EnumerateFileSystemEntries(absolute).Any(x => !x.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)))
                    continue;
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
#endif

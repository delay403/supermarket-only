#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Core.Content;
using Game.Maps;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 根据 GameProductSettings 同步 Build Settings，自动包含启用的主页模式场景。
    /// </summary>
    public static class GameProductFusionBuildTools
    {
        const string ProductSettingsPath = GameContentPaths.ProductSettingsAssetPath;

        static bool s_syncingBuildScenes;

        static readonly Dictionary<string, string> CanonicalScenePaths = new Dictionary<string, string>
        {
            ["SceneTransition"] = GameContentPaths.SceneTransitionScenePath,
            ["MainHome"] = GameContentPaths.MainHomeScenePath,
            ["FishScene"] = "Assets/Game/Modes/Fishing/Scenes/FishScene.unity",
            ["DestroyCityScene"] = "Assets/Game/Modes/DestroyCity/Scenes/DestroyCityScene.unity",
            ["EscapeWormScene"] = "Assets/Game/Modes/WormDodge/Scenes/EscapeWormScene.unity",
            ["SupermarketOrganizeScene"] = "Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity",
            ["AnimalHospitalScene"] = "Assets/Game/Modes/AnimalHospital/Scenes/AnimalHospitalScene.unity",
            ["Map1"] = "Assets/Game/Modes/Tower/Scenes/Map1.unity",
            ["Map2"] = "Assets/Game/Modes/Tower/Scenes/Map2.unity",
            ["Map3"] = "Assets/Game/Modes/Tower/Scenes/Map3.unity",
        };

        static readonly HashSet<string> BlockedLegacyScenePaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Assets/jiechongzi/jiechongzi.unity",
        };

        [MenuItem("Game/Product/Open Product Settings")]
        public static void OpenProductSettings()
        {
            var product = LoadProductSettingsAsset();
            if (product != null)
                Selection.activeObject = product;
            else
                EditorUtility.DisplayDialog("产品配置", "未找到 Game/Core/Resources/GameProductSettings.asset。", "确定");
        }

        [MenuItem("Game/Product/Sync Build Scenes From Product Settings")]
        public static void SyncBuildScenesFromProductSettingsMenu()
        {
            var product = LoadProductSettingsAsset();
            if (product == null)
            {
                EditorUtility.DisplayDialog("同步 Build Scenes", "未找到 Game/Core/Resources/GameProductSettings.asset。", "确定");
                return;
            }

            SyncBuildScenesFromProductSettings(product, silent: false);
        }

        public static void SyncBuildScenesFromProductSettings(GameProductSettings settings, bool silent)
        {
            SyncBuildScenesFromProductSettings(settings, silent, force: false, buildTarget: null);
        }

        public static void SyncBuildScenesFromProductSettings(
            GameProductSettings settings,
            bool silent,
            bool force,
            BuildTarget? buildTarget = null)
        {
            if (settings == null)
                return;

            if (!force && !settings.autoSyncBuildScenes)
                return;

            if (s_syncingBuildScenes)
                return;

            s_syncingBuildScenes = true;
            try
            {
                var resolvedTarget = buildTarget ?? EditorUserBuildSettings.activeBuildTarget;

                var nav = SceneNavigationSettings.TryLoadFromResources();
                var requiredNames = settings.CollectRequiredGameplaySceneNames(nav, includeRemoteGameplayScenes: true);

                var enabledPaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < requiredNames.Count; i++)
                {
                    var path = ResolveCanonicalScenePath(requiredNames[i]);
                    if (!string.IsNullOrEmpty(path))
                        enabledPaths.Add(NormalizePath(path));
                }

                var buildScenes = enabledPaths
                    .OrderBy(ResolveBuildOrder)
                    .ThenBy(p => p)
                    .Select(p => new EditorBuildSettingsScene(p, true))
                    .ToArray();

                if (BuildScenesAlreadyMatch(buildScenes))
                    return;

                EditorBuildSettings.scenes = buildScenes;

                if (!silent)
                {
                    var availableModes = settings.BuildAvailableModes();
                    var modeSummary = availableModes.Count == 0
                        ? "(无)"
                        : string.Join(", ", availableModes.ConvertAll(m => m.displayName));

                    Debug.Log(
                        $"[GameProduct] Build Settings 已同步。BuildTarget = {resolvedTarget}，启用场景 {buildScenes.Length} 个。\n" +
                        "（本地单包体：玩法场景已写入 Build Settings，运行时 SceneManager 本地加载。）\n" +
                        string.Join("\n", enabledPaths.OrderBy(ResolveBuildOrder).ThenBy(p => p)) +
                        $"\n当前可用模式：{modeSummary}" +
                        $"\n当前模块：{settings.ComputeModulesFromModes()}");
                }
            }
            finally
            {
                s_syncingBuildScenes = false;
            }
        }

        static bool BuildScenesAlreadyMatch(EditorBuildSettingsScene[] desired)
        {
            var current = EditorBuildSettings.scenes;
            if (current == null || current.Length != desired.Length)
                return false;

            for (var i = 0; i < desired.Length; i++)
            {
                if (!current[i].enabled)
                    return false;
                if (NormalizePath(current[i].path) != NormalizePath(desired[i].path))
                    return false;
            }

            return true;
        }

        public static void EnsureSceneInBuildSettings(string sceneAssetPath)
        {
            if (string.IsNullOrWhiteSpace(sceneAssetPath))
                return;

            sceneAssetPath = NormalizePath(sceneAssetPath);
            if (BlockedLegacyScenePaths.Contains(sceneAssetPath))
            {
                Debug.LogWarning($"[GameProduct] 忽略 legacy 场景，不会加入 Build Settings: {sceneAssetPath}");
                return;
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            for (var i = 0; i < scenes.Count; i++)
            {
                if (NormalizePath(scenes[i].path) == sceneAssetPath)
                {
                    if (!scenes[i].enabled)
                    {
                        scenes[i] = new EditorBuildSettingsScene(sceneAssetPath, true);
                        EditorBuildSettings.scenes = scenes.ToArray();
                    }

                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(sceneAssetPath, true));
            EditorBuildSettings.scenes = scenes
                .OrderBy(s => ResolveBuildOrder(s.path))
                .ThenBy(s => s.path)
                .ToArray();
        }

        public static GameProductSettings LoadProductSettingsAsset()
        {
            var product = AssetDatabase.LoadAssetAtPath<GameProductSettings>(ProductSettingsPath);
            return product != null ? product : GameProductSettings.TryLoadFromResources();
        }

        static string ResolveCanonicalScenePath(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return null;

            sceneName = sceneName.Trim();

            if (sceneName.Equals("jiechongzi", System.StringComparison.OrdinalIgnoreCase))
                sceneName = "EscapeWormScene";

            if (CanonicalScenePaths.TryGetValue(sceneName, out var canonical)
                && File.Exists(canonical))
                return NormalizePath(canonical);

            var guids = AssetDatabase.FindAssets($"{sceneName} t:Scene", new[] { "Assets" });
            string best = null;
            var bestScore = int.MinValue;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = NormalizePath(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (string.IsNullOrEmpty(path) || BlockedLegacyScenePaths.Contains(path))
                    continue;

                if (Path.GetFileNameWithoutExtension(path) != sceneName)
                    continue;

                var score = ScoreScenePath(path);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = path;
                }
            }

            return best;
        }

        static int ScoreScenePath(string path)
        {
            path = NormalizePath(path);
            if (path.StartsWith(GameContentPaths.CoreScenesRoot + "/", System.StringComparison.OrdinalIgnoreCase))
                return 120;
            if (path.StartsWith(GameContentPaths.ModesRoot + "/", System.StringComparison.OrdinalIgnoreCase)
                && path.Contains("/Scenes/"))
                return 100;
            if (path.StartsWith("Assets/Scenes/", System.StringComparison.OrdinalIgnoreCase))
                return 10;
            if (path.Contains("/Scenes/"))
                return 80;
            return 0;
        }

        static string NormalizePath(string path) => path?.Replace('\\', '/').Trim();

        /// <summary>
        /// Build Settings 排序决定启动场景（index 0）：
        /// 有主页时 MainHome 第一；无主页（useMainHomeScene=false）时玩法场景第一。
        /// SceneTransition 必须排在玩法场景之后——它只作为中转/过场，单独启动时没有待加载场景。
        /// </summary>
        static int ResolveBuildOrder(string path)
        {
            path = NormalizePath(path);
            if (path.EndsWith("MainHome.unity")) return 0;
            if (path.EndsWith("SceneTransition.unity")) return 20;
            return 10;
        }
    }

    /// <summary>
    /// 打包前按 GameProductSettings 自动同步 Build Settings。
    /// </summary>
    public sealed class GameProductBuildScenePreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = GameProductFusionBuildTools.LoadProductSettingsAsset();
            if (settings == null)
                return;

            GameProductFusionBuildTools.SyncBuildScenesFromProductSettings(
                settings,
                silent: false,
                force: true,
                buildTarget: report.summary.platform);
        }
    }
}
#endif

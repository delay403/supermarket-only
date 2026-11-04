using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Core;
using Game.Core.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 打包前审计：以 Build Settings 场景引用链为主，检查跨模式资源泄漏。
    /// </summary>
    public static class GameProductBuildAuditUtility
    {
        [MenuItem("Game/Content/Preview Build Content Audit")]
        public static void PreviewBuildAuditMenu()
        {
            var settings = GameProductFusionBuildTools.LoadProductSettingsAsset();
            if (settings == null)
            {
                EditorUtility.DisplayDialog("打包审计", "未找到 GameProductSettings.asset。", "确定");
                return;
            }

            var report = BuildAuditReport(settings);
            Debug.Log(report);
            GameProductBuildAuditWindow.Show(settings);
        }

        public static string BuildAuditReport(GameProductSettings settings)
        {
            var issues = CollectIssues(settings);
            var sb = new StringBuilder();
            sb.AppendLine("=== Game Build Content Audit ===");
            sb.AppendLine($"启用模块：{GameProductSettings.ResolveEnabledModules(settings)}");
            sb.AppendLine($"Build Scenes：{CollectEnabledBuildScenes().Count}");
            sb.AppendLine();

            if (issues.Count == 0)
            {
                sb.AppendLine("未发现跨模式引用链泄漏。");
                return sb.ToString();
            }

            AppendIssueGroup(sb, "模式 Resources 槽位", issues, issue => issue.Contains("Resources 槽位"));
            AppendIssueGroup(sb, "FGUI Resources", issues, issue => issue.StartsWith("FGUI ", StringComparison.Ordinal));
            AppendIssueGroup(sb, "Build 场景引用链", issues, issue => issue.StartsWith("Build 场景", StringComparison.Ordinal));
            AppendIssueGroup(sb, "配置缺失", issues, issue => issue.Contains("缺少配置资产"));
            AppendIssueGroup(sb, "Legacy Resources", issues, issue => issue.StartsWith("Legacy ", StringComparison.Ordinal));
            AppendIssueGroup(sb, "其他", issues, issue =>
                !issue.Contains("Resources 槽位")
                && !issue.StartsWith("FGUI ", StringComparison.Ordinal)
                && !issue.StartsWith("Build 场景", StringComparison.Ordinal)
                && !issue.Contains("缺少配置资产")
                && !issue.StartsWith("Legacy ", StringComparison.Ordinal));

            sb.AppendLine();
            sb.AppendLine($"合计 {issues.Count} 项问题。");
            sb.AppendLine();
            sb.AppendLine("建议：");
            sb.AppendLine("1) Game/Content/Auto-Wire Mode Config References");
            sb.AppendLine("2) Game/Product/Sync Scene References To Product Settings");
            sb.AppendLine("3) Game/Content/Sync Core Content Layout");
            return sb.ToString();
        }

        /// <summary>
        /// 打包前写入审计警告（不中断构建）。无问题时静默。
        /// </summary>
        public static void LogBuildAuditWarnings(GameProductSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning("[Game Build Content Audit] 未找到 GameProductSettings，跳过资源引用审计。");
                return;
            }

            var issues = CollectIssues(settings);
            if (issues.Count == 0)
                return;

            var report = BuildAuditReport(settings);
            Debug.LogWarning(
                $"[Game Build Content Audit] 发现 {issues.Count} 项资源引用问题，构建将继续。请打包人核对下方报告。\n{report}");
        }

        static void AppendIssueGroup(
            StringBuilder sb,
            string title,
            IReadOnlyList<string> issues,
            Func<string, bool> predicate)
        {
            var count = 0;
            for (var i = 0; i < issues.Count; i++)
            {
                if (!predicate(issues[i]))
                    continue;

                if (count == 0)
                {
                    sb.AppendLine($"[{title}]");
                }

                sb.AppendLine($"  - {issues[i]}");
                count++;
            }

            if (count > 0)
                sb.AppendLine();
        }

        public static IReadOnlyList<string> CollectIssues(GameProductSettings settings)
        {
            var issues = new List<string>();
            CollectLegacyResourcesIssues(issues);
            CollectModeResourcesSlotIssues(issues);
            CollectUnexpectedFguiIssues(settings, issues);
            CollectBuildSceneDependencyIssues(settings, issues);
            CollectBootstrapWiringIssues(settings, issues);
            return issues;
        }

        static void CollectBuildSceneDependencyIssues(GameProductSettings settings, List<string> issues)
        {
            var enabledModules = GameProductSettings.ResolveEnabledModules(settings);
            var scenePaths = CollectEnabledBuildScenes();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < scenePaths.Count; i++)
            {
                var scenePath = scenePaths[i];
                var deps = AssetDatabase.GetDependencies(scenePath, true);
                for (var d = 0; d < deps.Length; d++)
                {
                    var dep = deps[d].Replace('\\', '/');
                    if (!seen.Add(dep))
                        continue;

                    if (dep.EndsWith(".gitkeep", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var owner = TryResolveModuleOwner(dep);
                    if (owner == GameplayModule.None)
                        continue;

                    if ((enabledModules & owner) == owner)
                        continue;

                    issues.Add($"Build 场景引用了未启用模块资源：{scenePath} → {dep}（{owner}）");
                }
            }
        }

        static void CollectBootstrapWiringIssues(GameProductSettings settings, List<string> issues)
        {
            if (!GameProductSettings.HasModule(GameplayModule.SupermarketOrganize))
                return;

            var required = new[]
            {
                $"{GameContentPaths.SupermarketConfigRoot}/SupermarketOrganizeConfig.asset",
                GameContentPaths.SupermarketItemDatabaseAssetPath,
                $"{GameContentPaths.SupermarketConfigRoot}/SupermarketMapDatabase.asset",
            };

            for (var i = 0; i < required.Length; i++)
            {
                if (!File.Exists(required[i]))
                    issues.Add($"超市模式缺少配置资产：{required[i]}");
            }
        }

        static void CollectModeResourcesSlotIssues(List<string> issues)
        {
            foreach (var module in GameContentPaths.EnumerateGameplayModules())
            {
                var resourcesRoot = GameContentPaths.ModeResourcesRoot(module);
                if (!Directory.Exists(resourcesRoot))
                    continue;

                foreach (var entry in Directory.GetFileSystemEntries(resourcesRoot))
                {
                    var name = Path.GetFileName(entry);
                    if (string.IsNullOrEmpty(name)
                        || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                        || name.Equals(".gitkeep", StringComparison.OrdinalIgnoreCase))
                        continue;

                    issues.Add($"模式配置不应位于 Resources 槽位：{resourcesRoot}/{name}（应迁入 Content/Configs）");
                }
            }
        }

        static bool IsSharedCoreAsset(string assetPath)
        {
            return assetPath.IndexOf("/Game/Core/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/Game/Scripts/Core/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/Game/Scripts/Maps/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static GameplayModule TryResolveModuleOwner(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return GameplayModule.None;

            assetPath = assetPath.Replace('\\', '/');
            if (IsSharedCoreAsset(assetPath))
                return GameplayModule.None;

            for (var bit = 1; bit <= (int)GameplayModule.AnimalHospital; bit <<= 1)
            {
                var module = (GameplayModule)bit;
                var folder = GameContentPaths.ModuleFolderName(module);
                if (assetPath.IndexOf($"/Modes/{folder}/", StringComparison.OrdinalIgnoreCase) >= 0)
                    return module;
            }

            if (assetPath.IndexOf("/Fish/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/鱼", StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.Fishing;
            if (assetPath.IndexOf("/DestroyCity/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/City/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/cuihichengshi/", StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.DestroyCity;
            if (assetPath.IndexOf("/EscapeWorm/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/jiechongzi/", StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.WormDodge;
            if (assetPath.IndexOf("/Supermarket/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/chaoshi/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/超市", StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.SupermarketOrganize;
            if (assetPath.IndexOf("/AnimalHospital/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/动物医院", StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.AnimalHospital;
            if (assetPath.IndexOf("/pata/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/TreadZone/", StringComparison.OrdinalIgnoreCase) >= 0
                || assetPath.IndexOf("/Game/Scripts/Tower/", StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.Tower;

            return GameplayModule.None;
        }

        static void CollectLegacyResourcesIssues(List<string> issues)
        {
            if (!Directory.Exists(GameContentPaths.LegacyResourcesRoot))
                return;

            foreach (var entry in Directory.GetFileSystemEntries(GameContentPaths.LegacyResourcesRoot))
            {
                var name = Path.GetFileName(entry);
                if (string.IsNullOrEmpty(name) || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                issues.Add($"Legacy Resources 仍存在：{GameContentPaths.LegacyResourcesRoot}/{name}");
            }
        }

        static void CollectUnexpectedFguiIssues(GameProductSettings settings, List<string> issues)
        {
            if (!Directory.Exists(GameContentPaths.FguiResourcesRoot))
                return;

            var modules = GameProductSettings.ResolveEnabledModules(settings);
            var keepHome = !GameProductBuildReferenceRules.ShouldStripFguiHomePackage(settings);
            var keepGame = GameProductBuildReferenceRules.NeedsFguiGameHud(modules);

            foreach (var file in Directory.GetFiles(GameContentPaths.FguiResourcesRoot, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                var relative = file.Replace('\\', '/');
                var index = relative.IndexOf("/FGUIPublish/", StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    continue;

                var packageRelative = relative.Substring(index + 1);
                if (packageRelative.StartsWith("FGUIPublish/ui/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ShouldKeepFgui(packageRelative, keepHome, keepGame))
                    continue;

                issues.Add($"FGUI 包不应位于 Resources：{relative}");
            }
        }

        static bool ShouldKeepFgui(string packageRelative, bool keepHome, bool keepGame)
        {
            if (packageRelative.StartsWith("FGUIPublish/fgui/Loading", StringComparison.OrdinalIgnoreCase))
                return true;

            if (keepHome && (
                    packageRelative.StartsWith("FGUIPublish/fgui/Home", StringComparison.OrdinalIgnoreCase)
                    || packageRelative.StartsWith("FGUIPublish/fgui/bg1", StringComparison.OrdinalIgnoreCase)
                    || packageRelative.StartsWith("FGUIPublish/fgui/title1", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (keepGame && packageRelative.StartsWith("FGUIPublish/fgui/Game", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        static List<string> CollectEnabledBuildScenes()
        {
            var result = new List<string>();
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled && !string.IsNullOrEmpty(scenes[i].path))
                    result.Add(scenes[i].path);
            }

            return result;
        }
    }

    /// <summary>
    /// 打包前写入资源引用审计警告；仅日志提醒，不中断构建。
    /// </summary>
    public sealed class GameProductBuildAuditPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 20;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = GameProductFusionBuildTools.LoadProductSettingsAsset();
            GameProductBuildAuditUtility.LogBuildAuditWarnings(settings);
        }
    }
}

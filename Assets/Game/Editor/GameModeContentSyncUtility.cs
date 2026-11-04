#if UNITY_EDITOR
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
    /// 仅维护 Core/Resources 与 FGUI 布局；模式配置固定在 Content/Configs，不再做 Content↔Resources 同步。
    /// </summary>
    public static class GameModeContentSyncUtility
    {
        [MenuItem("Game/Content/Sync Core Content Layout")]
        public static void SyncCoreContentLayoutMenu()
        {
            var report = SyncCoreContentLayout(log: true);
            EditorUtility.DisplayDialog("Core 内容布局", report, "确定");
        }

        [MenuItem("Game/Content/Normalize Mode Configs To Content/Configs")]
        public static void NormalizeModeConfigsMenu()
        {
            var report = NormalizeModeConfigsToContentConfigs(log: true);
            EditorUtility.DisplayDialog("模式配置归位", report, "确定");
        }

        [MenuItem("Game/Content/Migrate Legacy Resources To Mode Layout")]
        public static void MigrateLegacyResourcesMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "迁移 Legacy Resources",
                    "将把遗留 Resources 与模式 Resources 槽位中的配置迁入 Content/Configs。\n是否继续？",
                    "继续",
                    "取消"))
                return;

            var report = MigrateLegacyResources(log: true);
            NormalizeModeConfigsToContentConfigs(log: true);
            EditorUtility.DisplayDialog("迁移完成", report, "确定");
        }

        [Obsolete("改用 SyncCoreContentLayout；模式配置不再在 Content/Resources 间搬运。")]
        public static string SyncContentLayout(GameProductSettings settings, bool log) =>
            SyncCoreContentLayout(log);

        public static string SyncCoreContentLayout(bool log)
        {
            var moved = 0;
            var sb = new StringBuilder();
            sb.AppendLine("=== Core Content Layout Sync ===");
            sb.AppendLine();

            EnsureFolder(GameContentPaths.CoreResourcesRoot);
            moved += SyncCoreEntries(sb, log);
            moved += SyncFguiPackages(sb, log);

            AssetDatabase.Refresh();
            sb.AppendLine($"共同步 {moved} 项。");
            if (log)
                Debug.Log(sb.ToString());

            return sb.ToString();
        }

        public static string NormalizeModeConfigsToContentConfigs(bool log)
        {
            var moved = 0;
            var sb = new StringBuilder();
            sb.AppendLine("=== Normalize Mode Configs → Content/Configs ===");

            for (var i = 0; i < GameModeContentLayout.Modules.Count; i++)
            {
                var spec = GameModeContentLayout.Modules[i];
                var targetRoot = GameModeContentLayout.ModeConfigRoot(spec.module);
                EnsureFolder(targetRoot);
                moved += NormalizeModuleEntries(spec, targetRoot, sb, log);
            }

            for (var i = 0; i < GameModeContentLayout.SharedFeatures.Count; i++)
            {
                var spec = GameModeContentLayout.SharedFeatures[i];
                var targetRoot = GameModeContentLayout.SharedConfigRoot(spec.feature);
                EnsureFolder(targetRoot);
                moved += SyncEntries(spec.entries, targetRoot, sb, log, spec.feature.ToString());
            }

            AssetDatabase.Refresh();
            sb.AppendLine($"共归位 {moved} 项。");
            if (log)
                Debug.Log(sb.ToString());

            return sb.ToString();
        }

        public static string MigrateLegacyResources(bool log)
        {
            var moved = 0;
            var sb = new StringBuilder();
            sb.AppendLine("=== Legacy Resources Migration ===");

            EnsureFolder(GameContentPaths.CoreResourcesRoot);
            moved += MoveEntriesFromLegacy(GameModeContentLayout.CoreEntries, GameContentPaths.CoreResourcesRoot, sb, log, "Core");

            for (var i = 0; i < GameModeContentLayout.Modules.Count; i++)
            {
                var spec = GameModeContentLayout.Modules[i];
                var targetRoot = GameModeContentLayout.ModeConfigRoot(spec.module);
                EnsureFolder(targetRoot);
                moved += MoveEntriesFromLegacy(spec.entries, targetRoot, sb, log, spec.module.ToString());
            }

            for (var i = 0; i < GameModeContentLayout.SharedFeatures.Count; i++)
            {
                var spec = GameModeContentLayout.SharedFeatures[i];
                var targetRoot = GameModeContentLayout.SharedConfigRoot(spec.feature);
                EnsureFolder(targetRoot);
                moved += MoveEntriesFromLegacy(spec.entries, targetRoot, sb, log, spec.feature.ToString());
            }

            moved += MigrateLegacyFgui(sb, log);
            AssetDatabase.Refresh();
            sb.AppendLine($"共迁移 {moved} 项。");
            if (log)
                Debug.Log(sb.ToString());

            return sb.ToString();
        }

        static int NormalizeModuleEntries(
            GameModeContentLayout.ModuleSpec spec,
            string targetRoot,
            StringBuilder sb,
            bool log)
        {
            var moved = SyncEntries(spec.entries, targetRoot, sb, log, spec.module.ToString());

            var legacyRoots = new[]
            {
                GameContentPaths.ModeResourcesRoot(spec.module),
                GameContentPaths.ModeContentRoot(spec.module),
            };

            for (var i = 0; i < legacyRoots.Length; i++)
            {
                var legacyRoot = legacyRoots[i];
                if (!Directory.Exists(legacyRoot))
                    continue;

                foreach (var entry in Directory.GetFileSystemEntries(legacyRoot))
                {
                    var name = Path.GetFileName(entry);
                    if (string.IsNullOrEmpty(name)
                        || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                        || name.Equals(".gitkeep", StringComparison.OrdinalIgnoreCase)
                        || name.Equals(GameContentPaths.ModeSlotConfigs, StringComparison.OrdinalIgnoreCase))
                        continue;

                    moved += MoveIfExists(
                        entry.Replace('\\', '/'),
                        $"{targetRoot}/{name}",
                        sb,
                        log,
                        spec.module.ToString());
                }
            }

            return moved;
        }

        static int SyncCoreEntries(StringBuilder sb, bool log)
        {
            var moved = 0;
            for (var i = 0; i < GameModeContentLayout.CoreEntries.Length; i++)
            {
                var entry = GameModeContentLayout.CoreEntries[i];
                moved += MoveIfExists(
                    FindCoreExistingPath(entry),
                    $"{GameContentPaths.CoreResourcesRoot}/{entry.legacyResourcesRelativePath}",
                    sb,
                    log,
                    "Core");
            }

            return moved;
        }

        static int SyncEntries(
            IReadOnlyList<GameModeContentLayout.ContentEntry> entries,
            string targetRoot,
            StringBuilder sb,
            bool log,
            string label)
        {
            var moved = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var source = FindConfigExistingPath(entries[i], targetRoot);
                var target = $"{targetRoot}/{entry.legacyResourcesRelativePath}";
                moved += MoveIfExists(source, target, sb, log, label);
            }

            return moved;
        }

        static int MoveEntriesFromLegacy(
            IReadOnlyList<GameModeContentLayout.ContentEntry> entries,
            string targetRoot,
            StringBuilder sb,
            bool log,
            string label)
        {
            var moved = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var legacy = $"{GameContentPaths.LegacyResourcesRoot}/{entry.legacyResourcesRelativePath}";
                var target = $"{targetRoot}/{entry.legacyResourcesRelativePath}";
                moved += MoveIfExists(legacy, target, sb, log, label);
            }

            return moved;
        }

        static string FindCoreExistingPath(GameModeContentLayout.ContentEntry entry)
        {
            var candidates = new[]
            {
                $"{GameContentPaths.LegacyResourcesRoot}/{entry.legacyResourcesRelativePath}",
                $"{GameContentPaths.CoreResourcesRoot}/{entry.legacyResourcesRelativePath}",
            };

            for (var i = 0; i < candidates.Length; i++)
            {
                if (AssetExists(candidates[i]))
                    return candidates[i];
            }

            return null;
        }

        static string FindConfigExistingPath(GameModeContentLayout.ContentEntry entry, string targetRoot)
        {
            var candidates = new List<string>
            {
                $"{targetRoot}/{entry.legacyResourcesRelativePath}",
                $"{GameContentPaths.LegacyResourcesRoot}/{entry.legacyResourcesRelativePath}",
            };

            for (var i = 0; i < GameModeContentLayout.Modules.Count; i++)
            {
                var module = GameModeContentLayout.Modules[i].module;
                candidates.Add($"{GameContentPaths.ModeConfigRoot(module)}/{entry.legacyResourcesRelativePath}");
                candidates.Add($"{GameContentPaths.ModeResourcesRoot(module)}/{entry.legacyResourcesRelativePath}");
                candidates.Add($"{GameContentPaths.ModeContentRoot(module)}/{entry.legacyResourcesRelativePath}");
            }

            for (var i = 0; i < GameModeContentLayout.SharedFeatures.Count; i++)
            {
                var feature = GameModeContentLayout.SharedFeatures[i].feature;
                candidates.Add($"{GameModeContentLayout.SharedConfigRoot(feature)}/{entry.legacyResourcesRelativePath}");
                candidates.Add($"{GameContentPaths.SharedFeatureContentRoot(feature)}/{entry.legacyResourcesRelativePath}");
                candidates.Add($"{GameContentPaths.SharedFeatureResourcesRoot(feature)}/{entry.legacyResourcesRelativePath}");
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (AssetExists(candidates[i]))
                    return candidates[i];
            }

            return null;
        }

        static int MoveIfExists(string source, string target, StringBuilder sb, bool log, string label)
        {
            if (string.IsNullOrEmpty(source) || !AssetExists(source))
                return 0;

            if (string.Equals(Normalize(source), Normalize(target), StringComparison.OrdinalIgnoreCase))
                return 0;

            EnsureParentFolder(target);
            var error = AssetDatabase.MoveAsset(source, target);
            if (!string.IsNullOrEmpty(error))
            {
                var message = $"[{label}] 移动失败：{source} → {target} ({error})";
                sb.AppendLine(message);
                if (log)
                    Debug.LogWarning(message);
                return 0;
            }

            sb.AppendLine($"[{label}] {source} → {target}");
            return 1;
        }

        static int SyncFguiPackages(StringBuilder sb, bool log)
        {
            var settings = GameProductFusionBuildTools.LoadProductSettingsAsset();
            if (settings == null)
                return 0;

            var moved = 0;
            var modules = GameProductSettings.ResolveEnabledModules(settings);
            var keepHome = !GameProductBuildReferenceRules.ShouldStripFguiHomePackage(settings);
            var keepGame = GameProductBuildReferenceRules.NeedsFguiGameHud(modules);

            EnsureFolder(GameContentPaths.FguiResourcesRoot);
            EnsureFolder(GameContentPaths.FguiContentRoot);

            moved += SyncFguiPrefix("FGUIPublish/fgui/Loading", true, sb, log);
            moved += SyncFguiPrefix("FGUIPublish/fgui/Home", keepHome, sb, log);
            moved += SyncFguiPrefix("FGUIPublish/fgui/bg1", keepHome, sb, log);
            moved += SyncFguiPrefix("FGUIPublish/fgui/title1", keepHome, sb, log);
            moved += SyncFguiPrefix("FGUIPublish/fgui/Game", keepGame, sb, log);
            moved += SyncFguiPrefix("FGUIPublish/fgui/Setting", false, sb, log);
            return moved;
        }

        static int MigrateLegacyFgui(StringBuilder sb, bool log)
        {
            if (!Directory.Exists(GameContentPaths.LegacyFguiResourcesRoot))
                return 0;

            EnsureFolder(GameContentPaths.FguiResourcesRoot);
            var moved = 0;
            foreach (var file in Directory.GetFiles(GameContentPaths.LegacyFguiResourcesRoot, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                var relative = NormalizeRelative(GameContentPaths.LegacyFguiResourcesRoot, file);
                var target = $"{GameContentPaths.FguiResourcesRoot}/{relative}";
                moved += MoveIfExists(file.Replace('\\', '/'), target, sb, log, "FGUI");
            }

            return moved;
        }

        static int SyncFguiPrefix(string relativePrefix, bool keepInResources, StringBuilder sb, bool log)
        {
            var moved = 0;
            var searchRoots = new[]
            {
                GameContentPaths.LegacyFguiResourcesRoot,
                GameContentPaths.FguiResourcesRoot,
                GameContentPaths.FguiContentRoot,
            };

            for (var r = 0; r < searchRoots.Length; r++)
            {
                var root = searchRoots[r];
                if (!Directory.Exists(root))
                    continue;

                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var relative = NormalizeRelative(root, file);
                    if (!relative.StartsWith(relativePrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var source = file.Replace('\\', '/');
                    var targetRoot = keepInResources
                        ? GameContentPaths.FguiResourcesRoot
                        : GameContentPaths.FguiContentRoot;
                    var target = $"{targetRoot}/{relative}";
                    moved += MoveIfExists(source, target, sb, log, keepInResources ? "FGUI/Resources" : "FGUI/Content");
                }
            }

            return moved;
        }

        static string NormalizeRelative(string root, string absolutePath)
        {
            var normalizedRoot = root.Replace('\\', '/').TrimEnd('/');
            var normalizedAbsolute = absolutePath.Replace('\\', '/');
            return normalizedAbsolute.Substring(normalizedRoot.Length + 1);
        }

        static bool AssetExists(string assetPath) =>
            !string.IsNullOrEmpty(assetPath)
            && (File.Exists(assetPath) || Directory.Exists(assetPath))
            && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath));

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var name = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        static void EnsureParentFolder(string assetPath)
        {
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
        }

        static string Normalize(string path) => path?.Replace('\\', '/');

    }

    public sealed class GameModeContentBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 5;

        public void OnPreprocessBuild(BuildReport report)
        {
            GameModeFolderLayoutUtility.EnsureStandardModeFolderLayout(log: false);
            GameModeContentSyncUtility.NormalizeModeConfigsToContentConfigs(log: false);
            GameModeContentSyncUtility.SyncCoreContentLayout(log: false);
            GameModeBootstrapWiringUtility.AutoWireEnabledModeConfigs(log: true);

            var settings = GameProductFusionBuildTools.LoadProductSettingsAsset();
            GameProductModeMediaUtility.PrepareForBuild(settings, log: true);
        }
    }
}
#endif

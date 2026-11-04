using System;
using System.Collections.Generic;
using System.IO;
using Game.Core.Content;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    [Serializable]
    public sealed class FguiTitleBgReplaceHistoryEntry
    {
        public string id;
        public string displayName;
        public string createdAtUtc;
        public string targetFolderAssetPath;
        public bool includesTitle;
        public bool includesBg;
        public string sceneNavSettingsAssetPath;
        public bool includesSceneNavTitle;
        public bool includesSceneNavBg;
        public string sceneNavTitleSpriteRef;
        public string sceneNavBgSpriteRef;
    }

    [Serializable]
    sealed class FguiTitleBgReplaceHistoryFile
    {
        public FguiTitleBgReplaceHistoryEntry[] entries = Array.Empty<FguiTitleBgReplaceHistoryEntry>();
    }

    [Serializable]
    sealed class FguiTitleBgReplaceSettingsFile
    {
        public string targetFolderAssetPath;
        public string sceneNavigationSettingsAssetPath;
    }

    /// <summary>
    /// 将替换历史与备份 PNG 存于 <c>ProjectSettings/FguiTitleBgReplace</c>，不受域重载、运行与构建影响。
    /// </summary>
    public static class FguiTitleBgReplaceHistoryStore
    {
        const string TitleFileName = "title.png";
        const string BgFileName = "bg.png";
        const string HistoryFileName = "history.json";
        const string SettingsFileName = "settings.json";
        const string PreferredFguiFolder = "Assets/Game/UI/FGUI/Resources/FGUIPublish/fgui";
        const string PreferredSceneNavSettingsPath = GameContentPaths.SceneNavigationSettingsAssetPath;
        const string FguiFolderName = "fgui";

        static string StorageRoot =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "FguiTitleBgReplace");

        static string HistoryJsonPath => Path.Combine(StorageRoot, HistoryFileName);
        static string SettingsJsonPath => Path.Combine(StorageRoot, SettingsFileName);

        static string BackupFolder(string entryId) => Path.Combine(StorageRoot, "backups", entryId);

        public static IReadOnlyList<FguiTitleBgReplaceHistoryEntry> LoadEntries()
        {
            EnsureStorageRoot();
            if (!File.Exists(HistoryJsonPath))
                return Array.Empty<FguiTitleBgReplaceHistoryEntry>();

            try
            {
                var json = File.ReadAllText(HistoryJsonPath);
                var data = JsonUtility.FromJson<FguiTitleBgReplaceHistoryFile>(json);
                return data?.entries ?? Array.Empty<FguiTitleBgReplaceHistoryEntry>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TitleBgReplace] 读取历史记录失败：{e.Message}");
                return Array.Empty<FguiTitleBgReplaceHistoryEntry>();
            }
        }

        public static void SaveEntries(IList<FguiTitleBgReplaceHistoryEntry> entries)
        {
            EnsureStorageRoot();
            var data = new FguiTitleBgReplaceHistoryFile
            {
                entries = entries == null ? Array.Empty<FguiTitleBgReplaceHistoryEntry>() : ToArray(entries)
            };
            File.WriteAllText(HistoryJsonPath, JsonUtility.ToJson(data, true));
        }

        public static FguiTitleBgReplaceHistoryEntry AddEntry(
            string displayName,
            string targetFolderAssetPath,
            bool replaceTitle,
            string titleSourceFullPath,
            bool replaceBg,
            string bgSourceFullPath,
            string sceneNavSettingsAssetPath,
            bool syncSceneNavTitle,
            bool syncSceneNavBg)
        {
            EnsureStorageRoot();

            var entry = new FguiTitleBgReplaceHistoryEntry
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = string.IsNullOrWhiteSpace(displayName)
                    ? DefaultEntryName()
                    : displayName.Trim(),
                createdAtUtc = DateTime.UtcNow.ToString("o"),
                targetFolderAssetPath = NormalizeAssetPath(targetFolderAssetPath),
                includesTitle = replaceTitle,
                includesBg = replaceBg,
                sceneNavSettingsAssetPath = NormalizeAssetPath(sceneNavSettingsAssetPath),
                includesSceneNavTitle = syncSceneNavTitle,
                includesSceneNavBg = syncSceneNavBg
            };

            if (replaceTitle || replaceBg)
            {
                var backupDir = BackupFolder(entry.id);
                Directory.CreateDirectory(backupDir);

                if (replaceTitle)
                    File.Copy(titleSourceFullPath, Path.Combine(backupDir, TitleFileName), true);

                if (replaceBg)
                    File.Copy(bgSourceFullPath, Path.Combine(backupDir, BgFileName), true);
            }

            var list = new List<FguiTitleBgReplaceHistoryEntry>(LoadEntries()) { entry };
            SaveEntries(list);
            return entry;
        }

        /// <summary>
        /// 写入应用换皮后的 Sprite 引用快照（与 FGUI 备份语义一致：还原到该次换皮完成时的状态）。
        /// </summary>
        public static void UpdateEntrySceneNavSpriteRefs(
            string entryId,
            string appliedTitleSpriteRef,
            string appliedBgSpriteRef)
        {
            var list = new List<FguiTitleBgReplaceHistoryEntry>(LoadEntries());
            var index = list.FindIndex(e => e.id == entryId);
            if (index < 0)
                return;

            if (list[index].includesSceneNavTitle)
                list[index].sceneNavTitleSpriteRef = appliedTitleSpriteRef;

            if (list[index].includesSceneNavBg)
                list[index].sceneNavBgSpriteRef = appliedBgSpriteRef;

            SaveEntries(list);
        }

        public static void RenameEntry(string entryId, string newDisplayName)
        {
            if (string.IsNullOrWhiteSpace(newDisplayName))
                return;

            var list = new List<FguiTitleBgReplaceHistoryEntry>(LoadEntries());
            var index = list.FindIndex(e => e.id == entryId);
            if (index < 0)
                return;

            list[index].displayName = newDisplayName.Trim();
            SaveEntries(list);
        }

        public static void DeleteEntry(string entryId)
        {
            var list = new List<FguiTitleBgReplaceHistoryEntry>(LoadEntries());
            var removed = list.RemoveAll(e => e.id == entryId);
            if (removed == 0)
                return;

            SaveEntries(list);

            var backupDir = BackupFolder(entryId);
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, true);
        }

        public static bool TryRestore(FguiTitleBgReplaceHistoryEntry entry, out string error)
        {
            error = null;
            if (entry == null)
            {
                error = "无效的历史记录。";
                return false;
            }

            var hasFileRestore = entry.includesTitle || entry.includesBg;
            var hasSceneNavRestore = entry.includesSceneNavTitle || entry.includesSceneNavBg;
            if (!hasFileRestore && !hasSceneNavRestore)
            {
                error = "该历史记录没有可还原的内容。";
                return false;
            }

            if (hasFileRestore)
            {
                var backupDir = BackupFolder(entry.id);
                if (!Directory.Exists(backupDir))
                {
                    error = "找不到该记录的备份文件，可能已被删除。";
                    return false;
                }

                var folder = entry.targetFolderAssetPath;
                if (string.IsNullOrEmpty(folder))
                {
                    error = "历史记录缺少目标目录。";
                    return false;
                }

                try
                {
                    if (entry.includesTitle)
                    {
                        var src = Path.Combine(backupDir, TitleFileName);
                        if (!File.Exists(src))
                        {
                            error = "缺少 title.png 备份。";
                            return false;
                        }

                        var dest = AssetPathToFullPath($"{folder}/{TitleFileName}");
                        if (dest == null)
                        {
                            error = "无法解析 title.png 目标路径。";
                            return false;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        File.Copy(src, dest, true);
                    }

                    if (entry.includesBg)
                    {
                        var src = Path.Combine(backupDir, BgFileName);
                        if (!File.Exists(src))
                        {
                            error = "缺少 bg.png 备份。";
                            return false;
                        }

                        var dest = AssetPathToFullPath($"{folder}/{BgFileName}");
                        if (dest == null)
                        {
                            error = "无法解析 bg.png 目标路径。";
                            return false;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        File.Copy(src, dest, true);
                    }
                }
                catch (Exception e)
                {
                    error = e.Message;
                    return false;
                }
            }

            if (hasSceneNavRestore)
            {
                if (!TryRestoreSceneNavigationSettings(entry, out error))
                    return false;
            }

            return true;
        }

        public static bool TryRestoreSceneNavigationSettings(FguiTitleBgReplaceHistoryEntry entry, out string error)
        {
            error = null;
            var settingsPath = entry.sceneNavSettingsAssetPath;
            if (string.IsNullOrEmpty(settingsPath))
            {
                error = "历史记录缺少 SceneNavigationSettings 路径。";
                return false;
            }

            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(settingsPath);
            if (settings == null)
            {
                error = $"找不到 SceneNavigationSettings：{settingsPath}";
                return false;
            }

            var serialized = new SerializedObject(settings);
            var titleProp = serialized.FindProperty("homeTitleSprite");
            var bgProp = serialized.FindProperty("homeBackgroundSprite");
            if (titleProp == null || bgProp == null)
            {
                error = "SceneNavigationSettings 缺少标题/背景 Sprite 字段。";
                return false;
            }

            if (entry.includesSceneNavTitle)
                titleProp.objectReferenceValue = ReskinSpriteUtility.LoadSpriteRef(entry.sceneNavTitleSpriteRef);

            if (entry.includesSceneNavBg)
                bgProp.objectReferenceValue = ReskinSpriteUtility.LoadSpriteRef(entry.sceneNavBgSpriteRef);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static string FormatLocalTime(string createdAtUtc)
        {
            if (string.IsNullOrEmpty(createdAtUtc))
                return "-";

            if (!DateTime.TryParse(createdAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc))
                return createdAtUtc;

            if (utc.Kind == DateTimeKind.Unspecified)
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static string DefaultEntryName() =>
            $"替换 {DateTime.Now:yyyy-MM-dd HH:mm}";

        public static string LoadTargetFolderAssetPath()
        {
            return NormalizeAssetPath(LoadSettingsFile().targetFolderAssetPath);
        }

        public static void SaveTargetFolderAssetPath(string assetPath)
        {
            var settings = LoadSettingsFile();
            settings.targetFolderAssetPath = NormalizeAssetPath(assetPath);
            SaveSettingsFile(settings);
        }

        public static string LoadSceneNavigationSettingsAssetPath()
        {
            return NormalizeAssetPath(LoadSettingsFile().sceneNavigationSettingsAssetPath);
        }

        public static void SaveSceneNavigationSettingsAssetPath(string assetPath)
        {
            var settings = LoadSettingsFile();
            settings.sceneNavigationSettingsAssetPath = NormalizeAssetPath(assetPath);
            SaveSettingsFile(settings);
        }

        public static string ResolveSceneNavigationSettingsAssetPath()
        {
            var saved = LoadSceneNavigationSettingsAssetPath();
            if (IsValidSettingsAssetPath(saved))
                return saved;

            if (IsValidSettingsAssetPath(PreferredSceneNavSettingsPath))
            {
                SaveSceneNavigationSettingsAssetPath(PreferredSceneNavSettingsPath);
                return PreferredSceneNavSettingsPath;
            }

            var guids = AssetDatabase.FindAssets("SceneNavigationSettings t:SceneNavigationSettings");
            foreach (var guid in guids)
            {
                var path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (IsValidSettingsAssetPath(path))
                {
                    SaveSceneNavigationSettingsAssetPath(path);
                    return path;
                }
            }

            return saved;
        }

        static bool IsValidSettingsAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            return AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) != null;
        }

        static FguiTitleBgReplaceSettingsFile LoadSettingsFile()
        {
            EnsureStorageRoot();
            if (!File.Exists(SettingsJsonPath))
                return new FguiTitleBgReplaceSettingsFile();

            try
            {
                var json = File.ReadAllText(SettingsJsonPath);
                return JsonUtility.FromJson<FguiTitleBgReplaceSettingsFile>(json)
                    ?? new FguiTitleBgReplaceSettingsFile();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReskinSettings] 读取设置失败：{e.Message}");
                return new FguiTitleBgReplaceSettingsFile();
            }
        }

        static void SaveSettingsFile(FguiTitleBgReplaceSettingsFile settings)
        {
            EnsureStorageRoot();
            File.WriteAllText(SettingsJsonPath, JsonUtility.ToJson(settings ?? new FguiTitleBgReplaceSettingsFile(), true));
        }

        /// <summary>
        /// 优先使用已保存目录；无效时自动搜索工程内的 fgui 文件夹并写回设置。
        /// </summary>
        public static string ResolveTargetFolderAssetPath()
        {
            var saved = LoadTargetFolderAssetPath();
            if (IsValidFolderAssetPath(saved))
                return saved;

            var found = FindDefaultFguiFolderAssetPath();
            if (!string.IsNullOrEmpty(found))
            {
                SaveTargetFolderAssetPath(found);
                return found;
            }

            return saved;
        }

        public static string FindDefaultFguiFolderAssetPath()
        {
            if (IsValidFolderAssetPath(PreferredFguiFolder))
                return PreferredFguiFolder;

            var candidates = new List<string>();
            try
            {
                foreach (var dir in Directory.GetDirectories(Application.dataPath, FguiFolderName, SearchOption.AllDirectories))
                {
                    var relative = "Assets" + dir.Substring(Application.dataPath.Length).Replace('\\', '/');
                    if (IsValidFolderAssetPath(relative))
                        candidates.Add(relative);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TitleBgReplace] 搜索 fgui 目录失败：{e.Message}");
            }

            if (candidates.Count == 0)
                return null;

            candidates.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                if (candidate.IndexOf("FGUIPublish", StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
            }

            return candidates[0];
        }

        public static DefaultAsset LoadFolderAsset(string assetPath)
        {
            if (!IsValidFolderAssetPath(assetPath))
                return null;

            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetPath);
            if (folder != null)
                return folder;

            folder = AssetDatabase.LoadMainAssetAtPath(assetPath) as DefaultAsset;
            if (folder != null)
                return folder;

            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) as DefaultAsset;
        }

        public static bool IsValidFolderAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            var normalized = NormalizeAssetPath(assetPath).TrimEnd('/');
            return normalized.StartsWith("Assets/", StringComparison.Ordinal)
                && AssetDatabase.IsValidFolder(normalized);
        }

        static void EnsureStorageRoot()
        {
            if (!Directory.Exists(StorageRoot))
                Directory.CreateDirectory(StorageRoot);
        }

        static FguiTitleBgReplaceHistoryEntry[] ToArray(IList<FguiTitleBgReplaceHistoryEntry> entries)
        {
            var array = new FguiTitleBgReplaceHistoryEntry[entries.Count];
            for (var i = 0; i < entries.Count; i++)
                array[i] = entries[i];
            return array;
        }

        static string NormalizeAssetPath(string assetPath) =>
            assetPath?.Replace('\\', '/');

        static string AssetPathToFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return null;
            var relative = assetPath.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}

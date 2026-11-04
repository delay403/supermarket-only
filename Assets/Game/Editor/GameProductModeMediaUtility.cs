#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Core.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// 打包前临时清除未启用模式的媒体引用，避免单包体拖入其它玩法资源；构建结束后自动还原。
    /// </summary>
    public static class GameProductModeMediaUtility
    {
        const string ProductSettingsPath = GameContentPaths.ProductSettingsAssetPath;
        const string MainHomeScenePath = Game.Core.Content.GameContentPaths.MainHomeScenePath;
        const string BackupFilePath = "Library/GameProductModeMediaBackup.json";

        sealed class ModeMediaBackup
        {
            public int modeIndex;
            public Sprite previewSprite;
            public Sprite mainModeSprite;
            public GameObject previewPrefab;
            public Sprite transitionLoadingSprite;
        }

        [Serializable]
        sealed class ModeMediaBackupDto
        {
            public int modeIndex;
            public string previewSpriteGuid;
            public string mainModeSpriteGuid;
            public string previewPrefabGuid;
            public string transitionLoadingSpriteGuid;
        }

        [Serializable]
        sealed class ModeMediaBackupFile
        {
            public ModeMediaBackupDto[] entries = Array.Empty<ModeMediaBackupDto>();
        }

        static readonly List<ModeMediaBackup> s_mediaBackup = new List<ModeMediaBackup>();

        [MenuItem("Game/MainHome/Sync Product Mode Media References")]
        public static void SyncProductModeMediaReferencesMenu()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameProductSettings>(ProductSettingsPath);
            if (settings == null)
            {
                EditorUtility.DisplayDialog("MainHome", "未找到 GameProductSettings.asset。", "确定");
                return;
            }

            var cleared = settings.ClearDisabledModeMediaReferences();
            ClearMainHomeScenePlaceholderPreviewSprite();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MainHome] 已同步产品模式媒体引用，清除 {cleared} 个未启用模式资源引用。");
        }

        [MenuItem("Game/MainHome/Restore Stripped Mode Media References")]
        public static void RestoreStrippedModeMediaMenu()
        {
            RestoreAfterBuild(log: true);
        }

        public static bool HasPendingBackup() =>
            s_mediaBackup.Count > 0 || File.Exists(BackupFilePath);

        public static void PrepareForBuild(GameProductSettings settings, bool log)
        {
            if (settings == null)
                return;

            RestoreAfterBuild(log: false);
            BackupAndStripDisabledModeMedia(settings, log);
        }

        public static void RestoreAfterBuild(bool log)
        {
            EnsureBackupLoadedFromDisk();
            if (s_mediaBackup.Count == 0)
                return;

            var settings = AssetDatabase.LoadAssetAtPath<GameProductSettings>(ProductSettingsPath);
            if (settings == null || settings.modes == null)
            {
                s_mediaBackup.Clear();
                DeleteBackupFile();
                return;
            }

            var restored = 0;
            for (var i = 0; i < s_mediaBackup.Count; i++)
            {
                var backup = s_mediaBackup[i];
                if (backup.modeIndex < 0 || backup.modeIndex >= settings.modes.Count)
                    continue;

                var entry = settings.modes[backup.modeIndex];
                if (entry == null)
                    continue;

                entry.previewSprite = backup.previewSprite;
                entry.mainModeSprite = backup.mainModeSprite;
                entry.previewPrefab = backup.previewPrefab;
                entry.transitionLoadingSprite = backup.transitionLoadingSprite;
                restored++;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            s_mediaBackup.Clear();
            DeleteBackupFile();

            if (log && restored > 0)
                Debug.Log($"[GameProductModeMedia] 已还原 {restored} 个未启用模式的媒体引用。");
        }

        static void BackupAndStripDisabledModeMedia(GameProductSettings settings, bool log)
        {
            s_mediaBackup.Clear();
            DeleteBackupFile();

            if (settings.modes == null)
                return;

            for (var i = 0; i < settings.modes.Count; i++)
            {
                var entry = settings.modes[i];
                if (entry == null || entry.IsAvailableForCurrentProduct())
                    continue;

                var hasMedia = entry.previewSprite != null
                               || entry.mainModeSprite != null
                               || entry.previewPrefab != null
                               || entry.transitionLoadingSprite != null;
                if (!hasMedia)
                    continue;

                s_mediaBackup.Add(new ModeMediaBackup
                {
                    modeIndex = i,
                    previewSprite = entry.previewSprite,
                    mainModeSprite = entry.mainModeSprite,
                    previewPrefab = entry.previewPrefab,
                    transitionLoadingSprite = entry.transitionLoadingSprite,
                });
            }

            if (s_mediaBackup.Count == 0)
                return;

            SaveBackupToDisk();

            var cleared = settings.ClearDisabledModeMediaReferences();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            if (log)
            {
                Debug.Log(
                    $"[GameProductModeMedia] 打包前已临时清除 {cleared} 个未启用模式媒体引用；" +
                    "构建结束后将自动还原。");
            }
        }

        static void EnsureBackupLoadedFromDisk()
        {
            if (s_mediaBackup.Count > 0)
                return;

            if (!File.Exists(BackupFilePath))
                return;

            try
            {
                var json = File.ReadAllText(BackupFilePath);
                var file = JsonUtility.FromJson<ModeMediaBackupFile>(json);
                if (file?.entries == null || file.entries.Length == 0)
                    return;

                for (var i = 0; i < file.entries.Length; i++)
                {
                    var dto = file.entries[i];
                    if (dto == null)
                        continue;

                    s_mediaBackup.Add(new ModeMediaBackup
                    {
                        modeIndex = dto.modeIndex,
                        previewSprite = LoadAssetByGuid<Sprite>(dto.previewSpriteGuid),
                        mainModeSprite = LoadAssetByGuid<Sprite>(dto.mainModeSpriteGuid),
                        previewPrefab = LoadAssetByGuid<GameObject>(dto.previewPrefabGuid),
                        transitionLoadingSprite = LoadAssetByGuid<Sprite>(dto.transitionLoadingSpriteGuid),
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameProductModeMedia] 读取备份文件失败，无法还原媒体引用：{ex.Message}");
                DeleteBackupFile();
            }
        }

        static void SaveBackupToDisk()
        {
            if (s_mediaBackup.Count == 0)
            {
                DeleteBackupFile();
                return;
            }

            var file = new ModeMediaBackupFile
            {
                entries = new ModeMediaBackupDto[s_mediaBackup.Count],
            };

            for (var i = 0; i < s_mediaBackup.Count; i++)
            {
                var backup = s_mediaBackup[i];
                file.entries[i] = new ModeMediaBackupDto
                {
                    modeIndex = backup.modeIndex,
                    previewSpriteGuid = AssetGuid(backup.previewSprite),
                    mainModeSpriteGuid = AssetGuid(backup.mainModeSprite),
                    previewPrefabGuid = AssetGuid(backup.previewPrefab),
                    transitionLoadingSpriteGuid = AssetGuid(backup.transitionLoadingSprite),
                };
            }

            File.WriteAllText(BackupFilePath, JsonUtility.ToJson(file, prettyPrint: false));
        }

        static void DeleteBackupFile()
        {
            if (File.Exists(BackupFilePath))
                File.Delete(BackupFilePath);
        }

        static string AssetGuid(UnityEngine.Object asset)
        {
            if (asset == null)
                return string.Empty;

            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        static T LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid))
                return null;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        static void ClearMainHomeScenePlaceholderPreviewSprite()
        {
            if (!File.Exists(MainHomeScenePath))
                return;

            var scene = EditorSceneManager.OpenScene(MainHomeScenePath, OpenSceneMode.Single);
            var cleared = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var nodes = roots[i].GetComponentsInChildren<Transform>(true);
                for (var n = 0; n < nodes.Length; n++)
                {
                    var node = nodes[n];
                    if (node == null || node.name != "PrewImage")
                        continue;

                    if (!node.TryGetComponent<Image>(out var image) || image.sprite == null)
                        continue;

                    image.sprite = null;
                    cleared++;
                }
            }

            if (cleared > 0)
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    /// <summary>
    /// 构建结束后还原 GameProductSettings 中被临时剔除的模式媒体引用。
    /// </summary>
    public sealed class GameProductModeMediaBuildPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            GameProductModeMediaUtility.RestoreAfterBuild(log: true);
        }
    }

    /// <summary>
    /// 编辑器启动时若仍存在未还原的备份，则自动恢复（应对构建中断 / 域重载）。
    /// </summary>
    [InitializeOnLoad]
    static class GameProductModeMediaBuildSafetyNet
    {
        static GameProductModeMediaBuildSafetyNet()
        {
            EditorApplication.delayCall += () =>
            {
                if (!GameProductModeMediaUtility.HasPendingBackup())
                    return;

                Debug.LogWarning(
                    "[GameProductModeMedia] 检测到上次构建未还原的模式媒体引用备份，正在自动恢复。");
                GameProductModeMediaUtility.RestoreAfterBuild(log: true);
            };
        }
    }
}
#endif

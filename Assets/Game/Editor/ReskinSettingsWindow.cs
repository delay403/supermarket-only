using System;
using System.Collections.Generic;
using System.IO;
using Game.Maps;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// 换皮设置：覆盖 FGUI 发布目录下的 <c>title.png</c> / <c>bg.png</c>，并同步
    /// <see cref="SceneNavigationSettings"/> 的 主页标题/背景 Sprite 引用。
    /// </summary>
    public sealed class ReskinSettingsWindow : EditorWindow
    {
        const string TitleFileName = "title.png";
        const string BgFileName = "bg.png";

        [SerializeField] DefaultAsset targetFolder;
        [SerializeField] string targetFolderAssetPath;
        [SerializeField] SceneNavigationSettings sceneNavigationSettings;
        [SerializeField] string sceneNavigationSettingsAssetPath;
        [SerializeField] Object newTitleSource;
        [SerializeField] Object newBgSource;
        [SerializeField] bool replaceTitle = true;
        [SerializeField] bool replaceBg = true;
        [SerializeField] bool syncSceneNavigationSettings = true;
        [SerializeField] SceneAsset mainHomeScene;
        [SerializeField] string mainHomeSceneAssetPath = MainHomeBuildSettingsSync.DefaultMainHomeScenePath;
        [SerializeField] string entryDisplayName = "";
        [SerializeField] Vector2 mainScroll;
        [SerializeField] Vector2 historyScroll;

        List<FguiTitleBgReplaceHistoryEntry> historyEntries = new List<FguiTitleBgReplaceHistoryEntry>();
        readonly Dictionary<string, string> renameBuffers = new Dictionary<string, string>();

        [MenuItem("Tools/资源工具/换皮设置工具")]
        static void Open()
        {
            var w = GetWindow<ReskinSettingsWindow>();
            w.titleContent = new GUIContent("换皮设置工具");
            w.minSize = new Vector2(500, 560);
            w.EnsureTargetFolderResolved();
            w.EnsureSceneNavigationSettingsResolved();
            w.EnsureMainHomeSceneResolved();
            w.ReloadHistory();
        }

        void OnEnable() => EditorApplication.delayCall += DelayedInitialize;

        void OnDisable() => EditorApplication.delayCall -= DelayedInitialize;

        void DelayedInitialize()
        {
            if (this == null)
                return;

            ReskinEditorStyles.EnsureInitialized();
            EnsureTargetFolderResolved();
            EnsureSceneNavigationSettingsResolved();
            EnsureMainHomeSceneResolved();
            ReloadHistory();
        }

        void EnsureMainHomeSceneResolved()
        {
            if (string.IsNullOrEmpty(mainHomeSceneAssetPath))
                mainHomeSceneAssetPath = MainHomeBuildSettingsSync.DefaultMainHomeScenePath;
            mainHomeScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(mainHomeSceneAssetPath);
            if (mainHomeScene == null && !string.IsNullOrEmpty(mainHomeSceneAssetPath))
            {
                var guids = AssetDatabase.FindAssets("MainHome t:Scene");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("/MainHome.unity", StringComparison.OrdinalIgnoreCase))
                    {
                        mainHomeSceneAssetPath = path;
                        mainHomeScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                        break;
                    }
                }
            }
        }

        void SaveMainHomeSceneFromAsset()
        {
            if (mainHomeScene == null)
                return;
            var path = AssetDatabase.GetAssetPath(mainHomeScene);
            if (!string.IsNullOrEmpty(path))
                mainHomeSceneAssetPath = path;
        }

        string GetMainHomeSceneAssetPath()
        {
            if (!string.IsNullOrEmpty(mainHomeSceneAssetPath))
                return mainHomeSceneAssetPath;
            var path = AssetDatabase.GetAssetPath(mainHomeScene);
            return string.IsNullOrEmpty(path) ? MainHomeBuildSettingsSync.DefaultMainHomeScenePath : path;
        }

        void EnsureTargetFolderResolved()
        {
            targetFolderAssetPath = FguiTitleBgReplaceHistoryStore.ResolveTargetFolderAssetPath();
            SyncTargetFolderAssetFromPath();
        }

        void EnsureSceneNavigationSettingsResolved()
        {
            sceneNavigationSettingsAssetPath = FguiTitleBgReplaceHistoryStore.ResolveSceneNavigationSettingsAssetPath();
            SyncSceneNavigationSettingsFromPath();
        }

        void SyncTargetFolderAssetFromPath()
        {
            targetFolder = string.IsNullOrEmpty(targetFolderAssetPath)
                ? null
                : FguiTitleBgReplaceHistoryStore.LoadFolderAsset(targetFolderAssetPath);
        }

        void SyncSceneNavigationSettingsFromPath()
        {
            sceneNavigationSettings = string.IsNullOrEmpty(sceneNavigationSettingsAssetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneNavigationSettings>(sceneNavigationSettingsAssetPath);
        }

        void SaveTargetFolderFromAsset()
        {
            if (targetFolder == null)
            {
                targetFolderAssetPath = null;
                FguiTitleBgReplaceHistoryStore.SaveTargetFolderAssetPath(null);
                return;
            }

            var path = AssetDatabase.GetAssetPath(targetFolder);
            if (!FguiTitleBgReplaceHistoryStore.IsValidFolderAssetPath(path))
                return;

            targetFolderAssetPath = path;
            FguiTitleBgReplaceHistoryStore.SaveTargetFolderAssetPath(path);
        }

        void SaveSceneNavigationSettingsFromAsset()
        {
            if (sceneNavigationSettings == null)
            {
                sceneNavigationSettingsAssetPath = null;
                FguiTitleBgReplaceHistoryStore.SaveSceneNavigationSettingsAssetPath(null);
                return;
            }

            var path = AssetDatabase.GetAssetPath(sceneNavigationSettings);
            if (string.IsNullOrEmpty(path))
                return;

            sceneNavigationSettingsAssetPath = path;
            FguiTitleBgReplaceHistoryStore.SaveSceneNavigationSettingsAssetPath(path);
        }

        string GetTargetFolderAssetPath()
        {
            if (FguiTitleBgReplaceHistoryStore.IsValidFolderAssetPath(targetFolderAssetPath))
                return targetFolderAssetPath;

            var path = AssetDatabase.GetAssetPath(targetFolder);
            return FguiTitleBgReplaceHistoryStore.IsValidFolderAssetPath(path) ? path : null;
        }

        void ReloadHistory()
        {
            historyEntries = new List<FguiTitleBgReplaceHistoryEntry>(FguiTitleBgReplaceHistoryStore.LoadEntries());
            historyEntries.Sort((a, b) => string.CompareOrdinal(b.createdAtUtc, a.createdAtUtc));
            renameBuffers.Clear();
            Repaint();
        }

        void OnGUI()
        {
            ReskinEditorStyles.EnsureInitialized();

            if (targetFolder == null && string.IsNullOrEmpty(targetFolderAssetPath))
                EnsureTargetFolderResolved();
            else if (targetFolder == null && !string.IsNullOrEmpty(targetFolderAssetPath))
                SyncTargetFolderAssetFromPath();

            if (sceneNavigationSettings == null && string.IsNullOrEmpty(sceneNavigationSettingsAssetPath))
                EnsureSceneNavigationSettingsResolved();
            else if (sceneNavigationSettings == null && !string.IsNullOrEmpty(sceneNavigationSettingsAssetPath))
                SyncSceneNavigationSettingsFromPath();

            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            GUILayout.BeginVertical(ReskinEditorStyles.WindowPadding);

            DrawHeader();
            DrawFguiSection();
            DrawSceneNavigationSection();
            DrawMainHomeSection();
            DrawApplySection();
            DrawHistorySection();

            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            EditorGUILayout.HelpBox(
                "覆盖 FGUI 发布目录中的 title.png / bg.png，并可选同步 SceneNavigationSettings 的 主页 Sprite 引用。\n" +
                "支持拖入 Texture2D 或其下的 Sprite 子资源；历史记录可命名与还原。",
                MessageType.Info);
            EditorGUILayout.Space(4);
        }

        void DrawFguiSection()
        {
            ReskinEditorStyles.DrawSectionTitle("FGUI 发布目录", "覆盖磁盘上的 PNG 文件");

            ReskinEditorStyles.BeginCard();
            EditorGUI.BeginChangeCheck();
            targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent("目标目录", "包含 title.png / bg.png 的 fgui 文件夹"), targetFolder, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
                SaveTargetFolderFromAsset();

            ReskinEditorStyles.DrawPath("路径", targetFolderAssetPath);

            EditorGUILayout.Space(4);
            replaceTitle = EditorGUILayout.ToggleLeft("替换 title.png", replaceTitle);
            using (new EditorGUI.DisabledScope(!replaceTitle))
            {
                newTitleSource = EditorGUILayout.ObjectField(
                    new GUIContent("新标题资源", "可拖入 PNG（Texture2D）或 Sprite 子资源"),
                    newTitleSource, typeof(Object), false);
                DrawSourcePreview(newTitleSource);
            }

            EditorGUILayout.Space(2);
            replaceBg = EditorGUILayout.ToggleLeft("替换 bg.png", replaceBg);
            using (new EditorGUI.DisabledScope(!replaceBg))
            {
                newBgSource = EditorGUILayout.ObjectField(
                    new GUIContent("新背景资源", "可拖入 PNG（Texture2D）或 Sprite 子资源"),
                    newBgSource, typeof(Object), false);
                DrawSourcePreview(newBgSource);
            }

            ReskinEditorStyles.EndCard();
        }

        void DrawSourcePreview(Object source)
        {
            var sprite = ReskinSpriteUtility.LoadSpriteFromObject(source);
            if (sprite == null)
                return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            var rect = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48));
            DrawSpritePreview(rect, sprite);
            EditorGUILayout.LabelField($"Sprite：{sprite.name}", ReskinEditorStyles.ScopeLabel);
            EditorGUILayout.EndHorizontal();
        }

        static void DrawSpritePreview(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return;

            var tex = sprite.texture;
            var tr = sprite.textureRect;
            var uv = new Rect(
                tr.x / tex.width,
                tr.y / tex.height,
                tr.width / tex.width,
                tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
        }

        void DrawSceneNavigationSection()
        {
            ReskinEditorStyles.DrawSectionTitle("SceneNavigationSettings", "仅更新 主页标题/背景 Sprite 引用");

            ReskinEditorStyles.BeginCard();
            EditorGUI.BeginChangeCheck();
            sceneNavigationSettings = (SceneNavigationSettings)EditorGUILayout.ObjectField(
                new GUIContent("配置文件", "场景导航配置"), sceneNavigationSettings,
                typeof(SceneNavigationSettings), false);
            if (EditorGUI.EndChangeCheck())
                SaveSceneNavigationSettingsFromAsset();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(sceneNavigationSettings == null))
            {
                if (GUILayout.Button("定位配置文件", ReskinEditorStyles.SecondaryButton))
                    PingSceneNavigationSettings();
            }

            if (GUILayout.Button("重新搜索", ReskinEditorStyles.SecondaryButton, GUILayout.Width(80)))
            {
                EnsureSceneNavigationSettingsResolved();
                if (sceneNavigationSettings != null)
                    PingSceneNavigationSettings();
            }
            EditorGUILayout.EndHorizontal();

            ReskinEditorStyles.DrawPath("路径", sceneNavigationSettingsAssetPath);

            using (new EditorGUI.DisabledScope(sceneNavigationSettings == null))
            {
                EditorGUILayout.Space(4);
                DrawCurrentSpriteRow("当前标题", sceneNavigationSettings?.homeTitleSprite);
                DrawCurrentSpriteRow("当前背景", sceneNavigationSettings?.homeBackgroundSprite);
            }

            EditorGUILayout.Space(4);
            syncSceneNavigationSettings = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "同步 主页标题/背景 Sprite 引用",
                    "按上方勾选项更新 Sprite 引用；优先使用 fgui 目录下导入后的 Sprite，否则回退到源资源"),
                syncSceneNavigationSettings);
            ReskinEditorStyles.EndCard();
        }

        void DrawMainHomeSection()
        {
            ReskinEditorStyles.DrawSectionTitle("主页场景", "UseMainHomeScene + Build Settings（index 2）");

            ReskinEditorStyles.BeginCard();
            using (new EditorGUI.DisabledScope(sceneNavigationSettings == null))
            {
                EditorGUI.BeginChangeCheck();
                var useMainHome = EditorGUILayout.ToggleLeft(
                    new GUIContent(
                        "使用主页场景 (useMainHomeScene)",
                        "勾选：写入 SceneNavigationSettings，并将 MainHome 加入 Build Settings 的第 3 项（index 2）\n" +
                        "取消：关闭 useMainHomeScene，并从 Build Settings 移除 MainHome"),
                    sceneNavigationSettings != null && sceneNavigationSettings.useMainHomeScene);
                if (EditorGUI.EndChangeCheck())
                    ApplyMainHomeSceneSetting(useMainHome);
            }

            EditorGUI.BeginChangeCheck();
            mainHomeScene = (SceneAsset)EditorGUILayout.ObjectField(
                new GUIContent("MainHome 场景", "默认 Assets/Game/Core/Scenes/MainHome.unity"),
                mainHomeScene, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
                SaveMainHomeSceneFromAsset();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(mainHomeScene == null))
            {
                if (GUILayout.Button("定位 MainHome 场景", ReskinEditorStyles.SecondaryButton))
                {
                    EditorGUIUtility.PingObject(mainHomeScene);
                    Selection.activeObject = mainHomeScene;
                }
            }
            if (GUILayout.Button("重新定位", ReskinEditorStyles.SecondaryButton, GUILayout.Width(80)))
                EnsureMainHomeSceneResolved();
            EditorGUILayout.EndHorizontal();

            ReskinEditorStyles.DrawPath("路径", GetMainHomeSceneAssetPath());

            var path = GetMainHomeSceneAssetPath();
            var inBuild = MainHomeBuildSettingsSync.IsSceneInBuild(path);
            var buildIndex = MainHomeBuildSettingsSync.GetBuildIndex(path);
            var status = inBuild
                ? $"Build Settings：已包含（index {buildIndex}）"
                : "Build Settings：未包含";
            if (sceneNavigationSettings != null &&
                sceneNavigationSettings.useMainHomeScene != inBuild)
                status += "  ·  与 useMainHomeScene 不一致，请再切换一次开关以同步";
            EditorGUILayout.LabelField(status, ReskinEditorStyles.ScopeLabel);

            if (GUILayout.Button("按当前开关同步 Build Settings", ReskinEditorStyles.SecondaryButton))
            {
                if (sceneNavigationSettings != null)
                    ApplyMainHomeSceneSetting(sceneNavigationSettings.useMainHomeScene);
            }
            ReskinEditorStyles.EndCard();
        }

        void ApplyMainHomeSceneSetting(bool useMainHome)
        {
            if (sceneNavigationSettings == null)
            {
                EditorUtility.DisplayDialog("未找到配置", "请先指定 SceneNavigationSettings。", "确定");
                return;
            }

            var scenePath = GetMainHomeSceneAssetPath();
            if (string.IsNullOrEmpty(scenePath) || AssetPathToFullPath(scenePath) == null)
            {
                EditorUtility.DisplayDialog("场景无效", $"找不到 MainHome 场景:\n{scenePath}", "确定");
                return;
            }

            Undo.RecordObject(sceneNavigationSettings, "Use Main Home Scene");
            sceneNavigationSettings.useMainHomeScene = useMainHome;
            EditorUtility.SetDirty(sceneNavigationSettings);

            MainHomeBuildSettingsSync.Apply(scenePath, useMainHome);

            AssetDatabase.SaveAssets();
            Repaint();
        }

        void DrawCurrentSpriteRow(string label, Sprite sprite)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            var rect = GUILayoutUtility.GetRect(36, 36, GUILayout.Width(36));
            if (sprite != null)
                DrawSpritePreview(rect, sprite);
            else
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.4f));

            EditorGUILayout.BeginVertical();
            if (sprite == null)
                EditorGUILayout.LabelField("（未设置）", ReskinEditorStyles.ScopeLabel);
            else
            {
                EditorGUILayout.LabelField(sprite.name, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(sprite), ReskinEditorStyles.PathLabel);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        void DrawApplySection()
        {
            ReskinEditorStyles.DrawSectionTitle("应用换皮");

            ReskinEditorStyles.BeginCard();
            entryDisplayName = EditorGUILayout.TextField(
                new GUIContent("记录名称", "留空则按时间自动生成"), entryDisplayName);

            EditorGUILayout.Space(6);
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = ReskinEditorStyles.Accent;
            using (new EditorGUI.DisabledScope(!CanApply()))
            {
                if (GUILayout.Button("应用换皮", ReskinEditorStyles.PrimaryButton))
                    TryApply();
            }
            GUI.backgroundColor = prevColor;
            ReskinEditorStyles.EndCard();
        }

        void PingSceneNavigationSettings()
        {
            if (sceneNavigationSettings == null)
            {
                EditorUtility.DisplayDialog("未找到配置", "请先指定或搜索 SceneNavigationSettings 配置文件。", "确定");
                return;
            }

            EditorGUIUtility.PingObject(sceneNavigationSettings);
            Selection.activeObject = sceneNavigationSettings;
        }

        void DrawHistorySection()
        {
            EditorGUILayout.Space(4);
            ReskinEditorStyles.DrawSectionTitle("换皮历史", historyEntries.Count > 0 ? $"{historyEntries.Count} 条记录" : "暂无记录");

            if (historyEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("完成一次换皮后会出现在这里，可还原或删除。", MessageType.None);
                return;
            }

            historyScroll = EditorGUILayout.BeginScrollView(historyScroll, GUILayout.MinHeight(180));
            for (var i = 0; i < historyEntries.Count; i++)
                DrawHistoryRow(historyEntries[i]);
            EditorGUILayout.EndScrollView();
        }

        void DrawHistoryRow(FguiTitleBgReplaceHistoryEntry entry)
        {
            if (!renameBuffers.ContainsKey(entry.id))
                renameBuffers[entry.id] = entry.displayName;

            EditorGUILayout.BeginVertical(ReskinEditorStyles.HistoryCard);
            EditorGUILayout.BeginHorizontal();

            renameBuffers[entry.id] = EditorGUILayout.TextField(renameBuffers[entry.id]);
            if (renameBuffers[entry.id] != entry.displayName)
            {
                if (GUILayout.Button("保存", GUILayout.Width(52)))
                {
                    FguiTitleBgReplaceHistoryStore.RenameEntry(entry.id, renameBuffers[entry.id]);
                    entry.displayName = renameBuffers[entry.id].Trim();
                }
            }

            EditorGUILayout.EndHorizontal();

            var scope = new List<string>();
            if (entry.includesTitle) scope.Add("fgui/title");
            if (entry.includesBg) scope.Add("fgui/bg");
            if (entry.includesSceneNavTitle) scope.Add("主页标题");
            if (entry.includesSceneNavBg) scope.Add("主页背景");
            EditorGUILayout.LabelField(
                $"{FguiTitleBgReplaceHistoryStore.FormatLocalTime(entry.createdAtUtc)}  ·  {string.Join(" + ", scope)}",
                ReskinEditorStyles.ScopeLabel);

            if (!string.IsNullOrEmpty(entry.targetFolderAssetPath))
                EditorGUILayout.LabelField(entry.targetFolderAssetPath, ReskinEditorStyles.PathLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("还原", ReskinEditorStyles.SecondaryButton))
                TryRestore(entry);

            if (GUILayout.Button("删除", ReskinEditorStyles.DangerButton, GUILayout.Width(56)))
                TryDelete(entry);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        bool CanApply()
        {
            if (!replaceTitle && !replaceBg)
                return false;

            if ((replaceTitle || replaceBg) && GetTargetFolderAssetPath() == null)
                return false;

            if (replaceTitle && !IsValidSourceObject(newTitleSource))
                return false;

            if (replaceBg && !IsValidSourceObject(newBgSource))
                return false;

            if (syncSceneNavigationSettings && (replaceTitle || replaceBg) && sceneNavigationSettings == null)
                return false;

            return true;
        }

        static bool IsValidSourceObject(Object source)
        {
            if (source == null)
                return false;

            if (source is Sprite)
                return true;

            if (source is Texture2D texture)
            {
                var path = AssetDatabase.GetAssetPath(texture);
                return !string.IsNullOrEmpty(path) && IsImportedImageFile(path);
            }

            return false;
        }

        static string GetSourceAssetPath(Object source)
        {
            if (source == null)
                return null;

            return AssetDatabase.GetAssetPath(source);
        }

        void TryApply()
        {
            var folderAssetPath = GetTargetFolderAssetPath();
            if ((replaceTitle || replaceBg) && string.IsNullOrEmpty(folderAssetPath))
            {
                EditorUtility.DisplayDialog("无效目录", "请指定有效的 FGUI 目标目录。", "确定");
                return;
            }

            string titleSrcPath = null;
            string bgSrcPath = null;

            if (replaceTitle)
            {
                titleSrcPath = GetSourceAssetPath(newTitleSource);
                if (!IsValidSourceObject(newTitleSource))
                {
                    EditorUtility.DisplayDialog("无效资源", "标题资源必须是工程内的 PNG 或 Sprite。", "确定");
                    return;
                }
            }

            if (replaceBg)
            {
                bgSrcPath = GetSourceAssetPath(newBgSource);
                if (!IsValidSourceObject(newBgSource))
                {
                    EditorUtility.DisplayDialog("无效资源", "背景资源必须是工程内的 PNG 或 Sprite。", "确定");
                    return;
                }
            }

            var syncSceneNavTitle = syncSceneNavigationSettings && replaceTitle && sceneNavigationSettings != null;
            var syncSceneNavBg = syncSceneNavigationSettings && replaceBg && sceneNavigationSettings != null;

            string titleSrcFull = null;
            string bgSrcFull = null;
            string titleDestFull = null;
            string bgDestFull = null;
            var titleDestAsset = replaceTitle ? $"{folderAssetPath}/{TitleFileName}" : null;
            var bgDestAsset = replaceBg ? $"{folderAssetPath}/{BgFileName}" : null;

            if (replaceTitle)
            {
                titleSrcFull = AssetPathToFullPath(titleSrcPath);
                titleDestFull = AssetPathToFullPath(titleDestAsset);
                if (titleSrcFull == null || titleDestFull == null)
                {
                    EditorUtility.DisplayDialog("路径错误", "无法解析标题资源或目标路径。", "确定");
                    return;
                }
            }

            if (replaceBg)
            {
                bgSrcFull = AssetPathToFullPath(bgSrcPath);
                bgDestFull = AssetPathToFullPath(bgDestAsset);
                if (bgSrcFull == null || bgDestFull == null)
                {
                    EditorUtility.DisplayDialog("路径错误", "无法解析背景资源或目标路径。", "确定");
                    return;
                }
            }

            SaveTargetFolderFromAsset();
            SaveSceneNavigationSettingsFromAsset();

            FguiTitleBgReplaceHistoryEntry entry;
            try
            {
                entry = FguiTitleBgReplaceHistoryStore.AddEntry(
                    entryDisplayName,
                    folderAssetPath,
                    replaceTitle,
                    titleSrcFull,
                    replaceBg,
                    bgSrcFull,
                    sceneNavigationSettingsAssetPath,
                    syncSceneNavTitle,
                    syncSceneNavBg);

                if (replaceTitle)
                    File.Copy(titleSrcFull, titleDestFull, true);
                if (replaceBg)
                    File.Copy(bgSrcFull, bgDestFull, true);

                if (replaceTitle)
                    ReskinSpriteUtility.ImportImageAsset(titleDestAsset, titleSrcPath);
                if (replaceBg)
                    ReskinSpriteUtility.ImportImageAsset(bgDestAsset, bgSrcPath);

                if (syncSceneNavTitle || syncSceneNavBg)
                {
                    var titleSprite = syncSceneNavTitle
                        ? ReskinSpriteUtility.ResolveSpriteForSceneNavigation(
                            newTitleSource,
                            titleSrcPath,
                            titleDestAsset,
                            sceneNavigationSettings.homeTitleSprite,
                            replaceTitle)
                        : null;
                    var bgSprite = syncSceneNavBg
                        ? ReskinSpriteUtility.ResolveSpriteForSceneNavigation(
                            newBgSource,
                            bgSrcPath,
                            bgDestAsset,
                            sceneNavigationSettings.homeBackgroundSprite,
                            replaceBg)
                        : null;

                    if (!ReskinSpriteUtility.TryApplySceneNavigationSprites(
                            sceneNavigationSettings,
                            syncSceneNavTitle,
                            titleSprite,
                            syncSceneNavBg,
                            bgSprite,
                            out var sceneNavError))
                    {
                        EditorUtility.DisplayDialog("SceneNavigationSettings 更新失败", sceneNavError, "确定");
                        return;
                    }

                    FguiTitleBgReplaceHistoryStore.UpdateEntrySceneNavSpriteRefs(
                        entry.id,
                        syncSceneNavTitle ? ReskinSpriteUtility.SerializeSpriteRef(titleSprite) : null,
                        syncSceneNavBg ? ReskinSpriteUtility.SerializeSpriteRef(bgSprite) : null);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("换皮失败", e.Message, "确定");
                Debug.LogException(e);
                return;
            }

            SyncSceneNavigationSettingsFromPath();
            ReloadHistory();
            entryDisplayName = "";

            var written = new List<string>();
            if (replaceTitle) written.Add(titleDestAsset);
            if (replaceBg) written.Add(bgDestAsset);
            if (syncSceneNavTitle || syncSceneNavBg)
                written.Add(sceneNavigationSettingsAssetPath);

            EditorUtility.DisplayDialog(
                "完成",
                $"已应用换皮并保存历史「{entry.displayName}」：\n{string.Join("\n", written)}",
                "确定");
        }

        void TryRestore(FguiTitleBgReplaceHistoryEntry entry)
        {
            var scope = new List<string>();
            if (entry.includesTitle) scope.Add("title.png");
            if (entry.includesBg) scope.Add("bg.png");
            if (entry.includesSceneNavTitle) scope.Add("主页标题 Sprite");
            if (entry.includesSceneNavBg) scope.Add("主页背景 Sprite");

            if (!EditorUtility.DisplayDialog(
                    "还原换皮",
                    $"将历史「{entry.displayName}」中的 {string.Join("、", scope)} 还原。\n\n继续？",
                    "还原",
                    "取消"))
                return;

            if (!FguiTitleBgReplaceHistoryStore.TryRestore(entry, out var error))
            {
                EditorUtility.DisplayDialog("还原失败", error, "确定");
                return;
            }

            if (entry.includesTitle)
                AssetDatabase.ImportAsset($"{entry.targetFolderAssetPath}/{TitleFileName}", ImportAssetOptions.ForceUpdate);
            if (entry.includesBg)
                AssetDatabase.ImportAsset($"{entry.targetFolderAssetPath}/{BgFileName}", ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            EnsureSceneNavigationSettingsResolved();
            EditorUtility.DisplayDialog("完成", $"已还原历史「{entry.displayName}」。", "确定");
        }

        void TryDelete(FguiTitleBgReplaceHistoryEntry entry)
        {
            if (!EditorUtility.DisplayDialog(
                    "删除历史",
                    $"删除「{entry.displayName}」及其备份？此操作不可撤销。",
                    "删除",
                    "取消"))
                return;

            FguiTitleBgReplaceHistoryStore.DeleteEntry(entry.id);
            ReloadHistory();
        }

        static bool IsImportedImageFile(string assetPath)
        {
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".tga" or ".bmp" or ".gif";
        }

        static string AssetPathToFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return null;
            var relative = assetPath.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}

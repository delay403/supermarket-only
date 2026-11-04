#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// 批量将工程内 UGUI <see cref="Text"/> 的字体引用统一为指定 Font 资产（修复移动字体包后的丢失引用）。
    /// </summary>
    public sealed class BatchUIFontRepairWindow : EditorWindow
    {
        const string MenuPath = "Game/Tools/Batch Assign UI Font";
        const string ToolsMenuPath = "Tools/资源工具/批量修复 UI 字体";
        const string DefaultFontPath = "Assets/Game/Core/Resources/喜脉体.ttf";
        const string PrefsFontGuid = "Game.BatchUIFontRepair.FontGuid";
        const string PrefsReplaceAll = "Game.BatchUIFontRepair.ReplaceAll";
        const string PrefsIncludeScenes = "Game.BatchUIFontRepair.IncludeScenes";
        const string PrefsIncludePrefabs = "Game.BatchUIFontRepair.IncludePrefabs";

        [SerializeField] Font targetFont;
        [SerializeField] bool replaceAllTexts;
        [SerializeField] bool includeScenes = true;
        [SerializeField] bool includePrefabs = true;
        [SerializeField] bool excludePluginFolders = true;

        Vector2 _scroll;
        readonly List<PreviewEntry> _preview = new List<PreviewEntry>();

        sealed class PreviewEntry
        {
            public string AssetPath;
            public int TextCount;
        }

        [MenuItem(MenuPath)]
        [MenuItem(ToolsMenuPath)]
        static void Open()
        {
            var window = GetWindow<BatchUIFontRepairWindow>("UI 字体批量修复");
            window.minSize = new Vector2(520f, 520f);
            window.Show();
        }

        void OnEnable()
        {
            replaceAllTexts = EditorPrefs.GetBool(PrefsReplaceAll, true);
            includeScenes = EditorPrefs.GetBool(PrefsIncludeScenes, true);
            includePrefabs = EditorPrefs.GetBool(PrefsIncludePrefabs, true);

            if (targetFont == null)
            {
                var guid = EditorPrefs.GetString(PrefsFontGuid, string.Empty);
                if (!string.IsNullOrEmpty(guid))
                    targetFont = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));

                if (targetFont == null && File.Exists(DefaultFontPath))
                    targetFont = AssetDatabase.LoadAssetAtPath<Font>(DefaultFontPath);
            }
        }

        void OnDisable()
        {
            SavePrefs();
        }

        void SavePrefs()
        {
            EditorPrefs.SetBool(PrefsReplaceAll, replaceAllTexts);
            EditorPrefs.SetBool(PrefsIncludeScenes, includeScenes);
            EditorPrefs.SetBool(PrefsIncludePrefabs, includePrefabs);
            if (targetFont != null)
                EditorPrefs.SetString(PrefsFontGuid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(targetFont)));
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("UI 字体批量修复", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "扫描工程内所有场景与 Prefab 中的 UGUI Text，将字体统一为下方指定资源。\n" +
                "「仅修复丢失引用」：font 为空时替换；「全部替换」：所有 Text 都改为目标字体。",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            targetFont = (Font)EditorGUILayout.ObjectField("目标字体", targetFont, typeof(Font), false);
            replaceAllTexts = EditorGUILayout.ToggleLeft("全部替换为指定字体（否则仅修复丢失引用）", replaceAllTexts);
            includeScenes = EditorGUILayout.ToggleLeft("处理场景 (.unity)", includeScenes);
            includePrefabs = EditorGUILayout.ToggleLeft("处理预制体 (.prefab)", includePrefabs);
            excludePluginFolders = EditorGUILayout.ToggleLeft("跳过 Plugins / PackageCache 目录", excludePluginFolders);

            if (targetFont == null)
                EditorGUILayout.HelpBox("请先指定目标 Font 资源。", MessageType.Warning);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("扫描预览", GUILayout.Height(26f)))
                    ScanPreview();

                using (new EditorGUI.DisabledScope(targetFont == null))
                {
                    if (GUILayout.Button("执行修复", GUILayout.Height(26f)))
                        ApplyRepair();
                }
            }

            DrawPreviewSection();
        }

        void DrawPreviewSection()
        {
            var totalTexts = 0;
            for (var i = 0; i < _preview.Count; i++)
                totalTexts += _preview[i].TextCount;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"预览：{_preview.Count} 个资源，{totalTexts} 处 Text 待修改", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(240f));
            for (var i = 0; i < _preview.Count; i++)
            {
                var entry = _preview[i];
                EditorGUILayout.LabelField($"{entry.AssetPath}  ({entry.TextCount})");
            }

            EditorGUILayout.EndScrollView();
        }

        void ScanPreview()
        {
            if (targetFont == null)
            {
                EditorUtility.DisplayDialog("UI 字体批量修复", "请先指定目标字体。", "确定");
                return;
            }

            _preview.Clear();
            CollectPreview(includeScenes, includePrefabs, excludePluginFolders, replaceAllTexts, targetFont, _preview);
            Repaint();
        }

        void ApplyRepair()
        {
            if (targetFont == null)
            {
                EditorUtility.DisplayDialog("UI 字体批量修复", "请先指定目标字体。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "UI 字体批量修复",
                    "将修改场景与 Prefab 中的 Text.font，建议先提交版本或备份。\n是否继续？",
                    "继续",
                    "取消"))
                return;

            SavePrefs();

            var changedAssets = 0;
            var changedTexts = 0;

            try
            {
                if (includePrefabs)
                {
                    var prefabPaths = CollectPrefabPaths(excludePluginFolders);
                    for (var i = 0; i < prefabPaths.Count; i++)
                    {
                        var path = prefabPaths[i];
                        EditorUtility.DisplayProgressBar("修复 Prefab 字体", path, (float)i / prefabPaths.Count);
                        if (TryRepairPrefab(path, targetFont, replaceAllTexts, out var count))
                        {
                            changedAssets++;
                            changedTexts += count;
                        }
                    }
                }

                if (includeScenes)
                {
                    var scenePaths = CollectScenePaths(excludePluginFolders);
                    var originalScene = SceneManager.GetActiveScene().path;
                    for (var i = 0; i < scenePaths.Count; i++)
                    {
                        var path = scenePaths[i];
                        EditorUtility.DisplayProgressBar("修复场景字体", path, (float)i / scenePaths.Count);
                        if (TryRepairScene(path, targetFont, replaceAllTexts, out var count))
                        {
                            changedAssets++;
                            changedTexts += count;
                        }
                    }

                    if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene))
                        EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ScanPreview();

            EditorUtility.DisplayDialog(
                "UI 字体批量修复",
                $"完成。修改 {changedAssets} 个资源，共 {changedTexts} 处 Text。",
                "确定");
        }

        static void CollectPreview(
            bool scenes,
            bool prefabs,
            bool excludePlugins,
            bool replaceAll,
            Font target,
            List<PreviewEntry> output)
        {
            if (prefabs)
            {
                var paths = CollectPrefabPaths(excludePlugins);
                for (var i = 0; i < paths.Count; i++)
                {
                    var count = CountTextsNeedingRepairInPrefab(paths[i], target, replaceAll);
                    if (count > 0)
                        output.Add(new PreviewEntry { AssetPath = paths[i], TextCount = count });
                }
            }

            if (scenes)
            {
                var paths = CollectScenePaths(excludePlugins);
                for (var i = 0; i < paths.Count; i++)
                {
                    var count = CountTextsNeedingRepairInScene(paths[i], target, replaceAll);
                    if (count > 0)
                        output.Add(new PreviewEntry { AssetPath = paths[i], TextCount = count });
                }
            }
        }

        static List<string> CollectPrefabPaths(bool excludePlugins)
        {
            var result = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (ShouldSkipPath(path, excludePlugins))
                    continue;

                result.Add(path);
            }

            return result;
        }

        static List<string> CollectScenePaths(bool excludePlugins)
        {
            var result = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (ShouldSkipPath(path, excludePlugins))
                    continue;

                result.Add(path);
            }

            return result;
        }

        static bool ShouldSkipPath(string path, bool excludePlugins)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            path = path.Replace('\\', '/');
            if (excludePlugins && (
                    path.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("/PackageCache/", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("/DOTweenPro Examples/", StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        static bool NeedsRepair(Text text, Font target, bool replaceAll)
        {
            if (text == null)
                return false;

            if (replaceAll)
                return text.font != target;

            return IsMissingOrBrokenFont(text.font);
        }

        static bool IsMissingOrBrokenFont(Font font)
        {
            if (font == null)
                return true;

            var path = AssetDatabase.GetAssetPath(font);
            return string.IsNullOrEmpty(path) || !File.Exists(path);
        }

        static int CountTextsNeedingRepairInPrefab(string path, Font target, bool replaceAll)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                return CountTextsInHierarchy(root, target, replaceAll);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static int CountTextsNeedingRepairInScene(string path, Font target, bool replaceAll)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var count = 0;
                var texts = GetTextsInOpenScene(scene);
                for (var i = 0; i < texts.Count; i++)
                {
                    if (NeedsRepair(texts[i], target, replaceAll))
                        count++;
                }

                return count;
            }
            finally
            {
                if (scene.IsValid())
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static bool TryRepairPrefab(string path, Font target, bool replaceAll, out int changedCount)
        {
            changedCount = 0;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                changedCount = ApplyFontToHierarchy(root, target, replaceAll);
                if (changedCount <= 0)
                    return false;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool TryRepairScene(string path, Font target, bool replaceAll, out int changedCount)
        {
            changedCount = 0;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var texts = GetTextsInOpenScene(scene);
            for (var i = 0; i < texts.Count; i++)
            {
                var text = texts[i];
                if (!NeedsRepair(text, target, replaceAll))
                    continue;

                Undo.RecordObject(text, "Batch Assign UI Font");
                text.font = target;
                changedCount++;
            }

            if (changedCount <= 0)
                return false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        static List<Text> GetTextsInOpenScene(Scene scene)
        {
            var result = new List<Text>();
            if (!scene.IsValid())
                return result;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                CollectTextsRecursive(roots[i], result);

            return result;
        }

        static void CollectTextsRecursive(GameObject go, List<Text> output)
        {
            if (go == null)
                return;

            var text = go.GetComponent<Text>();
            if (text != null)
                output.Add(text);

            var transform = go.transform;
            for (var i = 0; i < transform.childCount; i++)
                CollectTextsRecursive(transform.GetChild(i).gameObject, output);
        }

        static int CountTextsInHierarchy(GameObject root, Font target, bool replaceAll)
        {
            var count = 0;
            var texts = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                if (NeedsRepair(texts[i], target, replaceAll))
                    count++;
            }

            return count;
        }

        static int ApplyFontToHierarchy(GameObject root, Font target, bool replaceAll)
        {
            var changed = 0;
            var texts = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (!NeedsRepair(text, target, replaceAll))
                    continue;

                text.font = target;
                changed++;
            }

            return changed;
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 批量重命名 Project 资源或场景物体，支持前缀/后缀/连接词与前后排序规则。
    /// </summary>
    public sealed class BatchRenameWindow : EditorWindow
    {
        const string MenuPath = "Game/Tools/Batch Rename";
        const string SupermarketMenuPath = "Game/Supermarket Organize/Batch Rename";

        enum RenameTargetMode
        {
            ProjectAssets = 0,
            SceneObjects = 1,
        }

        [SerializeField] RenameTargetMode targetMode = RenameTargetMode.ProjectAssets;
        [SerializeField] string prefix = string.Empty;
        [SerializeField] string suffix = string.Empty;
        [SerializeField] string connector = "_";
        [SerializeField] BatchRenameSequenceKind frontSequence = BatchRenameSequenceKind.None;
        [SerializeField] BatchRenameSequenceKind backSequence = BatchRenameSequenceKind.LetterLowerA;
        [SerializeField] int frontNumberStart = 1;
        [SerializeField] int backNumberStart = 1;
        [SerializeField] string frontLetterStart = "a";
        [SerializeField] string backLetterStart = "a";
        [SerializeField] bool preserveAssetExtension = true;
        [SerializeField] bool sortSelectionByName = true;

        Vector2 _scroll;
        readonly List<PreviewEntry> _preview = new List<PreviewEntry>();

        sealed class PreviewEntry
        {
            public UnityEngine.Object Target;
            public string CurrentName;
            public string NewName;
            public string AssetPath;
        }

        [MenuItem(MenuPath)]
        [MenuItem(SupermarketMenuPath)]
        static void Open()
        {
            var window = GetWindow<BatchRenameWindow>("批量命名");
            window.minSize = new Vector2(520f, 560f);
            window.Show();
        }

        void OnEnable() => RebuildPreview();

        void OnSelectionChange() => RebuildPreview();

        void OnGUI()
        {
            EditorGUILayout.LabelField("批量命名工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "支持批量重命名 Project 资源或 Hierarchy 物体。\n" +
                "命名格式：按顺序拼接 前缀 → 前排序 → 后排序 → 后缀，非空片段之间插入连接词。\n" +
                "数字/字母排序可自定义起始值（例如数字从 4 开始、字母从 d 开始）。\n" +
                "数字排序会根据数量自动补零（<10 用 1,2,3；≥10 用 01,02；≥100 用 001...）。",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            targetMode = (RenameTargetMode)EditorGUILayout.EnumPopup("重命名目标", targetMode);
            prefix = EditorGUILayout.TextField("固定前缀", prefix);
            connector = EditorGUILayout.TextField("中间连接词", connector);
            suffix = EditorGUILayout.TextField("固定后缀", suffix);
            frontSequence = (BatchRenameSequenceKind)EditorGUILayout.EnumPopup("前排序规则", frontSequence);
            backSequence = (BatchRenameSequenceKind)EditorGUILayout.EnumPopup("后排序规则", backSequence);
            DrawSequenceStartFields(frontSequence, ref frontNumberStart, ref frontLetterStart, "前排序");
            DrawSequenceStartFields(backSequence, ref backNumberStart, ref backLetterStart, "后排序");

            using (new EditorGUI.DisabledScope(targetMode != RenameTargetMode.ProjectAssets))
                preserveAssetExtension = EditorGUILayout.Toggle("保留资源扩展名", preserveAssetExtension);

            sortSelectionByName = EditorGUILayout.Toggle("按自然数序排序后再编号", sortSelectionByName);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新预览", GUILayout.Height(24f)))
                    RebuildPreview();

                if (GUILayout.Button("使用当前选中", GUILayout.Height(24f)))
                    RebuildPreview();
            }

            DrawPreviewSection();
            DrawApplySection();
        }

        void DrawPreviewSection()
        {
            EditorGUILayout.LabelField($"预览（{_preview.Count} 项）", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(220f));
            for (var i = 0; i < _preview.Count; i++)
            {
                var entry = _preview[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(entry.CurrentName, GUILayout.Width(180f));
                    EditorGUILayout.LabelField("→", GUILayout.Width(16f));
                    EditorGUILayout.LabelField(entry.NewName, EditorStyles.boldLabel);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawApplySection()
        {
            using (new EditorGUI.DisabledScope(_preview.Count == 0))
            {
                if (GUILayout.Button($"批量重命名 {_preview.Count} 项", GUILayout.Height(36f)))
                    ApplyRename();
            }
        }

        void RebuildPreview()
        {
            _preview.Clear();
            var targets = CollectTargets();
            if (targets.Count == 0)
                return;

            if (sortSelectionByName)
            {
                targets.Sort((a, b) =>
                {
                    var left = GetSortKey(a);
                    var right = GetSortKey(b);
                    return BatchRenameSequenceUtility.CompareNatural(left, right);
                });
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var baseName = BatchRenameSequenceUtility.BuildName(
                    prefix,
                    suffix,
                    connector,
                    frontSequence,
                    backSequence,
                    i,
                    targets.Count,
                    frontNumberStart,
                    backNumberStart,
                    ParseLetterStart(frontLetterStart),
                    ParseLetterStart(backLetterStart));

                var newName = baseName;
                if (targetMode == RenameTargetMode.ProjectAssets && preserveAssetExtension)
                {
                    var extension = System.IO.Path.GetExtension(targets[i].CurrentName);
                    if (!string.IsNullOrEmpty(extension))
                        newName += extension;
                }

                _preview.Add(new PreviewEntry
                {
                    Target = targets[i].Target,
                    CurrentName = targets[i].CurrentName,
                    NewName = newName,
                    AssetPath = targets[i].AssetPath,
                });
            }

            Repaint();
        }

        List<PreviewEntry> CollectTargets()
        {
            var result = new List<PreviewEntry>();
            if (targetMode == RenameTargetMode.ProjectAssets)
            {
                var objects = Selection.objects;
                for (var i = 0; i < objects.Length; i++)
                {
                    var obj = objects[i];
                    if (obj == null)
                        continue;

                    var path = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                        continue;

                    var fileName = System.IO.Path.GetFileName(path);
                    result.Add(new PreviewEntry
                    {
                        Target = obj,
                        CurrentName = fileName,
                        AssetPath = path,
                    });
                }

                return result;
            }

            var gameObjects = Selection.gameObjects;
            for (var i = 0; i < gameObjects.Length; i++)
            {
                var go = gameObjects[i];
                if (go == null)
                    continue;

                result.Add(new PreviewEntry
                {
                    Target = go,
                    CurrentName = go.name,
                });
            }

            return result;
        }

        string GetSortKey(PreviewEntry entry)
        {
            var name = entry.CurrentName ?? string.Empty;
            if (targetMode == RenameTargetMode.ProjectAssets)
                name = System.IO.Path.GetFileNameWithoutExtension(name);

            return name;
        }

        static void DrawSequenceStartFields(
            BatchRenameSequenceKind kind,
            ref int numberStart,
            ref string letterStart,
            string label)
        {
            switch (kind)
            {
                case BatchRenameSequenceKind.Number1:
                    numberStart = EditorGUILayout.IntField($"{label}起始数字", numberStart);
                    break;
                case BatchRenameSequenceKind.LetterLowerA:
                case BatchRenameSequenceKind.LetterUpperA:
                    letterStart = EditorGUILayout.TextField($"{label}起始字母", letterStart);
                    break;
            }
        }

        static char ParseLetterStart(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return '\0';

            var c = text.Trim()[0];
            return char.IsLetter(c) ? c : '\0';
        }

        void ApplyRename()
        {
            if (_preview.Count == 0)
                return;

            var duplicateNames = _preview
                .GroupBy(entry => entry.NewName, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateNames.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "批量命名",
                    $"生成结果存在重名：{string.Join(", ", duplicateNames)}",
                    "确定");
                return;
            }

            var ok = 0;
            var fail = 0;
            try
            {
                for (var i = 0; i < _preview.Count; i++)
                {
                    var entry = _preview[i];
                    EditorUtility.DisplayProgressBar("批量命名", entry.CurrentName, (float)i / _preview.Count);

                    if (targetMode == RenameTargetMode.ProjectAssets)
                    {
                        if (TryRenameAsset(entry, out var error))
                            ok++;
                        else
                        {
                            fail++;
                            Debug.LogWarning($"[BatchRename] 失败: {entry.AssetPath} — {error}");
                        }
                    }
                    else if (TryRenameSceneObject(entry, out var sceneError))
                    {
                        ok++;
                    }
                    else
                    {
                        fail++;
                        Debug.LogWarning($"[BatchRename] 失败: {entry.CurrentName} — {sceneError}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                RebuildPreview();
            }

            EditorUtility.DisplayDialog("批量命名", $"完成：成功 {ok}，失败 {fail}。", "确定");
        }

        static bool TryRenameAsset(PreviewEntry entry, out string error)
        {
            error = null;
            if (entry.Target == null || string.IsNullOrEmpty(entry.AssetPath))
            {
                error = "资源无效";
                return false;
            }

            var directory = System.IO.Path.GetDirectoryName(entry.AssetPath)?.Replace('\\', '/');
            var targetPath = string.IsNullOrEmpty(directory)
                ? entry.NewName
                : $"{directory}/{entry.NewName}";

            if (string.Equals(entry.AssetPath, targetPath, StringComparison.OrdinalIgnoreCase))
                return true;

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath) != null)
            {
                error = "目标路径已存在";
                return false;
            }

            var message = AssetDatabase.RenameAsset(entry.AssetPath, entry.NewName);
            if (!string.IsNullOrEmpty(message))
            {
                error = message;
                return false;
            }

            return true;
        }

        static bool TryRenameSceneObject(PreviewEntry entry, out string error)
        {
            error = null;
            if (entry.Target is not GameObject go)
            {
                error = "不是场景物体";
                return false;
            }

            Undo.RecordObject(go, "Batch Rename");
            go.name = entry.NewName;
            EditorUtility.SetDirty(go);
            return true;
        }
    }
}
#endif

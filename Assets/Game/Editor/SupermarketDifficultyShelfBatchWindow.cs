#if UNITY_EDITOR
using System.Collections.Generic;
using Game.EditorTools;
using Game.SupermarketOrganize;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>批量将 Hierarchy 中的货架分配到指定难度 Setup。</summary>
    public sealed class SupermarketDifficultyShelfBatchWindow : EditorWindow
    {
        const string MenuPath = "Game/Supermarket Organize/Batch Assign Shelves To Difficulty";

        [SerializeField] SupermarketOrganizeDifficultySetup targetSetup;
        [SerializeField] List<GameObject> sourceRoots = new List<GameObject>();
        [SerializeField] bool includeChildren = true;
        [SerializeField] ShelfAssignMode assignMode = ShelfAssignMode.Merge;

        List<SupermarketContainerZone> _previewZones = new List<SupermarketContainerZone>();
        Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<SupermarketDifficultyShelfBatchWindow>("难度货架批量分配");
            window.minSize = new Vector2(460f, 520f);
            window.targetSetup ??= SupermarketDifficultyShelfAssignUtility.ResolveTargetSetup();
            window.Show();
        }

        public static void OpenWithSetup(SupermarketOrganizeDifficultySetup setup)
        {
            var window = GetWindow<SupermarketDifficultyShelfBatchWindow>("难度货架批量分配");
            window.minSize = new Vector2(460f, 520f);
            window.targetSetup = setup;
            window.Show();
        }

        [MenuItem("GameObject/Supermarket Organize/Add Selected Shelves To Difficulty Setup", false, 49)]
        static void AddSelectionToActiveSetupMenu()
        {
            var setup = SupermarketDifficultyShelfAssignUtility.ResolveTargetSetup();
            if (setup == null)
            {
                EditorUtility.DisplayDialog(
                    "难度货架分配",
                    "场景中未找到 SupermarketOrganizeDifficultySetup。\n请先选中 Setup 物体或打开批量窗口指定目标。",
                    "确定");
                return;
            }

            var zones = SupermarketDifficultyShelfAssignUtility.CollectZonesFromSelection(true);
            if (zones.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "难度货架分配",
                    "当前选中未包含 SupermarketContainerZone。",
                    "确定");
                return;
            }

            var count = SupermarketDifficultyShelfAssignUtility.AssignShelves(setup, zones, ShelfAssignMode.Merge);
            Debug.Log($"[DifficultySetup] 已将 {zones.Count} 个货架合并到 {setup.name}，当前共 {count} 个。");
        }

        [MenuItem("GameObject/Supermarket Organize/Add Selected Shelves To Difficulty Setup", true)]
        static bool AddSelectionToActiveSetupMenuValidate() => Selection.gameObjects.Length > 0;

        void OnGUI()
        {
            EditorGUILayout.LabelField("难度货架批量分配", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. 指定目标 Difficulty Setup\n" +
                "2. 在 Hierarchy 选中 Containers 父节点或多个 Container，点击「从 Hierarchy 选中扫描」\n" +
                "3. 选择合并/替换后应用",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            targetSetup = (SupermarketOrganizeDifficultySetup)EditorGUILayout.ObjectField(
                "目标 Setup",
                targetSetup,
                typeof(SupermarketOrganizeDifficultySetup),
                true);

            assignMode = (ShelfAssignMode)EditorGUILayout.EnumPopup("写入模式", assignMode);
            includeChildren = EditorGUILayout.Toggle("包含子物体中的货架", includeChildren);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("来源物体（可选）", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从 Hierarchy 选中扫描", GUILayout.Height(26f)))
                    ScanFromHierarchySelection();

                if (GUILayout.Button("清空来源列表", GUILayout.Height(26f)))
                {
                    sourceRoots.Clear();
                    _previewZones.Clear();
                }
            }

            DrawSourceRootList();
            DrawPreview();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(targetSetup == null || _previewZones.Count == 0))
            {
                if (GUILayout.Button("应用到目标 Setup", GUILayout.Height(32f)))
                    ApplyToSetup();
            }

            if (targetSetup != null)
            {
                targetSetup.ComputeCounts(out var shelfCount, out var itemCount);
                EditorGUILayout.LabelField("目标当前", $"货架 {shelfCount} 个，商品 {itemCount} 个");
            }
        }

        void DrawSourceRootList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(180f));
            for (var i = sourceRoots.Count - 1; i >= 0; i--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    sourceRoots[i] = (GameObject)EditorGUILayout.ObjectField(sourceRoots[i], typeof(GameObject), true);
                    if (GUILayout.Button("×", GUILayout.Width(22f)))
                        sourceRoots.RemoveAt(i);
                }
            }

            EditorGUILayout.EndScrollView();

            var drop = EditorGUILayout.ObjectField("拖入父节点", null, typeof(GameObject), true);
            if (drop is GameObject go && !sourceRoots.Contains(go))
            {
                sourceRoots.Add(go);
                RefreshPreview();
            }
        }

        void DrawPreview()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("待写入货架预览", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("数量", _previewZones.Count.ToString());

            if (_previewZones.Count == 0)
                return;

            var showMax = Mathf.Min(_previewZones.Count, 8);
            for (var i = 0; i < showMax; i++)
                EditorGUILayout.ObjectField(_previewZones[i], typeof(SupermarketContainerZone), true);

            if (_previewZones.Count > showMax)
                EditorGUILayout.LabelField("...", $"+ {_previewZones.Count - showMax} 更多");
        }

        void ScanFromHierarchySelection()
        {
            var selected = new List<GameObject>(Selection.gameObjects);
            for (var i = 0; i < selected.Count; i++)
            {
                if (selected[i] != null && !sourceRoots.Contains(selected[i]))
                    sourceRoots.Add(selected[i]);
            }

            RefreshPreview();
        }

        void RefreshPreview()
        {
            _previewZones = SupermarketDifficultyShelfAssignUtility.CollectZonesFromGameObjects(
                sourceRoots,
                includeChildren);
        }

        void ApplyToSetup()
        {
            if (targetSetup == null)
                return;

            RefreshPreview();
            if (_previewZones.Count == 0)
            {
                EditorUtility.DisplayDialog("难度货架分配", "没有可写入的货架。", "确定");
                return;
            }

            var count = SupermarketDifficultyShelfAssignUtility.AssignShelves(
                targetSetup,
                _previewZones,
                assignMode);

            EditorUtility.DisplayDialog(
                "难度货架分配",
                $"已写入 {targetSetup.name}\n当前货架总数：{count}",
                "确定");
        }
    }
}
#endif

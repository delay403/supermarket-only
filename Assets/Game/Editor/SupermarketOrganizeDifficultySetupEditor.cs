#if UNITY_EDITOR
using Game.EditorTools;
using Game.SupermarketOrganize;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(SupermarketOrganizeDifficultySetup))]
    public sealed class SupermarketOrganizeDifficultySetupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var setup = (SupermarketOrganizeDifficultySetup)target;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("货架批量分配", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在 Hierarchy 中选中 Container 或父节点（如 Containers），点击下方按钮合并/替换到本 Setup。\n" +
                "写入后会自动关闭 Auto Collect Shelves From Map Roots，避免覆盖手动列表。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("合并当前选中货架", GUILayout.Height(28f)))
                    AssignFromSelection(setup, ShelfAssignMode.Merge, true);

                if (GUILayout.Button("替换为当前选中", GUILayout.Height(28f)))
                    AssignFromSelection(setup, ShelfAssignMode.Replace, true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("仅合并选中物体自身", GUILayout.Height(24f)))
                    AssignFromSelection(setup, ShelfAssignMode.Merge, false);

                if (GUILayout.Button("清空货架列表", GUILayout.Height(24f)))
                {
                    SupermarketDifficultyShelfAssignUtility.AssignShelves(setup, null, ShelfAssignMode.Clear);
                    Debug.Log($"[DifficultySetup] 已清空 {setup.name} 的货架列表。");
                }
            }

            if (GUILayout.Button("将当前选中加入 Map Roots", GUILayout.Height(24f)))
            {
                var roots = new System.Collections.Generic.List<GameObject>(Selection.gameObjects);
                var count = SupermarketDifficultyShelfAssignUtility.AppendMapRoots(setup, roots, false);
                Debug.Log($"[DifficultySetup] {setup.name} Map Roots 现有 {count} 项。");
            }

            if (GUILayout.Button("打开批量分配窗口", GUILayout.Height(24f)))
                SupermarketDifficultyShelfBatchWindow.OpenWithSetup(setup);

            setup.ComputeCounts(out var shelfCount, out var itemCount);
            EditorGUILayout.LabelField("当前统计", $"货架 {shelfCount} 个，商品 {itemCount} 个");
        }

        static void AssignFromSelection(
            SupermarketOrganizeDifficultySetup setup,
            ShelfAssignMode mode,
            bool includeChildren)
        {
            var zones = SupermarketDifficultyShelfAssignUtility.CollectZonesFromSelection(includeChildren);
            if (zones.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "难度货架分配",
                    "未找到 SupermarketContainerZone。\n请在 Hierarchy 中选中 Container 或其父节点。",
                    "确定");
                return;
            }

            var count = SupermarketDifficultyShelfAssignUtility.AssignShelves(setup, zones, mode);
            Debug.Log(
                $"[DifficultySetup] {setup.name} 货架列表已更新：{count} 个（本次扫描 {zones.Count} 个，模式 {mode}）。");
        }
    }
}
#endif

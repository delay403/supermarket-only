#if UNITY_EDITOR
using System.Collections.Generic;
using Game.SupermarketOrganize;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 批量清理货架 Container 下的放置物品子物体，保留 GridOrigin / TipObject（含 TipObject 子树）。
    /// </summary>
    public static class SupermarketContainerChildCleanupUtility
    {
        const string MenuPath = "GameObject/Supermarket Organize/Clean Container Children";
        const string MenuPathAlt = "Game/Supermarket Organize/Clean Container Children";

        static readonly HashSet<string> PreservedDirectChildNames = new HashSet<string>
        {
            "GridOrigin",
            "TipObject",
            "PlacePreview",
        };

        static bool _isCleaning;

        [MenuItem(MenuPath, false, 51)]
        [MenuItem(MenuPathAlt, false, 51)]
        static void CleanFromMenu()
        {
            CleanSelectedContainers(showDialog: true);
        }

        [MenuItem(MenuPath, true)]
        [MenuItem(MenuPathAlt, true)]
        static bool ValidateCleanFromMenu()
        {
            return CountDeletableTargets(Selection.gameObjects) > 0;
        }

        public static int CleanSelectedContainers(bool showDialog)
        {
            if (_isCleaning)
                return 0;

            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("清理 Container 子物体", "请先在 Hierarchy 中选中 Container 或其下的物品。", "确定");

                return 0;
            }

            var targets = CollectDeleteTargets(selected);
            if (targets.Count == 0)
            {
                Debug.LogWarning(
                    "[Supermarket] 未找到可删除的子物体。请选中 Container（含 GridOrigin / TipObject 的物体），" +
                    "或其下的物品实例（如 6-a_1）。");
                return 0;
            }

            if (showDialog)
            {
                var message =
                    $"将删除 {targets.Count} 个子物体（保留 GridOrigin / TipObject 及其子物体）。\n\n" +
                    "此操作可通过 Ctrl+Z 撤销。";
                if (!EditorUtility.DisplayDialog("清理 Container 子物体", message, "删除", "取消"))
                    return 0;
            }

            _isCleaning = true;
            try
            {
                Undo.SetCurrentGroupName("Clean Container Children");
                var undoGroup = Undo.GetCurrentGroup();

                for (var i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != null)
                        Undo.DestroyObjectImmediate(targets[i]);
                }

                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log($"[Supermarket] 已删除 {targets.Count} 个 Container 子物体。");
                return targets.Count;
            }
            finally
            {
                _isCleaning = false;
            }
        }

        static int CountDeletableTargets(IReadOnlyList<GameObject> selected) =>
            CollectDeleteTargets(selected).Count;

        static List<GameObject> CollectDeleteTargets(IReadOnlyList<GameObject> selected)
        {
            var result = new List<GameObject>();
            var unique = new HashSet<int>();
            if (selected == null || selected.Count == 0)
                return result;

            var containerRoots = CollectContainerRoots(selected);
            foreach (var root in containerRoots)
            {
                if (root == null)
                    continue;

                AddDeletableDirectChildren(root, result, unique);
            }

            for (var i = 0; i < selected.Count; i++)
            {
                var selectedObject = selected[i];
                if (selectedObject == null || ShouldPreserveDirectChild(selectedObject.name))
                    continue;

                var parent = selectedObject.transform.parent;
                if (parent == null || !IsContainerRoot(parent))
                    continue;

                TryAddTarget(selectedObject, result, unique);
            }

            return result;
        }

        static HashSet<Transform> CollectContainerRoots(IReadOnlyList<GameObject> selected)
        {
            var roots = new HashSet<Transform>();
            for (var i = 0; i < selected.Count; i++)
            {
                var selectedObject = selected[i];
                if (selectedObject == null)
                    continue;

                if (IsContainerRoot(selectedObject.transform))
                    roots.Add(selectedObject.transform);

                var zones = selectedObject.GetComponentsInChildren<SupermarketContainerZone>(true);
                for (var z = 0; z < zones.Length; z++)
                {
                    if (zones[z] != null)
                        roots.Add(zones[z].transform);
                }
            }

            return roots;
        }

        static void AddDeletableDirectChildren(Transform root, List<GameObject> result, HashSet<int> unique)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null || ShouldPreserveDirectChild(child.name))
                    continue;

                TryAddTarget(child.gameObject, result, unique);
            }
        }

        static void TryAddTarget(GameObject target, List<GameObject> result, HashSet<int> unique)
        {
            if (target == null)
                return;

            var id = target.GetInstanceID();
            if (!unique.Add(id))
                return;

            result.Add(target);
        }

        static bool IsContainerRoot(Transform transform)
        {
            if (transform == null)
                return false;

            if (transform.GetComponent<SupermarketContainerZone>() != null)
                return true;

            return transform.Find("GridOrigin") != null || transform.Find("TipObject") != null;
        }

        static bool ShouldPreserveDirectChild(string childName)
        {
            if (string.IsNullOrWhiteSpace(childName))
                return false;

            return PreservedDirectChildNames.Contains(childName.Trim());
        }
    }
}
#endif

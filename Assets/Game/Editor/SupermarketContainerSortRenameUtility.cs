#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Game.SupermarketOrganize;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 货架 targetItemId 与物体名称的双向同步：按 ID 重命名排序，或按名称写回 ID。
    /// </summary>
    public static class SupermarketContainerSortRenameUtility
    {
        const string MenuPath = "GameObject/Supermarket Organize/Sort And Rename Containers By Target Item Id";
        const string MenuPathAlt = "Game/Supermarket Organize/Sort And Rename Containers By Target Item Id";
        const string SetIdMenuPath = "GameObject/Supermarket Organize/Set Target Item Id From Object Name";
        const string SetIdMenuPathAlt = "Game/Supermarket Organize/Set Target Item Id From Object Name";

        [MenuItem(MenuPath, false, 52)]
        [MenuItem(MenuPathAlt, false, 52)]
        static void SortAndRenameFromMenu()
        {
            SortAndRenameSelected(showDialog: true);
        }

        [MenuItem(MenuPath, true)]
        [MenuItem(MenuPathAlt, true)]
        static bool ValidateSortAndRenameFromMenu()
        {
            return CollectZones(Selection.gameObjects).Count > 0;
        }

        [MenuItem(SetIdMenuPath, false, 53)]
        [MenuItem(SetIdMenuPathAlt, false, 53)]
        static void SetTargetItemIdFromNameMenu()
        {
            SetTargetItemIdFromNamesSelected(showDialog: true);
        }

        [MenuItem(SetIdMenuPath, true)]
        [MenuItem(SetIdMenuPathAlt, true)]
        static bool ValidateSetTargetItemIdFromNameMenu()
        {
            return CollectZones(Selection.gameObjects).Count > 0;
        }

        public static int SortAndRenameSelected(bool showDialog)
        {
            var zones = CollectZones(Selection.gameObjects);
            if (zones.Count == 0)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "货架排序重命名",
                        "未找到 SupermarketContainerZone。\n请选中 Containers 父节点，或一个/多个 Container。",
                        "确定");
                }

                return 0;
            }

            var invalid = zones.Where(zone => string.IsNullOrWhiteSpace(zone.TargetItemId)).ToList();
            if (invalid.Count > 0)
            {
                Debug.LogWarning(
                    $"[Supermarket] 有 {invalid.Count} 个货架缺少 targetItemId，将排在同父节点末尾且不重命名。");
            }

            var duplicateIds = FindDuplicateTargetItemIds(zones);
            if (duplicateIds.Count > 0 && showDialog)
            {
                var message =
                    "检测到重复的 targetItemId：\n" +
                    string.Join("\n", duplicateIds.Take(8)) +
                    (duplicateIds.Count > 8 ? "\n..." : string.Empty) +
                    "\n\n仍要继续吗？重复 ID 可能导致物品无法全部收集。";
                if (!EditorUtility.DisplayDialog("货架排序重命名", message, "继续", "取消"))
                    return 0;
            }

            Undo.SetCurrentGroupName("Sort And Rename Containers");
            var undoGroup = Undo.GetCurrentGroup();

            var renamed = 0;
            var groups = GroupByParent(zones);
            foreach (var pair in groups)
            {
                var parent = pair.Key;
                var groupZones = pair.Value;
                groupZones.Sort(CompareZonesByTargetItemId);

                for (var i = 0; i < groupZones.Count; i++)
                {
                    var zone = groupZones[i];
                    if (zone == null)
                        continue;

                    var itemId = zone.TargetItemId?.Trim();
                    if (!string.IsNullOrWhiteSpace(itemId))
                    {
                        Undo.RecordObject(zone.gameObject, "Rename Container");
                        zone.gameObject.name = itemId;
                        renamed++;
                    }

                    if (zone.transform.parent != parent && parent != null)
                    {
                        Undo.SetTransformParent(zone.transform, parent, "Reparent Container");
                    }

                    zone.transform.SetSiblingIndex(i);
                    EditorUtility.SetDirty(zone.gameObject);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[Supermarket] 已按 targetItemId 排序 {zones.Count} 个货架，重命名 {renamed} 个。");
            return renamed;
        }

        public static int SetTargetItemIdFromNamesSelected(bool showDialog)
        {
            var zones = CollectZones(Selection.gameObjects);
            if (zones.Count == 0)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "写回 targetItemId",
                        "未找到 SupermarketContainerZone。\n请选中 Containers 父节点，或一个/多个 Container。",
                        "确定");
                }

                return 0;
            }

            var invalid = zones.Where(zone => zone != null && string.IsNullOrWhiteSpace(zone.gameObject.name)).ToList();
            if (invalid.Count > 0)
            {
                Debug.LogWarning(
                    $"[Supermarket] 有 {invalid.Count} 个货架物体名称为空，将跳过写回 targetItemId。");
            }

            var duplicateNames = FindDuplicateObjectNames(zones);
            if (duplicateNames.Count > 0 && showDialog)
            {
                var message =
                    "检测到重复的物体名称：\n" +
                    string.Join("\n", duplicateNames.Take(8)) +
                    (duplicateNames.Count > 8 ? "\n..." : string.Empty) +
                    "\n\n仍要继续吗？重复名称会导致多个货架使用相同 targetItemId。";
                if (!EditorUtility.DisplayDialog("写回 targetItemId", message, "继续", "取消"))
                    return 0;
            }

            Undo.SetCurrentGroupName("Set Target Item Id From Names");
            var undoGroup = Undo.GetCurrentGroup();

            var updated = 0;
            var skipped = 0;
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null)
                    continue;

                var objectName = zone.gameObject.name?.Trim();
                if (string.IsNullOrWhiteSpace(objectName))
                {
                    skipped++;
                    continue;
                }

                if (string.Equals(zone.TargetItemId?.Trim(), objectName, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                if (TrySetTargetItemId(zone, objectName))
                    updated++;
                else
                    skipped++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[Supermarket] 已从物体名称写回 targetItemId：更新 {updated} 个，跳过 {skipped} 个。");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "写回 targetItemId",
                    $"已处理 {zones.Count} 个货架。\n更新 {updated} 个，跳过 {skipped} 个。",
                    "确定");
            }

            return updated;
        }

        static bool TrySetTargetItemId(SupermarketContainerZone zone, string itemId)
        {
            if (zone == null)
                return false;

            var serialized = new SerializedObject(zone);
            var property = serialized.FindProperty("targetItemId");
            if (property == null)
            {
                Debug.LogWarning($"[Supermarket] 无法在 {zone.name} 上找到 targetItemId 字段。");
                return false;
            }

            Undo.RecordObject(zone, "Set Target Item Id");
            property.stringValue = itemId?.Trim() ?? string.Empty;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(zone);
            return true;
        }

        static List<SupermarketContainerZone> CollectZones(IReadOnlyList<GameObject> selected)
        {
            var result = new List<SupermarketContainerZone>();
            var unique = new HashSet<int>();
            if (selected == null || selected.Count == 0)
                return result;

            for (var i = 0; i < selected.Count; i++)
            {
                var selectedObject = selected[i];
                if (selectedObject == null)
                    continue;

                if (selectedObject.TryGetComponent<SupermarketContainerZone>(out var self)
                    && unique.Add(self.GetInstanceID()))
                {
                    result.Add(self);
                }

                var children = selectedObject.GetComponentsInChildren<SupermarketContainerZone>(true);
                for (var c = 0; c < children.Length; c++)
                {
                    var zone = children[c];
                    if (zone != null && unique.Add(zone.GetInstanceID()))
                        result.Add(zone);
                }
            }

            return result;
        }

        static Dictionary<Transform, List<SupermarketContainerZone>> GroupByParent(List<SupermarketContainerZone> zones)
        {
            var groups = new Dictionary<Transform, List<SupermarketContainerZone>>();
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null)
                    continue;

                var parent = zone.transform.parent;
                if (!groups.TryGetValue(parent, out var list))
                {
                    list = new List<SupermarketContainerZone>();
                    groups.Add(parent, list);
                }

                list.Add(zone);
            }

            return groups;
        }

        static int CompareZonesByTargetItemId(SupermarketContainerZone left, SupermarketContainerZone right)
        {
            var leftId = left != null ? left.TargetItemId : null;
            var rightId = right != null ? right.TargetItemId : null;

            var leftValid = !string.IsNullOrWhiteSpace(leftId);
            var rightValid = !string.IsNullOrWhiteSpace(rightId);
            if (leftValid != rightValid)
                return leftValid ? -1 : 1;

            if (!leftValid && !rightValid)
                return BatchRenameSequenceUtility.CompareNatural(left?.name, right?.name);

            var compare = BatchRenameSequenceUtility.CompareNatural(leftId.Trim(), rightId.Trim());
            if (compare != 0)
                return compare;

            return BatchRenameSequenceUtility.CompareNatural(left?.name, right?.name);
        }

        static List<string> FindDuplicateTargetItemIds(IReadOnlyList<SupermarketContainerZone> zones)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null || string.IsNullOrWhiteSpace(zone.TargetItemId))
                    continue;

                var id = zone.TargetItemId.Trim();
                counts.TryGetValue(id, out var count);
                counts[id] = count + 1;
            }

            return counts
                .Where(pair => pair.Value > 1)
                .Select(pair => $"{pair.Key} ×{pair.Value}")
                .OrderBy(text => text, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static List<string> FindDuplicateObjectNames(IReadOnlyList<SupermarketContainerZone> zones)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null || string.IsNullOrWhiteSpace(zone.gameObject.name))
                    continue;

                var name = zone.gameObject.name.Trim();
                counts.TryGetValue(name, out var count);
                counts[name] = count + 1;
            }

            return counts
                .Where(pair => pair.Value > 1)
                .Select(pair => $"{pair.Key} ×{pair.Value}")
                .OrderBy(text => text, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
#endif

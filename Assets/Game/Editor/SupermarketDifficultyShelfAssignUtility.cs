#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.SupermarketOrganize;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>批量向 <see cref="SupermarketOrganizeDifficultySetup"/> 写入货架列表。</summary>
    public static class SupermarketDifficultyShelfAssignUtility
    {
        public static List<SupermarketContainerZone> CollectZonesFromGameObjects(
            IEnumerable<GameObject> roots,
            bool includeChildren)
        {
            var set = new HashSet<SupermarketContainerZone>();
            if (roots == null)
                return new List<SupermarketContainerZone>();

            foreach (var root in roots)
            {
                if (root == null)
                    continue;

                if (root.TryGetComponent<SupermarketContainerZone>(out var self))
                    set.Add(self);

                if (!includeChildren)
                    continue;

                var zones = root.GetComponentsInChildren<SupermarketContainerZone>(true);
                for (var i = 0; i < zones.Length; i++)
                {
                    if (zones[i] != null)
                        set.Add(zones[i]);
                }
            }

            return SortZones(set);
        }

        public static List<SupermarketContainerZone> CollectZonesFromSelection(bool includeChildren = true)
        {
            return CollectZonesFromGameObjects(Selection.gameObjects, includeChildren);
        }

        public static int AssignShelves(
            SupermarketOrganizeDifficultySetup setup,
            IReadOnlyList<SupermarketContainerZone> zones,
            ShelfAssignMode mode)
        {
            if (setup == null)
                return 0;

            Undo.RecordObject(setup, "Assign Difficulty Shelves");
            var so = new SerializedObject(setup);
            var shelvesProp = so.FindProperty("shelves");
            if (shelvesProp == null)
                return 0;

            var merged = mode == ShelfAssignMode.Replace
                ? new HashSet<SupermarketContainerZone>()
                : ReadExistingShelves(shelvesProp);

            if (zones != null)
            {
                for (var i = 0; i < zones.Count; i++)
                {
                    if (zones[i] != null)
                        merged.Add(zones[i]);
                }
            }

            if (mode == ShelfAssignMode.Clear)
            {
                shelvesProp.arraySize = 0;
            }
            else
            {
                var sorted = SortZones(merged);
                shelvesProp.arraySize = sorted.Count;
                for (var i = 0; i < sorted.Count; i++)
                    shelvesProp.GetArrayElementAtIndex(i).objectReferenceValue = sorted[i];
            }

            var autoCollectProp = so.FindProperty("autoCollectShelvesFromMapRoots");
            if (autoCollectProp != null && mode != ShelfAssignMode.Clear)
                autoCollectProp.boolValue = false;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(setup);
            MarkActiveSceneDirty();
            return shelvesProp.arraySize;
        }

        public static int AppendMapRoots(
            SupermarketOrganizeDifficultySetup setup,
            IReadOnlyList<GameObject> roots,
            bool replace = false)
        {
            if (setup == null || roots == null || roots.Count == 0)
                return 0;

            Undo.RecordObject(setup, "Assign Difficulty Map Roots");
            var so = new SerializedObject(setup);
            var mapRootsProp = so.FindProperty("mapRoots");
            if (mapRootsProp == null)
                return 0;

            var merged = replace
                ? new List<GameObject>()
                : ReadExistingMapRoots(mapRootsProp);

            var set = new HashSet<GameObject>(merged);
            for (var i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null)
                    set.Add(roots[i]);
            }

            var list = new List<GameObject>(set);
            list.Sort((a, b) => string.Compare(a != null ? a.name : string.Empty,
                b != null ? b.name : string.Empty,
                StringComparison.Ordinal));

            mapRootsProp.arraySize = list.Count;
            for (var i = 0; i < list.Count; i++)
                mapRootsProp.GetArrayElementAtIndex(i).objectReferenceValue = list[i];

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(setup);
            MarkActiveSceneDirty();
            return mapRootsProp.arraySize;
        }

        public static SupermarketOrganizeDifficultySetup ResolveTargetSetup()
        {
            if (Selection.activeGameObject != null)
            {
                var selected = Selection.activeGameObject.GetComponent<SupermarketOrganizeDifficultySetup>();
                if (selected != null)
                    return selected;

                selected = Selection.activeGameObject.GetComponentInParent<SupermarketOrganizeDifficultySetup>();
                if (selected != null)
                    return selected;
            }

#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<SupermarketOrganizeDifficultySetup>(
                FindObjectsInactive.Include);
#else
            return UnityEngine.Object.FindObjectOfType<SupermarketOrganizeDifficultySetup>(true);
#endif
        }

        static HashSet<SupermarketContainerZone> ReadExistingShelves(SerializedProperty shelvesProp)
        {
            var merged = new HashSet<SupermarketContainerZone>();
            for (var i = 0; i < shelvesProp.arraySize; i++)
            {
                if (shelvesProp.GetArrayElementAtIndex(i).objectReferenceValue is SupermarketContainerZone zone)
                    merged.Add(zone);
            }

            return merged;
        }

        static List<GameObject> ReadExistingMapRoots(SerializedProperty mapRootsProp)
        {
            var list = new List<GameObject>();
            for (var i = 0; i < mapRootsProp.arraySize; i++)
            {
                if (mapRootsProp.GetArrayElementAtIndex(i).objectReferenceValue is GameObject root)
                    list.Add(root);
            }

            return list;
        }

        static List<SupermarketContainerZone> SortZones(IEnumerable<SupermarketContainerZone> zones)
        {
            var list = new List<SupermarketContainerZone>();
            foreach (var zone in zones)
            {
                if (zone != null)
                    list.Add(zone);
            }

            list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            return list;
        }

        static void MarkActiveSceneDirty()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    public enum ShelfAssignMode
    {
        Merge,
        Replace,
        Clear,
    }
}
#endif

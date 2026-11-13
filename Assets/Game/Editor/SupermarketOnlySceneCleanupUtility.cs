#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class SupermarketOnlySceneCleanupUtility
    {
        const string ScenePath = "Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity";

        public static void CleanBatch()
        {
            var removedComponents = 0;
            var removedMissingScripts = 0;
            var changedPrefabs = 0;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                CleanupHierarchy(root, ref removedComponents, ref removedMissingScripts);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            foreach (var dependency in AssetDatabase.GetDependencies(ScenePath, true))
            {
                if (!dependency.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                var prefabRoot = PrefabUtility.LoadPrefabContents(dependency);
                var beforeComponents = removedComponents;
                var beforeMissing = removedMissingScripts;
                try
                {
                    CleanupHierarchy(prefabRoot, ref removedComponents, ref removedMissingScripts);
                    if (beforeComponents != removedComponents || beforeMissing != removedMissingScripts)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, dependency);
                        changedPrefabs++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SupermarketOnlySceneCleanupUtility] Removed {removedComponents} legacy components, " +
                      $"{removedMissingScripts} missing scripts; changed {changedPrefabs} prefabs.");
        }

        static void CleanupHierarchy(GameObject gameObject, ref int removedComponents, ref int removedMissingScripts)
        {
            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (IsRemovedModeComponent(component))
                {
                    UnityEngine.Object.DestroyImmediate(component, true);
                    removedComponents++;
                }
            }

            removedMissingScripts += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
            for (var i = 0; i < gameObject.transform.childCount; i++)
                CleanupHierarchy(gameObject.transform.GetChild(i).gameObject, ref removedComponents, ref removedMissingScripts);
        }

        static bool IsRemovedModeComponent(Component component)
        {
            if (component == null || component is Transform)
                return false;

            var ns = component.GetType().Namespace ?? string.Empty;
            return ns.StartsWith("Game.Tower", StringComparison.Ordinal)
                   || ns.StartsWith("Game.Fishing", StringComparison.Ordinal)
                   || ns.StartsWith("Game.DestroyCity", StringComparison.Ordinal)
                   || ns.StartsWith("Game.WormDodge", StringComparison.Ordinal)
                   || ns.StartsWith("Game.EscapeSnake", StringComparison.Ordinal)
                   || ns.StartsWith("Game.AnimalHospital", StringComparison.Ordinal);
        }
    }
}
#endif

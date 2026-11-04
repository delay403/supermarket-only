#if UNITY_EDITOR
using System.Collections.Generic;
using FairyGUI;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 将 Build Settings 场景中的 Bootstrapper / FGUI 引用持久同步到 GameProductSettings（写回场景文件）。
    /// </summary>
    public static class GameProductSceneReferenceSyncUtility
    {
        [MenuItem("Game/Product/Sync Scene References To Product Settings")]
        public static void SyncSceneReferencesMenu()
        {
            var settings = GameProductFusionBuildTools.LoadProductSettingsAsset();
            if (settings == null)
            {
                EditorUtility.DisplayDialog("场景引用同步", "未找到 GameProductSettings.asset。", "确定");
                return;
            }

            var changedScenes = 0;
            var modules = GameProductBuildReferenceRules.ResolveModules(settings);
            var allowedVfx = GameProductBuildReferenceRules.ResolveAllowedVfxKinds(modules);
            var allowedSfx = GameProductBuildReferenceRules.ResolveAllowedSfxIds(modules, settings);

            var scenePaths = CollectEnabledBuildScenePaths();
            var originalScene = EditorSceneManager.GetActiveScene().path;

            for (var i = 0; i < scenePaths.Count; i++)
            {
                var scene = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
                var changed = false;

                var bootstrappers = Object.FindObjectsOfType<GameBootstrapper>(true);
                for (var b = 0; b < bootstrappers.Length; b++)
                {
                    if (SyncBootstrapper(bootstrappers[b], modules, settings, allowedVfx, allowedSfx))
                        changed = true;
                }

                var panels = Object.FindObjectsOfType<UIPanel>(true);
                for (var p = 0; p < panels.Length; p++)
                {
                    if (SyncFguiPanel(panels[p], settings, modules))
                        changed = true;
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    changedScenes++;
                }
            }

            if (!string.IsNullOrEmpty(originalScene) && System.IO.File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

            EditorUtility.DisplayDialog(
                "场景引用同步",
                changedScenes > 0
                    ? $"已同步 {changedScenes} 个场景。请重新打包并查看 Build Report。"
                    : "场景引用已与当前 GameProductSettings 一致，无需修改。",
                "确定");
        }

        static List<string> CollectEnabledBuildScenePaths()
        {
            var result = new List<string>();
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled && !string.IsNullOrEmpty(scenes[i].path))
                    result.Add(scenes[i].path);
            }

            return result;
        }

        static bool SyncBootstrapper(
            GameBootstrapper bootstrapper,
            GameplayModule modules,
            GameProductSettings settings,
            HashSet<int> allowedVfx,
            HashSet<int> allowedSfx)
        {
            if (bootstrapper == null)
                return false;

            var so = new SerializedObject(bootstrapper);
            so.Update();
            var changed = false;

            foreach (var fieldName in GameProductBuildReferenceRules.EnumerateBootstrapperOverrideFields(modules, settings))
            {
                var prop = so.FindProperty(fieldName);
                if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                if (prop.objectReferenceValue != null)
                {
                    prop.objectReferenceValue = null;
                    changed = true;
                }
            }

            if (FilterBindingArray(so.FindProperty("bootstrapVfxBindings"), "kind", allowedVfx))
                changed = true;
            if (FilterBindingArray(so.FindProperty("bootstrapSfxLibrary"), "id", allowedSfx))
                changed = true;

            if (!changed)
                return false;

            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static bool FilterBindingArray(SerializedProperty arrayProp, string kindFieldName, HashSet<int> allowed)
        {
            if (arrayProp == null || !arrayProp.isArray || allowed == null)
                return false;

            var changed = false;
            for (var i = arrayProp.arraySize - 1; i >= 0; i--)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                var kindProp = element.FindPropertyRelative(kindFieldName);
                if (kindProp == null || allowed.Contains(kindProp.intValue))
                    continue;

                arrayProp.DeleteArrayElementAtIndex(i);
                changed = true;
            }

            return changed;
        }

        static bool SyncFguiPanel(UIPanel panel, GameProductSettings settings, GameplayModule modules)
        {
            if (panel == null)
                return false;

            if (!GameProductBuildReferenceRules.ShouldStripFguiPackage(panel.packageName, settings, modules))
                return false;

            var so = new SerializedObject(panel);
            so.Update();
            so.FindProperty("packageName").stringValue = string.Empty;
            so.FindProperty("packagePath").stringValue = string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.enabled = false;
            if (panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(false);
                return true;
            }

            return true;
        }
    }
}
#endif

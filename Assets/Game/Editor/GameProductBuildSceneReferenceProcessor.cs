#if UNITY_EDITOR
using System.Collections.Generic;
using FairyGUI;
using Game.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// 打包期按 GameProductSettings 剥离场景内 Bootstrapper / FGUI 的跨模式硬引用（仅影响本次 Build，不写回磁盘）。
    /// </summary>
    public sealed class GameProductBuildSceneReferenceProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 100;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            var settings = GameProductFusionBuildTools.LoadProductSettingsAsset();
            if (settings == null)
                return;

            var modules = GameProductBuildReferenceRules.ResolveModules(settings);
            var allowedVfx = GameProductBuildReferenceRules.ResolveAllowedVfxKinds(modules);
            var allowedSfx = GameProductBuildReferenceRules.ResolveAllowedSfxIds(modules, settings);

            var bootstrappers = Object.FindObjectsOfType<GameBootstrapper>(true);
            for (var i = 0; i < bootstrappers.Length; i++)
                StripBootstrapper(bootstrappers[i], modules, settings, allowedVfx, allowedSfx);

            var panels = Object.FindObjectsOfType<UIPanel>(true);
            for (var i = 0; i < panels.Length; i++)
                StripFguiPanel(panels[i], settings, modules);
        }

        static void StripBootstrapper(
            GameBootstrapper bootstrapper,
            GameplayModule modules,
            GameProductSettings settings,
            HashSet<int> allowedVfx,
            HashSet<int> allowedSfx)
        {
            if (bootstrapper == null)
                return;

            var so = new SerializedObject(bootstrapper);
            so.Update();

            foreach (var fieldName in GameProductBuildReferenceRules.EnumerateBootstrapperOverrideFields(modules, settings))
            {
                var prop = so.FindProperty(fieldName);
                if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
                    prop.objectReferenceValue = null;
            }

            FilterBindingArray(so.FindProperty("bootstrapVfxBindings"), "kind", allowedVfx);
            FilterBindingArray(so.FindProperty("bootstrapSfxLibrary"), "id", allowedSfx);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void FilterBindingArray(SerializedProperty arrayProp, string kindFieldName, HashSet<int> allowed)
        {
            if (arrayProp == null || !arrayProp.isArray || allowed == null)
                return;

            for (var i = arrayProp.arraySize - 1; i >= 0; i--)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                var kindProp = element.FindPropertyRelative(kindFieldName);
                if (kindProp == null || !allowed.Contains(kindProp.intValue))
                    arrayProp.DeleteArrayElementAtIndex(i);
            }
        }

        static void StripFguiPanel(UIPanel panel, GameProductSettings settings, GameplayModule modules)
        {
            if (panel == null)
                return;

            if (!GameProductBuildReferenceRules.ShouldStripFguiPackage(panel.packageName, settings, modules))
                return;

            var so = new SerializedObject(panel);
            so.Update();
            so.FindProperty("packageName").stringValue = string.Empty;
            so.FindProperty("packagePath").stringValue = string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.enabled = false;
            panel.gameObject.SetActive(false);
        }
    }
}
#endif

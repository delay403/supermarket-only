using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// URP 默认 <see cref="PostProcessData"/> 会硬引用 FilmGrain / SMAA 等 Package 纹理，
    /// 「Strip Unused Post Processing Variants」只剥离 Shader 变体，不会去掉这些纹理引用。
    /// 构建前清除项目内 Renderer 上的 postProcessData，避免未使用后处理时仍被打进包。
    /// </summary>
    public sealed class UrpPostProcessBuildStripper : IPreprocessBuildWithReport
    {
        const string DefaultPostProcessDataGuid = "41439944d30ece34e96484bdb6645b55";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => StripProjectRendererPostProcessData(log: true);

        [MenuItem("Game/URP/Strip PostProcessData From Project Renderers")]
        public static void StripFromMenu() => StripProjectRendererPostProcessData(log: true);

        static void StripProjectRendererPostProcessData(bool log)
        {
            var cleared = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (renderer == null || renderer.postProcessData == null)
                    continue;

                var ppGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.postProcessData));
                renderer.postProcessData = null;
                EditorUtility.SetDirty(renderer);
                cleared.Add(path);
            }

            if (cleared.Count > 0)
            {
                AssetDatabase.SaveAssets();
                if (log)
                {
                    Debug.Log(
                        "[UrpPostProcessBuildStripper] Cleared postProcessData on renderer(s):\n- " +
                        string.Join("\n- ", cleared) +
                        "\n(Default PostProcessData guid " + DefaultPostProcessDataGuid +
                        " bundles FilmGrain/SMAA textures even when post-processing is disabled.)");
                }
            }
            else if (log)
            {
                Debug.Log("[UrpPostProcessBuildStripper] No project UniversalRendererData with postProcessData found.");
            }
        }
    }
}

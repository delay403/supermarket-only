using UnityEditor;
using UnityEngine;

namespace FairyGUIEditor
{
    /// <summary>
    /// FairyGUI atlas textures must not use mip maps (see UIPackage.LoadAtlas). Applies to published output under Resources/FGUIPublish.
    /// </summary>
    public sealed class FguiPublishTextureImporter : AssetPostprocessor
    {
        const string PublishFolderToken = "/Resources/FGUIPublish/";

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (path.IndexOf(PublishFolderToken, System.StringComparison.OrdinalIgnoreCase) < 0)
                return;

            var ti = (TextureImporter)assetImporter;
            if (ti.textureType != TextureImporterType.Default)
                ti.textureType = TextureImporterType.Default;
            if (ti.mipmapEnabled)
                ti.mipmapEnabled = false;
        }

        [MenuItem("Window/FairyGUI - Repair FGUIPublish Texture Imports")]
        static void RepairAllUnderPublishFolder()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("FairyGUI", "退出播放模式后再执行。", "OK");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/UI/FGUI/Resources/FGUIPublish" });
            int fixedCount = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Replace('\\', '/').IndexOf(PublishFolderToken, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;

                bool dirty = false;
                if (ti.textureType != TextureImporterType.Default)
                {
                    ti.textureType = TextureImporterType.Default;
                    dirty = true;
                }
                if (ti.mipmapEnabled)
                {
                    ti.mipmapEnabled = false;
                    dirty = true;
                }
                if (dirty)
                {
                    ti.SaveAndReimport();
                    fixedCount++;
                }
            }

            Debug.Log($"FairyGUI: FGUIPublish texture import repair finished. Reimported {fixedCount} texture(s).");
        }
    }
}

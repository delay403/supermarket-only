using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    static class ReskinSpriteUtility
    {
        public static string SerializeSpriteRef(Sprite sprite)
        {
            if (sprite == null)
                return null;

            return AssetDatabase.GetAssetPath(sprite) + "|" + sprite.name;
        }

        public static Sprite LoadSpriteRef(string serializedRef)
        {
            if (string.IsNullOrEmpty(serializedRef))
                return null;

            var separator = serializedRef.LastIndexOf('|');
            if (separator <= 0)
                return LoadSpriteAtPath(serializedRef);

            var assetPath = serializedRef.Substring(0, separator);
            var spriteName = serializedRef.Substring(separator + 1);
            return LoadSpriteAtPath(assetPath, spriteName);
        }

        public static Sprite LoadSpriteAtPath(string assetPath, string preferredSpriteName = null)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            Sprite matched = null;
            Sprite fallback = null;

            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (obj is not Sprite sprite)
                    continue;

                if (!string.IsNullOrEmpty(preferredSpriteName) && sprite.name == preferredSpriteName)
                    return sprite;

                matched ??= sprite;
                fallback ??= sprite;
            }

            if (matched != null)
                return matched;

            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            return direct != null ? direct : fallback;
        }

        public static Sprite LoadSpriteFromObject(Object source)
        {
            if (source == null)
                return null;

            if (source is Sprite sprite)
                return sprite;

            if (source is Texture2D texture)
                return LoadSpriteFromTexture(texture);

            return null;
        }

        public static Sprite LoadSpriteFromTexture(Texture2D texture)
        {
            if (texture == null)
                return null;

            return LoadSpriteAtPath(AssetDatabase.GetAssetPath(texture));
        }

        /// <summary>
        /// 解析用于 SceneNavigationSettings 的 Sprite：优先目标 fgui 路径，其次源贴图。
        /// </summary>
        public static Sprite ResolveSpriteForSceneNavigation(
            Object sourceObject,
            string sourceAssetPath,
            string destAssetPath,
            Sprite previousReference,
            bool copiedToDest)
        {
            var preferredName = previousReference != null
                ? previousReference.name
                : Path.GetFileNameWithoutExtension(destAssetPath ?? sourceAssetPath);

            if (copiedToDest && !string.IsNullOrEmpty(destAssetPath))
            {
                var fromDest = LoadSpriteAtPath(destAssetPath, preferredName);
                if (fromDest != null)
                    return fromDest;

                fromDest = LoadSpriteAtPath(destAssetPath);
                if (fromDest != null)
                    return fromDest;
            }

            var fromSourceObject = LoadSpriteFromObject(sourceObject);
            if (fromSourceObject != null)
                return fromSourceObject;

            if (!string.IsNullOrEmpty(sourceAssetPath))
            {
                var fromSourcePath = LoadSpriteAtPath(sourceAssetPath, preferredName);
                if (fromSourcePath != null)
                    return fromSourcePath;

                fromSourcePath = LoadSpriteAtPath(sourceAssetPath);
                if (fromSourcePath != null)
                    return fromSourcePath;
            }

            return null;
        }

        public static void EnsureSpriteImportFromSource(string sourceAssetPath, string destAssetPath)
        {
            var destImporter = AssetImporter.GetAtPath(destAssetPath) as TextureImporter;
            if (destImporter == null)
                return;

            var sourceImporter = AssetImporter.GetAtPath(sourceAssetPath) as TextureImporter;
            var sourceHasSprite = LoadSpriteAtPath(sourceAssetPath) != null;
            var shouldUseSprite = sourceImporter != null && sourceImporter.textureType == TextureImporterType.Sprite
                || sourceHasSprite
                || destImporter.spriteImportMode != SpriteImportMode.None;

            if (!shouldUseSprite)
                return;

            destImporter.textureType = TextureImporterType.Sprite;
            destImporter.spriteImportMode = SpriteImportMode.Single;

            if (sourceImporter != null)
            {
                if (sourceImporter.textureType == TextureImporterType.Sprite)
                    destImporter.spriteImportMode = sourceImporter.spriteImportMode;

                destImporter.spritePixelsToUnits = sourceImporter.spritePixelsToUnits;
                destImporter.spritePivot = sourceImporter.spritePivot;
                destImporter.alphaIsTransparency = sourceImporter.alphaIsTransparency;
                destImporter.mipmapEnabled = sourceImporter.mipmapEnabled;
                destImporter.filterMode = sourceImporter.filterMode;
                destImporter.textureCompression = sourceImporter.textureCompression;
                destImporter.maxTextureSize = sourceImporter.maxTextureSize;
            }

            destImporter.SaveAndReimport();
        }

        public static void ImportImageAsset(string assetPath, string sourceAssetPathForImportSettings)
        {
            if (!string.IsNullOrEmpty(sourceAssetPathForImportSettings))
                EnsureSpriteImportFromSource(sourceAssetPathForImportSettings, assetPath);
            else
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        public static bool TryApplySceneNavigationSprites(
            Object settingsAsset,
            bool applyTitle,
            Sprite titleSprite,
            bool applyBg,
            Sprite bgSprite,
            out string error)
        {
            error = null;
            if (settingsAsset == null)
            {
                error = "SceneNavigationSettings 未配置。";
                return false;
            }

            var serialized = new SerializedObject(settingsAsset);
            var titleProp = serialized.FindProperty("homeTitleSprite");
            var bgProp = serialized.FindProperty("homeBackgroundSprite");
            if (titleProp == null || bgProp == null)
            {
                error = "SceneNavigationSettings 缺少标题/背景 Sprite 字段。";
                return false;
            }

            if (applyTitle)
            {
                if (titleSprite == null)
                {
                    error = "无法解析标题 Sprite。请确认源 PNG 已设为 Sprite (2D and UI)，或目标 fgui 目录导入设置正确。";
                    return false;
                }

                titleProp.objectReferenceValue = titleSprite;
            }

            if (applyBg)
            {
                if (bgSprite == null)
                {
                    error = "无法解析背景 Sprite。请确认源 PNG 已设为 Sprite (2D and UI)，或目标 fgui 目录导入设置正确。";
                    return false;
                }

                bgProp.objectReferenceValue = bgSprite;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settingsAsset);
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}

#if UNITY_EDITOR
using System.IO;
using Game.Core.Content;
using Game.SupermarketOrganize.Config;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class SupermarketOrganizeConfigGenerator
    {
        const string MenuPath = "Game/Generate/Create Supermarket Organize Configs";

        [MenuItem(MenuPath)]
        static void CreateConfigs()
        {
            var root = GameContentPaths.SupermarketConfigRoot.Replace('/', Path.DirectorySeparatorChar);
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);

            var config = CreateOrLoad<SupermarketOrganizeConfig>(
                Path.Combine(root, "SupermarketOrganizeConfig.asset"));
            var itemDb = CreateOrLoad<SupermarketItemDatabase>(
                Path.Combine(root, "SupermarketItemDatabase.asset"));
            var mapDb = CreateOrLoad<SupermarketMapDatabase>(
                Path.Combine(root, "SupermarketMapDatabase.asset"));
            var progression = CreateOrLoad<SupermarketOrganizeProgressionConfig>(
                Path.Combine(root, "SupermarketOrganizeProgressionConfig.asset"));
            var upgradeUi = CreateOrLoad<SupermarketOrganizeUpgradeUiConfig>(
                Path.Combine(root, "SupermarketOrganizeUpgradeUiConfig.asset"));
            var homeConfig = CreateOrLoad<SupermarketOrganizeHomeConfig>(
                Path.Combine(root, "SupermarketOrganizeHomeConfig.asset"));
            if (upgradeUi.entries == null || upgradeUi.entries.Length == 0)
                upgradeUi.entries = SupermarketOrganizeUpgradeUiConfig.BuildDefaultEntries();
            if (homeConfig.difficulties == null || homeConfig.difficulties.Length == 0)
                homeConfig.difficulties = SupermarketOrganizeHomeConfig.BuildDefaultDifficulties();
            WireBootstrapAudioInOpenScene();

            if (mapDb.maps == null || mapDb.maps.Count == 0)
            {
                var mapDef = ScriptableObject.CreateInstance<SupermarketMapDef>();
                mapDef.mapId = "supermarket_map_1";
                mapDef.displayName = "超市收纳";
                mapDef.gameplaySceneName = "SupermarketOrganizeScene";
                AssetDatabase.CreateAsset(mapDef, Path.Combine(root, "SupermarketMapDef_Map1.asset"));
                mapDb.maps.Add(mapDef);
            }

            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(itemDb);
            EditorUtility.SetDirty(mapDb);
            EditorUtility.SetDirty(progression);
            EditorUtility.SetDirty(upgradeUi);
            EditorUtility.SetDirty(homeConfig);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnsureUrpOutlineMaterials(root);
            EditorUtility.DisplayDialog("超市收纳", $"配置已生成/更新：\n{root}", "确定");
        }

        static void WireBootstrapAudioInOpenScene()
        {
            var bootstrap = Object.FindObjectOfType<Game.Core.GameBootstrapper>();
            if (bootstrap == null)
                return;

            var so = new SerializedObject(bootstrap);
            SupermarketOrganizeBootstrapUtility.EnsureBootstrapSupermarketSfx(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        static void EnsureUrpOutlineMaterials(string resourcesRoot)
        {
            var materialsDir = Path.Combine("Assets", "QuickOutline", "Resources", "Materials");
            if (!Directory.Exists(materialsDir))
                Directory.CreateDirectory(materialsDir);

            CreateMaterialIfMissing(
                Path.Combine(materialsDir, "OutlineMaskURP.mat"),
                "Custom/Outline Mask URP");
            CreateMaterialIfMissing(
                Path.Combine(materialsDir, "OutlineFillURP.mat"),
                "Custom/Outline Fill URP");
        }

        static void CreateMaterialIfMissing(string assetPath, string shaderName)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(assetPath) != null)
                return;

            var shader = Shader.Find(shaderName);
            if (shader == null)
                return;

            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, assetPath);
        }

        static T CreateOrLoad<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }
    }
}
#endif

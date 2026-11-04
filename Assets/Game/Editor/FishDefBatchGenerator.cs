using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Game.Core.Content;
using Game.Collectibles;
using Game.Fishing.Config;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 按序号批量生成 FishDef：y1..yN 图标 + Fish1..FishN 预制体，DisplayName 为预制体名去掉 FishX 前缀。
    /// </summary>
    public sealed class FishDefBatchGenerator : EditorWindow
    {
        const string DefaultIconDir = "Assets/UI/Fish/Icon";
        const string DefaultPrefabDir = "Assets/Prefabs/鱼";
        static readonly string DefaultOutputDir = GameContentPaths.FishingOutputDir;
        static readonly string FishDatabasePath = GameContentPaths.ModeResourcesRoot(Game.Core.GameplayModule.Fishing) + "/FishDatabase.asset";

        [SerializeField] string iconDirectory = DefaultIconDir;
        [SerializeField] string prefabDirectory = DefaultPrefabDir;
        [SerializeField] string outputDirectory = DefaultOutputDir;
        [SerializeField] int fishCount = 27;
        [SerializeField] int startIndex = 1;
        [SerializeField] bool useZeroBasedAssetNames = true;
        [SerializeField] bool refreshFishDatabase = true;
        [SerializeField] long baseRewardCoins = 100;
        [SerializeField] bool assignQualityByTier = true;

        [MenuItem("Game/Fishing/Batch Generate FishDef (Icon y + Prefab Fish)")]
        public static void OpenWindow()
        {
            var w = GetWindow<FishDefBatchGenerator>(true, "FishDef 批量生成", true);
            w.minSize = new Vector2(420f, 320f);
        }

        [MenuItem("Game/Fishing/Batch Generate FishDef (Quick: 27 fish)")]
        public static void QuickGenerate27()
        {
            RunBatchGenerate(
                DefaultIconDir,
                DefaultPrefabDir,
                DefaultOutputDir,
                fishCount: 27,
                startIndex: 1,
                useZeroBasedAssetNames: true,
                refreshFishDatabase: true,
                baseRewardCoins: 100,
                assignQualityByTier: true);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("路径", EditorStyles.boldLabel);
            iconDirectory = EditorGUILayout.TextField("图标目录 (y1,y2...)", iconDirectory);
            prefabDirectory = EditorGUILayout.TextField("预制体目录 (Fish1...)", prefabDirectory);
            outputDirectory = EditorGUILayout.TextField("FishDef 输出目录", outputDirectory);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("数量与命名", EditorStyles.boldLabel);
            startIndex = EditorGUILayout.IntField("起始序号", Mathf.Max(1, startIndex));
            fishCount = EditorGUILayout.IntField("生成条数", Mathf.Max(1, fishCount));
            useZeroBasedAssetNames = EditorGUILayout.Toggle("资产名 FishDef 0..N-1", useZeroBasedAssetNames);
            baseRewardCoins = EditorGUILayout.LongField("基础金币奖励", baseRewardCoins);
            assignQualityByTier = EditorGUILayout.Toggle("按序号分档品质", assignQualityByTier);
            refreshFishDatabase = EditorGUILayout.Toggle("写入 FishDatabase.asset", refreshFishDatabase);

            EditorGUILayout.Space(12);
            if (GUILayout.Button("生成 / 更新 FishDef", GUILayout.Height(36f)))
                RunGenerate();
        }

        void RunGenerate() =>
            RunBatchGenerate(
                iconDirectory,
                prefabDirectory,
                outputDirectory,
                fishCount,
                startIndex,
                useZeroBasedAssetNames,
                refreshFishDatabase,
                baseRewardCoins,
                assignQualityByTier);

        static void RunBatchGenerate(
            string iconDir,
            string prefabDir,
            string outputDir,
            int fishCount,
            int startIndex,
            bool useZeroBasedAssetNames,
            bool refreshFishDatabase,
            long baseRewardCoins,
            bool assignQualityByTier)
        {
            if (!AssetDatabase.IsValidFolder(iconDir))
            {
                EditorUtility.DisplayDialog("FishDef", $"图标目录不存在：{iconDir}", "确定");
                return;
            }

            if (!AssetDatabase.IsValidFolder(prefabDir))
            {
                EditorUtility.DisplayDialog("FishDef", $"预制体目录不存在：{prefabDir}", "确定");
                return;
            }

            EnsureFolder(outputDir);

            var prefabIndex = BuildPrefabIndex(prefabDir);
            var created = new List<FishDef>(fishCount);
            var errors = new List<string>();

            for (var i = 0; i < fishCount; i++)
            {
                var seq = startIndex + i;
                var iconPath = $"{iconDir}/y{seq}.png";
                var icon = LoadSprite(iconPath);
                if (icon == null)
                    errors.Add($"缺少图标：{iconPath}");

                if (!prefabIndex.TryGetValue(seq, out var prefabEntry))
                {
                    errors.Add($"缺少预制体：Fish{seq}* @ {prefabDir}");
                    continue;
                }

                var displayName = ExtractDisplayNameFromPrefab(prefabEntry.fileName);
                var assetIndex = useZeroBasedAssetNames ? i : seq;
                var assetPath = $"{outputDir}/FishDef {assetIndex}.asset";

                var def = AssetDatabase.LoadAssetAtPath<FishDef>(assetPath);
                if (def == null)
                {
                    def = CreateInstance<FishDef>();
                    AssetDatabase.CreateAsset(def, assetPath);
                }

                def.fishId = $"fish_{seq}";
                def.displayName = displayName;
                def.icon = icon;
                def.worldCatchPrefab = prefabEntry.prefab;
                def.rewardCoins = baseRewardCoins * seq;
                if (assignQualityByTier)
                    def.quality = ResolveQualityForIndex(seq);

                EditorUtility.SetDirty(def);
                created.Add(def);
            }

            if (refreshFishDatabase)
                WriteFishDatabase(created);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var msg = $"已处理 {created.Count} 条 FishDef。\n输出：{outputDir}";
            if (errors.Count > 0)
                msg += "\n\n警告：\n" + string.Join("\n", errors);

            Debug.Log($"[FishDefBatchGenerator] {msg}");
            EditorUtility.DisplayDialog("FishDef 批量生成", msg, "确定");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        struct PrefabEntry
        {
            public string fileName;
            public GameObject prefab;
        }

        static Dictionary<int, PrefabEntry> BuildPrefabIndex(string directory)
        {
            var map = new Dictionary<int, PrefabEntry>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { directory });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (!TryParseFishIndex(fileName, out var index))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                map[index] = new PrefabEntry { fileName = fileName, prefab = prefab };
            }

            return map;
        }

        /// <summary>匹配 Fish1鳊鱼、Fish10红星…，避免 Fish1 误匹配 Fish10。</summary>
        static bool TryParseFishIndex(string prefabFileName, out int index)
        {
            index = 0;
            if (string.IsNullOrEmpty(prefabFileName))
                return false;

            var m = Regex.Match(prefabFileName, @"^Fish(\d+)", RegexOptions.CultureInvariant);
            if (!m.Success)
                return false;

            return int.TryParse(m.Groups[1].Value, out index) && index > 0;
        }

        static string ExtractDisplayNameFromPrefab(string prefabFileName)
        {
            var m = Regex.Match(prefabFileName, @"^Fish\d+(.+)$", RegexOptions.CultureInvariant);
            return m.Success ? m.Groups[1].Value.Trim() : prefabFileName;
        }

        static Sprite LoadSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                return sprite;

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite s)
                    return s;
            }

            return null;
        }

        static CosmeticQuality ResolveQualityForIndex(int seq)
        {
            if (seq <= 3) return CosmeticQuality.Common;
            if (seq <= 6) return CosmeticQuality.Excellent;
            if (seq <= 9) return CosmeticQuality.Rare;
            if (seq <= 15) return CosmeticQuality.Epic;
            if (seq <= 21) return CosmeticQuality.Legendary;
            return CosmeticQuality.Mythic;
        }

        static void WriteFishDatabase(List<FishDef> defs)
        {
            var db = AssetDatabase.LoadAssetAtPath<FishDatabase>(FishDatabasePath);
            if (db == null)
            {
                db = CreateInstance<FishDatabase>();
                AssetDatabase.CreateAsset(db, FishDatabasePath);
            }

            db.fish = new List<FishDef>(defs);
            EditorUtility.SetDirty(db);
        }
    }
}

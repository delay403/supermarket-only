#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core.Content;
using Game.SupermarketOrganize.Config;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 从 CSV / XLSX 导入超市物品 SupermarketItemDef，并更新已有 SO 与 ItemDatabase。
    /// 名称 → DisplayName；放置货架 → ItemId + worldPrefab。
    /// </summary>
    public sealed class SupermarketItemSheetImportWindow : EditorWindow
    {
        const string MenuPath = "Game/Supermarket Organize/Import Item Sheet";
        const string DefaultSheetPath = "Assets/配表txt.txt";
        const string FallbackSheetPath = "Assets/超市配表.xlsx";
        const string DefaultPrefabFolder = "Assets/Prefabs/超市物品";
        static readonly string DefaultItemDefFolder = GameContentPaths.SupermarketItemDefFolder;
        static readonly string DefaultDatabasePath = GameContentPaths.SupermarketItemDatabaseAssetPath;

        static readonly Regex ShelfIdPattern = new Regex(@"^\d+\s*-\s*[A-Za-z]+$", RegexOptions.Compiled);

        [SerializeField] DefaultAsset sheetFile;
        [SerializeField] DefaultAsset prefabFolder;
        [SerializeField] DefaultAsset itemDefFolder;
        [SerializeField] SupermarketItemDatabase itemDatabase;
        [SerializeField] string nameColumnHeader = "名称";
        [SerializeField] string shelfColumnHeader = "放置货架";
        [SerializeField] bool itemIdLowercase = true;
        [SerializeField] int minSpawnCount = 3;
        [SerializeField] int maxSpawnCount = 15;
        [SerializeField] bool disableShadows = true;
        [SerializeField] bool replaceDatabaseItems = true;
        [SerializeField] bool skipRowsWithoutPrefab = true;

        Vector2 _scroll;
        readonly List<ImportPreviewRow> _previewRows = new List<ImportPreviewRow>();
        readonly Dictionary<string, SupermarketItemDef> _existingDefByItemId =
            new Dictionary<string, SupermarketItemDef>(StringComparer.OrdinalIgnoreCase);

        string _statusMessage = string.Empty;
        int _resolvedNameColumn = -1;
        int _resolvedShelfColumn = -1;

        sealed class ImportPreviewRow
        {
            public string DisplayName;
            public string ShelfRaw;
            public string ItemId;
            public GameObject Prefab;
            public string PrefabPath;
            public SupermarketItemDef ExistingDef;
            public string Error;
            public bool CanImport => string.IsNullOrEmpty(Error);
        }

        [MenuItem(MenuPath)]
        [MenuItem("Game/Supermarket Organize/Import Item Sheet From Excel")]
        static void Open()
        {
            var window = GetWindow<SupermarketItemSheetImportWindow>("超市物品导表");
            window.minSize = new Vector2(700f, 640f);
            window.Show();
        }

        [MenuItem("Assets/Game/Supermarket Organize/Import Item Sheet", false, 2101)]
        static void OpenFromSelectedSheet()
        {
            var window = GetWindow<SupermarketItemSheetImportWindow>("超市物品导表");
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(path) && IsSupportedSheetPath(path))
                window.sheetFile = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);

            window.minSize = new Vector2(700f, 640f);
            window.Show();
        }

        [MenuItem("Assets/Game/Supermarket Organize/Import Item Sheet", true)]
        static bool OpenFromSelectedSheetValidate()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return IsSupportedSheetPath(path);
        }

        static bool IsSupportedSheetPath(string path) =>
            !string.IsNullOrEmpty(path)
            && (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".xls", StringComparison.OrdinalIgnoreCase));

        void OnEnable()
        {
            if (sheetFile == null)
            {
                if (File.Exists(DefaultSheetPath))
                    sheetFile = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultSheetPath);
                else if (File.Exists(FallbackSheetPath))
                    sheetFile = AssetDatabase.LoadAssetAtPath<DefaultAsset>(FallbackSheetPath);
                else if (File.Exists("Assets/超市配表.csv"))
                    sheetFile = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/超市配表.csv");
            }

            if (prefabFolder == null && AssetDatabase.IsValidFolder(DefaultPrefabFolder))
                prefabFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultPrefabFolder);

            if (itemDefFolder == null && AssetDatabase.IsValidFolder(DefaultItemDefFolder))
                itemDefFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultItemDefFolder);

            itemDatabase ??= AssetDatabase.LoadAssetAtPath<SupermarketItemDatabase>(DefaultDatabasePath);
            RebuildExistingDefLookup();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("超市物品导表", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "支持 TXT（Tab 分隔）/ CSV / XLSX（含误命名为 .csv 的 Excel 文件）。\n" +
                "名称 → DisplayName；放置货架（如 1-A）→ ItemId + Prefab 匹配。\n" +
                $"会优先更新 {GameContentPaths.SupermarketItemDefFolder} 下已有的 SupermarketItemDef 文件。",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            sheetFile = (DefaultAsset)EditorGUILayout.ObjectField("配表文件", sheetFile, typeof(DefaultAsset), false);
            prefabFolder = (DefaultAsset)EditorGUILayout.ObjectField("Prefab 目录", prefabFolder, typeof(DefaultAsset), false);
            itemDefFolder = (DefaultAsset)EditorGUILayout.ObjectField("Item SO 目录", itemDefFolder, typeof(DefaultAsset), false);
            itemDatabase = (SupermarketItemDatabase)EditorGUILayout.ObjectField(
                "Item Database",
                itemDatabase,
                typeof(SupermarketItemDatabase),
                false);

            EditorGUILayout.Space(4f);
            nameColumnHeader = EditorGUILayout.TextField("名称列标题", nameColumnHeader);
            shelfColumnHeader = EditorGUILayout.TextField("货架列标题", shelfColumnHeader);
            itemIdLowercase = EditorGUILayout.Toggle("ItemId 转小写", itemIdLowercase);
            minSpawnCount = EditorGUILayout.IntField("minSpawnCount", Mathf.Max(1, minSpawnCount));
            maxSpawnCount = EditorGUILayout.IntField("maxSpawnCount", Mathf.Max(minSpawnCount, maxSpawnCount));
            disableShadows = EditorGUILayout.Toggle("disableShadows", disableShadows);
            replaceDatabaseItems = EditorGUILayout.Toggle("导入后替换 Database 列表", replaceDatabaseItems);
            skipRowsWithoutPrefab = EditorGUILayout.Toggle("跳过找不到 Prefab 的行", skipRowsWithoutPrefab);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("读取并预览", GUILayout.Height(28f)))
                    LoadPreview();

                if (GUILayout.Button("执行导入", GUILayout.Height(28f)))
                    ImportRows();
            }

            if (!string.IsNullOrEmpty(_statusMessage))
                EditorGUILayout.HelpBox(_statusMessage, MessageType.None);

            DrawPreviewTable();
        }

        void DrawPreviewTable()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"预览（{_previewRows.Count} 行）", EditorStyles.boldLabel);
            if (_resolvedNameColumn >= 0 && _resolvedShelfColumn >= 0)
            {
                EditorGUILayout.LabelField(
                    $"列映射：名称 = 第 {_resolvedNameColumn + 1} 列，货架 = 第 {_resolvedShelfColumn + 1} 列",
                    EditorStyles.miniLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(260f));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("名称", EditorStyles.boldLabel, GUILayout.Width(150f));
                EditorGUILayout.LabelField("货架", EditorStyles.boldLabel, GUILayout.Width(56f));
                EditorGUILayout.LabelField("ItemId", EditorStyles.boldLabel, GUILayout.Width(56f));
                EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel, GUILayout.Width(140f));
                EditorGUILayout.LabelField("已有SO", EditorStyles.boldLabel, GUILayout.Width(140f));
                EditorGUILayout.LabelField("状态", EditorStyles.boldLabel);
            }

            for (var i = 0; i < _previewRows.Count; i++)
            {
                var row = _previewRows[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(row.DisplayName ?? string.Empty, GUILayout.Width(150f));
                    EditorGUILayout.LabelField(row.ShelfRaw ?? string.Empty, GUILayout.Width(56f));
                    EditorGUILayout.LabelField(row.ItemId ?? string.Empty, GUILayout.Width(56f));
                    EditorGUILayout.ObjectField(row.Prefab, typeof(GameObject), false, GUILayout.Width(140f));
                    EditorGUILayout.ObjectField(row.ExistingDef, typeof(SupermarketItemDef), false, GUILayout.Width(140f));
                    EditorGUILayout.LabelField(
                        row.CanImport ? "OK" : row.Error,
                        row.CanImport ? EditorStyles.label : EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        void LoadPreview()
        {
            _previewRows.Clear();
            _statusMessage = string.Empty;
            RebuildExistingDefLookup();

            try
            {
                var sheetPath = AssetDatabase.GetAssetPath(sheetFile);
                if (string.IsNullOrEmpty(sheetPath) || !IsSupportedSheetPath(sheetPath))
                {
                    _statusMessage = "请选择有效的 .txt / .csv 或 .xlsx 配表文件。";
                    return;
                }

                var table = SupermarketSheetReader.Read(sheetPath);
                if (table.RowCount == 0)
                {
                    _statusMessage = "表格为空。";
                    return;
                }

                var maxColumns = 0;
                for (var i = 0; i < table.RowCount; i++)
                    maxColumns = Math.Max(maxColumns, table.Rows[i].Count);

                if (!TryResolveColumns(table, out var nameColumn, out var shelfColumn, out var dataStartRow, out var headerError))
                {
                    _statusMessage = $"{headerError}\n（已读取 {table.RowCount} 行 × {maxColumns} 列）";
                    _resolvedNameColumn = -1;
                    _resolvedShelfColumn = -1;
                    return;
                }

                _resolvedNameColumn = nameColumn;
                _resolvedShelfColumn = shelfColumn;

                var prefabLookup = BuildPrefabLookup();
                for (var rowIndex = dataStartRow; rowIndex < table.RowCount; rowIndex++)
                {
                    table.TryGetCell(rowIndex, nameColumn, out var displayName);
                    table.TryGetCell(rowIndex, shelfColumn, out var shelfRaw);

                    displayName = SupermarketSheetColumnResolver.NormalizeCell(displayName);
                    shelfRaw = SupermarketSheetColumnResolver.NormalizeCell(shelfRaw);
                    if (string.IsNullOrEmpty(displayName) && string.IsNullOrEmpty(shelfRaw))
                        continue;

                    var preview = BuildPreviewRow(displayName, shelfRaw, prefabLookup);
                    _previewRows.Add(preview);
                }

                var okCount = _previewRows.Count(row => row.CanImport);
                var existingCount = _previewRows.Count(row => row.ExistingDef != null);
                _statusMessage =
                    $"读取完成：共 {_previewRows.Count} 行，可导入 {okCount} 行，异常 {_previewRows.Count - okCount} 行，匹配已有 SO {existingCount} 个。\n" +
                    $"列映射：名称 = 第 {nameColumn + 1} 列，货架 = 第 {shelfColumn + 1} 列。";
            }
            catch (Exception ex)
            {
                _statusMessage = $"读取失败：{ex.Message}";
                Debug.LogException(ex);
            }

            Repaint();
        }

        void ImportRows()
        {
            if (_previewRows.Count == 0)
                LoadPreview();

            if (_previewRows.Count == 0)
            {
                EditorUtility.DisplayDialog("超市物品导表", "没有可导入的数据，请先读取并预览。", "确定");
                return;
            }

            if (itemDatabase == null)
            {
                EditorUtility.DisplayDialog("超市物品导表", "请指定 SupermarketItemDatabase。", "确定");
                return;
            }

            var importRows = _previewRows.Where(row => row.CanImport).ToList();
            if (skipRowsWithoutPrefab)
                importRows = importRows.Where(row => row.Prefab != null).ToList();

            if (importRows.Count == 0)
            {
                EditorUtility.DisplayDialog("超市物品导表", "没有满足条件的行可导入。", "确定");
                return;
            }

            var created = 0;
            var updated = 0;
            var defs = new List<SupermarketItemDef>();

            try
            {
                AssetDatabase.StartAssetEditing();
                Undo.RecordObject(itemDatabase, "Import Supermarket Item Sheet");

                for (var i = 0; i < importRows.Count; i++)
                {
                    var row = importRows[i];
                    EditorUtility.DisplayProgressBar(
                        "超市物品导表",
                        row.DisplayName,
                        (float)i / importRows.Count);

                    var def = FindOrCreateDef(row.ItemId, out var isNew);
                    def.itemId = row.ItemId;
                    def.displayName = row.DisplayName;
                    def.worldPrefab = row.Prefab;
                    def.minSpawnCount = Mathf.Max(1, minSpawnCount);
                    def.maxSpawnCount = Mathf.Max(def.minSpawnCount, maxSpawnCount);
                    def.disableShadows = disableShadows;
                    EditorUtility.SetDirty(def);
                    defs.Add(def);

                    if (isNew) created++;
                    else updated++;
                }

                if (replaceDatabaseItems)
                    itemDatabase.items = defs;
                else
                    MergeIntoDatabase(defs);

                EditorUtility.SetDirty(itemDatabase);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                RebuildExistingDefLookup();
            }

            EditorUtility.DisplayDialog(
                "超市物品导表",
                $"导入完成：新建 {created}，更新 {updated}，Database 当前 {itemDatabase.items?.Count ?? 0} 条。",
                "确定");
        }

        ImportPreviewRow BuildPreviewRow(
            string displayName,
            string shelfRaw,
            Dictionary<string, PrefabLookupEntry> prefabLookup)
        {
            var row = new ImportPreviewRow
            {
                DisplayName = displayName,
            };

            shelfRaw = SupermarketSheetColumnResolver.NormalizeShelfToken(shelfRaw);
            row.ShelfRaw = shelfRaw;

            if (string.IsNullOrWhiteSpace(shelfRaw))
            {
                row.Error = "货架为空";
                return row;
            }

            if (!LooksLikeShelfId(shelfRaw))
            {
                row.Error = $"货架格式无效（期望如 1-A，实际 {shelfRaw}）";
                return row;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                row.Error = "名称为空";
                return row;
            }

            row.ItemId = itemIdLowercase ? shelfRaw.ToLowerInvariant() : shelfRaw.Trim();
            row.ExistingDef = FindExistingDef(row.ItemId);

            if (!TryResolvePrefab(shelfRaw, prefabLookup, out var entry))
            {
                row.Error = $"未找到 Prefab（货架 {shelfRaw}）";
                return row;
            }

            row.Prefab = entry.Prefab;
            row.PrefabPath = entry.AssetPath;
            return row;
        }

        static bool LooksLikeShelfId(string value)
        {
            value = SupermarketSheetColumnResolver.NormalizeShelfToken(value);
            return !string.IsNullOrWhiteSpace(value) && ShelfIdPattern.IsMatch(value);
        }

        static bool TryResolvePrefab(
            string shelfRaw,
            Dictionary<string, PrefabLookupEntry> prefabLookup,
            out PrefabLookupEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(shelfRaw))
                return false;

            var candidates = BuildShelfLookupCandidates(shelfRaw);
            for (var i = 0; i < candidates.Count; i++)
            {
                if (prefabLookup.TryGetValue(candidates[i], out entry))
                    return true;
            }

            return false;
        }

        static List<string> BuildShelfLookupCandidates(string shelfRaw)
        {
            var trimmed = shelfRaw.Trim();
            var lower = trimmed.ToLowerInvariant();
            var candidates = new List<string> { trimmed, lower };

            var dashIndex = trimmed.LastIndexOf('-');
            if (dashIndex > 0 && dashIndex < trimmed.Length - 1)
            {
                var prefix = trimmed.Substring(0, dashIndex);
                var suffix = trimmed.Substring(dashIndex + 1);
                candidates.Add($"{prefix}-{suffix.ToUpperInvariant()}");
                candidates.Add($"{prefix}-{suffix.ToLowerInvariant()}");
            }

            return candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        Dictionary<string, PrefabLookupEntry> BuildPrefabLookup()
        {
            var result = new Dictionary<string, PrefabLookupEntry>(StringComparer.OrdinalIgnoreCase);
            var folderPath = AssetDatabase.GetAssetPath(prefabFolder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return result;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                    continue;

                var key = Path.GetFileNameWithoutExtension(assetPath)?.Trim();
                if (string.IsNullOrEmpty(key) || result.ContainsKey(key))
                    continue;

                result.Add(key, new PrefabLookupEntry
                {
                    Prefab = prefab,
                    AssetPath = assetPath,
                });
            }

            return result;
        }

        void RebuildExistingDefLookup()
        {
            _existingDefByItemId.Clear();

            var folders = new List<string>();
            var itemFolder = AssetDatabase.GetAssetPath(itemDefFolder);
            if (!string.IsNullOrEmpty(itemFolder) && AssetDatabase.IsValidFolder(itemFolder))
                folders.Add(itemFolder);

            if (itemDatabase != null)
            {
                var dbFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(itemDatabase))?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dbFolder) && !folders.Contains(dbFolder))
                    folders.Add(dbFolder);
            }

            for (var f = 0; f < folders.Count; f++)
            {
                var guids = AssetDatabase.FindAssets("t:SupermarketItemDef", new[] { folders[f] });
                for (var i = 0; i < guids.Length; i++)
                {
                    var def = AssetDatabase.LoadAssetAtPath<SupermarketItemDef>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (def == null)
                        continue;

                    RegisterExistingDef(def);
                    RegisterExistingDefByAssetName(def);
                }
            }

            if (itemDatabase?.items != null)
            {
                for (var i = 0; i < itemDatabase.items.Count; i++)
                    RegisterExistingDef(itemDatabase.items[i]);
            }
        }

        void RegisterExistingDef(SupermarketItemDef def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.itemId))
                return;

            _existingDefByItemId[def.itemId.Trim()] = def;
        }

        void RegisterExistingDefByAssetName(SupermarketItemDef def)
        {
            if (def == null)
                return;

            var assetPath = AssetDatabase.GetAssetPath(def);
            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(fileName))
                return;

            const string prefix = "SupermarketItemDef_";
            if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return;

            var idFromFile = fileName.Substring(prefix.Length);
            if (!string.IsNullOrWhiteSpace(idFromFile))
                _existingDefByItemId[idFromFile] = def;
        }

        SupermarketItemDef FindExistingDef(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            return _existingDefByItemId.TryGetValue(itemId.Trim(), out var def) ? def : null;
        }

        bool TryResolveColumns(
            SupermarketSheetTable table,
            out int nameColumn,
            out int shelfColumn,
            out int dataStartRow,
            out string error)
        {
            return SupermarketSheetColumnResolver.TryResolve(
                table,
                nameColumnHeader,
                shelfColumnHeader,
                LooksLikeShelfId,
                IsNameHeader,
                IsShelfHeader,
                out nameColumn,
                out shelfColumn,
                out dataStartRow,
                out error);
        }

        bool IsNameHeader(string cell)
        {
            cell = SupermarketSheetColumnResolver.NormalizeCell(cell);
            if (IsShelfHeader(cell))
                return false;

            return string.Equals(cell, nameColumnHeader, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(cell, "名称", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(cell, "name", StringComparison.OrdinalIgnoreCase);
        }

        bool IsShelfHeader(string cell)
        {
            cell = SupermarketSheetColumnResolver.NormalizeCell(cell);
            return string.Equals(cell, shelfColumnHeader, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(cell, "放置货架", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(cell, "货架", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(cell, "shelf", StringComparison.OrdinalIgnoreCase);
        }

        SupermarketItemDef FindOrCreateDef(string itemId, out bool isNew)
        {
            isNew = false;
            itemDatabase.items ??= new List<SupermarketItemDef>();

            var existing = FindExistingDef(itemId);
            if (existing != null)
            {
                if (!itemDatabase.items.Contains(existing))
                    itemDatabase.items.Add(existing);
                return existing;
            }

            for (var i = 0; i < itemDatabase.items.Count; i++)
            {
                var entry = itemDatabase.items[i];
                if (entry != null && string.Equals(entry.itemId, itemId, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            var folder = AssetDatabase.GetAssetPath(itemDefFolder);
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(itemDatabase))?.Replace('\\', '/');
                folder = $"{folder}/Item";
            }

            if (!AssetDatabase.IsValidFolder(folder))
                Directory.CreateDirectory(folder);

            var safeId = SanitizeAssetFileName(itemId);
            var assetPath = $"{folder}/SupermarketItemDef_{safeId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<SupermarketItemDef>(assetPath);
            if (asset != null)
            {
                _existingDefByItemId[itemId] = asset;
                if (!itemDatabase.items.Contains(asset))
                    itemDatabase.items.Add(asset);
                return asset;
            }

            asset = ScriptableObject.CreateInstance<SupermarketItemDef>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(assetPath));
            itemDatabase.items.Add(asset);
            _existingDefByItemId[itemId] = asset;
            isNew = true;
            return asset;
        }

        void MergeIntoDatabase(List<SupermarketItemDef> importedDefs)
        {
            itemDatabase.items ??= new List<SupermarketItemDef>();
            for (var i = 0; i < importedDefs.Count; i++)
            {
                var def = importedDefs[i];
                if (def == null || itemDatabase.items.Contains(def))
                    continue;
                itemDatabase.items.Add(def);
            }
        }

        static string SanitizeAssetFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "item";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                    chars[i] = '_';
            }

            return new string(chars);
        }

        sealed class PrefabLookupEntry
        {
            public GameObject Prefab;
            public string AssetPath;
        }
    }
}
#endif

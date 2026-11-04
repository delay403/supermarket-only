#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Game.Core.Content;
using Game.SupermarketOrganize;
using Game.SupermarketOrganize.Config;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 批量为物品 Prefab 添加/配置 <see cref="SupermarketItem"/>，并统一使用 <see cref="BoxCollider"/>。
    /// </summary>
    public sealed class SupermarketItemBatchSetupWindow : EditorWindow
    {
        const string MenuPath = "Game/Supermarket Organize/Batch Setup Item Prefabs";
        const string DefaultFolder = "Assets/Prefabs/超市物品";

        enum ItemIdMode
        {
            PrefabFileName = 0,
            PrefabFileNameLower = 1,
            CustomPrefixPlusFileName = 2,
        }

        [SerializeField] DefaultAsset targetFolder;
        [SerializeField] List<GameObject> prefabList = new List<GameObject>();
        [SerializeField] ItemIdMode itemIdMode = ItemIdMode.PrefabFileNameLower;
        [SerializeField] string itemIdPrefix = "item_";
        [SerializeField] float defaultHandleWidth = 0.4f;
        [SerializeField] bool autoFitBoxCollider = true;
        [SerializeField] Vector3 colliderSizePadding = new Vector3(0.02f, 0.02f, 0.02f);
        [SerializeField] bool removeNonBoxColliders = true;
        [SerializeField] bool addRigidbody = true;
        [SerializeField] bool addPhysicsSettle = true;
        [SerializeField] bool addItemMotion = true;
        [SerializeField] int itemLayer = 16;
        [SerializeField] bool syncItemDatabase;
        [SerializeField] SupermarketItemDatabase itemDatabase;
        [SerializeField] bool disableShadowsInDatabase = true;
        [SerializeField] int minSpawnCount = 3;
        [SerializeField] int maxSpawnCount = 15;

        Vector2 _scroll;

        [MenuItem(MenuPath)]
        static void Open()
        {
            var window = GetWindow<SupermarketItemBatchSetupWindow>("超市物品批量配置");
            window.minSize = new Vector2(480f, 560f);
            window.Show();
        }

        [MenuItem("Assets/Game/Supermarket Organize/Setup Selected Item Prefabs", false, 2100)]
        static void SetupSelectedPrefabsFromMenu()
        {
            var prefabs = CollectSelectedPrefabs();
            if (prefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("超市物品批量配置", "请在 Project 中选中一个或多个 Prefab。", "确定");
                return;
            }

            var window = GetWindow<SupermarketItemBatchSetupWindow>("超市物品批量配置");
            window.prefabList = prefabs;
            window.Show();
        }

        [MenuItem("Assets/Game/Supermarket Organize/Setup Selected Item Prefabs", true)]
        static bool SetupSelectedPrefabsFromMenuValidate() => Selection.gameObjects.Length > 0;

        void OnEnable()
        {
            if (targetFolder == null && AssetDatabase.IsValidFolder(DefaultFolder))
                targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultFolder);

            itemDatabase ??= AssetDatabase.LoadAssetAtPath<SupermarketItemDatabase>(
                GameContentPaths.SupermarketItemDatabaseAssetPath);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("超市物品 Prefab 批量配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "为选中 Prefab 添加 SupermarketItem，统一使用 BoxCollider，并可选写入 ItemDatabase。\n" +
                "建议在配置前备份 Prefab。完整流程：批量命名 → 制作 Prefab → 本工具 → Excel 导表（Import Item Sheet From Excel）。",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            DrawCollectionSection();
            EditorGUILayout.Space(6f);
            DrawOptionsSection();
            EditorGUILayout.Space(6f);
            DrawDatabaseSection();
            EditorGUILayout.Space(10f);
            DrawApplySection();
        }

        void DrawCollectionSection()
        {
            EditorGUILayout.LabelField("目标 Prefab", EditorStyles.boldLabel);
            targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("扫描文件夹", targetFolder, typeof(DefaultAsset), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("扫描文件夹", GUILayout.Height(24f)))
                    ScanFolder();

                if (GUILayout.Button("加入当前选中", GUILayout.Height(24f)))
                    AddSelection();
            }

            if (GUILayout.Button("清空列表", GUILayout.Height(22f)))
                prefabList.Clear();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(140f), GUILayout.MaxHeight(220f));
            for (var i = prefabList.Count - 1; i >= 0; i--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    prefabList[i] = (GameObject)EditorGUILayout.ObjectField(prefabList[i], typeof(GameObject), false);
                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                        prefabList.RemoveAt(i);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"共 {prefabList.Count} 个 Prefab");
        }

        void DrawOptionsSection()
        {
            EditorGUILayout.LabelField("SupermarketItem / Collider", EditorStyles.boldLabel);
            itemIdMode = (ItemIdMode)EditorGUILayout.EnumPopup("Item Id 规则", itemIdMode);
            if (itemIdMode == ItemIdMode.CustomPrefixPlusFileName)
                itemIdPrefix = EditorGUILayout.TextField("Item Id 前缀", itemIdPrefix);

            defaultHandleWidth = EditorGUILayout.Slider("预制体 Handle Width", defaultHandleWidth, 0.05f, 2f);
            autoFitBoxCollider = EditorGUILayout.Toggle("按渲染器自动贴合 BoxCollider", autoFitBoxCollider);
            using (new EditorGUI.DisabledScope(!autoFitBoxCollider))
                colliderSizePadding = EditorGUILayout.Vector3Field("Collider 额外边距", colliderSizePadding);

            removeNonBoxColliders = EditorGUILayout.Toggle("移除非 BoxCollider", removeNonBoxColliders);
            itemLayer = EditorGUILayout.LayerField("物品 Layer", itemLayer);
            addRigidbody = EditorGUILayout.Toggle("添加 Rigidbody（若无）", addRigidbody);
            addPhysicsSettle = EditorGUILayout.Toggle("添加 SupermarketItemPhysicsSettle", addPhysicsSettle);
            addItemMotion = EditorGUILayout.Toggle("添加 SupermarketItemMotion", addItemMotion);
        }

        void DrawDatabaseSection()
        {
            EditorGUILayout.LabelField("Item Database（可选）", EditorStyles.boldLabel);
            syncItemDatabase = EditorGUILayout.Toggle("同步写入 ItemDatabase", syncItemDatabase);
            using (new EditorGUI.DisabledScope(!syncItemDatabase))
            {
                itemDatabase = (SupermarketItemDatabase)EditorGUILayout.ObjectField(
                    "Database",
                    itemDatabase,
                    typeof(SupermarketItemDatabase),
                    false);
                disableShadowsInDatabase = EditorGUILayout.Toggle("Database: disableShadows", disableShadowsInDatabase);
                minSpawnCount = EditorGUILayout.IntField("minSpawnCount", minSpawnCount);
                maxSpawnCount = EditorGUILayout.IntField("maxSpawnCount", maxSpawnCount);
            }
        }

        void DrawApplySection()
        {
            using (new EditorGUI.DisabledScope(prefabList.Count == 0))
            {
                if (GUILayout.Button($"批量配置 {prefabList.Count} 个 Prefab", GUILayout.Height(36f)))
                    ApplyBatch();
            }
        }

        void ScanFolder()
        {
            if (targetFolder == null)
                return;

            var path = AssetDatabase.GetAssetPath(targetFolder);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            {
                EditorUtility.DisplayDialog("超市物品批量配置", "请选择有效的文件夹。", "确定");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
            var added = 0;
            for (var i = 0; i < guids.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (prefab == null || prefabList.Contains(prefab))
                    continue;

                prefabList.Add(prefab);
                added++;
            }

            Debug.Log($"[SupermarketItemBatchSetup] 从 {path} 扫描到 {added} 个 Prefab。");
        }

        void AddSelection()
        {
            var selected = CollectSelectedPrefabs();
            for (var i = 0; i < selected.Count; i++)
            {
                if (!prefabList.Contains(selected[i]))
                    prefabList.Add(selected[i]);
            }
        }

        static List<GameObject> CollectSelectedPrefabs()
        {
            var result = new List<GameObject>();
            var objects = Selection.objects;
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] is not GameObject go)
                    continue;

                var path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!result.Contains(go))
                    result.Add(go);
            }

            return result;
        }

        void ApplyBatch()
        {
            var settings = BuildSettings();
            var ok = 0;
            var fail = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                for (var i = 0; i < prefabList.Count; i++)
                {
                    var prefab = prefabList[i];
                    if (prefab == null)
                    {
                        fail++;
                        continue;
                    }

                    var path = AssetDatabase.GetAssetPath(prefab);
                    if (string.IsNullOrEmpty(path))
                    {
                        fail++;
                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        "超市物品批量配置",
                        $"{prefab.name} ({i + 1}/{prefabList.Count})",
                        (float)i / Mathf.Max(1, prefabList.Count));

                    if (SupermarketItemPrefabSetupUtility.TrySetupPrefab(path, settings, out var error))
                        ok++;
                    else
                    {
                        fail++;
                        Debug.LogWarning($"[SupermarketItemBatchSetup] 失败: {path} — {error}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (syncItemDatabase && itemDatabase != null)
                SyncItemDatabase(settings);

            EditorUtility.DisplayDialog(
                "超市物品批量配置",
                $"完成：成功 {ok}，失败 {fail}。",
                "确定");
        }

        void SyncItemDatabase(SupermarketItemPrefabSetupUtility.Settings settings)
        {
            Undo.RecordObject(itemDatabase, "Sync Supermarket Item Database");
            itemDatabase.items ??= new List<SupermarketItemDef>();

            for (var i = 0; i < prefabList.Count; i++)
            {
                var prefab = prefabList[i];
                if (prefab == null)
                    continue;

                var path = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrEmpty(path))
                    continue;

                var itemId = SupermarketItemPrefabSetupUtility.ResolveItemId(prefab.name, settings);
                var def = FindOrCreateDef(itemId);
                def.itemId = itemId;
                def.displayName = prefab.name;
                def.worldPrefab = prefab;
                def.disableShadows = settings.DisableShadowsInDatabase;
                def.minSpawnCount = Mathf.Max(1, settings.MinSpawnCount);
                def.maxSpawnCount = Mathf.Max(def.minSpawnCount, settings.MaxSpawnCount);
                EditorUtility.SetDirty(def);
            }

            EditorUtility.SetDirty(itemDatabase);
            AssetDatabase.SaveAssets();
        }

        SupermarketItemDef FindOrCreateDef(string itemId)
        {
            for (var i = 0; i < itemDatabase.items.Count; i++)
            {
                var entry = itemDatabase.items[i];
                if (entry != null && entry.itemId == itemId)
                    return entry;
            }

            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(itemDatabase));
            folder = folder?.Replace('\\', '/');
            var assetPath = $"{folder}/SupermarketItemDef_{itemId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SupermarketItemDef>(assetPath);
            if (existing != null)
            {
                itemDatabase.items.Add(existing);
                return existing;
            }

            var def = ScriptableObject.CreateInstance<SupermarketItemDef>();
            AssetDatabase.CreateAsset(def, AssetDatabase.GenerateUniqueAssetPath(assetPath));
            itemDatabase.items.Add(def);
            return def;
        }

        SupermarketItemPrefabSetupUtility.Settings BuildSettings() => new()
        {
            ItemIdMode = (SupermarketItemPrefabSetupUtility.ItemIdMode)itemIdMode,
            ItemIdPrefix = itemIdPrefix,
            DefaultHandleWidth = defaultHandleWidth,
            AutoFitBoxCollider = autoFitBoxCollider,
            ColliderSizePadding = colliderSizePadding,
            RemoveNonBoxColliders = removeNonBoxColliders,
            AddRigidbody = addRigidbody,
            AddPhysicsSettle = addPhysicsSettle,
            AddItemMotion = addItemMotion,
            ItemLayer = itemLayer,
            DisableShadowsInDatabase = disableShadowsInDatabase,
            MinSpawnCount = minSpawnCount,
            MaxSpawnCount = maxSpawnCount,
        };
    }

    public static class SupermarketItemPrefabSetupUtility
    {
        public enum ItemIdMode
        {
            PrefabFileName = 0,
            PrefabFileNameLower = 1,
            CustomPrefixPlusFileName = 2,
        }

        public sealed class Settings
        {
            public ItemIdMode ItemIdMode = ItemIdMode.PrefabFileNameLower;
            public string ItemIdPrefix = "item_";
            public float DefaultHandleWidth = 0.4f;
            public bool AutoFitBoxCollider = true;
            public Vector3 ColliderSizePadding = new Vector3(0.02f, 0.02f, 0.02f);
            public bool RemoveNonBoxColliders = true;
            public bool AddRigidbody = true;
            public bool AddPhysicsSettle = true;
            public bool AddItemMotion = true;
            public int ItemLayer = 16;
            public bool DisableShadowsInDatabase = true;
            public int MinSpawnCount = 3;
            public int MaxSpawnCount = 15;
        }

        public static bool TrySetupPrefab(string prefabAssetPath, Settings settings, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(prefabAssetPath))
            {
                error = "路径为空";
                return false;
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabAssetPath);
                if (root == null)
                {
                    error = "无法加载 Prefab";
                    return false;
                }

                SetupGameObject(root, settings, prefabAssetPath);
                PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void SetupGameObject(GameObject root, Settings settings, string prefabAssetPath = null)
        {
            if (root == null || settings == null)
                return;

            ApplyLayerRecursive(root, settings.ItemLayer);

            if (settings.RemoveNonBoxColliders)
                RemoveNonBoxColliders(root);

            var box = RecreateFittedBoxCollider(root);
            if (settings.AutoFitBoxCollider && !HasValidFittedSize(box))
                FitBoxColliderToRenderers(box, root, settings.ColliderSizePadding);
            else if (settings.AutoFitBoxCollider && settings.ColliderSizePadding != Vector3.zero)
                ApplyColliderPadding(box, settings.ColliderSizePadding);

            if (settings.AddRigidbody && root.GetComponent<Rigidbody>() == null)
            {
                var body = root.AddComponent<Rigidbody>();
                body.mass = 1f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (settings.AddPhysicsSettle && root.GetComponent<SupermarketItemPhysicsSettle>() == null)
                root.AddComponent<SupermarketItemPhysicsSettle>();

            if (settings.AddItemMotion && root.GetComponent<SupermarketItemMotion>() == null)
                root.AddComponent<SupermarketItemMotion>();

            var item = root.GetComponent<SupermarketItem>();
            if (item == null)
                item = root.AddComponent<SupermarketItem>();

            var itemId = ResolveItemId(root.name, settings);
            var itemSo = new SerializedObject(item);
            itemSo.FindProperty("itemId").stringValue = itemId;
            itemSo.FindProperty("handleWidth").floatValue = Mathf.Max(0.01f, settings.DefaultHandleWidth);
            itemSo.FindProperty("interactionCollider").objectReferenceValue = box;
            itemSo.ApplyModifiedPropertiesWithoutUndo();

            if (!string.IsNullOrEmpty(prefabAssetPath))
                Debug.Log($"[SupermarketItemBatchSetup] 已配置 {prefabAssetPath} (itemId={itemId})");
        }

        public static string ResolveItemId(string prefabName, Settings settings)
        {
            var name = string.IsNullOrWhiteSpace(prefabName) ? "item_default" : prefabName.Trim();
            return settings.ItemIdMode switch
            {
                ItemIdMode.PrefabFileNameLower => name.ToLowerInvariant(),
                ItemIdMode.CustomPrefixPlusFileName => $"{settings.ItemIdPrefix}{name}",
                _ => name,
            };
        }

        static void ApplyLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            var transform = go.transform;
            for (var i = 0; i < transform.childCount; i++)
                ApplyLayerRecursive(transform.GetChild(i).gameObject, layer);
        }

        static void RemoveNonBoxColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || collider is BoxCollider)
                    continue;

                UnityEngine.Object.DestroyImmediate(collider, true);
            }
        }

        static BoxCollider RecreateFittedBoxCollider(GameObject root)
        {
            var existing = root.GetComponent<BoxCollider>();
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing, true);

            return root.AddComponent<BoxCollider>();
        }

        static bool HasValidFittedSize(BoxCollider box)
        {
            if (box == null)
                return false;

            var size = box.size;
            return size.x > 0.001f && size.y > 0.001f && size.z > 0.001f
                   && (Mathf.Abs(size.x - 1f) > 0.001f
                       || Mathf.Abs(size.y - 1f) > 0.001f
                       || Mathf.Abs(size.z - 1f) > 0.001f);
        }

        static void ApplyColliderPadding(BoxCollider box, Vector3 padding)
        {
            if (box == null || padding == Vector3.zero)
                return;

            box.size = Vector3.Max(Vector3.one * 0.01f, box.size + padding);
        }

        public static void FitBoxColliderToRenderers(BoxCollider box, GameObject root, Vector3 padding)
        {
            if (box == null || root == null)
                return;

            var colliderTransform = box.transform;
            var localBounds = CalculateMeshLocalBounds(colliderTransform);
            if (localBounds.size == Vector3.zero)
                localBounds = CalculateRendererLocalBounds(colliderTransform);

            if (localBounds.size == Vector3.zero)
            {
                box.center = Vector3.zero;
                box.size = Vector3.Max(Vector3.one * 0.2f, padding);
                return;
            }

            box.center = localBounds.center;
            box.size = Vector3.Max(Vector3.one * 0.01f, localBounds.size + padding);
        }

        static Bounds CalculateMeshLocalBounds(Transform colliderTransform)
        {
            var bounds = new Bounds();
            var initialized = false;
            var meshFilters = colliderTransform.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < meshFilters.Length; i++)
            {
                var meshFilter = meshFilters[i];
                var mesh = meshFilter != null ? meshFilter.sharedMesh : null;
                if (mesh == null)
                    continue;

                initialized = EncapsulateLocalBoundsCorners(
                    ref bounds,
                    initialized,
                    colliderTransform,
                    meshFilter.transform,
                    mesh.bounds);
            }

            return initialized ? bounds : new Bounds(Vector3.zero, Vector3.zero);
        }

        static Bounds CalculateRendererLocalBounds(Transform colliderTransform)
        {
            var bounds = new Bounds();
            var initialized = false;
            var renderers = colliderTransform.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                initialized = EncapsulateLocalBoundsCorners(
                    ref bounds,
                    initialized,
                    colliderTransform,
                    renderer.transform,
                    renderer.localBounds);
            }

            return initialized ? bounds : new Bounds(Vector3.zero, Vector3.zero);
        }

        static bool EncapsulateLocalBoundsCorners(
            ref Bounds localBounds,
            bool initialized,
            Transform colliderTransform,
            Transform sourceTransform,
            Bounds sourceBounds)
        {
            var center = sourceBounds.center;
            var extents = sourceBounds.extents;
            var corners = new[]
            {
                center + new Vector3(extents.x, extents.y, extents.z),
                center + new Vector3(extents.x, extents.y, -extents.z),
                center + new Vector3(extents.x, -extents.y, extents.z),
                center + new Vector3(extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x, extents.y, extents.z),
                center + new Vector3(-extents.x, extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y, extents.z),
                center + new Vector3(-extents.x, -extents.y, -extents.z),
            };

            for (var i = 0; i < corners.Length; i++)
            {
                var localPoint = colliderTransform.InverseTransformPoint(sourceTransform.TransformPoint(corners[i]));
                if (!initialized)
                {
                    localBounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(localPoint);
                }
            }

            return initialized;
        }
    }
}
#endif

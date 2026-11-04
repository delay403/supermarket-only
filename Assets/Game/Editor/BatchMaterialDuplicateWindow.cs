#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 为每个源物体按材质列表复制多份，并为每份赋予单独材质。
    /// </summary>
    public sealed class BatchMaterialDuplicateWindow : EditorWindow
    {
        const string MenuPath = "Game/Tools/Batch Material Duplicate";
        const string SupermarketMenuPath = "Game/Supermarket Organize/Batch Material Duplicate";

        [SerializeField] List<GameObject> sourceObjects = new List<GameObject>();
        [SerializeField] List<Material> materials = new List<Material>();
        [SerializeField] Transform parent;
        [SerializeField] bool useSourceNameAsPrefix = true;
        [SerializeField] string prefix = string.Empty;
        [SerializeField] string suffix = string.Empty;
        [SerializeField] string connector = "_";
        [SerializeField] BatchRenameSequenceKind frontSequence = BatchRenameSequenceKind.None;
        [SerializeField] BatchRenameSequenceKind backSequence = BatchRenameSequenceKind.Number1;
        [SerializeField] int frontNumberStart = 1;
        [SerializeField] int backNumberStart = 4;
        [SerializeField] string frontLetterStart = "a";
        [SerializeField] string backLetterStart = "a";
        [SerializeField] bool appendMaterialName;
        [SerializeField] float horizontalSpacing = 0f;
        [SerializeField] bool worldPositionStays = true;

        Vector2 _scroll;

        [MenuItem(MenuPath)]
        [MenuItem(SupermarketMenuPath)]
        static void Open()
        {
            var window = GetWindow<BatchMaterialDuplicateWindow>("材质批量复制");
            window.minSize = new Vector2(520f, 620f);
            window.Show();
        }

        void OnEnable() => SyncFromSelection();

        void OnSelectionChange() => SyncFromSelection();

        void OnGUI()
        {
            EditorGUILayout.LabelField("材质批量复制", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "为每个源物体按材质数量复制多份，每份赋予一个材质。\n" +
                "示例：7 个物体 × 7 个材质 → 生成 49 个实例，每个物体各 7 份不同材质。",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            parent = (Transform)EditorGUILayout.ObjectField("父节点（可选）", parent, typeof(Transform), true);

            DrawSourceObjects();
            DrawMaterials();
            DrawNamingSection();
            DrawLayoutSection();

            EditorGUILayout.Space(8f);
            var total = Mathf.Max(0, sourceObjects.Count) * Mathf.Max(0, materials.Count);
            using (new EditorGUI.DisabledScope(total <= 0))
            {
                if (GUILayout.Button($"生成 {total} 个副本", GUILayout.Height(36f)))
                    ExecuteDuplicate();
            }
        }

        void DrawSourceObjects()
        {
            EditorGUILayout.LabelField("源物体", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("使用当前选中物体", GUILayout.Height(22f)))
                    SyncFromSelection();

                if (GUILayout.Button("清空", GUILayout.Width(64f), GUILayout.Height(22f)))
                    sourceObjects.Clear();
            }

            var removeIndex = -1;
            for (var i = 0; i < sourceObjects.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    sourceObjects[i] = (GameObject)EditorGUILayout.ObjectField(
                        $"源 {i + 1}",
                        sourceObjects[i],
                        typeof(GameObject),
                        true);
                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                        removeIndex = i;
                }
            }

            if (removeIndex >= 0)
                sourceObjects.RemoveAt(removeIndex);

            if (GUILayout.Button("添加空槽位"))
                sourceObjects.Add(null);
        }

        void DrawMaterials()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("材质列表", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("使用当前选中材质", GUILayout.Height(22f)))
                    SyncMaterialsFromSelection();

                if (GUILayout.Button("清空", GUILayout.Width(64f), GUILayout.Height(22f)))
                    materials.Clear();
            }

            var removeIndex = -1;
            for (var i = 0; i < materials.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    materials[i] = (Material)EditorGUILayout.ObjectField(
                        $"材质 {i + 1}",
                        materials[i],
                        typeof(Material),
                        false);
                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                        removeIndex = i;
                }
            }

            if (removeIndex >= 0)
                materials.RemoveAt(removeIndex);

            if (GUILayout.Button("添加空槽位"))
                materials.Add(null);
        }

        void DrawNamingSection()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("命名规则", EditorStyles.boldLabel);
            useSourceNameAsPrefix = EditorGUILayout.Toggle("使用源物体名作为前缀", useSourceNameAsPrefix);
            using (new EditorGUI.DisabledScope(useSourceNameAsPrefix))
                prefix = EditorGUILayout.TextField("固定前缀", prefix);

            connector = EditorGUILayout.TextField("中间连接词", connector);
            suffix = EditorGUILayout.TextField("固定后缀", suffix);
            frontSequence = (BatchRenameSequenceKind)EditorGUILayout.EnumPopup("前排序规则", frontSequence);
            backSequence = (BatchRenameSequenceKind)EditorGUILayout.EnumPopup("后排序规则", backSequence);
            DrawSequenceStartFields(frontSequence, ref frontNumberStart, ref frontLetterStart, "前排序");
            DrawSequenceStartFields(backSequence, ref backNumberStart, ref backLetterStart, "后排序");
            appendMaterialName = EditorGUILayout.Toggle("名称末尾附加材质名", appendMaterialName);
        }

        void DrawLayoutSection()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("布局", EditorStyles.boldLabel);
            horizontalSpacing = EditorGUILayout.FloatField("副本横向间距", horizontalSpacing);
            worldPositionStays = EditorGUILayout.Toggle("保持世界坐标", worldPositionStays);
        }

        static void DrawSequenceStartFields(
            BatchRenameSequenceKind kind,
            ref int numberStart,
            ref string letterStart,
            string label)
        {
            switch (kind)
            {
                case BatchRenameSequenceKind.Number1:
                    numberStart = EditorGUILayout.IntField($"{label}起始数字", numberStart);
                    break;
                case BatchRenameSequenceKind.LetterLowerA:
                case BatchRenameSequenceKind.LetterUpperA:
                    letterStart = EditorGUILayout.TextField($"{label}起始字母", letterStart);
                    break;
            }
        }

        void SyncFromSelection()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return;

            sourceObjects.Clear();
            for (var i = 0; i < selected.Length; i++)
            {
                if (selected[i] != null)
                    sourceObjects.Add(selected[i]);
            }

            Repaint();
        }

        void SyncMaterialsFromSelection()
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
                return;

            materials.Clear();
            for (var i = 0; i < selected.Length; i++)
            {
                if (selected[i] is Material material)
                    materials.Add(material);
            }

            materials.Sort((left, right) =>
                BatchRenameSequenceUtility.CompareNatural(left != null ? left.name : string.Empty,
                    right != null ? right.name : string.Empty));

            Repaint();
        }

        void ExecuteDuplicate()
        {
            var validSources = new List<GameObject>();
            for (var i = 0; i < sourceObjects.Count; i++)
            {
                if (sourceObjects[i] != null)
                    validSources.Add(sourceObjects[i]);
            }

            var validMaterials = new List<Material>();
            for (var i = 0; i < materials.Count; i++)
            {
                if (materials[i] != null)
                    validMaterials.Add(materials[i]);
            }

            if (validSources.Count == 0 || validMaterials.Count == 0)
            {
                EditorUtility.DisplayDialog("材质批量复制", "请至少指定一个源物体和一个材质。", "确定");
                return;
            }

            var created = new List<GameObject>();
            try
            {
                var total = validSources.Count * validMaterials.Count;
                var progress = 0;
                for (var s = 0; s < validSources.Count; s++)
                {
                    var source = validSources[s];
                    for (var m = 0; m < validMaterials.Count; m++)
                    {
                        EditorUtility.DisplayProgressBar(
                            "材质批量复制",
                            $"{source.name} → {validMaterials[m].name}",
                            (float)progress / total);
                        progress++;

                        var duplicate = InstantiateSource(source);
                        if (duplicate == null)
                            continue;

                        var parentTransform = parent != null ? parent : source.transform.parent;
                        duplicate.transform.SetParent(parentTransform, worldPositionStays);

                        if (horizontalSpacing > 0f)
                        {
                            var offset = Vector3.right * horizontalSpacing * m;
                            duplicate.transform.position = source.transform.position + offset;
                        }

                        ApplyMaterial(duplicate, validMaterials[m]);
                        duplicate.name = BuildDuplicateName(source, validMaterials[m], m, validMaterials.Count);
                        Undo.RegisterCreatedObjectUndo(duplicate, "Batch Material Duplicate");
                        created.Add(duplicate);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (created.Count > 0)
                Selection.objects = created.ToArray();

            EditorUtility.DisplayDialog("材质批量复制", $"已生成 {created.Count} 个副本。", "确定");
        }

        string BuildDuplicateName(GameObject source, Material material, int materialIndex, int materialCount)
        {
            var sourcePrefix = useSourceNameAsPrefix && source != null ? source.name : prefix;
            var baseName = BatchRenameSequenceUtility.BuildName(
                sourcePrefix,
                suffix,
                connector,
                frontSequence,
                backSequence,
                materialIndex,
                materialCount,
                frontNumberStart,
                backNumberStart,
                ParseLetterStart(frontLetterStart),
                ParseLetterStart(backLetterStart));

            if (!appendMaterialName || material == null)
                return baseName;

            return string.IsNullOrEmpty(baseName)
                ? material.name
                : $"{baseName}{connector}{material.name}";
        }

        static char ParseLetterStart(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return '\0';

            var c = text.Trim()[0];
            return char.IsLetter(c) ? c : '\0';
        }

        static GameObject InstantiateSource(GameObject source)
        {
            if (source == null)
                return null;

            var assetPath = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(assetPath) && PrefabUtility.IsPartOfPrefabAsset(source))
                return PrefabUtility.InstantiatePrefab(source) as GameObject;

            if (PrefabUtility.IsPartOfPrefabInstance(source))
            {
                var prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(source);
                if (prefabRoot != null)
                    return PrefabUtility.InstantiatePrefab(prefabRoot, source.scene) as GameObject;
            }

            return Instantiate(source);
        }

        static void ApplyMaterial(GameObject root, Material material)
        {
            if (root == null || material == null)
                return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                var shared = renderer.sharedMaterials;
                for (var m = 0; m < shared.Length; m++)
                    shared[m] = material;

                renderer.sharedMaterials = shared;
            }
        }
    }
}
#endif

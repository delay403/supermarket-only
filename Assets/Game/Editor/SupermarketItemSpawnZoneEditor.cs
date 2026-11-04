#if UNITY_EDITOR
using Game.SupermarketOrganize;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(SupermarketItemSpawnZone))]
    public sealed class SupermarketItemSpawnZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var zone = (SupermarketItemSpawnZone)target;
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "在 XZ 平面定义矩形散落区。可放置多个 Spawn Zone 拼接不规则范围；\n" +
                "区域面积越大，生成概率越高。高度仅由地面射线决定，请将区域放在地面附近（只影响 XZ）。",
                MessageType.Info);

            if (GUILayout.Button("从 BoxCollider 拟合尺寸", GUILayout.Height(24f)))
                FitFromBoxCollider(zone);
        }

        [MenuItem("GameObject/Supermarket Organize/Item Spawn Zone", false, 10)]
        static void CreateSpawnZone()
        {
            var go = new GameObject("ItemSpawnZone");
            Undo.RegisterCreatedObjectUndo(go, "Create Item Spawn Zone");

            if (Selection.activeTransform != null)
                go.transform.SetParent(Selection.activeTransform, false);

            go.AddComponent<SupermarketItemSpawnZone>();
            Selection.activeGameObject = go;
        }

        static void FitFromBoxCollider(SupermarketItemSpawnZone zone)
        {
            if (zone == null)
                return;

            var collider = zone.GetComponent<BoxCollider>();
            if (collider == null)
            {
                EditorUtility.DisplayDialog("Item Spawn Zone", "当前物体没有 BoxCollider。", "确定");
                return;
            }

            Undo.RecordObject(zone, "Fit Spawn Zone");

            var so = new SerializedObject(zone);
            so.FindProperty("localHalfExtents").vector3Value = new Vector3(
                collider.size.x * 0.5f,
                collider.size.y * 0.5f,
                collider.size.z * 0.5f);
            so.FindProperty("groundLocalY").floatValue = collider.center.y;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(zone);
        }
    }
}
#endif

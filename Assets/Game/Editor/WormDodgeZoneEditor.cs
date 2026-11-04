using Game.EscapeSnake;
using Game.WormDodge;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(WormSafeZone))]
    sealed class WormSafeZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var zone = (WormSafeZone)target;
            EditorGUILayout.Space(6);
            if (GUILayout.Button("Fit Box Collider To Platform"))
            {
                var box = zone.GetComponent<BoxCollider>();
                if (box == null)
                {
                    EditorGUILayout.HelpBox("需要 BoxCollider 才能自动贴合平台。", MessageType.Info);
                }
                else
                {
                    Undo.RecordObject(box, "Fit Safe Zone Box");
                    Undo.RecordObject(zone, "Fit Safe Zone Box");
                    zone.FitBoxColliderToPlatform();
                    EditorUtility.SetDirty(zone);
                }
            }

            var boxCollider = zone.GetComponent<BoxCollider>();
            if (boxCollider != null && boxCollider.size.y > 6f)
            {
                EditorGUILayout.HelpBox(
                    "BoxCollider 高度异常偏大，可能导致安全区判定与平台不一致。请点击上方按钮自动贴合平台。",
                    MessageType.Warning);
            }
        }

        void OnSceneGUI()
        {
            var col = (target as WormSafeZone)?.GetComponent<Collider>();
            if (col == null)
                return;

            Handles.color = new Color(0.2f, 0.85f, 0.35f, 0.35f);
            if (col is BoxCollider box)
            {
                var t = box.transform;
                var center = t.TransformPoint(box.center);
                var size = Vector3.Scale(box.size, t.lossyScale);
                Handles.DrawWireCube(center, size);
            }
        }
    }

    [CustomEditor(typeof(WormFloorSettlementZone))]
    sealed class WormFloorSettlementZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var zone = (WormFloorSettlementZone)target;

            EditorGUILayout.HelpBox(
                "层号会根据判定区世界高度自动计算（最低平台 = 第 1 层）。只需把区域放到对应平台高度即可。",
                MessageType.Info);

            EditorGUILayout.LabelField("自动层号", zone.FloorIndex.ToString());
            EditorGUILayout.LabelField("采样高度 (Y)", zone.SampleWorldHeight.ToString("F2"));

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }

        void OnSceneGUI()
        {
            var zone = (WormFloorSettlementZone)target;
            var col = zone.GetComponent<Collider>();
            if (col == null)
                return;

            Handles.color = new Color(0.95f, 0.75f, 0.15f, 0.9f);
            if (col is BoxCollider box)
            {
                var t = box.transform;
                var center = t.TransformPoint(box.center);
                var size = Vector3.Scale(box.size, t.lossyScale);
                Handles.DrawWireCube(center, size);
            }

            Handles.Label(zone.transform.position + Vector3.up * 0.5f,
                $"Floor {zone.FloorIndex} (+{zone.CoinReward})");
        }
    }

    [CustomEditor(typeof(WormFinishZone))]
    sealed class WormFinishZoneEditor : UnityEditor.Editor
    {
        void OnSceneGUI()
        {
            var col = (target as WormFinishZone)?.GetComponent<Collider>();
            if (col == null)
                return;

            Handles.color = new Color(0.2f, 0.55f, 1f, 0.9f);
            if (col is BoxCollider box)
            {
                var t = box.transform;
                var center = t.TransformPoint(box.center);
                var size = Vector3.Scale(box.size, t.lossyScale);
                Handles.DrawWireCube(center, size);
            }

            Handles.Label((target as WormFinishZone).transform.position + Vector3.up,
                "终点区 / Victory");
        }
    }

    [CustomEditor(typeof(WormDodgeAiPath))]
    sealed class WormDodgeAiPathEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var path = (WormDodgeAiPath)target;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("路点工具", EditorStyles.boldLabel);

            if (GUILayout.Button("在路径物体位置添加路点"))
            {
                Undo.RecordObject(path, "Add AI Waypoint");
                AddWaypointAt(path.transform.position, path);
            }

            if (GUILayout.Button("在 Scene 视图相机位置添加路点"))
            {
                var sceneView = SceneView.lastActiveSceneView;
                var pos = sceneView != null ? sceneView.camera.transform.position : path.transform.position;
                Undo.RecordObject(path, "Add AI Waypoint");
                AddWaypointAt(pos, path);
            }

            if (GUILayout.Button("从子物体自动收集路点"))
                CollectChildWaypoints(path);

            if (path.WaypointCount > 1 && GUILayout.Button("倒序所有路点"))
                ReverseWaypoints(path);

            if (path.WaypointCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"已绑定 {path.WaypointCount} 个路点。终点索引：{path.EndWaypointIndex}（未设置 endWaypointIndex 时默认为最后一个路点）。",
                    MessageType.Info);
            }
        }

        static void CollectChildWaypoints(WormDodgeAiPath path)
        {
            var root = path.transform;
            var childCount = root.childCount;
            if (childCount == 0)
            {
                EditorUtility.DisplayDialog("收集路点", "当前物体下没有子物体可作为路点。", "确定");
                return;
            }

            Undo.RecordObject(path, "Collect AI Waypoints From Children");
            var so = new SerializedObject(path);
            var prop = so.FindProperty("waypoints");
            prop.arraySize = childCount;
            for (var i = 0; i < childCount; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = root.GetChild(i);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }

        static void ReverseWaypoints(WormDodgeAiPath path)
        {
            Undo.RecordObject(path, "Reverse AI Waypoints");
            var so = new SerializedObject(path);
            var prop = so.FindProperty("waypoints");
            var count = prop.arraySize;
            for (var i = 0; i < count / 2; i++)
            {
                var a = prop.GetArrayElementAtIndex(i).objectReferenceValue;
                var b = prop.GetArrayElementAtIndex(count - 1 - i).objectReferenceValue;
                prop.GetArrayElementAtIndex(i).objectReferenceValue = b;
                prop.GetArrayElementAtIndex(count - 1 - i).objectReferenceValue = a;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }

        static void AddWaypointAt(Vector3 worldPos, WormDodgeAiPath path)
        {
            var child = new GameObject($"Waypoint_{path.WaypointCount}");
            Undo.RegisterCreatedObjectUndo(child, "Add AI Waypoint");
            child.transform.SetParent(path.transform, true);
            child.transform.position = worldPos;

            var so = new SerializedObject(path);
            var prop = so.FindProperty("waypoints");
            prop.arraySize++;
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = child.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(path);
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
        static void DrawPathGizmo(WormDodgeAiPath path, GizmoType gizmoType)
        {
            if (path.WaypointCount == 0)
                return;

            var prevColor = Gizmos.color;
            Gizmos.color = new Color(0.25f, 0.65f, 1f, 0.85f);

            for (var i = 0; i < path.WaypointCount; i++)
            {
                var a = path.GetWaypointWorld(i);
                Gizmos.DrawWireSphere(a, 0.22f);

                if (i + 1 < path.WaypointCount)
                {
                    var b = path.GetWaypointWorld(i + 1);
                    Gizmos.DrawLine(a, b);
                    DrawArrow(a, b);
                }
            }

            Gizmos.color = prevColor;
        }

        static void DrawArrow(Vector3 from, Vector3 to)
        {
            var dir = to - from;
            if (dir.sqrMagnitude < 1e-4f)
                return;

            dir.Normalize();
            var mid = Vector3.Lerp(from, to, 0.65f);
            var side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-4f)
                side = Vector3.Cross(dir, Vector3.right);
            side.Normalize();

            var headLen = Mathf.Min(0.35f, (to - from).magnitude * 0.2f);
            Gizmos.DrawLine(mid, mid - dir * headLen + side * headLen * 0.35f);
            Gizmos.DrawLine(mid, mid - dir * headLen - side * headLen * 0.35f);
        }
    }
}

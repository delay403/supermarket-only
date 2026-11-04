#if UNITY_EDITOR
using Game.EscapeSnake;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    [CustomEditor(typeof(WormPath))]
    public sealed class WormPathEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var path = (WormPath)target;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("路点工具", EditorStyles.boldLabel);

            if (GUILayout.Button("在路径物体位置添加路点"))
            {
                Undo.RecordObject(path, "Add Worm Waypoint");
                AddWaypointAt(path.transform.position, path);
            }

            if (GUILayout.Button("在 Scene 视图相机位置添加路点"))
            {
                var sceneView = SceneView.lastActiveSceneView;
                var pos = sceneView != null ? sceneView.camera.transform.position : path.transform.position;
                Undo.RecordObject(path, "Add Worm Waypoint");
                AddWaypointAt(pos, path);
            }

            if (GUILayout.Button("从子物体自动收集路点"))
                CollectChildWaypoints(path);

            if (path.WaypointCount > 1 && GUILayout.Button("倒序所有路点"))
                ReverseWaypoints(path);
        }

        static void CollectChildWaypoints(WormPath path)
        {
            var root = path.transform;
            var childCount = root.childCount;
            if (childCount == 0)
            {
                EditorUtility.DisplayDialog("收集路点", "当前物体下没有子物体可作为路点。", "确定");
                return;
            }

            Undo.RecordObject(path, "Collect Worm Waypoints From Children");
            var so = new SerializedObject(path);
            var prop = so.FindProperty("waypoints");
            prop.arraySize = childCount;
            for (var i = 0; i < childCount; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = root.GetChild(i);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }

        static void ReverseWaypoints(WormPath path)
        {
            Undo.RecordObject(path, "Reverse Worm Waypoints");
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

        static void AddWaypointAt(Vector3 worldPos, WormPath path)
        {
            var child = new GameObject($"Waypoint_{path.WaypointCount}");
            Undo.RegisterCreatedObjectUndo(child, "Add Worm Waypoint");
            child.transform.SetParent(path.transform, true);
            child.transform.position = worldPos;

            var so = new SerializedObject(path);
            var prop = so.FindProperty("waypoints");
            prop.arraySize++;
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = child.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
        static void DrawPathGizmo(WormPath path, GizmoType gizmoType)
        {
            if (path.WaypointCount == 0)
                return;

            var prevColor = Gizmos.color;
            Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.85f);

            for (var i = 0; i < path.WaypointCount; i++)
            {
                var a = path.GetWaypointWorld(i);
                Gizmos.DrawWireSphere(a, 0.2f);

                if (i + 1 < path.WaypointCount)
                {
                    var b = path.GetWaypointWorld(i + 1);
                    Gizmos.DrawLine(a, b);
                    DrawArrow(a, b);
                }
                else if (path.Mode == WormPath.PathMode.Loop && path.WaypointCount > 1)
                {
                    var b = path.GetWaypointWorld(0);
                    Gizmos.DrawLine(a, b);
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

    [CustomEditor(typeof(WormChainMotor))]
    public sealed class WormChainMotorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var motor = (WormChainMotor)target;
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("自动收集节段"))
            {
                Undo.RecordObject(motor, "Auto Collect Worm Segments");
                motor.AutoCollectSegments();
                EditorUtility.SetDirty(motor);
            }

            if (Application.isPlaying)
            {
                if (GUILayout.Button("播放"))
                    motor.Play();
                if (GUILayout.Button("停止"))
                    motor.Stop();
                if (GUILayout.Button("开局巡逻"))
                    motor.BeginRunPatrol();
                if (GUILayout.Button("重置到起点"))
                    motor.ResetToPathStart();
                if (GUILayout.Button("取消追踪"))
                    motor.CancelChase();
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    [CustomEditor(typeof(WormDeathZone))]
    public sealed class WormDeathZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var col = ((WormDeathZone)target).GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                EditorGUILayout.HelpBox("请将 Collider 勾选 Is Trigger，否则无法检测目标进入。", MessageType.Warning);
        }
    }
}
#endif

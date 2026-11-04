#if UNITY_EDITOR
using Game.AnimalHospital;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(AnimalHospitalWard))]
    public sealed class AnimalHospitalWardEditor : UnityEditor.Editor
    {
        SerializedProperty _volumeCenter;
        SerializedProperty _volumeExtents;

        void OnEnable()
        {
            _volumeCenter = serializedObject.FindProperty("volumeCenter");
            _volumeExtents = serializedObject.FindProperty("volumeExtents");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Scene 视图中会以青色半透明方块显示病房范围（Volume Center + Volume Extents）。\n" +
                "移动 Volume Center 可改位置；修改 Volume Extents 可改半宽尺寸。",
                MessageType.Info);
        }

        void OnSceneGUI()
        {
            var ward = (AnimalHospitalWard)target;
            serializedObject.Update();

            var centerTransform = _volumeCenter.objectReferenceValue as Transform;
            if (centerTransform == null)
                centerTransform = ward.transform;

            var extents = _volumeExtents.vector3Value;
            var half = new Vector3(
                Mathf.Max(0.05f, extents.x),
                Mathf.Max(0.05f, extents.y),
                Mathf.Max(0.05f, extents.z));

            var wire = new Color(0.2f, 0.85f, 0.95f, 1f);
            var size = half * 2f;

            Handles.color = wire;
            using (new Handles.DrawingScope(centerTransform.localToWorldMatrix))
            {
                Handles.DrawWireCube(Vector3.zero, size);
            }

            Handles.color = wire;
            Handles.SphereHandleCap(
                0,
                centerTransform.position,
                Quaternion.identity,
                0.18f,
                EventType.Repaint);

            var labelPos = centerTransform.position + Vector3.up * (half.y + 0.25f);
            Handles.Label(
                labelPos,
                $"病房范围 [{ward.WardId}]\nExtents {extents}");

            if (_volumeCenter.objectReferenceValue is Transform movableCenter)
            {
                EditorGUI.BeginChangeCheck();
                var newPos = Handles.PositionHandle(movableCenter.position, movableCenter.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(movableCenter, "Move Ward Volume Center");
                    movableCenter.position = newPos;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

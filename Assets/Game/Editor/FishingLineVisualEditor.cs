#if UNITY_EDITOR
using Game.Fishing;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    [CustomEditor(typeof(FishingLineVisual))]
    public sealed class FishingLineVisualEditor : UnityEditor.Editor
    {
        SerializedProperty _castAnimDuration;
        SerializedProperty _castPrepareWaitTime;
        SerializedProperty _castKeyframe1Time;
        SerializedProperty _castKeyframe2Time;
        SerializedProperty _castForwardDistance;
        SerializedProperty _castForwardArcHeight;
        SerializedProperty _retractAnimDuration;

        float _castPreviewT;
        float _retractPreviewT;
        Vector3 _previewCastTarget;
        bool _hasPreviewTarget;

        void OnEnable()
        {
            _castAnimDuration = serializedObject.FindProperty("castAnimDuration");
            _castPrepareWaitTime = serializedObject.FindProperty("castPrepareWaitTime");
            _castKeyframe1Time = serializedObject.FindProperty("castKeyframe1Time");
            _castKeyframe2Time = serializedObject.FindProperty("castKeyframe2Time");
            _castForwardDistance = serializedObject.FindProperty("castForwardDistance");
            _castForwardArcHeight = serializedObject.FindProperty("castForwardArcHeight");
            _retractAnimDuration = serializedObject.FindProperty("retractAnimDuration");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var visual = (FishingLineVisual)target;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("抛竿鱼线预览（Scene 实时）", EditorStyles.boldLabel);

            DrawPreviewTargetField(visual);

            var endTime = Mathf.Max(
                _castAnimDuration.floatValue,
                _castKeyframe2Time.floatValue,
                _castKeyframe1Time.floatValue,
                _castPrepareWaitTime.floatValue,
                0.05f);

            EditorGUI.BeginChangeCheck();
            _castPreviewT = EditorGUILayout.Slider("抛竿预览进度（归一化）", _castPreviewT, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                ApplyCastPreview(visual);

            EditorGUILayout.LabelField(
                $"当前 ≈ {_castPreviewT * endTime:F2}s  |  准备 {_castPrepareWaitTime.floatValue:F1}s → " +
                $"后摆 {_castKeyframe1Time.floatValue:F1}s → 落水 {_castKeyframe2Time.floatValue:F1}s",
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"准备 ({_castPrepareWaitTime.floatValue:F1}s)"))
                SnapCastPreview(visual, _castPrepareWaitTime.floatValue, endTime);
            if (GUILayout.Button($"后摆 ({_castKeyframe1Time.floatValue:F1}s)"))
                SnapCastPreview(visual, _castKeyframe1Time.floatValue, endTime);
            if (GUILayout.Button($"落水 ({_castKeyframe2Time.floatValue:F1}s)"))
                SnapCastPreview(visual, _castKeyframe2Time.floatValue, endTime);
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            _retractPreviewT = EditorGUILayout.Slider("收线 morph 预览", _retractPreviewT, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                ApplyRetractPreview(visual);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("收线态"))
            {
                visual.SetRetractedImmediate();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("垂钓态"))
                ApplyFishingPreview(visual);

            if (GUILayout.Button("运行抛竿动画") && Application.isPlaying)
                visual.PlayCastAnimation(_previewCastTarget.y, visual.transform.root.forward, _castForwardDistance.floatValue);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "流程：0~准备等待(收线) → 后摆 → 平稳抛物线落水。\n" +
                "落水点由 castForwardDistance + 角色朝向决定，Y 约束在水面。\n" +
                "平稳度主要调 castForwardArcHeight（建议 0.6~1.0）。",
                MessageType.Info);
        }

        void DrawPreviewTargetField(FishingLineVisual visual)
        {
            if (!_hasPreviewTarget)
                _previewCastTarget = ResolvePreviewWaterAndForward(visual, out _);

            EditorGUI.BeginChangeCheck();
            _previewCastTarget = EditorGUILayout.Vector3Field("预览水面参考点", _previewCastTarget);
            if (EditorGUI.EndChangeCheck())
                _hasPreviewTarget = true;

            if (GUILayout.Button("使用玩家前方默认落点方向"))
            {
                _previewCastTarget = ResolvePreviewWaterAndForward(visual, out _);
                _hasPreviewTarget = true;
                ApplyCastPreview(visual);
            }
        }

        static Vector3 ResolvePreviewWaterAndForward(FishingLineVisual visual, out Vector3 forward)
        {
            FishingZone zone = null;
#if UNITY_2023_1_OR_NEWER
            zone = Object.FindFirstObjectByType<FishingZone>();
#else
            zone = Object.FindObjectOfType<FishingZone>();
#endif

            forward = visual.transform.root.forward;
            if (zone != null)
            {
                var p = zone.GetCastTargetWorldPosition();
                return new Vector3(p.x, zone.GetWaterSurfaceWorldY(), p.z);
            }

            var pos = visual.transform.position + forward * 6f;
            pos.y = visual.transform.position.y;
            return pos;
        }

        void SnapCastPreview(FishingLineVisual visual, float seconds, float endTime)
        {
            _castPreviewT = Mathf.Clamp01(seconds / endTime);
            ApplyCastPreview(visual);
        }

        void ApplyCastPreview(FishingLineVisual visual)
        {
            var waterRef = _previewCastTarget;
            ResolvePreviewWaterAndForward(visual, out var forward);
            visual.EditorPreviewCastAt(_castPreviewT, waterRef.y, forward, _castForwardDistance.floatValue);
            SceneView.RepaintAll();
        }

        void ApplyRetractPreview(FishingLineVisual visual)
        {
            var waterRef = _previewCastTarget;
            ResolvePreviewWaterAndForward(visual, out var forward);
            visual.EditorPreviewRetractAt(_retractPreviewT, waterRef.y, forward, _castForwardDistance.floatValue);
            SceneView.RepaintAll();
        }

        void ApplyFishingPreview(FishingLineVisual visual)
        {
            var waterRef = _previewCastTarget;
            ResolvePreviewWaterAndForward(visual, out var forward);
            visual.EditorPreviewCastAt(1f, waterRef.y, forward, _castForwardDistance.floatValue);
            SceneView.RepaintAll();
        }
    }
}
#endif

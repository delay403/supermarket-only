using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cutscene
{
    /// <summary>过场摄像机：按时间轴在点位之间插值。</summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneCameraController : MonoBehaviour
    {
        [Serializable]
        public sealed class CameraKeyframe
        {
            public float time;
            public Transform waypoint;
            public float blendDuration = 0.75f;
        }

        [SerializeField] private Camera cutsceneCamera;
        [SerializeField] private CameraKeyframe[] keyframes;

        int _nextIndex;
        CameraKeyframe _activeFrom;
        CameraKeyframe _activeTo;
        float _blendElapsed;
        bool _blending;

        public Camera Camera => cutsceneCamera;

        void Awake()
        {
            if (cutsceneCamera == null)
                cutsceneCamera = GetComponent<Camera>();
        }

        public void ResetPlayback()
        {
            _nextIndex = 0;
            _activeFrom = null;
            _activeTo = null;
            _blendElapsed = 0f;
            _blending = false;

            if (keyframes != null && keyframes.Length > 0)
                SnapToKeyframe(keyframes[0]);
        }

        public void SetCameraEnabled(bool enabled)
        {
            if (cutsceneCamera != null)
                cutsceneCamera.enabled = enabled;
        }

        public void Tick(float elapsedSeconds, float deltaTime)
        {
            if (keyframes == null || keyframes.Length == 0 || cutsceneCamera == null)
                return;

            while (_nextIndex < keyframes.Length && elapsedSeconds >= keyframes[_nextIndex].time)
            {
                BeginBlendTo(keyframes[_nextIndex]);
                _nextIndex++;
            }

            if (!_blending || _activeTo == null)
                return;

            _blendElapsed += deltaTime;
            var duration = Mathf.Max(0.01f, _activeTo.blendDuration);
            var t = Mathf.Clamp01(_blendElapsed / duration);
            ApplyBlend(t);

            if (t >= 1f)
                _blending = false;
        }

        void BeginBlendTo(CameraKeyframe target)
        {
            _activeFrom = new CameraKeyframe
            {
                waypoint = cutsceneCamera.transform,
                blendDuration = target.blendDuration,
            };
            _activeTo = target;
            _blendElapsed = 0f;
            _blending = true;
        }

        void ApplyBlend(float t)
        {
            if (_activeTo?.waypoint == null)
                return;

            var fromPos = _activeFrom?.waypoint != null ? _activeFrom.waypoint.position : cutsceneCamera.transform.position;
            var fromRot = _activeFrom?.waypoint != null ? _activeFrom.waypoint.rotation : cutsceneCamera.transform.rotation;
            var toPos = _activeTo.waypoint.position;
            var toRot = _activeTo.waypoint.rotation;

            cutsceneCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(fromPos, toPos, t),
                Quaternion.Slerp(fromRot, toRot, t));
        }

        void SnapToKeyframe(CameraKeyframe keyframe)
        {
            if (keyframe?.waypoint == null || cutsceneCamera == null)
                return;

            cutsceneCamera.transform.SetPositionAndRotation(
                keyframe.waypoint.position,
                keyframe.waypoint.rotation);
        }
    }
}

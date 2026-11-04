using System;
using System.Collections;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>接待区窗帘：交互后驱动一组物体在打开/关闭位置之间升降。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalCurtainController : MonoBehaviour
    {
        [Serializable]
        public sealed class MotionTarget
        {
            public Transform target;
            [Tooltip("拉帘关闭时，相对打开位置的本地 Y 偏移（负值=下落）。")]
            public float closedLocalYOffset = -2.5f;
            [NonSerialized] public Vector3 openLocalPosition;
        }

        [SerializeField] private MotionTarget[] targets = Array.Empty<MotionTarget>();
        [SerializeField, Min(0.05f)] private float moveDuration = 0.45f;

        Coroutine _moveRoutine;
        bool _closed;

        public bool IsClosed => _closed;

        void Awake() => CaptureOpenPositions();

        void OnValidate()
        {
            if (targets == null || targets.Length == 0)
                return;

            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                    targets[i] = new MotionTarget();
            }
        }

        public void CaptureOpenPositions()
        {
            if (targets == null)
                return;

            for (var i = 0; i < targets.Length; i++)
            {
                var entry = targets[i];
                if (entry?.target == null)
                    continue;

                entry.openLocalPosition = entry.target.localPosition;
            }
        }

        public void SetClosed(bool closed, bool immediate = false)
        {
            _closed = closed;
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            if (immediate || moveDuration <= 0.05f || !isActiveAndEnabled)
            {
                ApplyClosedState(closed, 1f);
                return;
            }

            _moveRoutine = StartCoroutine(AnimateRoutine(closed));
        }

        IEnumerator AnimateRoutine(bool closed)
        {
            var elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / moveDuration);
                ApplyClosedState(closed, t);
                yield return null;
            }

            ApplyClosedState(closed, 1f);
            _moveRoutine = null;
        }

        void ApplyClosedState(bool closed, float t)
        {
            if (targets == null)
                return;

            for (var i = 0; i < targets.Length; i++)
            {
                var entry = targets[i];
                if (entry?.target == null)
                    continue;

                var open = entry.openLocalPosition;
                var closedPos = open + new Vector3(0f, entry.closedLocalYOffset, 0f);
                entry.target.localPosition = closed
                    ? Vector3.Lerp(open, closedPos, t)
                    : Vector3.Lerp(closedPos, open, t);
            }
        }
    }
}

using System;
using UnityEngine;

namespace Game.Cutscene
{
    /// <summary>过场 NPC：按时间轴播放动画与位移。</summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneActorController : MonoBehaviour
    {
        [Serializable]
        public sealed class ActorCue
        {
            public float time;
            public string animationStateName;
            public Transform moveTarget;
            public float moveDuration = 0.75f;
        }

        [SerializeField] private Animator animator;
        [SerializeField] private Transform actorRoot;
        [SerializeField] private ActorCue[] cues;

        int _nextCueIndex;
        Coroutine _moveRoutine;

        void Awake()
        {
            if (actorRoot == null)
                actorRoot = transform;
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        public void ResetPlayback()
        {
            _nextCueIndex = 0;
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }
        }

        public void Tick(float elapsedSeconds)
        {
            if (cues == null || cues.Length == 0)
                return;

            while (_nextCueIndex < cues.Length && elapsedSeconds >= cues[_nextCueIndex].time)
            {
                ApplyCue(cues[_nextCueIndex]);
                _nextCueIndex++;
            }
        }

        void ApplyCue(ActorCue cue)
        {
            if (cue == null)
                return;

            if (animator != null && !string.IsNullOrWhiteSpace(cue.animationStateName))
                animator.Play(cue.animationStateName, 0, 0f);

            if (cue.moveTarget != null && actorRoot != null)
            {
                if (_moveRoutine != null)
                    StopCoroutine(_moveRoutine);
                _moveRoutine = StartCoroutine(CoMoveTo(cue.moveTarget.position, cue.moveDuration));
            }
        }

        System.Collections.IEnumerator CoMoveTo(Vector3 targetWorld, float duration)
        {
            if (actorRoot == null)
                yield break;

            var start = actorRoot.position;
            duration = Mathf.Max(0.01f, duration);
            var t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                actorRoot.position = Vector3.Lerp(start, targetWorld, Mathf.Clamp01(t / duration));
                yield return null;
            }

            actorRoot.position = targetWorld;
            _moveRoutine = null;
        }
    }
}

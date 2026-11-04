using System;
using Game.Cutscene;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>开场/过场：可接 <see cref="CutsceneSequenceController"/>，否则按计时占位。</summary>
    public sealed class SupermarketOrganizeIntroController : MonoBehaviour
    {
        [SerializeField] private bool playIntroOnStart;
        [SerializeField] private float introDurationSeconds = 0f;
        [SerializeField] private CutsceneSequenceController cutsceneSequence;

        public event Action IntroFinished;

        public bool IsIntroPending { get; private set; }

        public void Configure(bool enabled, float durationSeconds, CutsceneSequenceController sequence = null)
        {
            playIntroOnStart = enabled;
            introDurationSeconds = Mathf.Max(0f, durationSeconds);
            if (sequence != null)
                cutsceneSequence = sequence;
        }

        public void BeginIntroIfNeeded()
        {
            if (cutsceneSequence != null && playIntroOnStart)
            {
                IsIntroPending = true;
                cutsceneSequence.Finished += OnCutsceneFinished;
                cutsceneSequence.Play();
                return;
            }

            if (!playIntroOnStart || introDurationSeconds <= 0f)
            {
                IsIntroPending = false;
                IntroFinished?.Invoke();
                return;
            }

            IsIntroPending = true;
            CancelInvoke(nameof(FinishIntro));
            Invoke(nameof(FinishIntro), introDurationSeconds);
        }

        /// <summary>中止尚未完成的开场（中途退出到模式内主页时调用），不会触发 <see cref="IntroFinished"/>。</summary>
        public void CancelIntro()
        {
            CancelInvoke(nameof(FinishIntro));
            if (cutsceneSequence != null)
            {
                cutsceneSequence.Finished -= OnCutsceneFinished;
                cutsceneSequence.StopImmediate();
            }

            IsIntroPending = false;
        }

        void OnCutsceneFinished()
        {
            if (cutsceneSequence != null)
                cutsceneSequence.Finished -= OnCutsceneFinished;
            FinishIntro();
        }

        void FinishIntro()
        {
            IsIntroPending = false;
            IntroFinished?.Invoke();
        }
    }
}

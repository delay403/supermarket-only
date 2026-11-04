using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Cutscene
{
    /// <summary>原生过场播放控制器：摄像机、对白、NPC 与跳过。</summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneSequenceController : MonoBehaviour
    {
        [Serializable]
        public sealed class DialogueLine
        {
            public float startTime;
            public string speakerName;
            [TextArea(2, 5)] public string text;
            public float displayDuration;
        }

        [SerializeField] private float totalDuration = 5f;
        [SerializeField] private bool disableGameplayInput = true;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private CutsceneCameraController cutsceneCamera;
        [SerializeField] private CutsceneDialogueUi dialogueUi;
        [SerializeField] private Button skipButton;
        [SerializeField] private CutsceneActorController[] actors;
        [SerializeField] private DialogueLine[] dialogueLines;

        readonly List<int> _playedDialogue = new List<int>();
        float _elapsed;
        bool _playing;
        bool _finished;

        public event Action Finished;
        public bool IsPlaying => _playing;
        public bool IsFinished => _finished;

        void Awake()
        {
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            dialogueUi?.BindSkipButton(skipButton);
            if (dialogueUi != null)
                dialogueUi.SkipRequested += Skip;

            skipButton?.onClick.AddListener(Skip);
            StopImmediate();
        }

        void OnDestroy()
        {
            if (dialogueUi != null)
                dialogueUi.SkipRequested -= Skip;
            skipButton?.onClick.RemoveListener(Skip);
        }

        void Update()
        {
            if (!_playing)
                return;

            _elapsed += Time.deltaTime;
            cutsceneCamera?.Tick(_elapsed, Time.deltaTime);

            if (actors != null)
            {
                for (var i = 0; i < actors.Length; i++)
                    actors[i]?.Tick(_elapsed);
            }

            TickDialogue();

            if (_elapsed >= totalDuration)
                FinishPlayback();
        }

        public void Play()
        {
            if (_playing)
                return;

            _playing = true;
            _finished = false;
            _elapsed = 0f;
            _playedDialogue.Clear();

            if (gameplayCamera != null)
                gameplayCamera.enabled = false;

            cutsceneCamera?.ResetPlayback();
            cutsceneCamera?.SetCameraEnabled(true);

            if (actors != null)
            {
                for (var i = 0; i < actors.Length; i++)
                    actors[i]?.ResetPlayback();
            }

            if (skipButton != null)
                skipButton.gameObject.SetActive(true);
        }

        public void Skip() => FinishPlayback();

        public void StopImmediate()
        {
            _playing = false;
            _finished = true;
            _elapsed = 0f;

            cutsceneCamera?.SetCameraEnabled(false);
            if (gameplayCamera != null)
                gameplayCamera.enabled = true;

            dialogueUi?.HideImmediate();
            if (skipButton != null)
                skipButton.gameObject.SetActive(false);
        }

        void TickDialogue()
        {
            if (dialogueLines == null || dialogueUi == null)
                return;

            for (var i = 0; i < dialogueLines.Length; i++)
            {
                if (_playedDialogue.Contains(i))
                    continue;

                var line = dialogueLines[i];
                if (_elapsed < line.startTime)
                    continue;

                _playedDialogue.Add(i);
                dialogueUi.ShowLine(line.speakerName, line.text, line.displayDuration);
            }
        }

        void FinishPlayback()
        {
            if (_finished)
                return;

            _playing = false;
            _finished = true;
            StopImmediate();
            Finished?.Invoke();
        }
    }
}

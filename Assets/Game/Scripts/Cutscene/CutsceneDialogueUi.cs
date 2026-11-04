using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Cutscene
{
    /// <summary>过场 UI：说话者、打字机文本、跳过按钮。</summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneDialogueUi : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Button skipButton;
        [SerializeField] private float charsPerSecond = 28f;

        Coroutine _typeRoutine;
        StringBuilder _builder = new StringBuilder();

        public event Action SkipRequested;

        void Awake()
        {
            skipButton?.onClick.AddListener(OnSkipClicked);
            HideImmediate();
        }

        void OnDestroy()
        {
            skipButton?.onClick.RemoveListener(OnSkipClicked);
        }

        public void BindSkipButton(Button externalSkipButton)
        {
            if (externalSkipButton == null)
                return;

            externalSkipButton.onClick.AddListener(OnSkipClicked);
        }

        public void ShowLine(string speaker, string content, float maxDurationSeconds = 0f)
        {
            if (root != null)
                root.SetActive(true);

            if (speakerText != null)
                speakerText.text = speaker ?? string.Empty;

            if (_typeRoutine != null)
                StopCoroutine(_typeRoutine);

            _typeRoutine = StartCoroutine(CoTypeLine(content ?? string.Empty, maxDurationSeconds));
        }

        public void HideImmediate()
        {
            if (_typeRoutine != null)
            {
                StopCoroutine(_typeRoutine);
                _typeRoutine = null;
            }

            if (root != null)
                root.SetActive(false);

            if (bodyText != null)
                bodyText.text = string.Empty;
        }

        IEnumerator CoTypeLine(string content, float maxDurationSeconds)
        {
            if (bodyText == null)
                yield break;

            _builder.Clear();
            bodyText.text = string.Empty;

            var delay = charsPerSecond > 0f ? 1f / charsPerSecond : 0f;
            var elapsed = 0f;

            for (var i = 0; i < content.Length; i++)
            {
                _builder.Append(content[i]);
                bodyText.text = _builder.ToString();

                if (maxDurationSeconds > 0f)
                {
                    elapsed += delay;
                    if (elapsed >= maxDurationSeconds)
                    {
                        bodyText.text = content;
                        break;
                    }
                }

                if (delay > 0f)
                    yield return new WaitForSeconds(delay);
            }

            _typeRoutine = null;
        }

        void OnSkipClicked() => SkipRequested?.Invoke();
    }
}

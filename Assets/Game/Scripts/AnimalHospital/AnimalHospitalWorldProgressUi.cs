using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 世界空间进度 UI（接待电脑 / 病房电脑）。
    /// 请在场景中自建 World Space Canvas，并把 Canvas / Slider / Text 拖到下列字段；运行时不会自动生成。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalWorldProgressUi : MonoBehaviour
    {
        [Header("请自行在场景中创建并引用")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Slider progressBar;
        [SerializeField] private Text statusText;
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private bool hideOnAwake = true;

        Coroutine _routine;

        void Awake()
        {
            if (hideOnAwake)
                SetVisible(false);
        }

        void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            AnimalHospitalAudio.Resolve()?.SetComputerAnalysisLoop(false);
        }

        public bool IsConfigured =>
            canvas != null && progressBar != null && statusText != null;

        public void Play(
            string loadingText,
            string completedText,
            float stepIntervalSeconds,
            float stepPercent,
            Action onCompleted = null)
        {
            if (!IsConfigured)
            {
                Debug.LogWarning(
                    $"[AH] {name} 的 WorldProgressUi 未完整配置（需要 Canvas / ProgressBar / StatusText）。",
                    this);
                onCompleted?.Invoke();
                return;
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            SetVisible(true);
            if (_routine != null)
                StopCoroutine(_routine);
            AnimalHospitalAudio.Resolve()?.SetComputerAnalysisLoop(true);
            _routine = StartCoroutine(PlayRoutine(loadingText, completedText, stepIntervalSeconds, stepPercent, onCompleted));
        }

        IEnumerator PlayRoutine(
            string loadingText,
            string completedText,
            float stepIntervalSeconds,
            float stepPercent,
            Action onCompleted)
        {
            progressBar.gameObject.SetActive(true);
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
            statusText.text = loadingText ?? string.Empty;

            var progress = 0f;
            var step = Mathf.Clamp01(stepPercent);
            var interval = Mathf.Max(0.05f, stepIntervalSeconds);
            while (progress < 0.999f)
            {
                yield return new WaitForSeconds(interval);
                progress = Mathf.Min(1f, progress + step);
                progressBar.value = progress;
            }

            progressBar.gameObject.SetActive(false);
            statusText.text = completedText ?? string.Empty;
            AnimalHospitalAudio.Resolve()?.SetComputerAnalysisLoop(false);
            onCompleted?.Invoke();
            _routine = null;
        }

        void SetVisible(bool visible)
        {
            if (canvas == null)
            {
                // 无独立 Canvas 时不能关掉宿主，否则协程无法启动。
                if (visible && !gameObject.activeSelf)
                    gameObject.SetActive(true);
                return;
            }

            if (canvas.gameObject == gameObject)
                canvas.enabled = visible;
            else
                canvas.gameObject.SetActive(visible);
        }

        void LateUpdate()
        {
            if (!billboardToCamera || canvas == null || !canvas.isActiveAndEnabled)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            var dir = canvas.transform.position - cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                canvas.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}

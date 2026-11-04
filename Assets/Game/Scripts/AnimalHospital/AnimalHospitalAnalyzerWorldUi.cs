using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 分析机世界 UI。请自建 World Space Canvas + Text，并拖到 Status Text。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalAnalyzerWorldUi : MonoBehaviour
    {
        [Header("请自行在场景中创建并引用")]
        [SerializeField] private Canvas canvas;
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

        public bool IsConfigured => canvas != null && statusText != null;

        public void Play(float durationSeconds, Action onCompleted = null)
        {
            if (!IsConfigured)
            {
                Debug.LogWarning($"[AH] {name} 的 AnalyzerWorldUi 未配置 Canvas/StatusText。", this);
                onCompleted?.Invoke();
                return;
            }

            // 协程必须跑在激活物体上；显隐只关掉 Canvas，不关掉本组件宿主。
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            SetVisible(true);
            if (_routine != null)
                StopCoroutine(_routine);
            AnimalHospitalAudio.Resolve()?.SetComputerAnalysisLoop(true);
            _routine = StartCoroutine(PlayRoutine(durationSeconds, onCompleted));
        }

        IEnumerator PlayRoutine(float durationSeconds, Action onCompleted)
        {
            var duration = Mathf.Clamp(durationSeconds, 3f, 5f);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                statusText.text = $"{Mathf.RoundToInt(t * 100f)}%";
                yield return null;
            }

            statusText.text = "已分析";
            AnimalHospitalAudio.Resolve()?.SetComputerAnalysisLoop(false);
            onCompleted?.Invoke();
            _routine = null;
        }

        void SetVisible(bool visible)
        {
            if (canvas == null)
                return;

            // 脚本挂在 Canvas 同一物体上时，用 enabled，避免 SetActive 把协程宿主关掉。
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

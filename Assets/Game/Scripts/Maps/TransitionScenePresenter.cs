using UnityEngine;
using UnityEngine.UI;

namespace Game.Maps
{
    /// <summary>
    /// 过场场景：显示场景加载进度。
    /// </summary>
    public sealed class TransitionScenePresenter : MonoBehaviour
    {
        [SerializeField] private GameObject progressRoot;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Text statusText;

        void OnEnable()
        {
            SceneTransitionCoroutineHost.LoadProgressChanged += OnProgress;
            SceneTransitionCoroutineHost.LoadStatusChanged += OnStatus;
            ApplyProgress(0f);
            ApplyStatus(string.Empty);
        }

        void OnDisable()
        {
            SceneTransitionCoroutineHost.LoadProgressChanged -= OnProgress;
            SceneTransitionCoroutineHost.LoadStatusChanged -= OnStatus;
        }

        void OnProgress(float p)
        {
            ApplyProgress(p);
        }

        void OnStatus(string status)
        {
            ApplyStatus(status);
        }

        void ApplyProgress(float p)
        {
            p = Mathf.Clamp01(p);
            if (progressRoot != null)
                progressRoot.SetActive(p > 0.001f || _hasActiveStatus);

            if (progressSlider != null)
            {
                progressSlider.gameObject.SetActive(true);
                progressSlider.value = p;
            }
        }

        bool _hasActiveStatus;

        void ApplyStatus(string status)
        {
            _hasActiveStatus = !string.IsNullOrWhiteSpace(status);

            if (progressRoot != null && _hasActiveStatus)
                progressRoot.SetActive(true);

            if (statusText == null)
                return;

            statusText.gameObject.SetActive(_hasActiveStatus);
            statusText.text = status ?? string.Empty;
        }
    }
}

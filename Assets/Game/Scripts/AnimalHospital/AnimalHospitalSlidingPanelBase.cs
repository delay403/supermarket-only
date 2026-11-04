using System;
using Game.Audio;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 动物医院 UGUI 面板基类：Root / Mask / MainPanel + <see cref="UiSlidingPanelHost"/> 自下而上弹入。
    /// </summary>
    public abstract class AnimalHospitalSlidingPanelBase : MonoBehaviour, IUiPanelController
    {
        [Header("Shell")]
        [SerializeField] protected GameObject root;
        [SerializeField] protected RectTransform mainPanel;
        [SerializeField] protected Image maskImage;
        [SerializeField] protected Button maskCloseButton;

        [Header("Animation")]
        [SerializeField] protected float slideDuration = 0.38f;
        [SerializeField] protected float settleDuration = 0.28f;
        [SerializeField] protected float overshootDownPx = 28f;
        [SerializeField] protected UiSlidingPanelHost slideHost;

        [Tooltip("为 true 时点击遮罩关闭面板。")]
        [SerializeField] protected bool allowMaskClose;

        public abstract UiPanelId PanelId { get; }

        protected virtual void Awake()
        {
            ResolveShellReferences();
            if (allowMaskClose)
                maskCloseButton?.onClick.AddListener(OnMaskCloseClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        protected virtual void OnDestroy()
        {
            if (allowMaskClose)
                maskCloseButton?.onClick.RemoveListener(OnMaskCloseClicked);
            slideHost?.KillTweens();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        public abstract void Open(UiOpenPayload payload);

        public virtual void Close() => Hide();

        protected void Show()
        {
            ResolveShellReferences();
            EnsureSlideHost();
            if (maskImage != null)
                maskImage.raycastTarget = true;
            AudioGameplayFeedback.PlayPanelOpen();
            slideHost.PlayOpen();
        }

        protected void Hide(Action onComplete = null)
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;

            slideHost.RequestClose(onComplete);
        }

        protected void HideImmediate()
        {
            ResolveShellReferences();
            EnsureSlideHost();
            slideHost.HideImmediate();
        }

        protected virtual void OnMaskCloseClicked()
        {
            if (!allowMaskClose)
                return;

            AudioGameplayFeedback.PlayUiButton();
            Close();
        }

        protected void EnsureSlideHost()
        {
            UiSlidingPanelHost.EnsureOn(
                this,
                ref slideHost,
                root,
                mainPanel,
                UiSlidingPanelHost.SlideDirection.FromBottom,
                slideDuration,
                settleDuration,
                overshootDownPx);
        }

        protected void ResolveShellReferences()
        {
            if (root == null)
            {
                var rootTf = transform.Find("Root");
                root = rootTf != null ? rootTf.gameObject : gameObject;
            }

            if (mainPanel == null)
            {
                var mainTf = root.transform.Find("MainPanel") as RectTransform;
                mainPanel = mainTf != null ? mainTf : root.GetComponent<RectTransform>();
            }

            if (maskImage == null)
            {
                var maskTf = root.transform.Find("Mask");
                if (maskTf != null)
                    maskImage = maskTf.GetComponent<Image>();
            }

            if (maskCloseButton == null && maskImage != null)
                maskCloseButton = maskImage.GetComponent<Button>();
        }
    }
}

using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Game.UI.Framework
{
    /// <summary>
    /// 可复用 UGUI 面板壳：显隐、TouchGuard、滑入/弹回关闭动画。
    /// 业务 Panel 只需绑定 root/mainPanel 并调用 <see cref="PlayOpen"/> / <see cref="RequestClose"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiSlidingPanelHost : MonoBehaviour
    {
        public enum SlideDirection
        {
            None = 0,
            FromLeft = 1,
            FromRight = 2,
            FromBottom = 3,
        }

        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform mainPanel;
        [SerializeField] private SlideDirection slideDirection = SlideDirection.FromLeft;
        [SerializeField] private float slideDuration = 0.42f;
        [SerializeField] private float settleDuration = 0.32f;
        [SerializeField] private float overshootPx = 72f;
        [SerializeField] private float openSlideInMultiplier = 0.55f;
        [SerializeField] private float closeSpeedMultiplier = UiPanelSlideAnim.DefaultCloseSpeedMultiplier;
        [SerializeField] private bool manageTouchGuard = true;

        Sequence _sequence;
        bool _shown;
        bool _closing;
        bool _restCached;
        Vector2 _restAnchored;
        MonoBehaviour _owner;
        int _operationId;
        Action _pendingCloseCallback;
        Coroutine _deferredPopulateRoutine;

        public bool IsShown => _shown;
        public bool IsClosing => _closing;
        public GameObject Root => root;
        public RectTransform MainPanel => mainPanel;

        void Awake()
        {
            if (!_restCached)
                CacheRestPosition();
        }

        void OnDestroy()
        {
            CancelDeferredPopulate();
            KillTweens();
            if (_shown && manageTouchGuard)
                FairyGuiTouchGuard.PopBlock();
            _shown = false;
            _closing = false;
        }

        public void Bind(GameObject panelRoot, RectTransform panelMain)
        {
            root = panelRoot;
            mainPanel = panelMain;
            if (!_restCached)
                CacheRestPosition();
        }

        public void ConfigureSlide(
            SlideDirection direction,
            float slideSeconds = 0.42f,
            float settleSeconds = 0.32f,
            float overshoot = 72f,
            float slideInMultiplier = 0.55f,
            float closeSpeed = UiPanelSlideAnim.DefaultCloseSpeedMultiplier)
        {
            slideDirection = direction;
            slideDuration = slideSeconds;
            settleDuration = settleSeconds;
            overshootPx = overshoot;
            openSlideInMultiplier = slideInMultiplier;
            closeSpeedMultiplier = closeSpeed;
        }

        public void CacheRestPosition()
        {
            if (mainPanel == null)
                return;

            _restAnchored = mainPanel.anchoredPosition;
            _restCached = true;
        }

        public void ResetToRestPosition()
        {
            if (mainPanel == null)
                return;

            KillTweens();
            mainPanel.anchoredPosition = _restAnchored;
        }

        /// <summary>
        /// 先显示壳体并播放滑入；重逻辑（RebuildGrid 等）请放入 <paramref name="deferredPopulate"/>，避免同帧卡顿。
        /// </summary>
        public void PlayOpen(Action deferredPopulate = null)
        {
            CancelDeferredPopulate();
            _operationId++;
            _closing = false;
            _pendingCloseCallback = null;

            var wasHidden = !_shown;
            if (wasHidden && manageTouchGuard)
                FairyGuiTouchGuard.PushBlock();
            _shown = true;

            if (root != null)
                root.SetActive(true);

            UiPanelScrollUtility.SetScrollRectsActive(root, true);

            if (mainPanel == null || slideDirection == SlideDirection.None)
            {
                ScheduleDeferredPopulate(deferredPopulate);
                return;
            }

            KillTweens();
            switch (slideDirection)
            {
                case SlideDirection.FromRight:
                    UiPanelSlideAnim.PlayOpenFromRight(
                        mainPanel, _restAnchored, slideDuration, settleDuration, overshootPx, ref _sequence);
                    break;
                case SlideDirection.FromBottom:
                    UiPanelSlideAnim.PlayOpenFromBottom(
                        mainPanel, _restAnchored, slideDuration, settleDuration, overshootPx, ref _sequence);
                    break;
                default:
                    UiPanelSlideAnim.PlayOpenFromLeft(
                        mainPanel, _restAnchored, slideDuration, settleDuration, overshootPx,
                        openSlideInMultiplier, ref _sequence);
                    break;
            }

            ScheduleDeferredPopulate(deferredPopulate);
        }

        public void RequestClose(Action onClosed = null)
        {
            if (!_shown)
                return;

            if (_closing)
            {
                if (onClosed != null)
                    _pendingCloseCallback = (Action)Delegate.Combine(_pendingCloseCallback, onClosed);
                return;
            }

            CancelDeferredPopulate();
            _operationId++;
            var opId = _operationId;
            _pendingCloseCallback = onClosed;

            KillTweens();
            if (mainPanel == null || slideDirection == SlideDirection.None)
            {
                CompleteClose(opId);
                return;
            }

            mainPanel.anchoredPosition = _restAnchored;
            _closing = true;
            switch (slideDirection)
            {
                case SlideDirection.FromRight:
                    UiPanelSlideAnim.PlayCloseFromRight(
                        mainPanel, _restAnchored, slideDuration, settleDuration, overshootPx,
                        () => CompleteClose(opId), ref _sequence, closeSpeedMultiplier);
                    break;
                case SlideDirection.FromBottom:
                    UiPanelSlideAnim.PlayCloseFromBottom(
                        mainPanel, _restAnchored, slideDuration, settleDuration, overshootPx,
                        () => CompleteClose(opId), ref _sequence, closeSpeedMultiplier);
                    break;
                default:
                    UiPanelSlideAnim.PlayCloseFromLeft(
                        mainPanel, _restAnchored, slideDuration, settleDuration, overshootPx,
                        openSlideInMultiplier, () => CompleteClose(opId), ref _sequence, closeSpeedMultiplier);
                    break;
            }
        }

        public void HideImmediate()
        {
            CancelDeferredPopulate();
            _operationId++;
            KillTweens();
            if (mainPanel != null)
                mainPanel.anchoredPosition = _restAnchored;
            _closing = false;
            if (root != null)
                root.SetActive(false);
            UiPanelScrollUtility.SetScrollRectsActive(root, false);
            if (_shown && manageTouchGuard)
                FairyGuiTouchGuard.PopBlock();
            _shown = false;
            InvokePendingCloseCallback();
        }

        void CompleteClose(int opId)
        {
            if (opId != _operationId)
                return;

            _closing = false;
            if (mainPanel != null)
                mainPanel.anchoredPosition = _restAnchored;
            if (root != null)
                root.SetActive(false);
            UiPanelScrollUtility.SetScrollRectsActive(root, false);
            if (_shown && manageTouchGuard)
                FairyGuiTouchGuard.PopBlock();
            _shown = false;
            InvokePendingCloseCallback();
        }

        void InvokePendingCloseCallback()
        {
            var cb = _pendingCloseCallback;
            _pendingCloseCallback = null;
            cb?.Invoke();
        }

        void ScheduleDeferredPopulate(Action deferredPopulate)
        {
            if (deferredPopulate == null)
                return;

            if (_owner == null)
            {
                deferredPopulate();
                return;
            }

            var opId = _operationId;
            _deferredPopulateRoutine = _owner.StartCoroutine(DeferredPopulate(opId, deferredPopulate));
        }

        IEnumerator DeferredPopulate(int opId, Action populate)
        {
            yield return null;
            _deferredPopulateRoutine = null;
            if (opId != _operationId || !_shown || _closing)
                yield break;
            populate?.Invoke();
        }

        void CancelDeferredPopulate()
        {
            if (_deferredPopulateRoutine == null || _owner == null)
                return;

            _owner.StopCoroutine(_deferredPopulateRoutine);
            _deferredPopulateRoutine = null;
        }

        public void KillTweens()
        {
            _sequence?.Kill();
            _sequence = null;
            if (mainPanel != null)
                mainPanel.DOKill();
        }

        /// <summary>在业务 Panel 上确保存在并绑定滑动壳（减少各 Panel 重复样板代码）。</summary>
        public static UiSlidingPanelHost EnsureOn(
            MonoBehaviour owner,
            ref UiSlidingPanelHost host,
            GameObject panelRoot,
            RectTransform panelMain,
            SlideDirection direction,
            float slideSeconds = 0.42f,
            float settleSeconds = 0.32f,
            float overshoot = 72f,
            float slideInMultiplier = 0.55f,
            float closeSpeed = UiPanelSlideAnim.DefaultCloseSpeedMultiplier)
        {
            if (owner == null)
                return host;

            if (host == null)
                host = owner.GetComponent<UiSlidingPanelHost>();
            if (host == null)
                host = owner.gameObject.AddComponent<UiSlidingPanelHost>();

            host._owner = owner;
            host.Bind(panelRoot, panelMain);
            host.ConfigureSlide(direction, slideSeconds, settleSeconds, overshoot, slideInMultiplier, closeSpeed);
            return host;
        }
    }
}
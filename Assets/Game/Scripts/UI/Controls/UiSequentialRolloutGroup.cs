using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Controls
{
    public enum UiRolloutDirection
    {
        Right = 0,
        Left = 1,
        Up = 2,
        Down = 3,
    }

    /// <summary>
    /// 可展开/收起的按钮组：运行时统计可见按钮后自适应布局。
    /// 0 个可见按钮时隐藏自身；1 个时仅显示该按钮（无开关、Item 背景透明）；
    /// 2 个及以上时收起默认显示首个按钮 + 开关，展开显示全部。
    /// 支持左右上下四个展开方向。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiSequentialRolloutGroup : MonoBehaviour
    {
        const float ExpandedBaseSize = 100f;
        const float ExpandedSizeStep = 70f;
        const float CollapsedPanelSize = 50f;
        const float ExpandedToggleScaleSign = 1f;
        const float CollapsedToggleScaleSign = -1f;
        const float SingleButtonPanelAlpha = 1f / 255f;
        const int DotVisibleMinButtonCount = 3;
        const int StableFrameCount = 2;
        const float MaxWaitForButtonsSeconds = 3f;

        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private RectTransform itemsRoot;
        [SerializeField] private Button toggleButton;

        [Tooltip("开关按钮本体（Btn）：方向靠 localEulerAngles.z（右0/左180/下-90/上90）；展开收起只改 scale.x（展开 1，收起 -1）。")]
        [SerializeField] private RectTransform toggleIcon;

        [Tooltip("收起且可见按钮数 >= 3 时显示。")]
        [SerializeField] private GameObject notificationDot;

        [SerializeField] private bool startExpanded = false;

        [Header("Direction")]
        [Tooltip("展开生长方向：Right/Left 改宽度，Up/Down 改高度。")]
        [SerializeField] private UiRolloutDirection rolloutDirection = UiRolloutDirection.Right;

        [Header("Animation")]
        [SerializeField] private float itemDuration = 0.28f;
        [SerializeField] private float staggerDelay = 0.06f;
        [SerializeField] private float slideOffsetPx = 48f;
        [SerializeField] private Ease expandEase = Ease.OutBack;
        [SerializeField] private Ease collapseEase = Ease.InBack;

        bool _expanded;
        bool _animating;
        bool _initialized;
        int _trackedVisibleCount = -1;
        Sequence _sequence;
        LayoutGroup _layoutGroup;
        Image _panelImage;
        float _panelCrossAxisSize;
        float _panelNormalAlpha = 1f;

        struct ItemState
        {
            public RectTransform Rt;
            public Vector2 RestAnchoredPos;
            public CanvasGroup CanvasGroup;
        }

        readonly List<ItemState> _visibleItems = new List<ItemState>();

        public bool IsExpanded => _expanded;
        public UiRolloutDirection Direction => rolloutDirection;

        bool IsHorizontal =>
            rolloutDirection == UiRolloutDirection.Right || rolloutDirection == UiRolloutDirection.Left;

        void Awake()
        {
            toggleButton?.onClick.AddListener(OnToggleClicked);
            if (panelRoot != null)
            {
                panelRoot.TryGetComponent(out _panelImage);
                if (_panelImage != null)
                    _panelNormalAlpha = _panelImage.color.a;
            }

            ApplyToggleDirectionRotation();
            EnsureChildNotifiers();
        }

        void Start()
        {
            StartCoroutine(CoInitialize());
        }

        void OnDestroy()
        {
            KillTween();
            toggleButton?.onClick.RemoveListener(OnToggleClicked);
        }

        void LateUpdate()
        {
            if (!_initialized || _animating)
                return;

            var count = CountActiveChildren();
            if (count == _trackedVisibleCount)
                return;

            _trackedVisibleCount = count;
            KillTween();
            ApplyRuntimeLayout(false);
        }

        IEnumerator CoInitialize()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            var stableFrames = 0;
            var lastCount = -1;
            var elapsed = 0f;

            while (elapsed < MaxWaitForButtonsSeconds || stableFrames < StableFrameCount)
            {
                var count = CountActiveChildren();
                if (count == lastCount)
                    stableFrames++;
                else
                {
                    stableFrames = 0;
                    lastCount = count;
                }

                ApplyRuntimeLayout(false);
                elapsed += Time.unscaledDeltaTime;
                yield return new WaitForEndOfFrame();
            }

            _expanded = startExpanded;
            _initialized = true;
            ApplyRuntimeLayout(false);
        }

        internal void OnMemberVisibilityChanged()
        {
            if (!_initialized || _animating)
                return;

            KillTween();
            ApplyRuntimeLayout(false);
        }

        /// <summary>平台按钮显隐变化后调用，重新计算布局。</summary>
        public void RefreshVisibleState(bool animate = false)
        {
            if (!_initialized)
                return;

            KillTween();
            ApplyRuntimeLayout(animate);
        }

        public void SetExpanded(bool expanded, bool animate = true)
        {
            if (!_initialized || _visibleItems.Count <= 1)
                return;

            if (_expanded == expanded)
                return;

            _expanded = expanded;
            UpdateNotificationDot();
            if (animate)
                PlayAnimation(_expanded);
            else
                ApplyRuntimeLayout(false);
        }

        void OnToggleClicked()
        {
            if (!_initialized || _animating || _visibleItems.Count <= 1)
                return;

            AudioGameplayFeedback.PlayUiButton();
            SetExpanded(!_expanded);
        }

        void EnsureChildNotifiers()
        {
            if (itemsRoot == null)
                return;

            for (var i = 0; i < itemsRoot.childCount; i++)
            {
                var child = itemsRoot.GetChild(i).gameObject;
                if (child.GetComponent<UiRolloutGroupMemberNotifier>() == null)
                    child.AddComponent<UiRolloutGroupMemberNotifier>().Bind(this);
            }
        }

        int CountActiveChildren()
        {
            if (itemsRoot == null)
                return 0;

            var count = 0;
            for (var i = 0; i < itemsRoot.childCount; i++)
            {
                if (itemsRoot.GetChild(i).gameObject.activeSelf)
                    count++;
            }

            return count;
        }

        void CacheVisibleItems(bool enableLayout, bool leaveLayoutEnabled)
        {
            _visibleItems.Clear();
            if (itemsRoot == null)
                return;

            if (_layoutGroup == null)
                itemsRoot.TryGetComponent(out _layoutGroup);

            if (enableLayout && _layoutGroup != null)
                _layoutGroup.enabled = true;

            if (enableLayout)
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRoot);

            for (var i = 0; i < itemsRoot.childCount; i++)
            {
                var rt = itemsRoot.GetChild(i) as RectTransform;
                if (rt == null || !rt.gameObject.activeSelf)
                    continue;

                if (!rt.TryGetComponent(out CanvasGroup canvasGroup))
                    canvasGroup = rt.gameObject.AddComponent<CanvasGroup>();

                _visibleItems.Add(new ItemState
                {
                    Rt = rt,
                    RestAnchoredPos = rt.anchoredPosition,
                    CanvasGroup = canvasGroup
                });
            }

            if (_layoutGroup != null)
                _layoutGroup.enabled = leaveLayoutEnabled;

            if (panelRoot != null)
                _panelCrossAxisSize = IsHorizontal ? panelRoot.sizeDelta.y : panelRoot.sizeDelta.x;

            _trackedVisibleCount = _visibleItems.Count;
        }

        void RefreshCachedPositions()
        {
            for (var i = 0; i < _visibleItems.Count; i++)
            {
                var item = _visibleItems[i];
                if (item.Rt == null)
                    continue;

                item.RestAnchoredPos = item.Rt.anchoredPosition;
                _visibleItems[i] = item;
            }
        }

        static float GetExpandedMainAxisSize(int visibleButtonCount)
        {
            if (visibleButtonCount <= 0)
                return CollapsedPanelSize;
            return ExpandedBaseSize + (visibleButtonCount - 1) * ExpandedSizeStep;
        }

        Vector2 BuildPanelSize(float mainAxisSize)
        {
            return IsHorizontal
                ? new Vector2(mainAxisSize, _panelCrossAxisSize)
                : new Vector2(_panelCrossAxisSize, mainAxisSize);
        }

        Vector2 GetSlideOffset()
        {
            return rolloutDirection switch
            {
                UiRolloutDirection.Right => Vector2.left * slideOffsetPx,
                UiRolloutDirection.Left => Vector2.right * slideOffsetPx,
                UiRolloutDirection.Up => Vector2.down * slideOffsetPx,
                UiRolloutDirection.Down => Vector2.up * slideOffsetPx,
                _ => Vector2.left * slideOffsetPx,
            };
        }

        void ApplyPanelMainAxisSize(float mainAxisSize, bool instant)
        {
            if (panelRoot == null)
                return;

            panelRoot.DOKill(false);
            var target = BuildPanelSize(mainAxisSize);
            if (instant)
                panelRoot.sizeDelta = target;
            else
                panelRoot.DOSizeDelta(target, itemDuration).SetEase(Ease.OutQuad);
        }

        float PanelSizeDuration => staggerDelay > 0f ? staggerDelay : itemDuration * 0.5f;

        void InsertPanelSizeTween(float mainAxisSize, float delay)
        {
            if (panelRoot == null)
                return;

            _sequence.Insert(delay,
                panelRoot.DOSizeDelta(BuildPanelSize(mainAxisSize), PanelSizeDuration).SetEase(Ease.OutQuad));
        }

        void ApplyPanelBackgroundAlpha(bool singleButtonMode)
        {
            if (_panelImage == null)
                return;

            var color = _panelImage.color;
            color.a = singleButtonMode ? SingleButtonPanelAlpha : _panelNormalAlpha;
            _panelImage.color = color;
        }

        void ApplyToggleVisibility(bool visible)
        {
            if (toggleButton != null)
                toggleButton.gameObject.SetActive(visible);
        }

        void ApplyRuntimeLayout(bool animate)
        {
            CacheVisibleItems(enableLayout: true, leaveLayoutEnabled: true);

            if (_visibleItems.Count == 0)
            {
                KillTween();
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (_visibleItems.Count == 1)
            {
                KillTween();
                _expanded = false;
                ApplySingleButtonMode();
                return;
            }

            ApplyToggleVisibility(true);
            ApplyPanelBackgroundAlpha(false);

            if (animate && !_animating)
                PlayAnimation(_expanded);
            else
                ApplyMultiButtonInstantState(_expanded);
        }

        void ApplySingleButtonMode()
        {
            ApplyToggleVisibility(false);
            ApplyPanelBackgroundAlpha(true);
            UpdateNotificationDot();

            var item = _visibleItems[0];
            item.Rt.gameObject.SetActive(true);
            item.Rt.DOKill();
            item.CanvasGroup.DOKill();
            item.Rt.anchoredPosition = item.RestAnchoredPos;
            item.Rt.localScale = Vector3.one;
            item.CanvasGroup.alpha = 1f;
            item.CanvasGroup.blocksRaycasts = true;

            ApplyPanelMainAxisSize(GetExpandedMainAxisSize(1), true);
            CacheVisibleItems(enableLayout: true, leaveLayoutEnabled: true);
        }

        void ApplyMultiButtonInstantState(bool expanded)
        {
            for (var i = 0; i < _visibleItems.Count; i++)
            {
                var item = _visibleItems[i];
                if (item.Rt == null)
                    continue;

                item.Rt.gameObject.SetActive(true);
                item.Rt.DOKill();
                item.CanvasGroup.DOKill();

                var show = expanded || i == 0;
                ApplyItemVisual(item, show);
            }

            var size = expanded
                ? GetExpandedMainAxisSize(_visibleItems.Count)
                : GetExpandedMainAxisSize(1);
            ApplyPanelMainAxisSize(size, true);
            ApplyToggleScale(false);
            UpdateNotificationDot();
            CacheVisibleItems(enableLayout: true, leaveLayoutEnabled: expanded);
        }

        static void ApplyItemVisual(in ItemState item, bool show)
        {
            if (show)
            {
                item.Rt.anchoredPosition = item.RestAnchoredPos;
                item.Rt.localScale = Vector3.one;
                item.CanvasGroup.alpha = 1f;
                item.CanvasGroup.blocksRaycasts = true;
            }
            else
            {
                item.Rt.localScale = Vector3.zero;
                item.CanvasGroup.alpha = 0f;
                item.CanvasGroup.blocksRaycasts = false;
            }
        }

        void PlayAnimation(bool expand)
        {
            KillTween();
            if (_visibleItems.Count <= 1)
                return;

            CacheVisibleItems(enableLayout: true, leaveLayoutEnabled: true);

            _animating = true;
            _sequence = DOTween.Sequence();
            ApplyToggleScale(true);

            if (expand)
            {
                PrepareExpandAnimation();
                BuildExpandSequence();
            }
            else
            {
                PrepareCollapseAnimation();
                BuildCollapseSequence();
            }

            _sequence.OnComplete(() =>
            {
                _animating = false;
                CacheVisibleItems(enableLayout: true, leaveLayoutEnabled: expand);
                RefreshCachedPositions();
                ApplyMultiButtonInstantState(expand);
            });
        }

        /// <summary>
        /// 展开前：先按完整主轴尺寸重建 Layout 并缓存目标位，再立刻切回收起尺寸与隐藏态，避免窄容器下布局错乱。
        /// </summary>
        void PrepareExpandAnimation()
        {
            for (var i = 1; i < _visibleItems.Count; i++)
            {
                var item = _visibleItems[i];
                item.CanvasGroup.alpha = 0f;
                item.Rt.localScale = Vector3.zero;
            }

            ApplyPanelMainAxisSize(GetExpandedMainAxisSize(_visibleItems.Count), true);
            RebuildLayoutAndCachePositions(disableLayoutAfter: true);

            ApplyPanelMainAxisSize(GetExpandedMainAxisSize(1), true);
            EnsureFirstButtonVisible();

            var slide = GetSlideOffset();
            for (var i = 1; i < _visibleItems.Count; i++)
            {
                var item = _visibleItems[i];
                item.Rt.anchoredPosition = item.RestAnchoredPos + slide;
                item.Rt.localScale = Vector3.zero;
                item.CanvasGroup.alpha = 0f;
                item.CanvasGroup.blocksRaycasts = false;
            }
        }

        void PrepareCollapseAnimation()
        {
            ApplyPanelMainAxisSize(GetExpandedMainAxisSize(_visibleItems.Count), true);
            RebuildLayoutAndCachePositions(disableLayoutAfter: true);

            for (var i = 0; i < _visibleItems.Count; i++)
            {
                var item = _visibleItems[i];
                item.Rt.anchoredPosition = item.RestAnchoredPos;
                item.Rt.localScale = Vector3.one;
                item.CanvasGroup.alpha = 1f;
                item.CanvasGroup.blocksRaycasts = i == 0;
            }
        }

        void RebuildLayoutAndCachePositions(bool disableLayoutAfter)
        {
            if (_layoutGroup != null)
                _layoutGroup.enabled = true;

            LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRoot);

            for (var i = 0; i < _visibleItems.Count; i++)
            {
                var item = _visibleItems[i];
                if (item.Rt == null)
                    continue;

                item.RestAnchoredPos = item.Rt.anchoredPosition;
                _visibleItems[i] = item;
            }

            if (_layoutGroup != null)
                _layoutGroup.enabled = !disableLayoutAfter;
        }

        void BuildExpandSequence()
        {
            for (var i = 1; i < _visibleItems.Count; i++)
            {
                var item = _visibleItems[i];
                if (item.Rt == null)
                    continue;

                var delay = (i - 1) * staggerDelay;
                _sequence.Insert(delay, item.Rt.DOAnchorPos(item.RestAnchoredPos, itemDuration).SetEase(expandEase));
                _sequence.Insert(delay, item.Rt.DOScale(1f, itemDuration).SetEase(expandEase));
                _sequence.Insert(delay, item.CanvasGroup.DOFade(1f, itemDuration * 0.65f));
                InsertPanelSizeTween(GetExpandedMainAxisSize(i + 1), delay);
            }
        }

        void BuildCollapseSequence()
        {
            var count = _visibleItems.Count;
            var slide = GetSlideOffset();
            for (var i = 0; i < count - 1; i++)
            {
                var index = count - 1 - i;
                var item = _visibleItems[index];
                if (item.Rt == null)
                    continue;

                var hiddenPos = item.RestAnchoredPos + slide;
                var delay = i * staggerDelay;
                var remaining = index;

                item.CanvasGroup.blocksRaycasts = false;
                _sequence.Insert(delay, item.Rt.DOAnchorPos(hiddenPos, itemDuration).SetEase(collapseEase));
                _sequence.Insert(delay, item.Rt.DOScale(0f, itemDuration).SetEase(collapseEase));
                _sequence.Insert(delay, item.CanvasGroup.DOFade(0f, itemDuration * 0.65f));
                InsertPanelSizeTween(GetExpandedMainAxisSize(remaining), delay);
            }
        }

        void EnsureFirstButtonVisible()
        {
            if (_visibleItems.Count == 0)
                return;

            var item = _visibleItems[0];
            item.Rt.gameObject.SetActive(true);
            item.Rt.anchoredPosition = item.RestAnchoredPos;
            item.Rt.localScale = Vector3.one;
            item.CanvasGroup.alpha = 1f;
            item.CanvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// 方向 → Btn.z 旋转：Right=0, Left=180, Down=-90, Up=90。
        /// 展开/收起仅翻转 scale.x（1 / -1），不改 scale.y。
        /// </summary>
        void ApplyToggleScale(bool animate)
        {
            if (toggleIcon == null)
                return;

            ApplyToggleDirectionRotation();

            var targetSign = _expanded ? ExpandedToggleScaleSign : CollapsedToggleScaleSign;
            var scale = toggleIcon.localScale;
            toggleIcon.DOKill(false);

            var absY = Mathf.Abs(scale.y);
            var absZ = Mathf.Abs(scale.z);
            if (animate)
                toggleIcon.DOScaleX(targetSign, itemDuration).SetEase(Ease.OutQuad);
            else
                toggleIcon.localScale = new Vector3(targetSign, absY, absZ);
        }

        void ApplyToggleDirectionRotation()
        {
            if (toggleIcon == null)
                return;

            var z = rolloutDirection switch
            {
                UiRolloutDirection.Right => 0f,
                UiRolloutDirection.Left => 180f,
                UiRolloutDirection.Down => -90f,
                UiRolloutDirection.Up => 90f,
                _ => 0f,
            };

            var euler = toggleIcon.localEulerAngles;
            toggleIcon.localEulerAngles = new Vector3(euler.x, euler.y, z);
        }

        void UpdateNotificationDot()
        {
            if (notificationDot == null)
                return;

            var show = _visibleItems.Count >= DotVisibleMinButtonCount && !_expanded;
            notificationDot.SetActive(show);
        }

        void KillTween()
        {
            _sequence?.Kill();
            _sequence = null;
            _animating = false;
            panelRoot?.DOKill(false);

            for (var i = 0; i < _visibleItems.Count; i++)
            {
                _visibleItems[i].Rt?.DOKill();
                _visibleItems[i].CanvasGroup?.DOKill();
            }

            toggleIcon?.DOKill(false);
        }
    }

    [DisallowMultipleComponent]
    sealed class UiRolloutGroupMemberNotifier : MonoBehaviour
    {
        UiSequentialRolloutGroup _owner;

        public void Bind(UiSequentialRolloutGroup owner)
        {
            _owner = owner;
        }

        void OnEnable()
        {
            _owner?.OnMemberVisibilityChanged();
        }

        void OnDisable()
        {
            if (_owner != null && _owner.gameObject.activeInHierarchy)
                _owner.OnMemberVisibilityChanged();
        }
    }
}

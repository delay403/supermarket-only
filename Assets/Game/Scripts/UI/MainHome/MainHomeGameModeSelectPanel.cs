using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Game.Reward;
using Game.Audio;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.MainHome
{
    /// <summary>
    /// MainHome 选关页：单个横向 ScrollView，Content = MainMode + 副模式 Grid。
    /// 初始显示主模式 + 3 个副模式 +「更多游戏」；点击后横向滚动，主模式滑出视口，最多可见 6 个副模式（3 列 × 2 行）。
    /// </summary>
    public sealed class MainHomeGameModeSelectPanel : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameProductSettings productSettings;
        [SerializeField] private bool loadSettingsFromResources = true;

        [Header("Layout Scroll (MainMode + Secondary Grid)")]
        [SerializeField] private ScrollRect layoutScrollRect;
        [SerializeField] private RectTransform layoutRowContent;
        [SerializeField] private RectTransform mainModeRoot;
        [SerializeField] private Image mainModeImage;
        [SerializeField] private Button mainModeButton;
        [SerializeField] private RectTransform secondaryGridContent;
        [SerializeField] private MainHomeGameModeItemView itemPrefab;

        [Header("Grid")]
        [Tooltip("副模式网格固定行数（横向滚动：2 行 × N 列）。")]
        [SerializeField] private int gridColumnCount = 2;
        [SerializeField] private Vector2 referenceCellSize = new Vector2(270f, 255f);
        [SerializeField] private Vector2 gridSpacing = new Vector2(16f, 6f);
        [SerializeField] private float layoutRowSpacing = 16f;
        [Tooltip("展开后在视口内期望可见的副模式列数（3 列 × 2 行 = 6 个）。")]
        [SerializeField] private int expandedVisibleColumnCount = 3;

        [Header("Responsive")]
        [SerializeField] private float referenceViewportHeight = 720f;
        [SerializeField] private float referenceMainModeWidth = 420f;

        [Header("More Games")]
        [SerializeField] private int initialSecondaryVisibleCount = 3;
        [SerializeField] private Sprite moreGamesThumbnail;
        [SerializeField] private string moreGamesTitle = "更多游戏";

        [Header("Expand Animation")]
        [SerializeField] private float expandAnimDuration = 0.45f;

        readonly List<MainHomeGameModeItemView> _cells = new List<MainHomeGameModeItemView>();
        readonly List<MainHomeGameModeEntry> _visibleModes = new List<MainHomeGameModeEntry>();

        MainHomeGameModeEntry _mainModeEntry;
        MainHomeGameModeItemView _moreGamesCell;
        GridLayoutGroup _secondaryGrid;
        HorizontalLayoutGroup _rowLayout;
        LayoutElement _mainModeLayoutElement;
        LayoutElement _secondaryLayoutElement;
        Coroutine _expandScrollRoutine;
        Tween _expandTween;
        float _lastViewportHeight;
        bool _prepared;
        bool _secondaryExpanded;

        public int VisibleModeCount => _visibleModes.Count;

        void Awake()
        {
            ResolveProductSettings();
            ResolveSceneReferences();
            EnsureUnifiedLayout();
            EnsureSecondaryGridLayout();
            RefreshResponsiveLayout();
            ResolveMainModeButton();
            mainModeButton?.onClick.AddListener(OnMainModeClicked);
        }

        void OnDestroy()
        {
            KillExpandAnimation();
            mainModeButton?.onClick.RemoveListener(OnMainModeClicked);
        }

        void Start()
        {
            if (!Application.isPlaying || _prepared)
                return;

            if (ResolveAvailableModeCount() > 1)
                PrepareWhileHidden();
            else
                RefreshMainModeDisplay();
        }

        void Update()
        {
            if (!Application.isPlaying || layoutScrollRect?.viewport == null)
                return;

            var height = layoutScrollRect.viewport.rect.height;
            if (Mathf.Abs(height - _lastViewportHeight) > 0.5f)
                RefreshResponsiveLayout();
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (_visibleModes.Count == 0)
                RebuildList();
            else
                RefreshAllVisuals();
        }

        public void PrepareWhileHidden()
        {
            RebuildList();
            _prepared = true;
        }

        public void ShowPrepared()
        {
            if (!_prepared)
                PrepareWhileHidden();

            ResetSecondaryExpandState();
            gameObject.SetActive(true);
            RefreshResponsiveLayout();
            RefreshAllVisuals();
        }

        public void SetPanelVisible(bool visible) => gameObject.SetActive(visible);

        public void RebuildList()
        {
            ResolveProductSettings();
            KillExpandAnimation();
            ResetSecondaryExpandState();

            _visibleModes.Clear();
            if (ActiveSettings != null)
                _visibleModes.AddRange(ActiveSettings.BuildAvailableModes());

            if (_visibleModes.Count == 0 && ActiveSettings?.modes != null && ActiveSettings.modes.Count > 0)
            {
                Debug.LogWarning(
                    "[MainHomeGameModeSelectPanel] 已配置模式但无可用项。请检查 GameProductSettings 中各模式的 enabled 勾选，" +
                    "以及对应场景是否已加入 Build Settings。",
                    this);
            }

            _mainModeEntry = _visibleModes.Count > 0 ? _visibleModes[0] : null;
            EnsureUnifiedLayout();
            EnsureSecondaryGridLayout();
            BuildSecondaryCells();
            ApplySecondaryVisibility();
            RefreshResponsiveLayout();
            ApplyScrollInteractionState();
            RefreshAllVisuals();
            _prepared = true;
        }

        GameProductSettings ActiveSettings => productSettings;

        void ResolveProductSettings()
        {
            if (productSettings == null && loadSettingsFromResources)
                productSettings = GameProductSettings.TryLoadFromResources();
        }

        void ResolveSceneReferences()
        {
            var background = transform.Find("Background");
            if (background == null)
                return;

            if (layoutScrollRect == null)
            {
                foreach (Transform child in background)
                {
                    if (child.name != "Scroll View")
                        continue;

                    layoutScrollRect = child.GetComponent<ScrollRect>();
                    break;
                }
            }

            if (layoutRowContent == null && layoutScrollRect != null)
                layoutRowContent = layoutScrollRect.content;

            if (mainModeRoot == null)
                mainModeRoot = layoutRowContent?.Find("MainMode") as RectTransform
                               ?? background.Find("MainMode") as RectTransform;

            if (mainModeImage == null && mainModeRoot != null)
                mainModeImage = mainModeRoot.GetComponent<Image>();

            if (secondaryGridContent == null)
            {
                secondaryGridContent = layoutRowContent?.Find("SecondaryModes") as RectTransform
                                         ?? layoutRowContent?.Find("Content") as RectTransform;

                if (secondaryGridContent == null && layoutRowContent != null)
                {
                    var nestedScroll = layoutRowContent.Find("Scroll View/Viewport/Content");
                    secondaryGridContent = nestedScroll as RectTransform;
                }
            }
        }

        void EnsureUnifiedLayout()
        {
            if (layoutScrollRect == null || layoutRowContent == null)
                return;

            var nestedScrollTransform = layoutRowContent.Find("Scroll View");
            if (nestedScrollTransform != null)
            {
                var nestedContent = nestedScrollTransform.Find("Viewport/Content") as RectTransform;
                if (nestedContent != null)
                {
                    if (secondaryGridContent == null || secondaryGridContent == layoutRowContent)
                        secondaryGridContent = nestedContent;

                    if (secondaryGridContent.parent != layoutRowContent)
                    {
                        secondaryGridContent.SetParent(layoutRowContent, false);
                        secondaryGridContent.name = "SecondaryModes";
                    }

                    secondaryGridContent.SetSiblingIndex(mainModeRoot != null ? 1 : 0);
                }

                nestedScrollTransform.gameObject.SetActive(false);
            }

            if (mainModeRoot != null && mainModeRoot.parent != layoutRowContent)
                mainModeRoot.SetParent(layoutRowContent, false);

            if (mainModeRoot != null)
                mainModeRoot.SetSiblingIndex(0);

            ConfigureLayoutRowContent();
            ConfigureMainModeRoot();
            ConfigureSecondaryRoot();
        }

        void ConfigureLayoutRowContent()
        {
            layoutRowContent.anchorMin = new Vector2(0f, 0f);
            layoutRowContent.anchorMax = new Vector2(0f, 1f);
            layoutRowContent.pivot = new Vector2(0f, 0.5f);
            layoutRowContent.anchoredPosition = Vector2.zero;
            layoutRowContent.sizeDelta = new Vector2(layoutRowContent.sizeDelta.x, 0f);

            _rowLayout = layoutRowContent.GetComponent<HorizontalLayoutGroup>();
            if (_rowLayout == null)
                _rowLayout = layoutRowContent.gameObject.AddComponent<HorizontalLayoutGroup>();

            _rowLayout.spacing = layoutRowSpacing;
            _rowLayout.padding = new RectOffset(0, 0, 0, 0);
            _rowLayout.childAlignment = TextAnchor.MiddleLeft;
            _rowLayout.childControlWidth = true;
            _rowLayout.childControlHeight = true;
            _rowLayout.childForceExpandWidth = false;
            _rowLayout.childForceExpandHeight = false;

            var fitter = layoutRowContent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = layoutRowContent.gameObject.AddComponent<ContentSizeFitter>();

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        void ConfigureMainModeRoot()
        {
            if (mainModeRoot == null)
                return;

            mainModeRoot.anchorMin = new Vector2(0f, 0.5f);
            mainModeRoot.anchorMax = new Vector2(0f, 0.5f);
            mainModeRoot.pivot = new Vector2(0.5f, 0.5f);
            mainModeRoot.anchoredPosition = Vector2.zero;

            _mainModeLayoutElement = mainModeRoot.GetComponent<LayoutElement>();
            if (_mainModeLayoutElement == null)
                _mainModeLayoutElement = mainModeRoot.gameObject.AddComponent<LayoutElement>();

            _mainModeLayoutElement.minWidth = referenceMainModeWidth;
            _mainModeLayoutElement.minHeight = referenceCellSize.y * gridColumnCount
                                                 + gridSpacing.y * (gridColumnCount - 1);
        }

        void ConfigureSecondaryRoot()
        {
            if (secondaryGridContent == null)
                return;

            secondaryGridContent.anchorMin = new Vector2(0f, 0.5f);
            secondaryGridContent.anchorMax = new Vector2(0f, 0.5f);
            secondaryGridContent.pivot = new Vector2(0f, 0.5f);
            secondaryGridContent.anchoredPosition = Vector2.zero;

            _secondaryLayoutElement = secondaryGridContent.GetComponent<LayoutElement>();
            if (_secondaryLayoutElement == null)
                _secondaryLayoutElement = secondaryGridContent.gameObject.AddComponent<LayoutElement>();
        }

        void ResolveMainModeButton()
        {
            if (mainModeButton == null && mainModeImage != null)
                mainModeButton = mainModeImage.GetComponent<Button>();

            if (mainModeButton != null || mainModeRoot == null)
                return;

            mainModeButton = mainModeRoot.GetComponent<Button>();
            if (mainModeImage == null)
                mainModeImage = mainModeRoot.GetComponent<Image>();
        }

        void EnsureSecondaryGridLayout()
        {
            if (secondaryGridContent == null)
                return;

            var vertical = secondaryGridContent.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
                Destroy(vertical);

            _secondaryGrid = secondaryGridContent.GetComponent<GridLayoutGroup>();
            if (_secondaryGrid == null)
                _secondaryGrid = secondaryGridContent.gameObject.AddComponent<GridLayoutGroup>();

            _secondaryGrid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            _secondaryGrid.constraintCount = Mathf.Max(1, gridColumnCount);
            _secondaryGrid.childAlignment = TextAnchor.UpperLeft;
            _secondaryGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            _secondaryGrid.startAxis = GridLayoutGroup.Axis.Horizontal;

            var fitter = secondaryGridContent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = secondaryGridContent.gameObject.AddComponent<ContentSizeFitter>();

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void RefreshResponsiveLayout()
        {
            if (layoutScrollRect?.viewport == null || _secondaryGrid == null)
                return;

            var viewport = layoutScrollRect.viewport;
            var viewportHeight = Mathf.Max(1f, viewport.rect.height);
            var viewportWidth = Mathf.Max(1f, viewport.rect.width);
            if (viewportHeight < 32f)
                viewportHeight = referenceViewportHeight * 0.72f;
            _lastViewportHeight = viewportHeight;

            var heightScale = viewportHeight / Mathf.Max(1f, referenceViewportHeight);
            var cellHeight = (viewportHeight - gridSpacing.y * (gridColumnCount - 1)) / gridColumnCount;
            var aspect = referenceCellSize.x / Mathf.Max(1f, referenceCellSize.y);

            var expandedColumnWidth = expandedVisibleColumnCount > 0
                ? (viewportWidth - gridSpacing.x * (expandedVisibleColumnCount - 1)) / expandedVisibleColumnCount
                : referenceCellSize.x * heightScale;

            var heightBasedWidth = cellHeight * aspect;

            var mainWidth = referenceMainModeWidth * heightScale;
            var collapsedSecondaryWidth = Mathf.Max(0f, viewportWidth - mainWidth - layoutRowSpacing);
            const int collapsedColumnCount = 2;
            var collapsedMaxCellWidth = collapsedColumnCount > 0
                ? (collapsedSecondaryWidth - gridSpacing.x * (collapsedColumnCount - 1)) / collapsedColumnCount
                : expandedColumnWidth;

            var cellWidth = Mathf.Min(heightBasedWidth, expandedColumnWidth, collapsedMaxCellWidth);
            cellHeight = cellWidth / aspect;

            _secondaryGrid.cellSize = new Vector2(cellWidth, cellHeight);
            _secondaryGrid.spacing = gridSpacing;

            var mainHeight = gridColumnCount * cellHeight + gridSpacing.y * (gridColumnCount - 1);

            ApplyMainModeSize(mainWidth, mainHeight);

            UpdateSecondaryPreferredSize();
            RebuildLayoutImmediate();
        }

        void ApplyMainModeSize(float width, float height)
        {
            if (mainModeRoot == null)
                return;

            width = Mathf.Max(1f, width);
            height = Mathf.Max(1f, height);

            mainModeRoot.anchorMin = new Vector2(0f, 0.5f);
            mainModeRoot.anchorMax = new Vector2(0f, 0.5f);
            mainModeRoot.pivot = new Vector2(0.5f, 0.5f);
            mainModeRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            mainModeRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            if (_mainModeLayoutElement != null)
            {
                _mainModeLayoutElement.minWidth = width;
                _mainModeLayoutElement.minHeight = height;
                _mainModeLayoutElement.preferredWidth = width;
                _mainModeLayoutElement.preferredHeight = height;
                _mainModeLayoutElement.flexibleWidth = 0f;
                _mainModeLayoutElement.flexibleHeight = 0f;
            }
        }

        void UpdateSecondaryPreferredSize()
        {
            if (_secondaryGrid == null || _secondaryLayoutElement == null)
                return;

            var activeCount = CountActiveSecondarySlots();
            if (activeCount <= 0)
            {
                _secondaryLayoutElement.preferredWidth = 0f;
                _secondaryLayoutElement.preferredHeight = 0f;
                return;
            }

            var rowCount = Mathf.Max(1, gridColumnCount);
            var columnCount = Mathf.CeilToInt(activeCount / (float)rowCount);
            var cellSize = _secondaryGrid.cellSize;
            var spacing = _secondaryGrid.spacing;

            _secondaryLayoutElement.preferredWidth =
                columnCount * cellSize.x + Mathf.Max(0, columnCount - 1) * spacing.x;
            _secondaryLayoutElement.preferredHeight =
                rowCount * cellSize.y + Mathf.Max(0, rowCount - 1) * spacing.y;
        }

        int CountActiveSecondarySlots()
        {
            var count = 0;
            for (var i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] != null && _cells[i].gameObject.activeSelf)
                    count++;
            }

            if (_moreGamesCell != null && _moreGamesCell.gameObject.activeSelf)
                count++;

            return count;
        }

        void RebuildLayoutImmediate()
        {
            if (secondaryGridContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(secondaryGridContent);

            if (layoutRowContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRowContent);

            Canvas.ForceUpdateCanvases();
        }

        void BuildSecondaryCells()
        {
            ClearSecondaryGridContent();

            if (secondaryGridContent == null || itemPrefab == null || _visibleModes.Count <= 1)
                return;

            var secondaryCount = GetSecondaryModeCount();
            var leadingVisibleCount = NeedsMoreGamesButton()
                ? initialSecondaryVisibleCount
                : secondaryCount;

            for (var i = 0; i < leadingVisibleCount; i++)
                CreateSecondaryCell(_visibleModes[i + 1]);

            if (NeedsMoreGamesButton())
            {
                _moreGamesCell = Instantiate(itemPrefab, secondaryGridContent);
                _moreGamesCell.gameObject.SetActive(true);
                _moreGamesCell.BindAsMoreGames(moreGamesThumbnail, moreGamesTitle, OnMoreGamesClicked);
            }

            for (var i = initialSecondaryVisibleCount; i < secondaryCount; i++)
                CreateSecondaryCell(_visibleModes[i + 1]);
        }

        void CreateSecondaryCell(MainHomeGameModeEntry entry)
        {
            var cell = Instantiate(itemPrefab, secondaryGridContent);
            cell.gameObject.SetActive(true);

            var unlocked = MainHomeModeUnlockAccess.IsUnlocked(entry, isMainMode: false);
            cell.Bind(entry, unlocked, OnSecondaryModeClicked);
            _cells.Add(cell);
        }

        void ClearSecondaryGridContent()
        {
            _cells.Clear();
            _moreGamesCell = null;

            if (secondaryGridContent == null)
                return;

            for (var i = secondaryGridContent.childCount - 1; i >= 0; i--)
            {
                var child = secondaryGridContent.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        void ApplySecondaryVisibility()
        {
            for (var i = 0; i < _cells.Count; i++)
            {
                var show = _secondaryExpanded || i < initialSecondaryVisibleCount;
                _cells[i].gameObject.SetActive(show);
            }

            if (_moreGamesCell != null)
                _moreGamesCell.gameObject.SetActive(!_secondaryExpanded && NeedsMoreGamesButton());

            UpdateSecondaryPreferredSize();
            RebuildLayoutImmediate();
        }

        void ApplyScrollInteractionState()
        {
            if (layoutScrollRect == null)
                return;

            layoutScrollRect.horizontal = _secondaryExpanded || !NeedsMoreGamesButton();
            layoutScrollRect.vertical = false;
        }

        bool NeedsMoreGamesButton() => GetSecondaryModeCount() > initialSecondaryVisibleCount;

        int GetSecondaryModeCount() => Mathf.Max(0, _visibleModes.Count - 1);

        void ResetSecondaryExpandState()
        {
            _secondaryExpanded = false;

            if (mainModeRoot != null)
            {
                mainModeRoot.DOKill();
                mainModeRoot.gameObject.SetActive(true);
            }

            if (layoutScrollRect != null)
            {
                layoutScrollRect.StopMovement();
                layoutScrollRect.horizontalNormalizedPosition = 0f;
            }

            ApplySecondaryVisibility();
            ApplyScrollInteractionState();

            if (mainModeButton != null && _mainModeEntry != null)
                mainModeButton.interactable = _mainModeEntry.CanLaunchFromHome();
        }

        void RefreshAllVisuals()
        {
            RefreshMainModeDisplay();
            RefreshSecondaryCells();
        }

        void RefreshMainModeDisplay()
        {
            _mainModeEntry = _visibleModes.Count > 0 ? _visibleModes[0] : null;

            if (mainModeImage != null)
            {
                var mainSprite = _mainModeEntry?.ResolveMainModeSprite();
                mainModeImage.sprite = mainSprite;
                mainModeImage.enabled = mainSprite != null;
                mainModeImage.preserveAspect = true;
            }

            if (mainModeButton != null)
                mainModeButton.interactable = _mainModeEntry != null && _mainModeEntry.CanLaunchFromHome();
        }

        void RefreshSecondaryCells()
        {
            for (var i = 0; i < _cells.Count; i++)
            {
                var modeIndex = i + 1;
                if (modeIndex >= _visibleModes.Count)
                    continue;

                var entry = _visibleModes[modeIndex];
                var unlocked = MainHomeModeUnlockAccess.IsUnlocked(entry, isMainMode: false);
                _cells[i].Bind(entry, unlocked, OnSecondaryModeClicked);
            }
        }

        void OnMoreGamesClicked()
        {
            if (_secondaryExpanded || !NeedsMoreGamesButton())
                return;

            _secondaryExpanded = true;
            ApplySecondaryVisibility();

            if (_moreGamesCell != null)
                _moreGamesCell.gameObject.SetActive(false);

            RefreshResponsiveLayout();
            ApplyScrollInteractionState();
            PlayExpandAnimation();
        }

        void PlayExpandAnimation()
        {
            KillExpandAnimation();

            var targetScroll = ComputeExpandScrollTarget();
            var duration = Mathf.Max(0.05f, expandAnimDuration);

            if (layoutScrollRect == null || targetScroll <= 0.001f)
                return;

            if (_expandScrollRoutine != null)
                StopCoroutine(_expandScrollRoutine);

            _expandScrollRoutine = StartCoroutine(CoAnimateLayoutScroll(targetScroll, duration));
        }

        IEnumerator CoAnimateLayoutScroll(float targetNormalized, float duration)
        {
            var scroll = layoutScrollRect;
            if (scroll == null)
                yield break;

            var start = scroll.horizontalNormalizedPosition;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                scroll.horizontalNormalizedPosition = Mathf.Lerp(start, targetNormalized, t);
                yield return null;
            }

            scroll.horizontalNormalizedPosition = targetNormalized;
            _expandScrollRoutine = null;
        }

        float ComputeExpandScrollTarget()
        {
            if (layoutScrollRect == null || layoutRowContent == null || mainModeRoot == null)
                return 0f;

            RebuildLayoutImmediate();

            var viewport = layoutScrollRect.viewport != null
                ? layoutScrollRect.viewport
                : layoutScrollRect.GetComponent<RectTransform>();

            var contentWidth = layoutRowContent.rect.width;
            var viewportWidth = viewport.rect.width;
            var overflow = contentWidth - viewportWidth;
            if (overflow <= 1f)
                return 0f;

            var mainWidth = _mainModeLayoutElement != null
                ? _mainModeLayoutElement.preferredWidth
                : mainModeRoot.rect.width;
            var scrollDistance = mainWidth + layoutRowSpacing;
            return Mathf.Clamp01(scrollDistance / overflow);
        }

        void KillExpandAnimation()
        {
            _expandTween?.Kill();
            _expandTween = null;

            if (_expandScrollRoutine != null)
            {
                StopCoroutine(_expandScrollRoutine);
                _expandScrollRoutine = null;
            }

            mainModeRoot?.DOKill();
        }

        void OnSecondaryModeClicked(MainHomeGameModeEntry entry)
        {
            if (entry == null)
                return;

            if (!MainHomeModeUnlockAccess.IsUnlocked(entry, isMainMode: false))
            {
                FreeRewardGate.Grant(() =>
                {
                    MainHomeModeUnlockAccess.Unlock(entry.modeId);
                    RefreshSecondaryCells();
                });
                return;
            }

            if (!entry.CanLaunchFromHome())
            {
                AudioGameplayFeedback.PlayUiError();
                Debug.LogWarning("[MainHomeGameModeSelectPanel] 副模式不可进入（模块未启用或场景不可加载；请确认场景已加入 Build Settings）。");
                return;
            }

            LaunchEntry(entry);
        }

        void OnMainModeClicked()
        {
            if (_mainModeEntry == null || !_mainModeEntry.CanLaunchFromHome())
            {
                AudioGameplayFeedback.PlayUiError();
                Debug.LogWarning("[MainHomeGameModeSelectPanel] 主模式不可进入（模块未启用或场景不可加载；请确认场景已加入 Build Settings）。");
                return;
            }

            LaunchEntry(_mainModeEntry);
        }

        public bool TryLaunchIfSingleMode()
        {
            EnsureVisibleModesBuilt();
            if (_visibleModes.Count != 1)
                return false;

            var entry = _visibleModes[0];
            if (entry == null || !entry.CanLaunchFromHome())
                return false;

            LaunchEntry(entry);
            return true;
        }

        public int GetAvailableModeCount()
        {
            if (_visibleModes.Count > 0)
                return _visibleModes.Count;

            return ResolveAvailableModeCount();
        }

        int ResolveAvailableModeCount()
        {
            ResolveProductSettings();
            return ActiveSettings != null ? ActiveSettings.BuildAvailableModes().Count : 0;
        }

        void EnsureVisibleModesBuilt()
        {
            if (_visibleModes.Count > 0)
                return;

            RebuildList();
        }

        void LaunchEntry(MainHomeGameModeEntry entry)
        {
            if (entry == null || !entry.CanLaunchFromHome())
                return;

            AudioGameplayFeedback.PlayUiButton();
            entry.NavigateToGameplay(fromMainHomeLobby: true);
        }
    }
}

using System.Collections.Generic;
using Game.Audio;
using Game.Economy;
using Game.SupermarketOrganize.Config;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>超市收纳技能/属性升级面板（左图标 + 右详情）。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeUpgradePanel : MonoBehaviour, IUiPanelController
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform mainPanel;
        [SerializeField] private Button maskCloseButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private SupermarketOrganizeUpgradeUiConfig uiConfig;
        [SerializeField] private RectTransform skillGridContent;
        [SerializeField] private RectTransform attributeGridContent;
        [SerializeField] private SupermarketOrganizeUpgradeRowUi itemPrefab;
        [SerializeField] private SupermarketOrganizeUpgradeDetailUi detailUi;

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.38f;
        [SerializeField] private float settleDuration = 0.28f;
        [SerializeField] private float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        readonly List<SupermarketOrganizeUpgradeRowUi> _cells = new List<SupermarketOrganizeUpgradeRowUi>();
        readonly List<SupermarketOrganizeUpgradeEntryDef> _entries = new List<SupermarketOrganizeUpgradeEntryDef>();
        int _selectedIndex = -1;

        public UiPanelId PanelId => UiPanelId.SupermarketOrganizeUpgrade;

        void Awake()
        {
            maskCloseButton?.onClick.AddListener(OnDismissClicked);
            closeButton?.onClick.AddListener(OnDismissClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable()
        {
            UiCoordinator.Register(this);
            SupermarketOrganizeProgressionService.SkillUpgraded += OnProgressionChanged;
            SupermarketOrganizeProgressionService.AttributeUpgraded += OnProgressionChanged;
            EconomyDisplayEvents.Changed += RefreshAll;
        }

        void OnDisable()
        {
            UiCoordinator.Unregister(this);
            SupermarketOrganizeProgressionService.SkillUpgraded -= OnProgressionChanged;
            SupermarketOrganizeProgressionService.AttributeUpgraded -= OnProgressionChanged;
            EconomyDisplayEvents.Changed -= RefreshAll;
        }

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null)
        {
            EnsureBuilt();
            RefreshAll();
            Show();
        }

        public void Close() => Hide();

        void OnProgressionChanged(SupermarketSkillId _, int __) => RefreshAll();

        void OnProgressionChanged(SupermarketAttributeId _, int __) => RefreshAll();

        void EnsureBuilt()
        {
            uiConfig ??= SupermarketOrganizeUpgradeUiConfig.ResolveRuntime();
            _entries.Clear();
            _entries.AddRange(uiConfig.GetSortedEntries());

            if (_cells.Count > 0 || itemPrefab == null)
                return;

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var parent = entry.kind == SupermarketOrganizeUpgradeEntryKind.Skill
                    ? skillGridContent
                    : attributeGridContent;
                if (parent == null)
                    continue;

                var cell = Instantiate(itemPrefab, parent);
                cell.name = $"UpgradeCell_{i}";
                cell.Bind(entry, uiConfig.ResolveCellBackground(entry));
                cell.SetupClick(OnCellClicked);
                _cells.Add(cell);
            }

            if (_cells.Count > 0)
                SelectIndex(0, playPulse: false);
        }

        void OnCellClicked(SupermarketOrganizeUpgradeRowUi cell)
        {
            AudioGameplayFeedback.PlayUiButton();
            var index = _cells.IndexOf(cell);
            if (index < 0)
                return;

            SelectIndex(index, playPulse: true);
        }

        void SelectIndex(int index, bool playPulse)
        {
            _selectedIndex = Mathf.Clamp(index, 0, _cells.Count - 1);
            for (var i = 0; i < _cells.Count; i++)
                _cells[i]?.ApplySelection(i == _selectedIndex, playPulse && i == _selectedIndex);

            if (_selectedIndex >= 0 && _selectedIndex < _entries.Count)
                detailUi?.Bind(_entries[_selectedIndex]);
        }

        public void RefreshAll()
        {
            uiConfig ??= SupermarketOrganizeUpgradeUiConfig.ResolveRuntime();
            for (var i = 0; i < _cells.Count; i++)
            {
                if (i < _entries.Count)
                    _cells[i]?.Bind(_entries[i], uiConfig.ResolveCellBackground(_entries[i]));
                else
                    _cells[i]?.Refresh();
            }

            if (_selectedIndex >= 0 && _selectedIndex < _entries.Count)
                detailUi?.Refresh();
        }

        void Show()
        {
            EnsureSlideHost();
            slideHost.PlayOpen();
        }

        void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;

            slideHost.RequestClose();
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
        }

        void EnsureSlideHost()
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

        void OnDismissClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            Hide();
        }
    }
}

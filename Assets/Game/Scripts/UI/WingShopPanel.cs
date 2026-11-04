using System;
using System.Collections;
using System.Collections.Generic;
using Game.Collectibles;
using Game.Collectibles.Stats;
using Game.Collectibles.Trails;
using Game.Collectibles.Wings;
using Game.Core.Config;
using Game.Economy;
using Game.Save;
using Game.Core;
using Game.UI.ViewModels;
using UnityEngine;
using Game.UI.Framework;
using Game.UI.Preview;
using Game.Audio;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 外观商店（UGUI）：翅膀 / 拖尾共用。拖尾请使用 <see cref="TrailShopPanel"/> 或序列化子类。
    /// 左侧格子列表 + 右侧详情；打开时通过 <see cref="FairyGuiTouchGuard"/> 屏蔽 FairyGUI 穿透。
    /// </summary>
    public class WingShopPanel : MonoBehaviour, IUiPanelController
    {
        protected virtual CosmeticKind CatalogKind => CosmeticKind.Wing;

        public UiPanelId PanelId =>
            CatalogKind == CosmeticKind.Wing ? UiPanelId.WingShop : UiPanelId.TrailShop;

        [Header("Bindings (assign in Inspector)")]
        public GameObject root;
        public RectTransform mainPanel;
        public Image maskImage;
        public Button maskCloseButton;
        public Button closeButton;
        public ScrollRect wingScroll;
        public RectTransform wingGridContent;
        [Tooltip("左侧商品格子预制体（必选）。")]
        public WingShopItemView itemPrefab;

        [Header("Quality visuals (optional override)")]
        [Tooltip("空则使用 ConfigRegistry / Resources/QualityVisualLibrary")]
        [SerializeField] private QualityVisualLibrary qualityVisualLibrary;

        [Header("Detail (right)")]
        public Text detailNameText;
        public Text detailUnlockHintText;
        public Image detailPreviewImage;
        public Text detailStatText;
        public Text detailQualityText;
        public GameObject detailPriceRoot;
        public Text detailPriceText;
        public Button equipButton;
        public Text equipButtonLabel;
        public Button unequipButton;
        public Text unequipButtonLabel;

        [Header("Animation")]
        public float slideDuration = 0.42f;
        public float settleDuration = 0.32f;
        public float overshootLeftPx = 72f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        [Header("Scroll (grid)")]
        [Tooltip(
            "打开商店时按行滚动：行距取 Grid 的 cellSize.y + spacing.y。此处为每行列数；0=自动（Fixed Column 约束或按 Content 宽度估算）。翅膀/拖尾为 3 列时可填 3 以防 Flexible 误判。")]
        [SerializeField] int scrollGridColumns;

        private readonly List<WingShopItemView> _cells = new List<WingShopItemView>();
        private UiGridCellPool<WingShopItemView> _cellPool;
        private Coroutine _populateRoutine;
        private CosmeticShopScreenViewModel _vm;
        private int _selectedIndex = -1;
        private bool _detailMythicQuality;

        private void Awake()
        {
            if (maskCloseButton != null)
                maskCloseButton.onClick.AddListener(() =>
                {
                    AudioGameplayFeedback.PlayUiButton();
                    Hide();
                });
            if (closeButton != null)
                closeButton.onClick.AddListener(() =>
                {
                    AudioGameplayFeedback.PlayUiButton();
                    Hide();
                });
            if (equipButton != null)
                equipButton.onClick.AddListener(() =>
                {
                    AudioGameplayFeedback.PlayUiButton();
                    OnEquipOrBuyClicked();
                });
            if (unequipButton != null)
                unequipButton.onClick.AddListener(() =>
                {
                    AudioGameplayFeedback.PlayUiButton();
                    OnUnequipClicked();
                });

            EnsureSlideHost();
            if (wingGridContent != null && itemPrefab != null)
                _cellPool = new UiGridCellPool<WingShopItemView>(wingGridContent, itemPrefab);
            HideImmediate();
        }

        private void OnEnable()
        {
            UiCoordinator.Register(this);
            EconomyDisplayEvents.Changed += OnEconomyDisplayChanged;
        }

        private void OnDisable()
        {
            EconomyDisplayEvents.Changed -= OnEconomyDisplayChanged;
            UiCoordinator.Unregister(this);
        }

        void OnEconomyDisplayChanged() => RefreshOwnedCatalogIfOpen();

        /// <summary>从存档重建目录并刷新列表（优惠奖励、后台购买等）。</summary>
        public void RefreshOwnedCatalogIfOpen()
        {
            if (slideHost == null || !slideHost.IsShown)
                return;

            _vm = BuildShopViewModel();
            RefreshCellsVisuals(-1);
            RefreshDetail();
        }

        private void OnDestroy()
        {
            CancelPopulateRoutine();
            _cellPool?.DisposeAll(_cells);
            KillPanelTweens();
        }

        private void Update()
        {
            if (slideHost == null || !slideHost.IsShown || !_detailMythicQuality || detailQualityText == null || _vm == null ||
                _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
                return;
            RefreshDetailQualityTextOnly();
        }

        public void Open(UiOpenPayload payload = null)
        {
            Show();
        }

        public void Close()
        {
            Hide();
        }

        public void Show()
        {
            EnsureSlideHost();
            slideHost.PlayOpen(PopulateAfterOpen);
        }

        void PopulateAfterOpen()
        {
            CancelPopulateRoutine();
            _populateRoutine = StartCoroutine(PopulateAfterOpenRoutine());
        }

        IEnumerator PopulateAfterOpenRoutine()
        {
            _vm = BuildShopViewModel();
            var rowCount = _vm?.Rows.Count ?? 0;

            if (_cellPool != null)
                yield return _cellPool.EnsureActiveCountBatched(rowCount, _cells);
            else
                RebuildGridSync();

            if (slideHost == null || !slideHost.IsShown)
                yield break;

            BindGridClicks();
            RefreshCellsVisuals();

            var pv = CosmeticPreviewController.Instance;
            if (pv != null)
            {
                pv.EnterShop(CatalogKind == CosmeticKind.Wing
                    ? CosmeticPreviewController.CosmeticShopKind.Wing
                    : CosmeticPreviewController.CosmeticShopKind.Trail);
            }

            if (_vm != null && _vm.Rows.Count > 0)
            {
                var idx = ResolveInitialSelectedIndex();
                SelectIndex(idx, false);
                ShopUiScroll.ScrollToItemIndex(wingScroll, idx, _vm.Rows.Count, scrollGridColumns);
            }
            else
            {
                _selectedIndex = -1;
                ClearDetail();
            }

            RefreshDetail();
            _populateRoutine = null;
        }

        void CancelPopulateRoutine()
        {
            if (_populateRoutine == null)
                return;
            StopCoroutine(_populateRoutine);
            _populateRoutine = null;
        }

        public void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing) return;
            slideHost.RequestClose(OnShopSlideClosed);
        }

        void OnShopSlideClosed() => CosmeticPreviewController.Instance?.LeaveShop();

        private void HideImmediate()
        {
            CancelPopulateRoutine();
            EnsureSlideHost();
            slideHost.HideImmediate();
            CosmeticPreviewController.Instance?.LeaveShop();
        }

        private void KillPanelTweens() => slideHost?.KillTweens();

        void EnsureSlideHost()
        {
            UiSlidingPanelHost.EnsureOn(
                this,
                ref slideHost,
                root,
                mainPanel,
                UiSlidingPanelHost.SlideDirection.FromLeft,
                slideDuration,
                settleDuration,
                overshootLeftPx,
                0.55f);
        }

        private QualityVisualLibrary ResolvedQualityLibrary()
        {
            return qualityVisualLibrary != null ? qualityVisualLibrary : ConfigRegistry.QualityVisuals;
        }

        CosmeticShopScreenViewModel BuildShopViewModel() =>
            CatalogKind == CosmeticKind.Wing
                ? CosmeticShopScreenViewModel.BuildWings()
                : CosmeticShopScreenViewModel.BuildTrails();

        int ResolveInitialSelectedIndex()
        {
            if (_vm == null || _vm.Rows.Count == 0) return 0;
            var eq = _vm.EquippedId;
            if (string.IsNullOrEmpty(eq)) return 0;
            for (var i = 0; i < _vm.Rows.Count; i++)
            {
                if (string.Equals(_vm.Rows[i].Id, eq, StringComparison.Ordinal))
                    return i;
            }

            return 0;
        }

        protected string ShopTitleText => CatalogKind == CosmeticKind.Wing ? "翅膀" : "拖尾";

        protected string PrevUnlockHintText =>
            CatalogKind == CosmeticKind.Wing ? "需解锁上一个翅膀" : "需解锁上一个拖尾";

        CosmeticQuality RowQualityFromId(string cosmeticId)
        {
            if (CatalogKind == CosmeticKind.Wing)
            {
                ConfigRegistry.Wings.TryGet(cosmeticId, out var d);
                return d != null ? d.quality : CosmeticQuality.Common;
            }

            ConfigRegistry.Trails.TryGet(cosmeticId, out var t);
            return t != null ? t.quality : CosmeticQuality.Common;
        }

        Sprite RowShopIcon(string cosmeticId)
        {
            if (CatalogKind == CosmeticKind.Wing)
            {
                ConfigRegistry.Wings.TryGet(cosmeticId, out var d);
                return d?.shopIcon;
            }

            ConfigRegistry.Trails.TryGet(cosmeticId, out var t);
            return t?.shopIcon;
        }

        private void RebuildGrid()
        {
            RebuildGridSync();
            BindGridClicks();
            RefreshCellsVisuals();
        }

        void RebuildGridSync()
        {
            if (wingGridContent == null || itemPrefab == null)
            {
                if (itemPrefab == null)
                    Debug.LogError($"{nameof(WingShopPanel)}: 请在 Inspector 指定 {nameof(itemPrefab)}。", this);
                return;
            }

            if (_cellPool == null)
                _cellPool = new UiGridCellPool<WingShopItemView>(wingGridContent, itemPrefab);

            _cellPool.EnsureActiveCount(_vm?.Rows.Count ?? 0, _cells);
        }

        void BindGridClicks()
        {
            for (var i = 0; i < _cells.Count; i++)
            {
                var idx = i;
                _cells[i].SetupClick(_ => SelectIndex(idx, true));
            }
        }

        private void SelectIndex(int index, bool userTriggeredPulse)
        {
            if (_vm == null || index < 0 || index >= _vm.Rows.Count) return;
            var changed = _selectedIndex != index;
            _selectedIndex = index;
            if (userTriggeredPulse && changed)
                AudioGameplayFeedback.PlayUiSelect();

            RefreshCellsVisuals(changed && userTriggeredPulse ? index : -1);
            RefreshDetail();
        }

        private void RefreshCellsVisuals(int pulseIndex = -1)
        {
            if (_vm == null) return;
            var equippedId = _vm.EquippedId;
            var lib = ResolvedQualityLibrary();
            for (var i = 0; i < _cells.Count; i++)
            {
                var row = _vm.Rows[i];
                var cell = _cells[i];
                var pulse = pulseIndex == i;
                var q = RowQualityFromId(row.Id);
                var slotBg = lib != null ? lib.ResolveSlotBackground(q) : null;
                var icon = RowShopIcon(row.Id);
                cell.Apply(row, equippedId, i == _selectedIndex, slotBg, icon, BuildAttrLine(row.Id), pulse);
            }
        }

        private void RefreshDetail()
        {
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
            {
                ClearDetail();
                return;
            }

            var row = _vm.Rows[_selectedIndex];
            WingDef wdef = null;
            TrailDef tdef = null;
            if (CatalogKind == CosmeticKind.Wing)
                ConfigRegistry.Wings.TryGet(row.Id, out wdef);
            else
                ConfigRegistry.Trails.TryGet(row.Id, out tdef);

            var lib = ResolvedQualityLibrary();
            var q = RowQualityFromId(row.Id);
            long coins = SaveService.Data != null ? SaveService.Data.coins : 0L;

            _detailMythicQuality = q == CosmeticQuality.Mythic;

            if (detailNameText != null)
                detailNameText.text = wdef != null ? wdef.displayName : tdef != null ? tdef.displayName : row.DisplayName;

            if (detailUnlockHintText != null)
            {
                var req = CatalogKind == CosmeticKind.Wing
                    ? wdef?.requiredUnlockedLevel ?? 0
                    : tdef?.requiredUnlockedLevel ?? 0;
                bool gated = !row.Owned && !row.UnlockedForPurchase && req > 0;
                detailUnlockHintText.gameObject.SetActive(gated);
                if (gated)
                    detailUnlockHintText.text = PrevUnlockHintText;
            }

            if (detailPreviewImage != null)
            {
                var detailBg = lib != null ? lib.ResolveDetailBackground(q) : null;
                var shopSpr = RowShopIcon(row.Id);
                if (detailBg != null)
                {
                    detailPreviewImage.sprite = detailBg;
                    detailPreviewImage.color = Color.white;
                }
                else if (shopSpr != null)
                {
                    detailPreviewImage.sprite = shopSpr;
                    detailPreviewImage.color = Color.white;
                }
                else
                {
                    detailPreviewImage.sprite = WhiteSprite();
                    detailPreviewImage.color = new Color(1f, 1f, 1f, 0.08f);
                }
                detailPreviewImage.preserveAspect = true;
            }

            if (detailStatText != null)
                detailStatText.text = CatalogKind == CosmeticKind.Wing
                    ? BuildDetailStat(wdef)
                    : BuildDetailStatTrail(tdef);

            if (detailQualityText != null)
                detailQualityText.text =
                    QualityVisualLibrary.FormatQualityLineRichTextLoose(lib, q, Time.unscaledTime);

            bool owned = row.Owned;
            bool equipped = owned && string.Equals(_vm.EquippedId, row.Id, StringComparison.Ordinal);
            bool canBuy = !owned && row.UnlockedForPurchase;
            bool canAfford = coins >= row.PriceCoins;

            if (detailPriceText != null)
            {
                if (!owned)
                {
                    detailPriceRoot.gameObject.SetActive(true);
                    detailPriceText.gameObject.SetActive(true);
                    bool showWhite = row.UnlockedForPurchase && canAfford;
                    detailPriceText.color = showWhite ? Color.white : new Color(0.94f, 0.22f, 0.2f, 1f);
                    detailPriceText.text = $"x{row.PriceCoins}";
                }
                else
                {
                    detailPriceRoot.gameObject.SetActive(false);
                    detailPriceText.gameObject.SetActive(false);
                }
            }

            if (equipButton != null)
            {
                equipButton.gameObject.SetActive(!equipped);
                if (equipButtonLabel != null)
                {
                    if (!owned && canBuy)
                        equipButtonLabel.text = "购买";
                    else if (!owned && !canBuy)
                        equipButtonLabel.text = "未解锁";
                    else
                        equipButtonLabel.text = "装备";
                }
                equipButton.interactable = owned || canBuy;
            }

            if (unequipButton != null)
            {
                unequipButton.gameObject.SetActive(equipped);
                if (unequipButtonLabel != null)
                    unequipButtonLabel.text = "卸下";
            }

            RefreshCosmeticPreviewFromSelection();
        }

        void RefreshCosmeticPreviewFromSelection()
        {
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
                return;
            var pv = CosmeticPreviewController.Instance;
            if (pv == null) return;
            var id = _vm.Rows[_selectedIndex].Id;
            if (CatalogKind == CosmeticKind.Wing)
                pv.SetWingPreview(id);
            else
                pv.SetTrailPreview(id);
        }

        private void RefreshDetailQualityTextOnly()
        {
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count || detailQualityText == null)
                return;
            var row = _vm.Rows[_selectedIndex];
            var lib = ResolvedQualityLibrary();
            var q = RowQualityFromId(row.Id);
            detailQualityText.text = QualityVisualLibrary.FormatQualityLineRichTextLoose(lib, q, Time.unscaledTime);
        }

        private void ClearDetail()
        {
            _detailMythicQuality = false;
            if (detailNameText != null) detailNameText.text = "";
            if (detailUnlockHintText != null)
            {
                detailUnlockHintText.text = "";
                detailUnlockHintText.gameObject.SetActive(false);
            }
            if (detailStatText != null) detailStatText.text = "";
            if (detailQualityText != null) detailQualityText.text = "";
            if (detailPriceText != null)
            {
                detailPriceRoot.gameObject.SetActive(false);
                detailPriceText.text = "";
                detailPriceText.gameObject.SetActive(false);
            }
            if (equipButton != null) equipButton.gameObject.SetActive(false);
            if (unequipButton != null) unequipButton.gameObject.SetActive(false);
        }

        private void OnEquipOrBuyClicked()
        {
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count) return;
            var row = _vm.Rows[_selectedIndex];

            if (!row.Owned)
            {
                if (!row.UnlockedForPurchase)
                {
                    AudioGameplayFeedback.PlayUiError();
                    return;
                }

                long playerCoins = SaveService.Data != null ? SaveService.Data.coins : 0L;
                if (playerCoins < row.PriceCoins)
                {
                    UiRouter.OpenFreeCoinsOffer();
                    return;
                }

                if (CatalogKind == CosmeticKind.Wing)
                {
                    if (!WingService.TryBuy(row.Id))
                    {
                        AudioGameplayFeedback.PlayUiError();
                        return;
                    }
                }
                else
                {
                    if (!TrailService.TryBuy(row.Id))
                    {
                        AudioGameplayFeedback.PlayUiError();
                        return;
                    }
                }

                SaveService.Save();
            }
            else
            {
                if (CatalogKind == CosmeticKind.Wing)
                {
                    WingService.Equip(row.Id);
                    SaveService.Save();
                    UiCoordinator.RefreshEquipmentVisuals();
                }
                else
                {
                    TrailService.Equip(row.Id);
                    SaveService.Save();
                    UiCoordinator.RefreshEquipmentVisuals();
                }
            }

            _vm = BuildShopViewModel();
            RefreshCellsVisuals();
            RefreshDetail();
        }

        private void OnUnequipClicked()
        {
            if (CatalogKind == CosmeticKind.Wing)
            {
                WingService.Equip("");
                SaveService.Save();
                UiCoordinator.RefreshEquipmentVisuals();
            }
            else
            {
                TrailService.Equip("");
                SaveService.Save();
                UiCoordinator.RefreshEquipmentVisuals();
            }

            _vm = BuildShopViewModel();
            RefreshCellsVisuals();
            RefreshDetail();
        }

        private static string BuildAttrLine(string cosmeticId)
        {
            if (ConfigRegistry.Wings != null && ConfigRegistry.Wings.TryGet(cosmeticId, out var w) && w != null)
                return StatModifiersUiFormat.FormatItemLine(w.modifiers);
            if (ConfigRegistry.Trails != null && ConfigRegistry.Trails.TryGet(cosmeticId, out var t) && t != null)
                return StatModifiersUiFormat.FormatItemLine(t.modifiers);
            return StatModifiersUiFormat.NoBonusText;
        }

        private static string BuildDetailStat(WingDef def)
        {
            if (def == null) return "";
            return StatModifiersUiFormat.FormatItemLine(def.modifiers);
        }

        private static string BuildDetailStatTrail(TrailDef def)
        {
            if (def == null) return "";
            return StatModifiersUiFormat.FormatItemLine(def.modifiers);
        }

        static Sprite _cachedWhiteSprite;

        /// <summary>详情区无品质底图与商品图时的占位（非动态搭 UI，仅 Image 需要合法 Sprite）。</summary>
        static Sprite WhiteSprite()
        {
            if (_cachedWhiteSprite != null) return _cachedWhiteSprite;
            var tex = Texture2D.whiteTexture;
            _cachedWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                100f);
            return _cachedWhiteSprite;
        }
    }
}

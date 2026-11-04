using System;
using System.Collections;
using System.Collections.Generic;
using Game.Audio;
using Game.Collectibles;
using Game.Collectibles.Rods;
using Game.Collectibles.Stats;
using Game.Core.Config;
using Game.Economy;
using Game.Save;
using Game.UI.Framework;
using Game.UI.Preview;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 钓竿商店（UGUI）：交互与 <see cref="WingShopPanel"/> / <see cref="TrailShopPanel"/> 同源。
    /// </summary>
    public sealed class RodShopPanel : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.RodShop;

        [Header("Bindings (assign in Inspector)")]
        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Button closeButton;
        public ScrollRect rodScroll;
        public RectTransform rodGridContent;
        public RodShopItemView itemPrefab;

        [Header("Quality visuals (optional override)")]
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
        public float openSlideInMultiplier = 0.55f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        [Header("Scroll (grid)")]
        [SerializeField] int scrollGridColumns = 3;

        readonly List<RodShopItemView> _cells = new List<RodShopItemView>();
        UiGridCellPool<RodShopItemView> _cellPool;
        Coroutine _populateRoutine;
        RodShopScreenViewModel _vm;
        int _selectedIndex = -1;
        bool _detailMythicQuality;

        void Awake()
        {
            maskCloseButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                Hide();
            });
            closeButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                Hide();
            });
            equipButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnEquipOrBuyClicked();
            });
            unequipButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnUnequipClicked();
            });

            EnsureSlideHost();
            if (rodGridContent != null && itemPrefab != null)
                _cellPool = new UiGridCellPool<RodShopItemView>(rodGridContent, itemPrefab);
            HideImmediate();
        }

        void OnEnable()
        {
            UiCoordinator.Register(this);
            EconomyDisplayEvents.Changed += OnEconomyDisplayChanged;
        }

        void OnDisable()
        {
            EconomyDisplayEvents.Changed -= OnEconomyDisplayChanged;
            UiCoordinator.Unregister(this);
        }

        void OnDestroy()
        {
            CancelPopulateRoutine();
            _cellPool?.DisposeAll(_cells);
            KillPanelTweens();
        }

        void Update()
        {
            if (slideHost == null || !slideHost.IsShown || !_detailMythicQuality || detailQualityText == null ||
                _vm == null ||
                _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
                return;
            RefreshDetailQualityTextOnly();
        }

        void OnEconomyDisplayChanged() => RefreshOwnedCatalogIfOpen();

        /// <summary>从存档重建目录并刷新列表（优惠奖励、后台购买等）。</summary>
        public void RefreshOwnedCatalogIfOpen()
        {
            if (slideHost == null || !slideHost.IsShown)
                return;

            _vm = RodShopScreenViewModel.Build();
            RefreshCellsVisuals(-1);
            RefreshDetail();
        }

        public void Open(UiOpenPayload payload = null) => Show();
        public void Close() => Hide();

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
            _vm = RodShopScreenViewModel.Build();
            var rowCount = _vm?.Rows.Count ?? 0;

            if (_cellPool != null)
                yield return _cellPool.EnsureActiveCountBatched(rowCount, _cells);
            else
                RebuildGridSync();

            if (slideHost == null || !slideHost.IsShown)
                yield break;

            BindGridClicks();
            RefreshCellsVisuals();

            CosmeticPreviewController.Instance?.EnterShop(CosmeticPreviewController.CosmeticShopKind.Rod);

            if (_vm != null && _vm.Rows.Count > 0)
            {
                var idx = ResolveInitialSelectedIndex();
                SelectIndex(idx, false);
                ShopUiScroll.ScrollToItemIndex(rodScroll, idx, _vm.Rows.Count, scrollGridColumns);
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

        void HideImmediate()
        {
            CancelPopulateRoutine();
            EnsureSlideHost();
            slideHost.HideImmediate();
            CosmeticPreviewController.Instance?.LeaveShop();
        }

        void KillPanelTweens() => slideHost?.KillTweens();

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
                openSlideInMultiplier);
        }

        QualityVisualLibrary ResolvedQualityLibrary() =>
            qualityVisualLibrary != null ? qualityVisualLibrary : ConfigRegistry.QualityVisuals;

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

        void RebuildGrid()
        {
            RebuildGridSync();
            BindGridClicks();
            RefreshCellsVisuals();
        }

        void RebuildGridSync()
        {
            if (rodGridContent == null || itemPrefab == null)
            {
                if (itemPrefab == null)
                    Debug.LogError($"{nameof(RodShopPanel)}: 请在 Inspector 指定 {nameof(itemPrefab)}。", this);
                return;
            }

            if (_cellPool == null)
                _cellPool = new UiGridCellPool<RodShopItemView>(rodGridContent, itemPrefab);

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

        void SelectIndex(int index, bool userTriggeredPulse)
        {
            if (_vm == null || index < 0 || index >= _vm.Rows.Count) return;
            var changed = _selectedIndex != index;
            _selectedIndex = index;
            if (userTriggeredPulse && changed)
                AudioGameplayFeedback.PlayUiSelect();

            RefreshCellsVisuals(changed && userTriggeredPulse ? index : -1);
            RefreshDetail();
            RefreshRodPreviewFromSelection();
        }

        void RefreshCellsVisuals(int pulseIndex = -1)
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

        CosmeticQuality RowQualityFromId(string rodId)
        {
            ConfigRegistry.Rods.TryGet(rodId, out var def);
            return def != null ? def.quality : CosmeticQuality.Common;
        }

        Sprite RowShopIcon(string rodId)
        {
            ConfigRegistry.Rods.TryGet(rodId, out var def);
            return def?.shopIcon;
        }

        void RefreshDetail()
        {
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
            {
                ClearDetail();
                return;
            }

            var row = _vm.Rows[_selectedIndex];
            ConfigRegistry.Rods.TryGet(row.Id, out var def);
            var lib = ResolvedQualityLibrary();
            var q = def != null ? def.quality : CosmeticQuality.Common;
            var coins = SaveService.Data != null ? SaveService.Data.coins : 0L;

            _detailMythicQuality = q == CosmeticQuality.Mythic;

            if (detailNameText != null)
                detailNameText.text = def != null ? def.displayName : row.DisplayName;

            if (detailUnlockHintText != null)
            {
                var req = def?.requiredUnlockedLevel ?? 0;
                var gated = !row.Owned && !row.UnlockedForPurchase && req > 0;
                detailUnlockHintText.gameObject.SetActive(gated);
                if (gated)
                    detailUnlockHintText.text = "需解锁上一个钓竿";
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
                detailStatText.text = def != null ? StatModifiersUiFormat.FormatItemLine(def.modifiers) : "";

            if (detailQualityText != null)
                detailQualityText.text =
                    QualityVisualLibrary.FormatQualityLineRichTextLoose(lib, q, Time.unscaledTime);

            var owned = row.Owned;
            var equipped = owned && string.Equals(_vm.EquippedId, row.Id, StringComparison.Ordinal);
            var canBuy = !owned && row.UnlockedForPurchase;
            var canAfford = coins >= row.PriceCoins;

            if (detailPriceText != null && detailPriceRoot != null)
            {
                if (!owned)
                {
                    detailPriceRoot.SetActive(true);
                    detailPriceText.gameObject.SetActive(true);
                    var showWhite = row.UnlockedForPurchase && canAfford;
                    detailPriceText.color = showWhite ? Color.white : new Color(0.94f, 0.22f, 0.2f, 1f);
                    detailPriceText.text = $"x{row.PriceCoins}";
                }
                else
                {
                    detailPriceRoot.SetActive(false);
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

            RefreshRodPreviewFromSelection();
        }

        void RefreshRodPreviewFromSelection()
        {
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
                return;
            CosmeticPreviewController.Instance?.SetRodPreview(_vm.Rows[_selectedIndex].Id);
        }

        void RefreshDetailQualityTextOnly()
        {
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count || detailQualityText == null)
                return;

            var row = _vm.Rows[_selectedIndex];
            var lib = ResolvedQualityLibrary();
            var q = RowQualityFromId(row.Id);
            detailQualityText.text = QualityVisualLibrary.FormatQualityLineRichTextLoose(lib, q, Time.unscaledTime);
        }

        void ClearDetail()
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
                if (detailPriceRoot != null) detailPriceRoot.SetActive(false);
                detailPriceText.text = "";
                detailPriceText.gameObject.SetActive(false);
            }

            if (equipButton != null) equipButton.gameObject.SetActive(false);
            if (unequipButton != null) unequipButton.gameObject.SetActive(false);
        }

        void OnEquipOrBuyClicked()
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

                var playerCoins = SaveService.Data != null ? SaveService.Data.coins : 0L;
                if (playerCoins < row.PriceCoins)
                {
                    UiRouter.OpenFreeCoinsOffer();
                    return;
                }

                if (!RodService.TryBuy(row.Id))
                {
                    AudioGameplayFeedback.PlayUiError();
                    return;
                }

                SaveService.Save();
            }
            else
            {
                RodService.Equip(row.Id);
                SaveService.Save();
                UiCoordinator.RefreshEquipmentVisuals();
            }

            _vm = RodShopScreenViewModel.Build();
            RefreshCellsVisuals();
            RefreshDetail();
        }

        void OnUnequipClicked()
        {
            RodService.Equip("");
            SaveService.Save();
            UiCoordinator.RefreshEquipmentVisuals();
            _vm = RodShopScreenViewModel.Build();
            RefreshCellsVisuals();
            RefreshDetail();
        }

        static string BuildAttrLine(string rodId)
        {
            if (ConfigRegistry.Rods != null && ConfigRegistry.Rods.TryGet(rodId, out var def) && def != null)
                return StatModifiersUiFormat.FormatItemLine(def.modifiers);
            return StatModifiersUiFormat.NoBonusText;
        }

        static Sprite _cachedWhiteSprite;

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

using System;
using System.Collections;
using System.Collections.Generic;
using Game.Collectibles;
using Game.Collectibles.Characters;
using Game.Core.Config;
using Game.Economy;
using Game.Save;
using Game.UI.Framework;
using Game.UI.Preview;
using Game.UI.ViewModels;
using Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 角色养成面板（UGUI）：卡片列表 + 底部「解锁/使用」；打开动效与 <see cref="PetPanel"/> / 翅膀商店同源。
    /// </summary>
    public sealed class CharacterPanel : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.CharacterShop;

        [Header("Bindings")]
        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Button closeButton;
        public ScrollRect scrollRect;
        public RectTransform gridContent;
        [Tooltip("角色卡片预制体")]
        public CharacterItemView itemPrefab;

        [Tooltip("格子品质底图库；空则用 ConfigRegistry")]
        [SerializeField] private QualityVisualLibrary qualityVisualLibrary;

        [Header("Coins / Gems icon (cards)")]
        [SerializeField] private Sprite coinsListIcon;
        [SerializeField] private Sprite gemsListIcon;

        [Header("Bottom action")]
        public Button btnUse;
        public Text btnUseLabel;
        [Tooltip("若为奖励占位装饰，可与购买分支无关时保持隐藏")]
        public GameObject btnUseFreeDecor;

        [Header("Gate hint")]
        public Text unlockGateHint;

        [Header("Animation")]
        public float slideDuration = 0.42f;
        public float settleDuration = 0.32f;
        public float overshootLeftPx = 72f;
        public float openSlideInMultiplier = 0.55f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        readonly List<CharacterItemView> _cells = new List<CharacterItemView>();
        UiGridCellPool<CharacterItemView> _cellPool;
        Coroutine _populateRoutine;
        CharacterShopScreenViewModel _vm;
        int _selectedIndex = -1;

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
            btnUse?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnUseOrUnlockClicked();
            });
            EnsureSlideHost();
            if (gridContent != null && itemPrefab != null)
                _cellPool = new UiGridCellPool<CharacterItemView>(gridContent, itemPrefab);
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

        void OnEconomyDisplayChanged()
        {
            if (slideHost == null || !slideHost.IsShown) return;
            RefreshCellsVisuals(-1);
            RefreshActionBar();
        }

        void OnDestroy()
        {
            CancelPopulateRoutine();
            _cellPool?.DisposeAll(_cells);
            KillPanelTweens();
        }

        QualityVisualLibrary Lib => qualityVisualLibrary != null ? qualityVisualLibrary : ConfigRegistry.QualityVisuals;

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
            _vm = CharacterShopScreenViewModel.Build();
            var rowCount = _vm?.Rows.Count ?? 0;

            if (_cellPool != null)
                yield return _cellPool.EnsureActiveCountBatched(rowCount, _cells);
            else
                RebuildGridSync();

            if (slideHost == null || !slideHost.IsShown)
                yield break;

            BindGridClicks();
            RefreshCellsVisuals();

            if (_vm != null && _vm.Rows.Count > 0)
            {
                var idx = ResolveInitialSelectedIndex();
                SelectIndex(idx, false);
                ShopUiScroll.ScrollToItemIndex(scrollRect, idx, _vm.Rows.Count);
            }
            else
            {
                _selectedIndex = -1;
                ClearActionBar();
            }

            RefreshActionBar();
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
            slideHost.RequestClose();
        }

        void HideImmediate()
        {
            CancelPopulateRoutine();
            EnsureSlideHost();
            slideHost.HideImmediate();
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

        void RebuildGrid()
        {
            RebuildGridSync();
            BindGridClicks();
            RefreshCellsVisuals();
        }

        void RebuildGridSync()
        {
            if (gridContent == null || itemPrefab == null)
            {
                if (itemPrefab == null)
                    Debug.LogError($"{nameof(CharacterPanel)}: 请指定 {nameof(itemPrefab)}。", this);
                return;
            }

            if (_cellPool == null)
                _cellPool = new UiGridCellPool<CharacterItemView>(gridContent, itemPrefab);

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

        void SelectIndex(int index, bool userPulse)
        {
            if (_vm == null || index < 0 || index >= _vm.Rows.Count) return;
            var changed = _selectedIndex != index;
            _selectedIndex = index;
            if (userPulse && changed)
                AudioGameplayFeedback.PlayUiSelect();

            RefreshCellsVisuals(changed && userPulse ? index : -1);
            RefreshActionBar();
        }

        void RefreshCellsVisuals(int pulseIndex = -1)
        {
            if (_vm == null) return;
            var lib = Lib;
            var eq = _vm.EquippedId;

            for (var i = 0; i < _cells.Count && i < _vm.Rows.Count; i++)
            {
                var row = _vm.Rows[i];
                var cell = _cells[i];
                var def = ResolveDef(row.Id);
                var q = def != null ? def.quality : CosmeticQuality.Common;
                var slotBg = lib != null ? lib.ResolveSlotBackground(q) : null;
                var portrait = def?.portraitIcon;
                var stars = CharacterService.GetDisplayStars(row.Id, def);
                var pulse = pulseIndex == i;

                cell.Apply(row, eq, i == _selectedIndex, q, slotBg, portrait, coinsListIcon, gemsListIcon,
                    CharacterItemView.ResolveAttrLine(row.Id), stars, pulse);
            }
        }

        static CharacterDef ResolveDef(string id)
        {
            return ConfigRegistry.Characters != null && ConfigRegistry.Characters.TryGet(id, out var d) ? d : null;
        }

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

        void RefreshActionBar()
        {
            if (btnUseFreeDecor != null)
                btnUseFreeDecor.SetActive(false);

            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
            {
                ClearActionBar();
                return;
            }

            var row = _vm.Rows[_selectedIndex];

            if (unlockGateHint != null)
            {
                var gated = !row.Owned && !row.UnlockedForPurchase && row.RequiredUnlockedLevel > 0;
                unlockGateHint.gameObject.SetActive(gated);
                if (gated)
                    unlockGateHint.text = $"需已拥有等级 ≥ {row.RequiredUnlockedLevel} 的角色";
            }

            bool owned = row.Owned;
            bool equipped = owned && string.Equals(_vm.EquippedId, row.Id, StringComparison.Ordinal);
            bool canBuy = !owned && row.UnlockedForPurchase;
            bool canAfford = CanAfford(row);

            if (btnUse != null)
            {
                if (equipped)
                {
                    btnUse.interactable = false;
                    if (btnUseLabel != null) btnUseLabel.text = "使用中";
                }
                else if (!owned)
                {
                    if (canBuy && canAfford)
                    {
                        btnUse.interactable = true;
                        if (btnUseLabel != null) btnUseLabel.text = "解锁";
                    }
                    else if (canBuy && !canAfford)
                    {
                        btnUse.interactable = true;
                        if (btnUseLabel != null) btnUseLabel.text = "解锁";
                    }
                    else
                    {
                        btnUse.interactable = false;
                        if (btnUseLabel != null) btnUseLabel.text = "未解锁";
                    }
                }
                else
                {
                    btnUse.interactable = true;
                    if (btnUseLabel != null) btnUseLabel.text = "使用";
                }
            }
        }

        static bool CanAfford(CharacterShopRowViewModel row)
        {
            if (SaveService.Data == null) return false;
            return row.PriceCurrency == CharacterCurrencyType.Gems
                ? SaveService.Data.gems >= row.PriceAmount
                : SaveService.Data.coins >= row.PriceAmount;
        }

        void ClearActionBar()
        {
            if (unlockGateHint != null)
            {
                unlockGateHint.text = "";
                unlockGateHint.gameObject.SetActive(false);
            }

            if (btnUse != null) btnUse.interactable = false;
            if (btnUseLabel != null) btnUseLabel.text = "";
        }

        void OnUseOrUnlockClicked()
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

                if (!CanAfford(row))
                {
                    if (row.PriceCurrency == CharacterCurrencyType.Gems)
                        UiRouter.OpenFreeDiamondsOffer();
                    else
                        UiRouter.OpenFreeCoinsOffer();
                    return;
                }

                if (!CharacterService.TryBuy(row.Id))
                {
                    AudioGameplayFeedback.PlayUiError();
                    return;
                }

                SaveService.Save();
            }
            else
            {
                if (string.Equals(_vm.EquippedId, row.Id, StringComparison.Ordinal)) return;
                CharacterService.Equip(row.Id);
                SaveService.Save();
                UiCoordinator.RefreshEquipmentVisuals();
            }

            _vm = CharacterShopScreenViewModel.Build();
            RefreshCellsVisuals();
            RefreshActionBar();
            if (UiCoordinator.Hud != null)
                UiCoordinator.Hud.RefreshEconomyDisplay();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using Game.Collectibles;
using Game.Collectibles.Pets;
using Game.Collectibles.Stats;
using Game.Core;
using Game.Core.Config;
using Game.Economy;
using Game.Save;
using Game.Audio;
using Game.UI.Framework;
using Game.UI.Preview;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 宠物背包：列表、扩容、详情、合成弹窗（二级）、一键合成。
    /// </summary>
    public sealed class PetPanel : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.Pet;

        [Header("Bindings (assign in Inspector)")]
        public GameObject root;
        public RectTransform mainPanel;
        public ScrollRect scroll;
        public RectTransform gridContent;
        [Tooltip("背包格子预制体（必选）。")]
        public PetBackpackItemView itemPrefab;

        public Text detailName;
        public Text detailLevel;
        public Text detailQuality;
        public Text detailModifiersLine;
        public Image detailPetIcon;
        public Text equipCapacityLine;
        public Button expandEquipSlotButton;
        public Text backpackCapacityLine;
        public Button expandBackpackButton;
        public Button synthesizeOpenButton;
        public Button equipButton;
        public Button unequipButton;
        public Button oneKeySynthesizeButton;
        [Tooltip("钓鱼模式：出售当前选中宠物。")]
        public Button sellPetButton;

        public Button maskCloseButton;
        public Button closeButton;

        [Header("Synth overlay")]
        public GameObject synthOverlay;
        public Image synthSourceIcon;
        public Image synthTargetIcon;
        public Image synthSourceQualityFrame;
        public Image synthTargetQualityFrame;
        public Text synthRateText;
        public Text synthCountText;
        public Button synthDoButton;
        public Button synthCloseButton;

        [Header("Confirm overlay")]
        public GameObject confirmOverlay;
        public Text confirmMessage;
        public Button confirmOkButton;
        public Button confirmCancelButton;

        [SerializeField] private QualityVisualLibrary qualityVisualLibrary;

        [Header("Animation")]
        public float slideDuration = 0.42f;
        public float settleDuration = 0.32f;
        public float overshootLeftPx = 72f;
        public float openSlideInMultiplier = 0.55f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        [Tooltip("合成成功/一键收获弹窗；运行时自动挂在 root 下")]
        [SerializeField] private PetFusionOutcomeOverlay fusionOutcome;

        enum ConfirmIntent { None, EquipSlotExpand, BackpackExpand, FusionFailureNotice, AcknowledgeOnly, SellPet }

        ConfirmIntent _pendingConfirm;
        int _selectedIdx = -1;
        readonly List<PetBackpackItemView> _cells = new List<PetBackpackItemView>();
        UiGridCellPool<PetBackpackItemView> _cellPool;
        Coroutine _populateRoutine;
        List<PetRowViewModel> _rows = new List<PetRowViewModel>();

        private void Awake()
        {
            EnsureFusionOutcomeHost();

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
            expandEquipSlotButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                BeginEquipSlotExpand();
            });
            expandBackpackButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                BeginBackpackExpand();
            });
            synthesizeOpenButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OpenSynth();
            });
            equipButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnEquip();
            });
            unequipButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnUnequip();
            });
            oneKeySynthesizeButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnOneKeyFuse();
            });
            sellPetButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                TrySellSelectedPet();
            });

            synthCloseButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                synthOverlay?.SetActive(false);
            });
            synthDoButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnDoSynth();
            });

            confirmCancelButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                confirmOverlay?.SetActive(false);
            });
            confirmOkButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnConfirmOk();
            });

            EnsureSlideHost();
            EnsureConfirmOverlayResolved();
            if (gridContent != null && itemPrefab != null)
                _cellPool = new UiGridCellPool<PetBackpackItemView>(gridContent, itemPrefab);
            HideImmediate();
        }

        void EnsureConfirmOverlayResolved()
        {
            if (confirmOverlay != null && confirmMessage != null && confirmOkButton != null)
                return;

            Transform searchRoot = root != null ? root.transform : transform;
            if (confirmOverlay == null)
            {
                var hit = searchRoot.Find("ConfirmOverlay");
                if (hit != null)
                    confirmOverlay = hit.gameObject;
            }

            if (confirmOverlay == null)
                return;

            if (confirmMessage == null)
                confirmMessage = confirmOverlay.GetComponentInChildren<Text>(true);
            if (confirmOkButton == null)
            {
                var buttons = confirmOverlay.GetComponentsInChildren<Button>(true);
                for (var i = 0; i < buttons.Length; i++)
                {
                    var btn = buttons[i];
                    if (btn == null) continue;
                    var label = btn.GetComponentInChildren<Text>();
                    if (label != null && label.text.Contains("确定"))
                    {
                        confirmOkButton = btn;
                        break;
                    }
                }

                if (confirmOkButton == null && buttons.Length > 0)
                    confirmOkButton = buttons[0];
            }

            if (confirmCancelButton == null)
            {
                var buttons = confirmOverlay.GetComponentsInChildren<Button>(true);
                for (var i = 0; i < buttons.Length; i++)
                {
                    var btn = buttons[i];
                    if (btn == null || btn == confirmOkButton) continue;
                    var label = btn.GetComponentInChildren<Text>();
                    if (label != null && (label.text.Contains("取消") || label.text.Contains("Cancel")))
                    {
                        confirmCancelButton = btn;
                        break;
                    }
                }
            }
        }

        QualityVisualLibrary Lib => qualityVisualLibrary != null ? qualityVisualLibrary : ConfigRegistry.QualityVisuals;

        static CosmeticQuality Qual(PetQuality q) =>
            (CosmeticQuality)Mathf.Clamp((int)q, 0, (int)CosmeticQuality.Mythic);

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

        /// <summary>从存档重建背包并刷新列表（优惠奖励等）。</summary>
        public void RefreshOwnedCatalogIfOpen()
        {
            if (slideHost == null || !slideHost.IsShown)
                return;

            RebuildRows();
            if (_selectedIdx >= _rows.Count)
                _selectedIdx = _rows.Count > 0 ? 0 : -1;
            RefreshAll();
        }

        private void OnDestroy()
        {
            CancelPopulateRoutine();
            _cellPool?.DisposeAll(_cells);
            KillPanelTweens();
        }

        public void Open(UiOpenPayload payload = null) => Show();
        public void Close() => Hide();

        public void Show()
        {
            EnsureSlideHost();
            synthOverlay?.SetActive(false);
            confirmOverlay?.SetActive(false);

            slideHost.PlayOpen(PopulateAfterOpen);
        }

        void PopulateAfterOpen()
        {
            CancelPopulateRoutine();
            _populateRoutine = StartCoroutine(PopulateAfterOpenRoutine());
        }

        IEnumerator PopulateAfterOpenRoutine()
        {
            RebuildRows();

            if (_cellPool != null)
                yield return _cellPool.EnsureActiveCountBatched(_rows.Count, _cells);
            else
                EnsureGridCellCount(_rows.Count);

            if (slideHost == null || !slideHost.IsShown)
                yield break;

            BindGridClicks();
            ApplyGridCells(-1);

            CosmeticPreviewController.Instance?.EnterShop(CosmeticPreviewController.CosmeticShopKind.Pet);

            if (_rows.Count > 0)
                SelectIndex(0, false);
            else
                ClearDetail();

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
            slideHost.RequestClose(() => CosmeticPreviewController.Instance?.LeaveShop());
        }

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

        void RebuildRows()
        {
            _rows.Clear();
            foreach (var inv in PetService.GetOwnedInstancesSorted())
            {
                var def = inv.Def;
                _rows.Add(new PetRowViewModel
                {
                    InstanceId = inv.InstanceId,
                    PetId = inv.PetId,
                    DisplayName = def != null ? def.displayName : inv.PetId,
                    Quality = def != null ? def.quality : PetQuality.Green,
                    Level = def != null ? def.level : 1,
                    Count = 1,
                });
            }
        }

        void SelectIndex(int i, bool pulse)
        {
            if (_rows.Count == 0)
            {
                _selectedIdx = -1;
                RefreshGrid(-1);
                ClearDetail();
                return;
            }

            _selectedIdx = Mathf.Clamp(i, 0, _rows.Count - 1);
            if (pulse && _rows.Count > 0)
                AudioGameplayFeedback.PlayUiSelect();

            RefreshGrid(pulse ? _selectedIdx : -1);
            RefreshDetail();
        }

        void RefreshAll()
        {
            RefreshCapacityTexts();
            RefreshGrid(-1);
            RefreshDetail();
        }

        void RefreshCapacityTexts()
        {
            PetService.EnsureLoadout();
            var carryCur = PetService.CountEquippedPetsInUnlockedSlots();
            var carryMax = PetService.GetUnlockedEquipSlotCount();
            if (equipCapacityLine != null)
                equipCapacityLine.text = $"可携带宠物 {carryCur}/{carryMax}";

            var cap = PetService.GetBackpackCapacity();
            var used = PetService.GetTotalInventoryPetItemCount();
            if (backpackCapacityLine != null)
                backpackCapacityLine.text = $"背包容量 {used}/{cap}";
        }

        void RefreshGrid(int pulseIdx)
        {
            if (gridContent == null) return;
            if (itemPrefab == null)
            {
                Debug.LogError($"{nameof(PetPanel)}: 请在 Inspector 指定 {nameof(itemPrefab)}。", this);
                return;
            }

            EnsureGridCellCount(_rows.Count);
            ApplyGridCells(pulseIdx);
        }

        void EnsureGridCellCount(int count)
        {
            if (_cellPool == null && gridContent != null && itemPrefab != null)
                _cellPool = new UiGridCellPool<PetBackpackItemView>(gridContent, itemPrefab);

            _cellPool?.EnsureActiveCount(count, _cells);
            BindGridClicks();
        }

        void BindGridClicks()
        {
            for (var i = 0; i < _cells.Count; i++)
            {
                var idx = i;
                _cells[i].SetupClick(_ => SelectIndex(idx, true));
            }
        }

        void ApplyGridCells(int pulseIdx)
        {
            var lib = Lib;
            for (var i = 0; i < _cells.Count && i < _rows.Count; i++)
            {
                var cell = _cells[i];
                var row = _rows[i];
                var q = Qual(row.Quality);
                var slotBg = lib != null ? lib.ResolveSlotBackground(q) : null;
                Sprite icon = null;
                var attrLine = StatModifiersUiFormat.NoBonusText;
                if (ConfigRegistry.Pets != null &&
                    ConfigRegistry.Pets.TryGet(row.PetId, out var def) && def != null)
                {
                    icon = def.inventoryIcon;
                    attrLine = StatModifiersUiFormat.FormatItemLine(def.modifiers);
                }

                cell.Apply(row, i == _selectedIdx, PetService.IsInstanceEquipped(row.InstanceId), slotBg, icon,
                    attrLine, pulseIdx == i);
            }

            RefreshCapacityTexts();
        }

        void ClearDetail()
        {
            if (detailName != null) detailName.text = "";
            if (detailLevel != null) detailLevel.text = "";
            if (detailQuality != null) detailQuality.text = "";
            if (detailModifiersLine != null) detailModifiersLine.text = "";
            if (detailPetIcon != null)
            {
                detailPetIcon.sprite = null;
                detailPetIcon.enabled = false;
            }

            CosmeticPreviewController.Instance?.SetPetPreviewPetDefId(null);
        }

        void ApplyDetailQualityLine()
        {
            if (detailQuality == null || _selectedIdx < 0 || _selectedIdx >= _rows.Count) return;
            var row = _rows[_selectedIdx];
            var cq = Qual(row.Quality);
            detailQuality.supportRichText = true;
            detailQuality.text = QualityVisualLibrary.FormatQualityLineRichTextLoose(Lib, cq, Time.unscaledTime);
        }

        void RefreshDetail()
        {
            if (_selectedIdx < 0 || _selectedIdx >= _rows.Count)
            {
                ClearDetail();
                return;
            }

            var row = _rows[_selectedIdx];
            if (detailName != null) detailName.text = row.DisplayName;
            if (detailLevel != null) detailLevel.text = $"等级: {row.Level}级";

            if (detailQuality != null)
                ApplyDetailQualityLine();

            if (detailModifiersLine != null)
            {
                if (ConfigRegistry.Pets != null &&
                    ConfigRegistry.Pets.TryGet(row.PetId, out var pdef) && pdef != null)
                    detailModifiersLine.text = StatModifiersUiFormat.FormatItemLine(pdef.modifiers);
                else
                    detailModifiersLine.text = StatModifiersUiFormat.NoBonusText;
            }

            if (detailPetIcon != null)
            {
                if (ConfigRegistry.Pets != null &&
                    ConfigRegistry.Pets.TryGet(row.PetId, out var dp) && dp != null && dp.inventoryIcon != null)
                {
                    detailPetIcon.sprite = dp.inventoryIcon;
                    detailPetIcon.enabled = true;
                    detailPetIcon.preserveAspect = true;
                    detailPetIcon.color = Color.white;
                }
                else
                {
                    detailPetIcon.sprite = null;
                    detailPetIcon.enabled = false;
                }
            }

            CosmeticPreviewController.Instance?.SetPetPreviewPetDefId(row.PetId, row.InstanceId);

            if (equipButton != null)
                equipButton.interactable = !PetService.IsInstanceEquipped(row.InstanceId);

            if (unequipButton != null)
                unequipButton.gameObject.SetActive(PetService.IsInstanceEquipped(row.InstanceId));

            var fusionOff = IsPetFusionDisabledInCurrentProduct();
            if (synthesizeOpenButton != null)
            {
                synthesizeOpenButton.gameObject.SetActive(!fusionOff);
                synthesizeOpenButton.interactable = !fusionOff &&
                    PetFusionService.TryGetFusionTarget(row.PetId, out _);
            }
            if (oneKeySynthesizeButton != null)
            {
                oneKeySynthesizeButton.gameObject.SetActive(!fusionOff);
                oneKeySynthesizeButton.interactable = !fusionOff && HasAnySynthable();
            }

            if (sellPetButton != null)
            {
                var showSell = PetSellService.CanSellInCurrentProduct();
                sellPetButton.gameObject.SetActive(showSell);
                var canSellSelected = showSell
                                        && _selectedIdx >= 0
                                        && _selectedIdx < _rows.Count
                                        && PetSellService.IsInstanceSellable(_rows[_selectedIdx].InstanceId);
                sellPetButton.interactable = canSellSelected;
            }
        }

        static bool IsPetFusionDisabledInCurrentProduct() =>
            GameProductSettings.IsFishingContentEnabled && !GameProductSettings.IsTowerContentEnabled;

        void TrySellSelectedPet()
        {
            if (!PetSellService.CanSellInCurrentProduct()) return;
            if (_selectedIdx < 0 || _selectedIdx >= _rows.Count) return;

            EnsureConfirmOverlayResolved();
            var row = _rows[_selectedIdx];
            if (PetService.IsInstanceEquipped(row.InstanceId))
            {
                AudioGameplayFeedback.PlayUiError();
                _pendingConfirm = ConfirmIntent.AcknowledgeOnly;
                ShowConfirm("携带中的宠物不可出售。", okOnly: true);
                return;
            }

            if (!PetSellService.TryGetSellPrice(row.InstanceId, out var price))
            {
                AudioGameplayFeedback.PlayUiError();
                _pendingConfirm = ConfirmIntent.AcknowledgeOnly;
                ShowConfirm("该宠物不可出售。", okOnly: true);
                return;
            }

            var name = string.IsNullOrWhiteSpace(row.DisplayName) ? row.PetId : row.DisplayName;
            _pendingConfirm = ConfirmIntent.SellPet;
            ShowConfirm($"是否出售 {name} 并获得 {EconomyDisplayFormat.Format(price)} 金币？");
        }

        void ExecuteSellSelectedPet()
        {
            if (_selectedIdx < 0 || _selectedIdx >= _rows.Count) return;
            var row = _rows[_selectedIdx];
            if (!PetSellService.TrySell(row.InstanceId))
            {
                AudioGameplayFeedback.PlayUiError();
                _pendingConfirm = ConfirmIntent.AcknowledgeOnly;
                ShowConfirm("出售失败，请稍后再试。", okOnly: true);
                return;
            }

            RebuildRows();
            if (_rows.Count > 0) SelectIndex(Mathf.Min(_selectedIdx, _rows.Count - 1), false);
            else ClearDetail();
            RefreshAll();
        }

        bool HasEmptyEquipSlot()
        {
            for (var i = 0; i < PetService.GetUnlockedEquipSlotCount(); i++)
            {
                if (string.IsNullOrWhiteSpace(PetService.GetEquippedInstanceId(i))) return true;
            }
            return false;
        }

        bool HasAnySynthable()
        {
            foreach (var r in _rows)
            {
                if (PetFusionService.CanFuseThreeToNextLevel(r.PetId)) return true;
            }
            return false;
        }

        void Update()
        {
            if (slideHost == null || !slideHost.IsShown || _selectedIdx < 0 || _selectedIdx >= _rows.Count) return;
            if (Qual(_rows[_selectedIdx].Quality) != CosmeticQuality.Mythic) return;
            ApplyDetailQualityLine();
        }

        void OnEquip()
        {
            if (_selectedIdx < 0 || _selectedIdx >= _rows.Count) return;
            var inst = _rows[_selectedIdx].InstanceId;
            if (PetService.IsInstanceEquipped(inst))
                return;

            var ok = false;
            if (HasEmptyEquipSlot())
            {
                for (var i = 0; i < PetService.GetUnlockedEquipSlotCount(); i++)
                {
                    if (!string.IsNullOrWhiteSpace(PetService.GetEquippedInstanceId(i))) continue;
                    if (PetService.TryEquipSlot(i, inst))
                    {
                        ok = true;
                        break;
                    }
                }
            }
            else
                ok = PetService.TryEquipSlot(0, inst);

            if (ok)
            {
                SaveService.Save();
                UiCoordinator.RefreshEquipmentVisuals();
                RefreshAll();
            }
        }

        void OnUnequip()
        {
            if (_selectedIdx < 0 || _selectedIdx >= _rows.Count) return;
            PetService.UnequipInstance(_rows[_selectedIdx].InstanceId);
            SaveService.Save();
            UiCoordinator.RefreshEquipmentVisuals();
            RefreshAll();
        }

        void BeginEquipSlotExpand()
        {
            if (SaveService.Data == null) return;
            if (PetService.GetUnlockedEquipSlotCount() >= PetBackpackEconomy.EquipSlotsMax)
            {
                _pendingConfirm = ConfirmIntent.AcknowledgeOnly;
                ShowConfirm("已经升至满级，无法再提升", okOnly: true);
                return;
            }

            long cost = PetService.GetNextEquipSlotExpandCostCoins();
            if (SaveService.Data.coins < cost)
            {
                UiRouter.OpenFreeCoinsOffer();
                return;
            }

            _pendingConfirm = ConfirmIntent.EquipSlotExpand;
            ShowConfirm($"是否消耗 {cost} 金币扩充可携带栏位？");
        }

        void BeginBackpackExpand()
        {
            if (SaveService.Data == null) return;
            if (SaveService.Data.petBackpackCapacity >= PetBackpackEconomy.BackpackCapacityMax)
            {
                _pendingConfirm = ConfirmIntent.AcknowledgeOnly;
                ShowConfirm("已经升至满级，无法再提升", okOnly: true);
                return;
            }

            long cost = PetService.GetNextBackpackExpandCostCoins();
            if (SaveService.Data.coins < cost)
            {
                UiRouter.OpenFreeCoinsOffer();
                return;
            }

            _pendingConfirm = ConfirmIntent.BackpackExpand;
            ShowConfirm($"是否消耗 {cost} 金币将背包扩充 {PetBackpackEconomy.BackpackExpandStep}？");
        }

        void ShowFusionFailureConfirm()
        {
            _pendingConfirm = ConfirmIntent.FusionFailureNotice;
            AudioGameplayFeedback.PlayUiError();
            ShowConfirm(
                $"啊哦~合成失败了，返还{PetFusionService.FuseThreeFailureReturnCount}只宠物。",
                okOnly: true);
        }

        void ShowConfirm(string msg, bool okOnly = false)
        {
            EnsureConfirmOverlayResolved();
            if (confirmOverlay == null || confirmMessage == null) return;
            if (confirmCancelButton != null)
                confirmCancelButton.gameObject.SetActive(!okOnly);
            confirmMessage.text = msg;
            confirmOverlay.SetActive(true);
            confirmOverlay.transform.SetAsLastSibling();
        }

        void OnConfirmOk()
        {
            var intent = _pendingConfirm;
            confirmOverlay?.SetActive(false);
            _pendingConfirm = ConfirmIntent.None;
            switch (intent)
            {
                case ConfirmIntent.EquipSlotExpand:
                    if (PetService.TryExpandEquipSlotSpendCoins())
                        SaveService.Save();
                    else
                        UiRouter.OpenFreeCoinsOffer();

                    break;
                case ConfirmIntent.BackpackExpand:
                    if (PetService.TryExpandBackpackSpendCoins())
                        SaveService.Save();
                    else
                        UiRouter.OpenFreeCoinsOffer();

                    break;
                case ConfirmIntent.FusionFailureNotice:
                    break;
                case ConfirmIntent.AcknowledgeOnly:
                    break;
                case ConfirmIntent.SellPet:
                    ExecuteSellSelectedPet();
                    return;
            }

            RefreshAll();
        }

        void OpenSynth()
        {
            if (_selectedIdx < 0 || _selectedIdx >= _rows.Count) return;
            var id = _rows[_selectedIdx].PetId;
            if (!ConfigRegistry.Pets.TryGet(id, out var src) || src == null) return;
            if (!PetFusionService.TryGetFusionTarget(id, out var tgt) || tgt == null)
                return;

            synthOverlay?.SetActive(true);

            synthSourceIcon.sprite = src.inventoryIcon;
            synthSourceIcon.enabled = src.inventoryIcon != null;
            synthSourceIcon.color = Color.white;
            synthTargetIcon.sprite = tgt.inventoryIcon;
            synthTargetIcon.enabled = tgt.inventoryIcon != null;
            synthTargetIcon.color = Color.white;

            var lib = Lib;
            var qSrc = Qual(src.quality);
            var qTgt = Qual(tgt.quality);
            if (synthSourceQualityFrame != null)
            {
                synthSourceQualityFrame.sprite =
                    lib != null ? lib.ResolveSlotBackground(qSrc) : null;
                synthSourceQualityFrame.enabled = synthSourceQualityFrame.sprite != null;
                synthSourceQualityFrame.color = Color.white;
            }
            if (synthTargetQualityFrame != null)
            {
                synthTargetQualityFrame.sprite =
                    lib != null ? lib.ResolveSlotBackground(qTgt) : null;
                synthTargetQualityFrame.enabled = synthTargetQualityFrame.sprite != null;
                synthTargetQualityFrame.color = Color.white;
            }

            if (synthRateText != null)
                synthRateText.text = $"合成成功概率为 {src.fusionSuccessChance01 * 100f:0.#}%";

            var c = Mathf.Min(3, PetService.GetCount(id));
            if (synthCountText != null)
            {
                synthCountText.text = $"{c}/3";
                synthCountText.color = c >= 3 ? Color.white : new Color(1f, 0.32f, 0.32f, 1f);
            }

            if (synthDoButton != null)
                synthDoButton.interactable = PetFusionService.CanFuseThreeToNextLevel(id);
        }

        void OnDoSynth()
        {
            if (_selectedIdx < 0 || _selectedIdx >= _rows.Count) return;
            var id = _rows[_selectedIdx].PetId;
            if (!PetFusionService.TryFuseThreeToNextLevel(id, out var resultPetId, out var rolledOk))
            {
                AudioGameplayFeedback.PlayUiError();
                return;
            }
            SaveService.Save();
            synthOverlay?.SetActive(false);
            RebuildRows();
            if (_rows.Count == 0) _selectedIdx = -1;
            else _selectedIdx = Mathf.Min(_selectedIdx, _rows.Count - 1);
            RefreshAll();
            UiCoordinator.RefreshEquipmentVisuals();
            if (rolledOk)
            {
                AudioManager.Instance?.PlaySfx(SfxId.PetObtainMerge);
                EnsureFusionOutcomeHost();
                fusionOutcome?.ShowSingleSuccess(resultPetId);
            }
            else
            {
                ShowFusionFailureConfirm();
            }
        }

        void OnOneKeyFuse()
        {
            PetFusionService.RunOneKeyFuseAll(out _, out _, out var rewards);
            SaveService.Save();
            RebuildRows();
            SelectIndex(Mathf.Clamp(_selectedIdx, 0, Mathf.Max(0, _rows.Count - 1)), false);
            RefreshAll();
            UiCoordinator.RefreshEquipmentVisuals();
            if (rewards != null && rewards.Count > 0)
            {
                AudioManager.Instance?.PlaySfx(SfxId.PetObtainMerge);
                EnsureFusionOutcomeHost();
                fusionOutcome?.ShowBatchRewards(rewards);
            }
        }

        void EnsureFusionOutcomeHost()
        {
            if (fusionOutcome == null)
                fusionOutcome = GetComponentInChildren<PetFusionOutcomeOverlay>(true);

            if (fusionOutcome != null)
                fusionOutcome.SetQualityLibrary(qualityVisualLibrary);
            else
                Debug.LogWarning(
                    $"{nameof(PetPanel)}: 请在层级中为 {nameof(fusionOutcome)} 指定或为子物体挂载 {nameof(PetFusionOutcomeOverlay)}。",
                    this);
        }
    }
}

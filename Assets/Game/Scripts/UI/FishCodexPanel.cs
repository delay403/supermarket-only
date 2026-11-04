using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Game.Audio;
using Game.Collectibles;
using Game.Core.Config;
using Game.Fishing;
using Game.Fishing.Config;
using Game.UI.Framework;
using Game.UI.Preview;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 池塘图鉴：按钓鱼区分 Tab，左侧鱼格 + 右侧详情；交互与 <see cref="WingShopPanel"/> 同源。
    /// </summary>
    public sealed class FishCodexPanel : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.FishCodex;

        [Header("Bindings")]
        public GameObject root;
        public RectTransform mainPanel;
        public Button closeButton;
        public Button maskCloseButton;
        public Transform tabRoot;
        public Button tabButtonPrefab;
        public ScrollRect fishScroll;
        public RectTransform fishGridContent;
        public FishCodexItemView itemPrefab;

        [Header("Quality visuals (optional override)")]
        [SerializeField] private QualityVisualLibrary qualityVisualLibrary;

        [Header("Detail (right)")]
        public Text detailName;
        public Text detailQuality;
        public GameObject detailValueRoot;
        public Text detailValue;
        public Text detailProbability;
        public RawImage detailPreview;
        public Button equipButton;
        public Text equipButtonLabel;
        public Button lockedStatusButton;

        [Header("Animation")]
        public float slideDuration = 0.42f;
        public float settleDuration = 0.32f;
        public float overshootLeftPx = 72f;
        public float openSlideInMultiplier = 0.55f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        [Header("Scroll (grid)")]
        [SerializeField] int scrollGridColumns = 3;

        [Header("Tab")]
        [SerializeField] float selectedTabScale = 1.2f;
        [SerializeField] float tabScaleDuration = 0.22f;

        readonly List<Button> _tabButtons = new List<Button>();
        readonly List<FishCodexItemView> _cells = new List<FishCodexItemView>();
        UiGridCellPool<FishCodexItemView> _cellPool;
        Coroutine _populateRoutine;
        List<FishingZoneDef> _zones = new List<FishingZoneDef>();
        List<FishCodexEntry> _entries = new List<FishCodexEntry>();
        int _zoneIndex;
        int _selectedFishIndex = -1;
        int _totalPoolWeight;
        bool _detailMythicQuality;
        RenderTexture _detailPreviewRt;

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
                OnEquipClicked();
            });
            lockedStatusButton?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
            });

            if (lockedStatusButton != null)
                lockedStatusButton.interactable = true;

            EnsureSlideHost();
            if (fishGridContent != null && itemPrefab != null)
                _cellPool = new UiGridCellPool<FishCodexItemView>(fishGridContent, itemPrefab);
            HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);
        void OnDisable() => UiCoordinator.Unregister(this);

        void OnDestroy()
        {
            CancelPopulateRoutine();
            _cellPool?.DisposeAll(_cells);
            KillPanelTweens();
            KillTabScaleTweens();
            ReleaseDetailPreviewRenderTexture();
            CosmeticPreviewController.Instance?.LeaveShop();
        }

        void Update()
        {
            if (slideHost == null || !slideHost.IsShown || !_detailMythicQuality || detailQuality == null ||
                _selectedFishIndex < 0 || _selectedFishIndex >= _entries.Count)
                return;
            RefreshDetailQualityTextOnly();
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
            BuildZoneTabs();
            yield return SelectZoneBatched(0, false);
            _populateRoutine = null;
        }

        IEnumerator SelectZoneBatched(int index, bool userTriggered)
        {
            _zoneIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _zones.Count - 1));
            _selectedFishIndex = -1;

            if (_zones.Count == 0)
            {
                _entries = new List<FishCodexEntry>();
                _totalPoolWeight = 0;
                RebuildGridSync();
                BindGridClicks();
                RefreshCellsVisuals();
                ClearDetail();
                yield break;
            }

            _entries = FishCodexService.GetZoneCodex(_zones[_zoneIndex]);
            _totalPoolWeight = 0;
            for (var i = 0; i < _entries.Count; i++)
                _totalPoolWeight += _entries[i].zonePoolWeight;

            if (_cellPool != null)
                yield return _cellPool.EnsureActiveCountBatched(_entries.Count, _cells);
            else
                RebuildGridSync();

            if (slideHost == null || !slideHost.IsShown)
                yield break;

            BindGridClicks();
            RefreshCellsVisuals();

            CosmeticPreviewController.Instance?.EnterShop(CosmeticPreviewController.CosmeticShopKind.Fish);
            BindDetailPreviewCamera();

            if (_entries.Count > 0)
            {
                var startIdx = ResolveInitialSelectedFishIndex();
                SelectFish(startIdx, false);
                if (userTriggered)
                    ShopUiScroll.ScrollToItemIndex(fishScroll, startIdx, _entries.Count, scrollGridColumns);
            }
            else
                ClearDetail();

            RefreshTabScales(userTriggered);
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
            slideHost.RequestClose(OnCodexSlideClosed);
        }

        void OnCodexSlideClosed()
        {
            UnbindDetailPreviewCamera();
            CosmeticPreviewController.Instance?.LeaveShop();
        }

        void HideImmediate()
        {
            CancelPopulateRoutine();
            EnsureSlideHost();
            slideHost.HideImmediate();
            UnbindDetailPreviewCamera();
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

        void BuildZoneTabs()
        {
            _zones.Clear();
            var db = ConfigRegistry.FishingZones;
            if (db?.zones != null)
            {
                for (var i = 0; i < db.zones.Count; i++)
                {
                    if (db.zones[i] != null)
                        _zones.Add(db.zones[i]);
                }
            }

            _zones.Sort((a, b) => a.zoneLevel.CompareTo(b.zoneLevel));

            foreach (var b in _tabButtons)
                if (b != null) Destroy(b.gameObject);
            _tabButtons.Clear();

            if (tabRoot == null || tabButtonPrefab == null)
                return;

            for (var i = 0; i < _zones.Count; i++)
            {
                var idx = i;
                var zone = _zones[i];
                var btn = Instantiate(tabButtonPrefab, tabRoot);
                btn.gameObject.SetActive(true);
                ApplyZoneTabVisual(btn, zone);
                btn.onClick.AddListener(() =>
                {
                    AudioGameplayFeedback.PlayUiButton();
                    SelectZone(idx, true);
                });
                _tabButtons.Add(btn);
            }

            RefreshTabScales(animate: false);
        }

        static void ApplyZoneTabVisual(Button btn, FishingZoneDef zone)
        {
            if (btn == null || zone == null)
                return;

            foreach (var label in btn.GetComponentsInChildren<Text>(true))
                label.gameObject.SetActive(false);

            var icon = ResolveZoneTabIconImage(btn);
            if (icon == null)
                return;

            if (zone.codexTabIcon != null)
            {
                icon.sprite = zone.codexTabIcon;
                icon.color = Color.white;
                icon.preserveAspect = true;
                icon.gameObject.SetActive(true);
            }
            else
            {
                icon.sprite = null;
                icon.gameObject.SetActive(false);
            }
        }

        static Image ResolveZoneTabIconImage(Button btn)
        {
            var iconTr = btn.transform.Find("Icon");
            if (iconTr != null)
                return iconTr.GetComponent<Image>();

            for (var i = 0; i < btn.transform.childCount; i++)
            {
                var child = btn.transform.GetChild(i);
                var img = child.GetComponent<Image>();
                if (img != null && img != btn.targetGraphic)
                    return img;
            }

            return btn.GetComponent<Image>();
        }

        void SelectZone(int index, bool userTriggered)
        {
            if (_populateRoutine != null)
            {
                CancelPopulateRoutine();
                _populateRoutine = StartCoroutine(SelectZoneBatched(index, userTriggered));
                return;
            }

            _zoneIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _zones.Count - 1));
            _selectedFishIndex = -1;

            if (_zones.Count == 0)
            {
                _entries = new List<FishCodexEntry>();
                _totalPoolWeight = 0;
                RebuildGrid();
                ClearDetail();
                return;
            }

            _entries = FishCodexService.GetZoneCodex(_zones[_zoneIndex]);
            _totalPoolWeight = 0;
            for (var i = 0; i < _entries.Count; i++)
                _totalPoolWeight += _entries[i].zonePoolWeight;

            RebuildGrid();
            if (_entries.Count > 0)
            {
                var startIdx = ResolveInitialSelectedFishIndex();
                SelectFish(startIdx, false);
                if (userTriggered)
                    ShopUiScroll.ScrollToItemIndex(fishScroll, startIdx, _entries.Count, scrollGridColumns);
            }
            else
            {
                ClearDetail();
            }

            RefreshTabScales(userTriggered);
        }

        void RefreshTabScales(bool animate)
        {
            for (var i = 0; i < _tabButtons.Count; i++)
            {
                var btn = _tabButtons[i];
                if (btn == null)
                    continue;

                var rt = btn.transform as RectTransform;
                if (rt == null)
                    continue;

                var target = i == _zoneIndex ? selectedTabScale : 1f;
                rt.DOKill();
                if (animate)
                {
                    rt.DOScale(target, tabScaleDuration)
                        .SetEase(Ease.OutBack, 1.08f)
                        .SetLink(btn.gameObject);
                }
                else
                {
                    rt.localScale = Vector3.one * target;
                }
            }
        }

        void KillTabScaleTweens()
        {
            for (var i = 0; i < _tabButtons.Count; i++)
            {
                var btn = _tabButtons[i];
                if (btn == null)
                    continue;
                (btn.transform as RectTransform)?.DOKill();
            }
        }

        int ResolveInitialSelectedFishIndex()
        {
            if (_entries.Count == 0) return 0;
            var equipped = FishCodexEquipService.GetEquippedBackFishId();
            if (string.IsNullOrEmpty(equipped)) return 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].fishId, equipped, StringComparison.Ordinal))
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
            if (itemPrefab == null || fishGridContent == null)
            {
                if (itemPrefab == null)
                    Debug.LogError($"{nameof(FishCodexPanel)}: 请在 Inspector 指定 {nameof(itemPrefab)}。", this);
                return;
            }

            if (_cellPool == null)
                _cellPool = new UiGridCellPool<FishCodexItemView>(fishGridContent, itemPrefab);

            _cellPool.EnsureActiveCount(_entries.Count, _cells);
        }

        void BindGridClicks()
        {
            for (var i = 0; i < _cells.Count; i++)
            {
                var idx = i;
                _cells[i].SetupClick(_ => SelectFish(idx, true));
            }
        }

        void SelectFish(int index, bool userTriggered)
        {
            if (index < 0 || index >= _entries.Count)
            {
                ClearDetail();
                return;
            }

            var changed = _selectedFishIndex != index;
            _selectedFishIndex = index;
            if (userTriggered && changed)
                AudioGameplayFeedback.PlayUiSelect();

            RefreshCellsVisuals(changed && userTriggered ? index : -1);
            RefreshDetail();
        }

        void RefreshCellsVisuals(int pulseIndex = -1)
        {
            var lib = ResolvedQualityLibrary();

            for (var i = 0; i < _cells.Count && i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var q = entry.def != null ? entry.def.quality : entry.quality;
                var slotBg = lib != null ? lib.ResolveSlotBackground(q) : null;
                _cells[i].Apply(
                    entry,
                    i == _selectedFishIndex,
                    slotBg,
                    entry.icon,
                    _totalPoolWeight,
                    pulseIndex == i);
            }
        }

        void RefreshDetail()
        {
            if (_selectedFishIndex < 0 || _selectedFishIndex >= _entries.Count)
            {
                ClearDetail();
                return;
            }

            var e = _entries[_selectedFishIndex];
            var lib = ResolvedQualityLibrary();
            var q = e.def != null ? e.def.quality : e.quality;
            _detailMythicQuality = e.isDiscovered && q == CosmeticQuality.Mythic;

            if (detailName != null)
                detailName.text = e.isDiscovered ? e.displayName : "???";

            if (detailQuality != null)
            {
                detailQuality.gameObject.SetActive(e.isDiscovered);
                detailQuality.text = e.isDiscovered
                    ? QualityVisualLibrary.FormatQualityLineRichTextLoose(lib, q, Time.unscaledTime)
                    : "";
            }

            if (detailValueRoot != null)
                detailValueRoot.SetActive(e.isDiscovered);

            if (detailValue != null)
                detailValue.text = e.isDiscovered ? Economy.EconomyDisplayFormat.Format(e.sellPrice) : "";

            if (detailProbability != null)
            {
                if (e.isDiscovered && e.zonePoolWeight > 0 && _totalPoolWeight > 0)
                {
                    var pct = 100f * e.zonePoolWeight / _totalPoolWeight;
                    detailProbability.text = $"{pct:0.#}%";
                }
                else
                {
                    detailProbability.text = "";
                }
            }

            if (detailPreview != null)
            {
                detailPreview.gameObject.SetActive(e.isDiscovered);
                if (e.isDiscovered)
                {
                    BindDetailPreviewCamera();
                    detailPreview.uvRect = new Rect(0f, 0f, 1f, 1f);
                    detailPreview.color = Color.white;
                    CosmeticPreviewController.Instance?.SetFishPreview(e.fishId);
                }
                else
                {
                    CosmeticPreviewController.Instance?.SetFishPreview(null);
                }
            }

            var equipped = FishCodexEquipService.GetEquippedBackFishId() == e.fishId;
            if (equipButton != null) equipButton.gameObject.SetActive(e.isDiscovered);
            if (equipButtonLabel != null)
                equipButtonLabel.text = equipped ? "已佩戴" : "佩戴";
            if (lockedStatusButton != null)
            {
                lockedStatusButton.gameObject.SetActive(!e.isDiscovered);
                lockedStatusButton.interactable = true;
            }
        }

        void RefreshDetailQualityTextOnly()
        {
            if (_selectedFishIndex < 0 || _selectedFishIndex >= _entries.Count || detailQuality == null)
                return;

            var e = _entries[_selectedFishIndex];
            if (!e.isDiscovered)
                return;

            var lib = ResolvedQualityLibrary();
            var q = e.def != null ? e.def.quality : e.quality;
            detailQuality.text = QualityVisualLibrary.FormatQualityLineRichTextLoose(lib, q, Time.unscaledTime);
        }

        void ClearDetail()
        {
            _detailMythicQuality = false;
            if (detailName != null) detailName.text = "";
            if (detailQuality != null)
            {
                detailQuality.text = "";
                detailQuality.gameObject.SetActive(false);
            }
            if (detailValueRoot != null) detailValueRoot.SetActive(false);
            if (detailValue != null) detailValue.text = "";
            if (detailProbability != null) detailProbability.text = "";
            if (detailPreview != null)
            {
                CosmeticPreviewController.Instance?.SetFishPreview(null);
                detailPreview.gameObject.SetActive(false);
            }
            if (equipButton != null) equipButton.gameObject.SetActive(false);
            if (lockedStatusButton != null) lockedStatusButton.gameObject.SetActive(false);
        }

        void OnEquipClicked()
        {
            if (_selectedFishIndex < 0 || _selectedFishIndex >= _entries.Count)
                return;

            var e = _entries[_selectedFishIndex];
            if (!e.isDiscovered)
            {
                AudioGameplayFeedback.PlayUiError();
                return;
            }

            FishCodexEquipService.TryEquipBackFish(e.fishId);
            UiCoordinator.RefreshBackFishVisual();
            RefreshCellsVisuals();
            RefreshDetail();
        }

        void BindDetailPreviewCamera()
        {
            if (detailPreview == null)
                return;

            var cam = CosmeticPreviewController.Instance?.PreviewCamera;
            if (cam == null)
                return;

            var w = Mathf.Max(256, (int)detailPreview.rectTransform.rect.width);
            var h = Mathf.Max(256, (int)detailPreview.rectTransform.rect.height);
            if (_detailPreviewRt == null || _detailPreviewRt.width != w || _detailPreviewRt.height != h)
            {
                ReleaseDetailPreviewRenderTexture();
                _detailPreviewRt = new RenderTexture(w, h, 24);
            }

            detailPreview.texture = _detailPreviewRt;
            cam.targetTexture = _detailPreviewRt;
        }

        void UnbindDetailPreviewCamera()
        {
            var cam = CosmeticPreviewController.Instance?.PreviewCamera;
            if (cam != null && cam.targetTexture == _detailPreviewRt)
                cam.targetTexture = null;
        }

        void ReleaseDetailPreviewRenderTexture()
        {
            UnbindDetailPreviewCamera();
            if (_detailPreviewRt == null)
                return;
            _detailPreviewRt.Release();
            Destroy(_detailPreviewRt);
            _detailPreviewRt = null;
        }
    }
}

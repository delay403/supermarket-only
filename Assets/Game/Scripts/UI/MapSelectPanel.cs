using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Game.Audio;
using Game.Core.Config;
using Game.Maps;
using Game.Economy;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 地图切换面板（UGUI）：列表 + 底部「解锁/传送」；打开动效与 <see cref="CharacterPanel"/> 同源。
    /// 若场景中同时挂了 <see cref="Game.UI.FairyGUI.Mediators.MapSelectFguiMediator"/>，二者会争抢同一 <see cref="UiPanelId.MapSelect"/>，请只保留其一。
    /// </summary>
    public sealed class MapSelectPanel : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.MapSelect;

        [Header("Bindings")]
        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Button closeButton;
        public ScrollRect scrollRect;
        public RectTransform gridContent;
        public MapItemView itemPrefab;

        [Tooltip("列表上方大图预览；可不绑定。")]
        public Image previewImage;

        [Header("Icons")]
        [SerializeField] private Sprite medalsListIcon;

        [Header("Bottom action")]
        public Button btnMain;
        public Text btnMainLabel;

        [Header("Unlock popup")]
        public GameObject unlockOverlayRoot;
        public RectTransform unlockPopupPanel;
        public Text unlockTitleText;
        public Image unlockMapIconImage;
        public Button unlockBtnReturn;
        public Button unlockBtnTeleport;

        [Header("Animation")]
        public float slideDuration = 0.42f;
        public float settleDuration = 0.32f;
        public float overshootLeftPx = 72f;
        public float openSlideInMultiplier = 0.55f;
        public float unlockPopupPopDuration = 0.38f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        readonly List<MapItemView> _cells = new List<MapItemView>();
        UiGridCellPool<MapItemView> _cellPool;
        Coroutine _populateRoutine;
        MapSelectViewModel _vm;
        int _selectedIndex = -1;
        Tweener _unlockPopupTween;
        MapDef _popupTeleportTarget;

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
            btnMain?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                OnMainClicked();
            });
            unlockBtnReturn?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                HideUnlockPopup(immediate: false);
                _popupTeleportTarget = null;
            });
            unlockBtnTeleport?.onClick.AddListener(() =>
            {
                AudioGameplayFeedback.PlayUiButton();
                var def = _popupTeleportTarget;
                HideUnlockPopup(immediate: true);
                _popupTeleportTarget = null;

                if (def != null)
                    MapService.TeleportToMap(def);

                Hide();
            });

            EnsureSlideHost();
            if (gridContent != null && itemPrefab != null)
                _cellPool = new UiGridCellPool<MapItemView>(gridContent, itemPrefab);
            HideUnlockPopup(immediate: true);
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
            RefreshFull();
        }

        void OnDestroy()
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
            HideUnlockPopup(immediate: true);
            _popupTeleportTarget = null;

            slideHost.PlayOpen(PopulateAfterOpen);
        }

        void PopulateAfterOpen()
        {
            CancelPopulateRoutine();
            _populateRoutine = StartCoroutine(PopulateAfterOpenRoutine());
        }

        IEnumerator PopulateAfterOpenRoutine()
        {
            _vm = MapSelectViewModel.Build();
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
                var startIx = 0;
                for (var i = 0; i < _vm.Rows.Count; i++)
                {
                    if (string.Equals(_vm.Rows[i].MapId, _vm.CurrentMapId, System.StringComparison.Ordinal))
                    {
                        startIx = i;
                        break;
                    }
                }

                SelectIndex(startIx, false);
            }
            else
            {
                _selectedIndex = -1;
                ClearActionBar();
            }

            RefreshActionBar();
            RefreshPreview();
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
            slideHost.RequestClose(OnPanelClosed);
        }

        void OnPanelClosed()
        {
            HideUnlockPopup(immediate: true);
            _popupTeleportTarget = null;
        }

        void HideImmediate()
        {
            CancelPopulateRoutine();
            EnsureSlideHost();
            HideUnlockPopup(immediate: true);
            slideHost.HideImmediate();
        }

        void KillPanelTweens()
        {
            slideHost?.KillTweens();
            _unlockPopupTween?.Kill();
            unlockPopupPanel?.DOKill();
        }

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
                Debug.LogError($"{nameof(MapSelectPanel)}: 请在 Inspector 指定 {nameof(gridContent)} 与 {nameof(itemPrefab)}。", this);
                return;
            }

            if (_cellPool == null)
                _cellPool = new UiGridCellPool<MapItemView>(gridContent, itemPrefab);

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
            RefreshPreview();
        }

        void RefreshCellsVisuals(int pulseIndex = -1)
        {
            if (_vm == null) return;
            var curId = _vm.CurrentMapId;

            for (var i = 0; i < _cells.Count && i < _vm.Rows.Count; i++)
            {
                var row = _vm.Rows[i];
                var cell = _cells[i];
                var pulse = pulseIndex == i;
                cell.Apply(row, i == _selectedIndex, string.Equals(curId, row.MapId, System.StringComparison.Ordinal),
                    medalsListIcon, pulse);
            }
        }

        MapRowViewModel CurrentRow()
        {
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count) return null;
            return _vm.Rows[_selectedIndex];
        }

        MapDef CurrentDef()
        {
            var row = CurrentRow();
            if (row == null) return null;
            var db = ConfigRegistry.Maps;
            return db != null && db.TryGet(row.MapId, out var d) ? d : null;
        }

        void RefreshPreview()
        {
            if (previewImage == null) return;
            var row = CurrentRow();
            previewImage.sprite = row?.Icon;
            previewImage.enabled = row?.Icon != null;
            previewImage.preserveAspect = true;
        }

        void RefreshActionBar()
        {
            if (btnMain == null) return;

            var row = CurrentRow();
            if (row == null || btnMainLabel == null)
            {
                btnMain.interactable = false;
                if (btnMainLabel != null) btnMainLabel.text = "";
                return;
            }

            btnMain.interactable = true;

            if (!row.Unlocked)
            {
                var cost = Mathf.Max(0, row.MedalsRequiredToUnlock);
                btnMainLabel.text = cost <= 0 ? "解锁" : $"解锁({cost}奖牌)";
                btnMain.interactable = MapService.CanAffordUnlock(CurrentDef());
            }
            else
            {
                btnMainLabel.text = "传送";
                btnMain.interactable = true;
            }
        }

        void ClearActionBar()
        {
            if (btnMain != null) btnMain.interactable = false;
            if (btnMainLabel != null) btnMainLabel.text = "";
        }

        void OnMainClicked()
        {
            var def = CurrentDef();
            var row = CurrentRow();
            if (def == null || row == null) return;

            if (!row.Unlocked)
            {
                if (!MapService.CanAffordUnlock(def))
                {
                    AudioGameplayFeedback.PlayUiError();
                    RefreshFull();
                    return;
                }

                if (!MapService.TryUnlockWithMedals(def))
                {
                    AudioGameplayFeedback.PlayUiError();
                    RefreshFull();
                    return;
                }

                _popupTeleportTarget = def;
                RefreshFull();
                ShowUnlockPopup(def);
                UiCoordinator.Hud?.RefreshEconomyDisplay();
                return;
            }

            if (!MapService.TeleportToMap(def))
                AudioGameplayFeedback.PlayUiError();

            Hide();
        }

        void RefreshFull()
        {
            _vm = MapSelectViewModel.Build();
            if (_vm.Rows.Count == 0)
            {
                _selectedIndex = -1;
                foreach (var c in _cells)
                    if (c != null) Destroy(c.gameObject);
                _cells.Clear();
                ClearActionBar();
                RefreshPreview();
                return;
            }

            if (_selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
                _selectedIndex = 0;

            if (_cells.Count != _vm.Rows.Count)
            {
                RebuildGrid();
                RefreshPreview();
            }
            else
            {
                RefreshCellsVisuals();
                RefreshActionBar();
                RefreshPreview();
            }
        }

        void ShowUnlockPopup(MapDef def)
        {
            if (unlockOverlayRoot == null || unlockPopupPanel == null || def == null) return;

            _unlockPopupTween?.Kill();
            unlockPopupPanel.DOKill();

            if (unlockMapIconImage != null)
            {
                unlockMapIconImage.sprite = def.mapIcon;
                unlockMapIconImage.enabled = def.mapIcon != null;
                unlockMapIconImage.preserveAspect = true;
            }

            if (unlockTitleText != null)
                unlockTitleText.text = $"解锁新地图!\n{def.displayName}";

            unlockOverlayRoot.SetActive(true);
            unlockPopupPanel.localScale = Vector3.one * 0.08f;
            _unlockPopupTween = unlockPopupPanel
                .DOScale(1f, unlockPopupPopDuration)
                .SetEase(Ease.OutBack);
        }

        void HideUnlockPopup(bool immediate)
        {
            if (unlockOverlayRoot == null || unlockPopupPanel == null) return;

            _unlockPopupTween?.Kill();
            unlockPopupPanel.DOKill();

            if (immediate)
            {
                unlockOverlayRoot.SetActive(false);
                return;
            }

            unlockPopupPanel
                .DOScale(0f, 0.15f)
                .SetEase(Ease.InQuad)
                .OnComplete(() => unlockOverlayRoot.SetActive(false));
        }
    }
}

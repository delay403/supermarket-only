using System.Collections.Generic;
using FairyGUI;
using Game;
using Game.Audio;
using Game.Core.Config;
using Game.Maps;
using Game.Save;
using Game.UI.FairyGUI;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;

namespace Game.UI.FairyGUI.Mediators
{
    /// <summary>
    /// 地图切换 / 解锁（FGUI）。请在编辑器指向包内窗口组件名，并保持与序列化字段一致的子节点命名（或通过 Inspector 修改）。
    /// 若项目改用 UGUI <see cref="Game.UI.MapSelectPanel"/>，请勿在同一场景中再挂本组件，否则会与前者争抢 <see cref="UiPanelId.MapSelect"/>。
    /// </summary>
    public sealed class MapSelectFguiMediator : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.MapSelect;

        [Header("FGUI package/component")]
        [SerializeField] private string packageName = "Game";
        [Tooltip("例如切换地图整窗组件名（与 FairyGUI 导出一致）。")]
        [SerializeField] private string componentName = "";

        [Header("Child names (GameWnd 外包一层组件)")]
        [SerializeField] private string mapListChildName = "listMap";
        [SerializeField] private bool mapListIsGList = true;
        [SerializeField] private string btnMainName = "BtnTeleport";
        [SerializeField] private string btnCloseName = "BtnClose";
        [SerializeField] private string previewLoaderName = "LoaderMapPreview";

        [Header("Unlock popup (optional)")]
        [SerializeField] private string unlockPopupName = "UnlockPopup";
        [SerializeField] private string unlockPopupPreviewLoaderName = "LoaderPreview";
        [SerializeField] private string unlockPopupTitleName = "LabelTitle";
        [SerializeField] private string unlockPopupBtnTeleportName = "BtnTeleportPopup";
        [SerializeField] private string unlockPopupBtnReturnName = "BtnReturn";

        private GComponent _view;
        private GList _mapList;
        private GComponent _mapStrip;
        private GButton _btnMain;
        private GButton _btnClose;
        private GLoader _previewLoader;

        private GComponent _unlockPopup;
        private GLoader _unlockPreviewLoader;
        private GTextField _unlockTitle;
        private GButton _unlockBtnTeleport;
        private GButton _unlockBtnReturn;

        private readonly List<UIItemMap> _stripItems = new List<UIItemMap>();

        private MapSelectViewModel _vm;
        private int _selectedIndex = -1;
        private MapDef _popupTeleportTarget;

        private EventCallback1 _mainClickHandler;
        private EventCallback1 _closeClickHandler;
        private EventCallback1 _unlockTeleportClickHandler;
        private EventCallback1 _unlockReturnClickHandler;

        private void OnEnable() => UiCoordinator.Register(this);

        private void OnDisable() => UiCoordinator.Unregister(this);

        private void OnDestroy() => DisposeView();

        public void Open(UiOpenPayload payload = null)
        {
            if (_view == null)
            {
                if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(componentName))
                {
                    Debug.LogWarning("[MapSelectFguiMediator] packageName / componentName 未配置。");
                    return;
                }

                _view = UIPackage.CreateObject(packageName.Trim(), componentName.Trim())?.asCom;
                if (_view != null)
                {
                    _view.Center();
                    GRoot.inst.AddChild(_view);
                }
            }

            if (_view == null)
            {
                Debug.LogWarning($"[MapSelectFguiMediator] 无法创建组件 {packageName}/{componentName}");
                return;
            }

            EnsureBindings();
            _view.visible = true;
            RefreshFull();
        }

        public void Close()
        {
            HideUnlockPopup(immediate: true);
            if (_view != null) _view.visible = false;
        }

        void EnsureBindings()
        {
            if (_btnMain != null) return;

            ResolveMapHost();
            _previewLoader = _view.GetChild(previewLoaderName)?.asLoader;

            _btnMain = _view.GetChild(btnMainName)?.asButton;
            _btnClose = _view.GetChild(btnCloseName)?.asButton;

            _unlockPopup = _view.GetChild(unlockPopupName)?.asCom;
            if (_unlockPopup != null)
            {
                _unlockPopup.visible = false;
                _unlockPreviewLoader = _unlockPopup.GetChild(unlockPopupPreviewLoaderName)?.asLoader;
                _unlockTitle = _unlockPopup.GetChild(unlockPopupTitleName)?.asTextField;
                _unlockBtnTeleport = _unlockPopup.GetChild(unlockPopupBtnTeleportName)?.asButton;
                _unlockBtnReturn = _unlockPopup.GetChild(unlockPopupBtnReturnName)?.asButton;
            }

            if (_btnClose != null)
            {
                _closeClickHandler = OnCloseClicked;
                _btnClose.onClick.Add(_closeClickHandler);
            }

            if (_btnMain != null)
            {
                _mainClickHandler = OnMainClicked;
                _btnMain.onClick.Add(_mainClickHandler);
            }

            if (_unlockBtnTeleport != null)
            {
                _unlockTeleportClickHandler = OnUnlockPopupTeleportClicked;
                _unlockBtnTeleport.onClick.Add(_unlockTeleportClickHandler);
            }

            if (_unlockBtnReturn != null)
            {
                _unlockReturnClickHandler = OnUnlockPopupReturnClicked;
                _unlockBtnReturn.onClick.Add(_unlockReturnClickHandler);
            }

            if (_mapList != null)
            {
                _mapList.itemRenderer = OnMapListItemRenderer;
                _mapList.onClickItem.Add(OnMapListClickItem);
            }
        }

        void ResolveMapHost()
        {
            var host = _view.GetChild(mapListChildName);
            if (mapListIsGList)
                _mapList = host?.asList;

            if (_mapList == null)
                _mapStrip = host?.asCom ?? _view;
        }

        void RefreshFull()
        {
            _vm = MapSelectViewModel.Build();

            if (_vm.Rows.Count == 0)
            {
                _selectedIndex = -1;
                RefreshChrome();
                return;
            }

            var curIx = -1;
            for (var i = 0; i < _vm.Rows.Count; i++)
            {
                if (string.Equals(_vm.Rows[i].MapId, _vm.CurrentMapId, System.StringComparison.Ordinal))
                {
                    curIx = i;
                    break;
                }
            }

            _selectedIndex = curIx >= 0 ? curIx : 0;

            if (_mapList != null)
            {
                _mapList.numItems = _vm.Rows.Count;
                _mapList.selectedIndex = _selectedIndex;
            }
            else
                RebuildStrip();

            RefreshChrome();
        }

        void RebuildStrip()
        {
            var host = _mapStrip;
            if (host == null) return;

            foreach (var it in _stripItems)
            {
                if (it != null && !it.isDisposed)
                    host.RemoveChild(it, true);
            }

            _stripItems.Clear();

            for (var i = 0; i < _vm.Rows.Count; i++)
            {
                var row = _vm.Rows[i];
                var item = UIItemMap.CreateInstance();
                BindRowToItem(item, row, i, _selectedIndex);
                item.onClick.Clear();
                var captured = i;
                item.onClick.Add(_ => SelectRow(captured));
                host.AddChild(item);
                _stripItems.Add(item);
            }
        }

        void OnMapListItemRenderer(int index, GObject obj)
        {
            var item = (UIItemMap)obj;
            if (_vm == null || index < 0 || index >= _vm.Rows.Count) return;
            BindRowToItem(item, _vm.Rows[index], index, _selectedIndex);
        }

        static void BindRowToItem(UIItemMap item, MapRowViewModel row, int index, int selectedIndex)
        {
            if (item.m_Label != null)
                item.m_Label.text = row.DisplayName ?? row.MapId;

            item.grayed = !row.Unlocked;
            item.selected = index == selectedIndex;

            FguiNTextureUtil.ApplySpriteToButtonIcon(item, row.Icon);

            item.data = index;
        }

        void OnMapListClickItem(EventContext ctx)
        {
            if (ctx.data is GObject obj && obj is UIItemMap im && im.data is int ix)
                SelectRow(ix);
        }

        /// <summary>GList 点击项索引解析。</summary>
        void SelectRow(int index)
        {
            if (_vm == null || index < 0 || index >= _vm.Rows.Count) return;
            _selectedIndex = index;

            if (_mapList != null)
                _mapList.selectedIndex = index;
            else
            {
                for (var i = 0; i < _stripItems.Count; i++)
                    _stripItems[i].selected = i == index;
            }

            RefreshChrome();
        }

        void RefreshChrome()
        {
            ApplyPreviewForSelection();
            RefreshMainButton();

            if (_mapList != null)
                _mapList.RefreshVirtualList();
        }

        void ApplyPreviewForSelection()
        {
            var row = CurrentRow();
            if (row == null) return;

            var nt = FguiNTextureUtil.FromSprite(row.Icon);
            if (_previewLoader != null && nt != null)
                _previewLoader.texture = nt;
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

        void RefreshMainButton()
        {
            if (_btnMain == null) return;

            var row = CurrentRow();
            if (row == null)
            {
                _btnMain.touchable = false;
                _btnMain.title = "";
                return;
            }

            _btnMain.touchable = true;

            if (!row.Unlocked)
            {
                var cost = Mathf.Max(0, row.MedalsRequiredToUnlock);
                _btnMain.title = cost <= 0 ? "解锁" : $"解锁({cost}奖牌)";
                _btnMain.grayed = !MapService.CanAffordUnlock(CurrentDef());
            }
            else
            {
                _btnMain.title = "传送";
                _btnMain.grayed = false;
            }
        }

        void OnCloseClicked(EventContext _)
        {
            AudioGameplayFeedback.PlayUiButton();
            Close();
        }

        void OnMainClicked(EventContext _)
        {
            AudioGameplayFeedback.PlayUiButton();

            var def = CurrentDef();
            var row = CurrentRow();
            if (def == null || row == null) return;

            if (!row.Unlocked)
            {
                if (!MapService.TryUnlockWithMedals(def))
                {
                    AudioGameplayFeedback.PlayUiError();
                    RefreshFull();
                    return;
                }

                _popupTeleportTarget = def;
                ShowUnlockPopup(def);
                RefreshFull();
                return;
            }

            if (!MapService.TeleportToMap(def))
                AudioGameplayFeedback.PlayUiError();

            Close();
        }

        void ShowUnlockPopup(MapDef def)
        {
            if (_unlockPopup == null || def == null) return;

            GTween.Kill(_unlockPopup);

            var nt = FguiNTextureUtil.FromSprite(def.mapIcon);
            if (_unlockPreviewLoader != null && nt != null)
                _unlockPreviewLoader.texture = nt;

            if (_unlockTitle != null)
                _unlockTitle.text = $"解锁新地图!\n{def.displayName}";

            _unlockPopup.visible = true;
            _unlockPopup.SetScale(0.08f, 0.08f);
            _unlockPopup.TweenScale(Vector2.one, 0.38f).SetEase(EaseType.BackOut);
        }

        void HideUnlockPopup(bool immediate)
        {
            if (_unlockPopup == null) return;
            GTween.Kill(_unlockPopup);
            if (immediate)
                _unlockPopup.visible = false;
            else
                _unlockPopup.TweenScale(Vector2.zero, 0.15f).OnComplete(() => _unlockPopup.visible = false);
        }

        void OnUnlockPopupTeleportClicked(EventContext _)
        {
            AudioGameplayFeedback.PlayUiButton();
            var def = _popupTeleportTarget;
            HideUnlockPopup(immediate: true);
            _popupTeleportTarget = null;

            if (def != null)
                MapService.TeleportToMap(def);

            Close();
        }

        void OnUnlockPopupReturnClicked(EventContext _)
        {
            AudioGameplayFeedback.PlayUiButton();
            _popupTeleportTarget = null;
            HideUnlockPopup(immediate: false);
        }

        void DisposeView()
        {
            HideUnlockPopup(immediate: true);

            if (_view == null || _view.isDisposed)
            {
                _view = null;
                return;
            }

            _view.Dispose();
            _view = null;
            _mapList = null;
            _mapStrip = null;
            _stripItems.Clear();
        }
    }
}

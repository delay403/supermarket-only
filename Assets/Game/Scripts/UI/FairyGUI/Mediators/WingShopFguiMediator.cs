using UnityEngine;
using FairyGUI;
using Game.UI.Framework;
using Game.UI.ViewModels;

namespace Game.UI.FairyGUI.Mediators
{
    /// <summary>
    /// FGUI 版翅膀商店模板。若场景里同时使用 <see cref="Game.UI.WingShopPanel"/>（UGUI），
    /// 二者均注册 <c>UiPanelId.WingShop</c> 会互相覆盖 — 请只启用其一。
    /// </summary>
    public sealed class WingShopFguiMediator : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.WingShop;

        [Header("FGUI package/component")]
        [SerializeField] private string packageName = "";
        [SerializeField] private string componentName = "";

        private GComponent _view;

        private void OnEnable() => UiCoordinator.Register(this);
        private void OnDisable() => UiCoordinator.Unregister(this);
        private void OnDestroy() => DisposeView();

        public void Open(UiOpenPayload payload = null)
        {
            if (_view == null)
            {
                _view = UIPackage.CreateObject(packageName, componentName)?.asCom;
                if (_view != null) GRoot.inst.AddChild(_view);
            }

            if (_view == null) return;
            _view.visible = true;

            // TODO: Bind ViewModel fields into FGUI controls.
            // Suggested VM type for this panel: CosmeticShopScreenViewModel
        }

        public void Close()
        {
            if (_view != null) _view.visible = false;
        }

        void DisposeView()
        {
            if (_view == null || _view.isDisposed)
            {
                _view = null;
                return;
            }

            _view.Dispose();
            _view = null;
        }
    }
}

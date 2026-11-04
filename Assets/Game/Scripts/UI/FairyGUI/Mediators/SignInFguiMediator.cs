using UnityEngine;
using FairyGUI;
using Game.UI.Framework;
using Game.UI.ViewModels;

namespace Game.UI.FairyGUI.Mediators
{
    /// <summary>
    /// Auto-generated template. Fill TODO bindings according to your FGUI component structure.
    /// </summary>
    public sealed class SignInFguiMediator : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.SignIn;

        [Header("FGUI package/component")]
        [SerializeField] private string packageName = "";
        [SerializeField] private string componentName = "";

        private GComponent _view;

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(componentName))
                return;
            UiCoordinator.Register(this);
        }

        private void OnDisable()
        {
            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(componentName))
                return;
            UiCoordinator.Unregister(this);
        }

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
            // Suggested VM type for this panel: SignInScreenViewModel
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

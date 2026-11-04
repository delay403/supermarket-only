using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeStatsAggregator : MonoBehaviour
    {
        [SerializeField] private Text handleCountText;

        SupermarketOrganizeService _service;

        void OnEnable()
        {
            ApplyProductModeVisibility();
            BindService();
        }

        void OnDisable()
        {
            UnbindService();
        }

        public void BindService(SupermarketOrganizeService service)
        {
            UnbindService();
            _service = service;
            if (_service != null)
                _service.HandleCountChanged += OnHandleCountChanged;
            RefreshDisplay();
        }

        void BindService()
        {
            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            if (director != null)
                BindService(director.Service);
        }

        void UnbindService()
        {
            if (_service != null)
                _service.HandleCountChanged -= OnHandleCountChanged;
            _service = null;
        }

        void OnHandleCountChanged(int current, int max) => RefreshDisplay(current, max);

        public void RefreshDisplay()
        {
            if (_service?.HandleZone != null)
                RefreshDisplay(_service.HandleZone.Count, _service.HandleZone.MaxCapacity);
            else if (handleCountText != null)
                handleCountText.text = "0/0";
        }

        void RefreshDisplay(int current, int max)
        {
            if (handleCountText != null)
                handleCountText.text = $"{current}/{max}";
        }

        void ApplyProductModeVisibility()
        {
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.SupermarketOrganize))
                enabled = false;
        }
    }
}

using Game.Core;
using Game.UI;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeHudSection : MonoBehaviour
    {
        [SerializeField] private GameObject sectionRoot;
        [SerializeField] private Text handleCountText;
        [SerializeField] private Text shelfProgressText;
        [SerializeField] private Text itemProgressText;
        [SerializeField] private Text sweepRobotRemainingText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button sweepRobotButton;
        [SerializeField] private SupermarketCrosshairUi crosshairUi;
        [SerializeField] private SupermarketShelfFeedbackUi shelfFeedback;

        SupermarketOrganizeService _service;
        float _nextSweepUiTick;

        void OnEnable()
        {
            ApplyProductModeVisibility();
            EnsureCrosshair();
            BindService();
            upgradeButton?.onClick.AddListener(OnUpgradeClicked);
            sweepRobotButton?.onClick.AddListener(OnSweepRobotClicked);
            SupermarketOrganizeProgressionService.AttributeUpgraded += OnAttributeUpgraded;
        }

        void OnDisable()
        {
            UnbindService();
            upgradeButton?.onClick.RemoveListener(OnUpgradeClicked);
            sweepRobotButton?.onClick.RemoveListener(OnSweepRobotClicked);
            SupermarketOrganizeProgressionService.AttributeUpgraded -= OnAttributeUpgraded;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextSweepUiTick)
                return;

            _nextSweepUiTick = Time.unscaledTime + 0.25f;
            RefreshSweepRobotRemaining();
        }

        public void RefreshAll()
        {
            ApplyProductModeVisibility();
            EnsureCrosshair();
            BindService();
            RefreshCounts();
        }

        void BindService()
        {
            UnbindService();
            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            _service = director?.Service;
            if (_service == null)
                return;

            _service.HandleCountChanged += OnHandleChanged;
            _service.ProgressChanged += OnProgressChanged;
            _service.GameStarted += OnGameStarted;
            _service.GameWon += OnGameWon;
            RefreshCounts();

            var stats = FindObjectOfType<SupermarketOrganizeStatsAggregator>();
            stats?.BindService(_service);
        }

        void UnbindService()
        {
            if (_service == null)
                return;

            _service.HandleCountChanged -= OnHandleChanged;
            _service.ProgressChanged -= OnProgressChanged;
            _service.GameStarted -= OnGameStarted;
            _service.GameWon -= OnGameWon;
            _service = null;
        }

        void OnGameStarted() => RefreshCounts();

        void OnHandleChanged(int current, int max)
        {
            if (handleCountText != null)
                handleCountText.text = $"{current}/{max}";
        }

        void OnProgressChanged() => RefreshProgressTexts();

        void OnGameWon() => RefreshProgressTexts();

        void RefreshCounts()
        {
            if (_service == null)
                return;

            OnHandleChanged(_service.HandleZone.Count, _service.HandleZone.MaxCapacity);
            RefreshProgressTexts();
            RefreshSweepRobotRemaining();
        }

        void RefreshProgressTexts()
        {
            if (_service == null)
                return;

            if (shelfProgressText != null)
                shelfProgressText.text = $"{_service.CompletedShelfCount}/{_service.TotalShelfCount}";

            if (itemProgressText != null)
                itemProgressText.text = $"{_service.PlacedItemCount}/{_service.TotalItemSlotCount}";
        }

        void RefreshSweepRobotRemaining()
        {
            if (sweepRobotRemainingText == null)
                return;

            if (!SupermarketOrganizeProgressionService.IsSweepRobotActive())
            {
                sweepRobotRemainingText.text = string.Empty;
                return;
            }

            sweepRobotRemainingText.text = FormatDuration(
                SupermarketOrganizeProgressionService.GetSweepRobotRemainingSeconds());
        }

        static string FormatDuration(float seconds)
        {
            if (seconds <= 0f)
                return "0:00";

            var total = Mathf.CeilToInt(seconds);
            var minutes = total / 60;
            var secs = total % 60;
            return $"{minutes:00}:{secs:00}";
        }

        void EnsureCrosshair()
        {
            if (crosshairUi == null)
                crosshairUi = FindObjectOfType<SupermarketCrosshairUi>(true);
            crosshairUi?.EnsureDefaultCrosshair();
        }

        void ApplyProductModeVisibility()
        {
            var active = GameplayModuleContext.IsModuleActive(GameplayModule.SupermarketOrganize);
            if (sectionRoot != null)
                sectionRoot.SetActive(active);
            else
                gameObject.SetActive(active);
        }

        void OnUpgradeClicked()
        {
            UiRouter.OpenSupermarketOrganizeUpgrade();
        }

        void OnSweepRobotClicked()
        {
            UiRouter.OpenSupermarketOrganizeSweepRobot();
        }

        void OnAttributeUpgraded(SupermarketAttributeId attributeId, int level)
        {
            if (attributeId == SupermarketAttributeId.HandleCapacity)
                RefreshCounts();
        }
    }
}

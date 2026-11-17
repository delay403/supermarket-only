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
        [SerializeField] private Text countdownText;
        [SerializeField] private Sprite runScoreFrameSprite;
        [SerializeField] private Sprite runScoreIconSprite;

        SupermarketOrganizeService _service;
        float _nextSweepUiTick;
        Text _runScoreText;
        GameObject _runScoreRoot;

        void OnEnable()
        {
            ApplyProductModeVisibility();
            if (upgradeButton != null)
                upgradeButton.gameObject.SetActive(false);
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
            RefreshCountdown();

            if (Time.unscaledTime < _nextSweepUiTick)
                return;

            _nextSweepUiTick = Time.unscaledTime + 0.25f;
            RefreshSweepRobotRemaining();
        }

        public void RefreshAll()
        {
            ApplyProductModeVisibility();
            if (upgradeButton != null)
                upgradeButton.gameObject.SetActive(false);
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
            _service.RunScoreChanged += OnRunScoreChanged;
            _service.CountdownChanged += OnCountdownChanged;
            _service.GameEnded += OnGameEnded;
            RefreshCounts();
            RefreshCountdownAndScore();

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
            _service.RunScoreChanged -= OnRunScoreChanged;
            _service.CountdownChanged -= OnCountdownChanged;
            _service.GameEnded -= OnGameEnded;
            _service = null;
        }

        void OnGameStarted()
        {
            RefreshCounts();
            RefreshCountdownAndScore();
        }

        void OnRunScoreChanged(int _) => RefreshRunScore();

        void OnCountdownChanged() => RefreshCountdown();

        void OnGameEnded(SupermarketOrganizeRunEndReason _) => RefreshCountdownAndScore();

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
            RefreshCountdownAndScore();
        }

        void RefreshCountdownAndScore()
        {
            EnsureRunScoreUi();
            RefreshCountdown();
            RefreshRunScore();
        }

        void RefreshCountdown()
        {
            if (countdownText == null)
                return;

            countdownText.raycastTarget = false;
            if (countdownText.transform.parent != null)
                countdownText.transform.parent.gameObject.SetActive(true);
            if (!countdownText.gameObject.activeSelf)
                countdownText.gameObject.SetActive(true);

            var visible = _service != null && (_service.IsGameRunning || _service.IsRunEnding || _service.IsRunSettled);
            countdownText.enabled = visible;
            if (!visible)
                return;

            countdownText.text = FormatDuration(_service.RemainingCountdownSeconds);
        }

        void RefreshRunScore()
        {
            EnsureRunScoreUi();
            if (_runScoreRoot == null)
                return;

            var visible = _service != null && (_service.IsGameRunning || _service.IsRunEnding || _service.IsRunSettled);
            _runScoreRoot.SetActive(visible);
            if (_runScoreText != null)
                _runScoreText.text = $"积分 {(_service != null ? _service.RunScore : 0)}";
        }

        void EnsureRunScoreUi()
        {
            if (_runScoreText != null)
                return;

            var parent = sectionRoot != null ? sectionRoot.transform as RectTransform : transform as RectTransform;
            if (parent == null)
                return;

            _runScoreRoot = new GameObject("RunScore", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rootRt = _runScoreRoot.GetComponent<RectTransform>();
            rootRt.SetParent(parent, false);
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = new Vector2(280f, -87f);
            rootRt.sizeDelta = new Vector2(220f, 48f);

            var frame = _runScoreRoot.GetComponent<Image>();
            frame.raycastTarget = false;
            if (runScoreFrameSprite != null)
            {
                frame.sprite = runScoreFrameSprite;
                frame.preserveAspect = false;
                frame.color = Color.white;
            }
            else
            {
                frame.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);
            }

            if (runScoreIconSprite != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.SetParent(rootRt, false);
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(28f, 0f);
                iconRt.sizeDelta = new Vector2(36f, 36f);
                var icon = iconGo.GetComponent<Image>();
                icon.sprite = runScoreIconSprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            var textGo = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(rootRt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(52f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);
            _runScoreText = textGo.GetComponent<Text>();
            _runScoreText.font = countdownText != null && countdownText.font != null
                ? countdownText.font
                : Resources.Load<Font>("喜脉体");
            _runScoreText.fontSize = 24;
            _runScoreText.alignment = TextAnchor.MiddleLeft;
            _runScoreText.color = new Color(0.95f, 0.96f, 0.9f, 1f);
            _runScoreText.raycastTarget = false;
            _runScoreText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _runScoreText.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = textGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            _runScoreText.text = "积分 0";
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

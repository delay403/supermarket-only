using Game.AnimalHospital.Config;
using Game.Audio;
using Game.Core;
using Game.Save;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.AnimalHospital
{
    /// <summary>模式 HUD：理智、药物、交互进度、道具放回/攻击。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalHudSection : MonoBehaviour
    {
        [SerializeField] private Slider sanityBar;
        [SerializeField] private Image sanityFillImage;
        [SerializeField] private Text sanityText;
        [SerializeField] private Text survivalDayText;
        [SerializeField] private Text heldMedicineText;
        [SerializeField] private Text heldPropText;
        [SerializeField] private Text toastText;
        [SerializeField] private Image holdProgressFill;
        [SerializeField] private Text aimHintText;
        [SerializeField] private AnimalHospitalCrosshairUi crosshairUi;
        [SerializeField] private Button interactHoldButton;
        [SerializeField] private GameObject holdProgressRoot;
        [SerializeField] private Button holsterPropButton;
        [SerializeField] private Button attackPropButton;
        [SerializeField] private Text attackPropButtonLabel;

        [Header("交互反馈 UI（场景中自建并拖入）")]
        [SerializeField] private AnimalHospitalScreenFlashUi screenFlashUi;

        [Header("理智扰动 UI")]
        [Tooltip("全屏扰动层（建议挂 CanvasGroup）。理智低于 75% 时显示，20% 时不透明度=1。")]
        [SerializeField] private CanvasGroup sanityDisturbanceGroup;
        [SerializeField, Range(0.01f, 1f)] private float disturbanceAppearNormalized = 0.75f;
        [SerializeField, Range(0f, 1f)] private float disturbanceFullNormalized = 0.2f;
        [SerializeField, Range(0f, 1f)] private float disturbanceMinAlpha = 0.1f;
        [SerializeField, Range(0f, 1f)] private float disturbanceMaxAlpha = 1f;

        AnimalHospitalService _service;
        AnimalHospitalCrosshairInteractor _interactor;
        AnimalHospitalPropCombatController _combat;
        float _toastTimer;

        public AnimalHospitalScreenFlashUi ScreenFlashUi =>
            screenFlashUi != null ? screenFlashUi : GetComponentInChildren<AnimalHospitalScreenFlashUi>(true);

        void Awake()
        {
            if (survivalDayText == null)
            {
                var dayTf = transform.Find("SurvivalDay") ?? transform.Find("DayText");
                if (dayTf != null)
                    survivalDayText = dayTf.GetComponent<Text>();
            }
        }

        void OnEnable()
        {
            EnsureCrosshair();
            EnsurePropButtons();
            RefreshAll();
            if (interactHoldButton != null)
            {
                var trigger = interactHoldButton.gameObject.GetComponent<AnimalHospitalHoldButtonBridge>()
                              ?? interactHoldButton.gameObject.AddComponent<AnimalHospitalHoldButtonBridge>();
                trigger.Bind(this);
            }

            WirePropButtons();
        }

        void OnDisable()
        {
            Unbind();
            UnwirePropButtons();
        }

        void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f && toastText != null)
                    toastText.text = string.Empty;
            }

            RefreshPropButtonsVisibility();
        }

        public void RefreshAll()
        {
            Unbind();
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.AnimalHospital))
            {
                gameObject.SetActive(false);
                return;
            }

            var director = AnimalHospitalDirector.Instance ?? FindObjectOfType<AnimalHospitalDirector>();
            _service = director?.Service;
            _interactor = FindObjectOfType<AnimalHospitalCrosshairInteractor>();
            _combat = FindObjectOfType<AnimalHospitalPropCombatController>();

            if (_service != null)
            {
                _service.Sanity.Changed += OnSanityChanged;
                _service.HeldMedicineChanged += OnMedicineChanged;
                _service.Props.HeldPropChanged += OnPropChanged;
                _service.GameStarted += OnGameStarted;
                OnSanityChanged(_service.Sanity.Current, _service.Sanity.Max);
                OnMedicineChanged(_service.HeldMedicineId);
                OnPropChanged(_service.Props.HeldProp);
                RefreshSurvivalDay();
            }
            else
            {
                RefreshSurvivalDay();
            }

            if (_interactor != null)
            {
                _interactor.HoldProgressChanged += OnHoldProgress;
                _interactor.AimChanged += OnAimChanged;
                _interactor.AimPatientChanged += OnAimPatientChanged;
                RefreshCrosshair();
            }

            RefreshPropButtonsVisibility();
        }

        public void ShowToast(string message)
        {
            if (toastText == null)
                return;
            toastText.text = message ?? string.Empty;
            _toastTimer = 2.5f;
        }

        public void SetHudHolding(bool holding)
        {
            _interactor?.SetHudHolding(holding);
            RefreshCrosshair();
        }

        void Unbind()
        {
            if (_service != null)
            {
                _service.Sanity.Changed -= OnSanityChanged;
                _service.HeldMedicineChanged -= OnMedicineChanged;
                _service.Props.HeldPropChanged -= OnPropChanged;
                _service.GameStarted -= OnGameStarted;
                _service = null;
            }

            if (_interactor != null)
            {
                _interactor.HoldProgressChanged -= OnHoldProgress;
                _interactor.AimChanged -= OnAimChanged;
                _interactor.AimPatientChanged -= OnAimPatientChanged;
                _interactor = null;
            }

            _combat = null;
        }

        void OnGameStarted() => RefreshSurvivalDay();

        /// <summary>刷新 HUD 上的存活天数（开局、结算换班、死亡重置后调用）。</summary>
        public void RefreshSurvivalDay()
        {
            if (survivalDayText == null)
                return;

            var day = _service != null
                ? _service.CurrentSurvivalDay
                : Mathf.Max(1, SaveService.Data?.animalHospital?.currentDay ?? 1);
            survivalDayText.text = $"第{day}天";
        }

        void OnSanityChanged(float current, float max)
        {
            if (sanityBar != null)
            {
                sanityBar.maxValue = max;
                sanityBar.value = current;
            }

            if (sanityFillImage != null)
            {
                var ratio = max > 0.0001f ? Mathf.Clamp01(current / max) : 0f;
                sanityFillImage.fillAmount = ratio;
            }

            if (sanityText != null)
                sanityText.text = $"理智 {Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";

            RefreshSanityDisturbance(max > 0.0001f ? Mathf.Clamp01(current / max) : 0f);
        }

        void RefreshSanityDisturbance(float normalized)
        {
            if (sanityDisturbanceGroup == null)
                return;

            var appear = Mathf.Clamp01(disturbanceAppearNormalized);
            var full = Mathf.Clamp01(disturbanceFullNormalized);
            if (full > appear)
                full = appear;

            float alpha;
            if (normalized >= appear)
            {
                alpha = 0f;
            }
            else if (normalized <= full)
            {
                alpha = disturbanceMaxAlpha;
            }
            else
            {
                // appear(75%) → minAlpha(0.1)；full(20%) → maxAlpha(1)
                var t = Mathf.InverseLerp(appear, full, normalized);
                alpha = Mathf.Lerp(disturbanceMinAlpha, disturbanceMaxAlpha, t);
            }

            var visible = alpha > 0.001f;
            sanityDisturbanceGroup.alpha = alpha;
            sanityDisturbanceGroup.blocksRaycasts = false;
            sanityDisturbanceGroup.interactable = false;
            if (sanityDisturbanceGroup.gameObject.activeSelf != visible)
                sanityDisturbanceGroup.gameObject.SetActive(visible);
        }

        void OnMedicineChanged(string medicineId)
        {
            if (heldMedicineText == null)
                return;

            if (string.IsNullOrEmpty(medicineId))
            {
                heldMedicineText.text = "药物：无";
                return;
            }

            var name = medicineId;
            if (_service != null)
            {
                var med = _service.FindMedicineDef(medicineId);
                if (med != null && !string.IsNullOrWhiteSpace(med.displayName))
                    name = med.displayName;
            }

            heldMedicineText.text = $"药物：{name}";
        }

        void OnPropChanged(AnimalHospitalPropId propId)
        {
            if (heldPropText != null)
                heldPropText.text = propId == AnimalHospitalPropId.None ? "道具：无" : $"道具：{propId}";

            RefreshPropButtonsVisibility();
            RefreshAttackLabel();
        }

        void OnAimPatientChanged(AnimalHospitalPatient _) => RefreshPropButtonsVisibility();

        void OnHoldProgress(AnimalHospitalInteractable target, float current, float required)
        {
            var show = target != null && required > 0f && current > 0f;
            if (holdProgressRoot != null)
                holdProgressRoot.SetActive(show);
            if (holdProgressFill != null)
                holdProgressFill.fillAmount = required > 0f ? Mathf.Clamp01(current / required) : 0f;

            RefreshCrosshair();
        }

        void OnAimChanged(AnimalHospitalInteractable target)
        {
            if (aimHintText != null)
            {
                if (target is AnimalHospitalPropInteractable propStation
                    && TryBuildPropUnlockHint(propStation, out var unlockHint))
                {
                    aimHintText.text = unlockHint;
                }
                else
                {
                    aimHintText.text = target != null ? $"按住交互：{target.DisplayName}" : string.Empty;
                }
            }

            RefreshCrosshair();
            RefreshPropButtonsVisibility();
        }

        bool TryBuildPropUnlockHint(AnimalHospitalPropInteractable propStation, out string hint)
        {
            hint = null;
            if (propStation == null || propStation.PropId == AnimalHospitalPropId.StunBaton)
                return false;

            var save = SaveService.Data?.animalHospital;
            if (save == null || save.IsPropUnlocked(propStation.PropId))
                return false;

            var config = _service?.Config ?? AnimalHospitalConfig.ResolveRuntime();
            var cost = config != null ? config.ResolvePropUnlockCost(propStation.PropId) : 0;
            hint = $"解锁需要 {cost} 金币";
            return true;
        }

        void RefreshCrosshair()
        {
            crosshairUi?.ApplyAimTarget(
                _interactor != null ? _interactor.AimTarget : null,
                _interactor != null && _interactor.IsHolding);
        }

        void EnsureCrosshair()
        {
            if (crosshairUi == null)
                crosshairUi = GetComponentInChildren<AnimalHospitalCrosshairUi>(true);

            if (crosshairUi != null)
            {
                crosshairUi.EnsureDefaultCrosshair();
                return;
            }

            crosshairUi = AnimalHospitalCrosshairUi.CreateCrosshair(transform);
        }

        void EnsurePropButtons()
        {
            if (holsterPropButton == null)
                holsterPropButton = CreateRuntimeButton("HolsterPropButton", new Vector2(0.78f, 0.22f), new Vector2(0.95f, 0.30f), "放回", new Color(0.35f, 0.35f, 0.4f, 0.92f));

            if (attackPropButton == null)
                attackPropButton = CreateRuntimeButton("AttackPropButton", new Vector2(0.78f, 0.32f), new Vector2(0.95f, 0.42f), "攻击", new Color(0.85f, 0.25f, 0.2f, 0.92f));

            if (attackPropButtonLabel == null && attackPropButton != null)
                attackPropButtonLabel = attackPropButton.GetComponentInChildren<Text>();
        }

        Button CreateRuntimeButton(string name, Vector2 anchorMin, Vector2 anchorMax, string label, Color color)
        {
            var existing = transform.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 22;
            text.raycastTarget = false;
            return btn;
        }

        void WirePropButtons()
        {
            if (holsterPropButton != null)
            {
                holsterPropButton.onClick.RemoveListener(OnHolsterClicked);
                holsterPropButton.onClick.AddListener(OnHolsterClicked);
            }

            if (attackPropButton != null)
            {
                var bridge = attackPropButton.gameObject.GetComponent<AnimalHospitalAttackButtonBridge>()
                             ?? attackPropButton.gameObject.AddComponent<AnimalHospitalAttackButtonBridge>();
                bridge.Bind(this);
            }
        }

        void UnwirePropButtons()
        {
            if (holsterPropButton != null)
                holsterPropButton.onClick.RemoveListener(OnHolsterClicked);

            var bridge = attackPropButton != null
                ? attackPropButton.GetComponent<AnimalHospitalAttackButtonBridge>()
                : null;
            bridge?.Bind(null);
        }

        void OnHolsterClicked() => _combat?.HolsterHeldProp();

        public void SetAttackHolding(bool holding) => _combat?.SetAttackHeld(holding);

        void RefreshPropButtonsVisibility()
        {
            if (_combat == null)
                _combat = FindObjectOfType<AnimalHospitalPropCombatController>();

            if (holsterPropButton != null)
                holsterPropButton.gameObject.SetActive(_combat != null && _combat.CanShowHolsterButton);

            if (attackPropButton != null)
            {
                var show = _combat != null && _combat.CanShowAttackButton;
                // 按钮因准星离开而隐藏时，PointerUp 不会触发，必须主动松开攻击。
                if (!show && attackPropButton.gameObject.activeSelf)
                    SetAttackHolding(false);
                attackPropButton.gameObject.SetActive(show);
            }

            RefreshAttackLabel();
        }

        void RefreshAttackLabel()
        {
            if (attackPropButtonLabel == null)
                return;

            if (_combat == null || _service == null || _service.Props.HeldProp == AnimalHospitalPropId.None)
            {
                attackPropButtonLabel.text = "攻击";
                return;
            }

            attackPropButtonLabel.text = _combat.IsTapAttackProp ? "攻击" : "按住攻击";
        }
    }

    /// <summary>攻击按钮：点按/按住均通过 Pointer 事件转发。</summary>
    public sealed class AnimalHospitalAttackButtonBridge : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        AnimalHospitalHudSection _hud;

        public void Bind(AnimalHospitalHudSection hud) => _hud = hud;

        public void OnPointerDown(PointerEventData eventData) => _hud?.SetAttackHolding(true);

        public void OnPointerUp(PointerEventData eventData) => _hud?.SetAttackHolding(false);

        public void OnPointerExit(PointerEventData eventData) => _hud?.SetAttackHolding(false);

        void OnDisable() => _hud?.SetAttackHolding(false);
    }
}

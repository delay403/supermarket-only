using Game.Core;
using Game.Player;
using Game.AnimalHospital.Config;
using Game.Save;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>场景级导演：装配服务、绑定区域、开局日程。</summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalDirector : MonoBehaviour
    {
        public static AnimalHospitalDirector Instance { get; private set; }

        [SerializeField] private AnimalHospitalConfig config;
        [SerializeField] private AnimalHospitalAudio audio;
        [SerializeField] private AnimalHospitalWaitingZone waitingZone;
        [SerializeField] private AnimalHospitalReceptionZone receptionZone;
        [SerializeField] private AnimalHospitalWard[] wards;
        [SerializeField] private AnimalHospitalCorridorZone corridorZone;
        [SerializeField] private AnimalHospitalHospitalExit hospitalExit;
        [SerializeField] private AnimalHospitalMedicineCabinet medicineCabinet;
        [Header("Debug")]
        [SerializeField] private bool aiDebugLogging = true;

        AnimalHospitalService _service;
        AnimalHospitalHighlightController _highlights;
        AnimalHospitalCrosshairInteractor _crosshair;
        AnimalHospitalFirstPersonCamera _fpCamera;
        bool _gameplayInitialized;

        public AnimalHospitalService Service => _service;
        public AnimalHospitalConfig Config => config;
        public bool GameplayInitialized => _gameplayInitialized;

        void Awake()
        {
            Instance = this;
            config ??= AnimalHospitalConfig.ResolveRuntime();
            AnimalHospitalAiDebug.Enabled = aiDebugLogging;
            EnsureAudio();
            Game.Audio.AudioManager.EnsureCreated();
        }

        void EnsureAudio()
        {
            if (audio == null)
                audio = GetComponent<AnimalHospitalAudio>();
            if (audio == null)
                audio = gameObject.AddComponent<AnimalHospitalAudio>();
        }

        void OnValidate()
        {
            AnimalHospitalAiDebug.Enabled = aiDebugLogging;
        }

        void Start()
        {
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.AnimalHospital))
                return;

            BuildRuntimeGraph();
            BeginGameplay();
        }

        void Update()
        {
            if (_service != null && _service.IsGameRunning)
                _service.Tick(Time.deltaTime);
        }

        void OnDestroy()
        {
            if (_service != null)
            {
                _service.DaySettlementRequested -= OnDaySettlementRequested;
                _service.ToastRequested -= OnToast;
            }

            if (Instance == this)
                Instance = null;
        }

        public void BuildRuntimeGraph()
        {
            var motor = FindObjectOfType<PlayerMotor>();
            if (motor != null)
                AnimalHospitalPlayerSetup.ConfigurePlayer(motor.gameObject);

            _highlights = motor != null
                ? motor.GetComponent<AnimalHospitalHighlightController>()
                : FindObjectOfType<AnimalHospitalHighlightController>();
            _crosshair = motor != null
                ? motor.GetComponent<AnimalHospitalCrosshairInteractor>()
                : FindObjectOfType<AnimalHospitalCrosshairInteractor>();
            _fpCamera = FindObjectOfType<AnimalHospitalFirstPersonCamera>();

            var save = SaveService.Data?.animalHospital ?? new AnimalHospitalModeSaveData();
            save.MigrateDefaultsIfNeeded();

            _service ??= new AnimalHospitalService(config, _highlights, save);
            _service.BindScene(
                waitingZone,
                receptionZone,
                wards,
                corridorZone,
                hospitalExit,
                medicineCabinet,
                motor != null ? motor.transform : null,
                _fpCamera);

            if (_highlights != null && config != null)
            {
                config.EnsureDefaultInteractableMask();
                _highlights.ApplyColors(
                    config.aimHighlightColor,
                    config.guideHighlightColor,
                    config.aimOutlineColor,
                    config.aimOutlineWidth,
                    config.guideOutlineColor,
                    config.guideOutlineWidth);
            }

            if (_crosshair != null)
            {
                var cam = _fpCamera != null ? _fpCamera.GetComponent<Camera>() : Camera.main;
                _crosshair.Configure(cam, _service, _highlights, config);
            }

            if (motor != null)
            {
                var cam = _fpCamera != null ? _fpCamera.GetComponent<Camera>() : Camera.main;
                var taserBeam = motor.GetComponent<AnimalHospitalTaserBeamController>();
                if (taserBeam != null)
                    taserBeam.Configure(_service, config, cam);

                var propHand = motor.GetComponent<AnimalHospitalPropHandPresenter>();
                if (propHand != null)
                    propHand.Configure(_service, config, cam);

                var propCombat = motor.GetComponent<AnimalHospitalPropCombatController>();
                if (propCombat == null)
                    propCombat = motor.gameObject.AddComponent<AnimalHospitalPropCombatController>();
                propCombat.Configure(_service, config, _crosshair, propHand, cam);
            }

            WireStationKinds();
        }

        public void BeginGameplay()
        {
            if (_gameplayInitialized)
                return;

            BuildRuntimeGraph();
            _gameplayInitialized = true;
            SetGameplayHudVisible(true);

            if (_service != null)
            {
                _service.DaySettlementRequested -= OnDaySettlementRequested;
                _service.DaySettlementRequested += OnDaySettlementRequested;
                _service.SanityDepleted -= OnSanityDepleted;
                _service.SanityDepleted += OnSanityDepleted;
                _service.ToastRequested -= OnToast;
                _service.ToastRequested += OnToast;
                _service.StartDay();
            }
        }

        /// <summary>结算完成后开始新的一天（不重置存活天数，由结算面板已 +1）。</summary>
        public void ContinueNextDay()
        {
            BuildRuntimeGraph();
            _gameplayInitialized = true;
            SetGameplayHudVisible(true);
            UiCoordinator.Close(UiPanelId.AnimalHospitalResult);
            UiCoordinator.Close(UiPanelId.AnimalHospitalDeath);

            if (_service == null)
                return;

            _service.DaySettlementRequested -= OnDaySettlementRequested;
            _service.DaySettlementRequested += OnDaySettlementRequested;
            _service.SanityDepleted -= OnSanityDepleted;
            _service.SanityDepleted += OnSanityDepleted;
            _service.ToastRequested -= OnToast;
            _service.ToastRequested += OnToast;
            _service.StartDay();
        }

        /// <summary>死亡面板「重新开始」：存活天数已重置为 1。</summary>
        public void RestartAfterDeath()
        {
            _service?.StopDay();
            ContinueNextDay();
        }

        /// <summary>死亡面板恢复理智后继续本日。</summary>
        public void RecoverSanityFromDeathOffer()
        {
            if (_service == null)
                return;

            var amount = config != null ? config.deathSanityRestore : 50f;
            _service.Sanity.Restore(amount);
            _service.ResumeAfterSanityRecover();
            SetGameplayHudVisible(true);
        }

        void OnDaySettlementRequested()
        {
            _service?.StopDay();
            SetGameplayHudVisible(false);
            UiRouter.OpenAnimalHospitalResult(_service?.BuildDayResultModel());
        }

        void OnSanityDepleted()
        {
            SetGameplayHudVisible(false);
            UiRouter.OpenAnimalHospitalDeath();
        }

        void OnToast(string message)
        {
            var hud = FindObjectOfType<AnimalHospitalHudSection>(true);
            hud?.ShowToast(message);
        }

        void WireStationKinds()
        {
            if (receptionZone != null)
            {
                ConfigureAndDisable(receptionZone.Report, AnimalHospitalInteractableKind.Report, "report", "报告单");
                ConfigureAndDisable(receptionZone.CameraStation, AnimalHospitalInteractableKind.Camera, "camera", "相机");
                ConfigureAndDisable(receptionZone.Computer, AnimalHospitalInteractableKind.Computer, "computer", "电脑");
                ConfigureAndDisable(receptionZone.Printer, AnimalHospitalInteractableKind.Printer, "printer", "打印机");
                receptionZone.Curtain?.Configure(AnimalHospitalInteractableKind.Curtain, "curtain", "窗帘");
                receptionZone.Curtain?.SetInteractable(true);
            }

            if (wards == null)
                return;

            for (var i = 0; i < wards.Length; i++)
            {
                var ward = wards[i];
                if (ward == null)
                    continue;
                ConfigureAndDisable(ward.Analyzer, AnimalHospitalInteractableKind.Analyzer, $"analyzer_{ward.WardId}", "分析机");
                ConfigureAndDisable(ward.WardComputer, AnimalHospitalInteractableKind.WardComputer, $"ward_pc_{ward.WardId}", "病房电脑");
                ConfigureAndDisable(ward.WardDisplay, AnimalHospitalInteractableKind.WardDisplay, $"ward_display_{ward.WardId}", "病房显示屏");
            }
        }

        static void ConfigureAndDisable(
            AnimalHospitalInteractable target,
            AnimalHospitalInteractableKind kind,
            string id,
            string displayName)
        {
            if (target == null)
                return;
            target.Configure(kind, id, displayName);
            target.SetInteractable(false);
        }

        static void SetGameplayHudVisible(bool visible)
        {
            var hud = FindObjectOfType<AnimalHospitalHudSection>(true);
            if (hud == null)
                return;

            hud.gameObject.SetActive(visible);
            if (visible)
                hud.RefreshSurvivalDay();
        }
    }
}

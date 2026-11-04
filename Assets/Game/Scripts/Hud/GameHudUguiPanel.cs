using System;
using System.Collections;
using FairyGUI;
using Game.Reward;
using Game.Collectibles.Stats;
using Game.Economy;
using Game.Audio;
using Game.Core;
using Game.Core.Config;
using Game.Features.StrengthTraining;
using Game.Fishing;
using Game.Glide;
using Game.Interaction;
using Game.Maps;
using Game.Player;
using Game.Save;
using Game.Tower;
using Game.UI;
using Game.UI.Effects;
using Game.UI.Fishing;
using Game.UI.Framework;
using Game.UI.MainHome;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hud
{
    /// <summary>
    /// 游戏内主 HUD（UGUI）：爬塔与钓鱼共用顶栏/侧栏；按 <see cref="GameProductSettings"/> 切换模式区块。
    /// 进入游戏场景后应禁用 FGUI <c>GameWnd</c>，仅保留 MainHome 使用 FGUI。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHudUguiPanel : MonoBehaviour
    {
        [Header("FGUI 抑制（可选）")]
        [Tooltip("场景中 Game 包 GameWnd 的 UIPanel；绑定后进入游戏会隐藏。")]
        [SerializeField] private UIPanel suppressFguiGameWnd;

        [Header("共享")]
        [SerializeField] private Button btnSetting;
        [SerializeField] private Text coinsText;
        [SerializeField] private RectTransform coinFlyTarget;
        [SerializeField] private Text gemsText;
        [SerializeField] private Button coinsPlusButton;
        [SerializeField] private Button gemsPlusButton;
        [SerializeField] private Button btnDoubleCoins;
        [SerializeField] private Text doubleCoinsTimerText;
        [SerializeField] private Button btnSignIn;
        [SerializeField] private Button btnLuckyWheel;
        [SerializeField] private Button btnOnlineReward;
        [SerializeField] private Button routerPetBackpack;
        [SerializeField] private Button routerPetHatch;
        [SerializeField] private Button routerCharacter;
        [SerializeField] private Button routerTrail;
        [SerializeField] private Button routerWing;
        [SerializeField] private Button btnJump;
        [SerializeField] private Button btnInteract;
        [SerializeField] private UnityEngine.UI.Image btnInteractIcon;

        [Header("爬塔专用")]
        [SerializeField] private GameObject towerSectionRoot;
        [SerializeField] private Button btnMapSelect;
        [SerializeField] private Text mapTitleText;
        [SerializeField] private Text medalsText;
        [SerializeField] private GameObject medalsRoot;
        [SerializeField] private Text heightText;
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private Text staminaLabel;
        [SerializeField] private Button btnDoubleSpeed;
        [SerializeField] private Text doubleSpeedTimerText;
        [SerializeField] private Button btnAutoClimb;
        [SerializeField] private Text autoClimbLabel;
        [SerializeField] private GameObject autoClimbCheckmark;

        [Header("躲避虫子专用")]
        [SerializeField] private GameObject wormDodgeStaminaRoot;
        [SerializeField] private Text wormDodgeStaminaAmountText;
        [SerializeField] private Text wormDodgeStaminaRegenText;
        [SerializeField] private Button btnWormDodgeStamina;
        [Tooltip("加体力按钮文案；留空则从 btnWormDodgeStamina 子节点 Text 自动查找。")]
        [SerializeField] private Text wormDodgeStaminaLabel;

        [Header("钓鱼专用")]
        [SerializeField] private GameObject fishingSectionRoot;
        [SerializeField] private Text fishingZoneLabel;
        [SerializeField] private Text strengthText;
        [SerializeField] private Button btnStrength;
        [Tooltip("加力量按钮文案；留空则从 btnStrength 子节点 Text 自动查找。")]
        [SerializeField] private Text strengthLabel;
        [SerializeField] private Button routerRodShop;
        [SerializeField] private Button routerFishCodex;
        [SerializeField] private Button btnFishingAction;
        [SerializeField] private Text fishingActionLabel;
        [SerializeField] private GameObject fishingActiveRoot;
        [SerializeField] private Text fishingHintText;
        [Tooltip("「点击抽杆」提示根节点；留空则使用 fishingHintText 所在物体。")]
        [SerializeField] private GameObject fishingReelHintRoot;
        [SerializeField] private Slider fishingProgressSlider;
        [SerializeField] private Text fishingProgressText;
        [SerializeField] private Button btnEndFishing;
        [Tooltip("进度条数值追赶速度（进度单位/秒）；0 则按 ProgressMax 在约 0.4 秒内追上。")]
        [SerializeField] private float fishingProgressAnimUnitsPerSecond = 0f;
        [SerializeField] private Button btnTenCatchReward;
        [SerializeField] private Button btnAutoFishing;
        [SerializeField] private Text autoFishingLabel;
        [Tooltip("自动钓鱼按钮右上角奖励角标；未绑定时从 btnAutoFishing 子节点 Image 自动查找。")]
        [SerializeField] private GameObject autoFishingRewardIcon;
        [Tooltip("自动钓鱼通行证剩余时间；未绑定时可留空。")]
        [SerializeField] private GameObject autoFishingTimerRoot;
        [SerializeField] private Text autoFishingTimerText;

        [Header("收杆进度飘字")]
        [SerializeField] private FishingReelProgressFloater reelProgressFloater;
        [SerializeField] private FishingReelProgressPopupView reelProgressPopupPrefab;
        [Tooltip("飘字生成父节点；未指定时用进度条父级。")]
        [SerializeField] private RectTransform reelProgressFloatAnchor;
        [SerializeField] private Vector2 reelFloatOffsetMin = new Vector2(-120f, 20f);
        [SerializeField] private Vector2 reelFloatOffsetMax = new Vector2(120f, 80f);


        [Header("力量增加飘字")]
        [SerializeField] private StrengthGainFloater strengthGainFloater;

        [Header("钓获 Toast")]
        [Tooltip("钓获奖励 Toast 挂载点；留空则使用本 HUD 根 RectTransform。")]
        [SerializeField] private RectTransform catchToastAnchor;

        [SerializeField] private FishingHudInputBridge fishingBridge;
        [SerializeField] private LegacyPlayerInput jumpInput;
        [SerializeField] private FishingStatsAggregator fishingStats;

        ClimbController _climb;
        PlayerFishingController _fishing;
        bool _fishingEventsWired;
        float _nextBuffTick;
        float _nextFishingUiTick;
        float _nextWormDodgeStaminaTick;
        bool _wormDodgeStaminaButtonWired;
        int _lastStrengthValue = -1;
        int _lastStrengthOfferValue = -1;
        RollingNumberText _coinsRolling;
        RollingNumberText _gemsRolling;
        RollingNumberText _strengthRolling;
        float _displayedProgressCurrent;
        float _displayedProgressMax = 1f;
        bool _blockFishingActionUntilCatchToast;
        FishingCatchNotificationPresenter _catchNotificationPresenter;
        PlayerFishingLineDriver _fishingLineDriver;
        bool _catchHudEventsWired;

        public bool HasBindings => coinsText != null || gemsText != null;

        public Button InteractButton => btnInteract;
        public UnityEngine.UI.Image InteractButtonIcon => btnInteractIcon;

        /// <summary>钓获 Toast 的 UGUI 父节点（Screen Space HUD Canvas 下）。</summary>
        public RectTransform CatchToastAnchor
        {
            get
            {
                if (catchToastAnchor != null)
                    return catchToastAnchor;
                return transform as RectTransform;
            }
        }

        void Awake()
        {
            ResolveInteractButton();
            TryResolveWormDodgeStaminaButton();
            CacheRollingTexts();
            ResolveStrengthFloater();
            ConfigureReelHintRaycast();
            WireButtons();
            ApplyProductModeVisibility();
            SuppressFguiIfConfigured();
        }

        void ConfigureReelHintRaycast()
        {
            var hintRoot = ResolveReelHintRoot();
            if (hintRoot == null)
                return;

            foreach (var graphic in hintRoot.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        GameObject ResolveReelHintRoot()
        {
            if (fishingReelHintRoot != null)
                return fishingReelHintRoot;
            return fishingHintText != null ? fishingHintText.gameObject : null;
        }

        void ResolveStrengthFloater()
        {
            if (strengthGainFloater == null)
                strengthGainFloater = GetComponentInChildren<StrengthGainFloater>(true);
        }

        void ResolveStrengthLabel()
        {
            if (strengthLabel == null && btnStrength != null)
                strengthLabel = btnStrength.GetComponentInChildren<Text>(true);
        }

        void TryResolveWormDodgeStaminaButton()
        {
            if (btnWormDodgeStamina != null)
                return;

            if (wormDodgeStaminaRoot == null)
                return;

            var add = wormDodgeStaminaRoot.transform.Find("Add");
            if (add != null)
                btnWormDodgeStamina = add.GetComponent<Button>();

            if (btnWormDodgeStamina == null)
            {
                foreach (var btn in wormDodgeStaminaRoot.GetComponentsInChildren<Button>(true))
                {
                    if (btn.gameObject == wormDodgeStaminaRoot)
                        continue;

                    btnWormDodgeStamina = btn;
                    break;
                }
            }
        }

        void EnsureWormDodgeStaminaButtonWired()
        {
            TryResolveWormDodgeStaminaButton();
            if (btnWormDodgeStamina == null)
                return;

            var graphic = btnWormDodgeStamina.targetGraphic;
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }
            else
            {
                var image = btnWormDodgeStamina.GetComponent<UnityEngine.UI.Image>();
                if (image != null)
                {
                    image.raycastTarget = true;
                    btnWormDodgeStamina.targetGraphic = image;
                }
            }

            if (_wormDodgeStaminaButtonWired)
                return;

            btnWormDodgeStamina.onClick.AddListener(OnWormDodgeStaminaClicked);
            _wormDodgeStaminaButtonWired = true;
        }

        void ResolveWormDodgeStaminaLabel()
        {
            if (wormDodgeStaminaLabel == null && btnWormDodgeStamina != null)
                wormDodgeStaminaLabel = btnWormDodgeStamina.GetComponentInChildren<Text>(true);
        }

        void ResolveAutoFishingRewardIcon()
        {
            if (autoFishingRewardIcon != null || btnAutoFishing == null)
                return;

            var iconTr = btnAutoFishing.transform.Find("Image");
            if (iconTr != null)
                autoFishingRewardIcon = iconTr.gameObject;
        }

        void CacheRollingTexts()
        {
            _coinsRolling = EnsureRollingNumber(coinsText, _coinsRolling);
            _gemsRolling = EnsureRollingNumber(gemsText, _gemsRolling);
            _strengthRolling = EnsureRollingNumber(strengthText, _strengthRolling);
        }

        static RollingNumberText EnsureRollingNumber(Text text, RollingNumberText existing)
        {
            if (text == null)
                return existing;
            if (existing != null)
                return existing;
            var rolling = text.GetComponent<RollingNumberText>();
            return rolling != null ? rolling : text.gameObject.AddComponent<RollingNumberText>();
        }

        void OnEnable()
        {
            MapRuntime.Changed += OnMapRuntimeChanged;
            StrengthTrainingService.BaseStrengthAdded += OnBaseStrengthAdded;
            AutoFishingService.PassChanged += OnAutoFishingPassChanged;
            AutoFishingService.ModeChanged += OnAutoFishingModeChanged;
            WormDodge.WormDodgeStaminaService.Changed += RefreshWormDodgeStaminaDisplay;
            EnsureFishingRefsResolved();
            WireFishingEvents();
        }

        void OnDisable()
        {
            MapRuntime.Changed -= OnMapRuntimeChanged;
            StrengthTrainingService.BaseStrengthAdded -= OnBaseStrengthAdded;
            AutoFishingService.PassChanged -= OnAutoFishingPassChanged;
            AutoFishingService.ModeChanged -= OnAutoFishingModeChanged;
            WormDodge.WormDodgeStaminaService.Changed -= RefreshWormDodgeStaminaDisplay;
            PauseAutoFishingMode();
            UnwireFishingEvents();
        }

        void OnBaseStrengthAdded(float delta)
        {
            ShowStrengthGain(delta);
            RefreshStrengthDisplay();
        }

        void Start()
        {
            EnsureCoinFlyEffect();
            WorldInteractionPromptController.Instance?.BindInteractButton(btnInteract, btnInteractIcon);
            EnsureFishingRefsResolved();
            WireFishingEvents();
            RefreshEconomyFromSave(immediate: true);
            RefreshMapOrZoneLabel();
            RefreshStrengthDisplay();
            RefreshFishingHud();
            TickBuffDisplays();
            SetClimbOnlyHudVisible(false);
            ApplyProductModeVisibility();
            RefreshWormDodgeStaminaDisplay();
            WormDodgeLeaderboardHud.EnsureInstalledUnder(transform);
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextBuffTick)
            {
                _nextBuffTick = Time.unscaledTime + 0.25f;
                TickBuffDisplays();
            }

            if (GameProductSettings.IsWormDodgeContentEnabled && Time.unscaledTime >= _nextWormDodgeStaminaTick)
            {
                _nextWormDodgeStaminaTick = Time.unscaledTime + 0.5f;
                RefreshWormDodgeStaminaDisplay();
            }

            if (GameProductSettings.IsFishingContentEnabled && Time.unscaledTime >= _nextFishingUiTick)
            {
                _nextFishingUiTick = Time.unscaledTime + 0.15f;
                RefreshStrengthDisplay();
                RefreshFishingHud();
            }

            if (GameProductSettings.IsFishingContentEnabled)
                TickFishingProgressDisplay();
        }

        void ResolveInteractButton()
        {
            if (btnInteractIcon == null && btnInteract != null)
            {
                btnInteractIcon = btnInteract.targetGraphic as UnityEngine.UI.Image;
                if (btnInteractIcon == null)
                    btnInteractIcon = btnInteract.GetComponent<UnityEngine.UI.Image>();
            }

            SetInteractPrompt(null, false);
        }

        Sprite _defaultInteractIcon;

        void CacheDefaultInteractIcon()
        {
            if (_defaultInteractIcon == null && btnInteractIcon != null)
                _defaultInteractIcon = btnInteractIcon.sprite;
        }

        public void SetInteractPrompt(Sprite icon, bool visible)
        {
            if (btnInteractIcon == null && btnInteract != null)
            {
                btnInteractIcon = btnInteract.targetGraphic as UnityEngine.UI.Image;
                if (btnInteractIcon == null)
                    btnInteractIcon = btnInteract.GetComponent<UnityEngine.UI.Image>();
            }

            CacheDefaultInteractIcon();

            if (btnInteract != null)
                btnInteract.gameObject.SetActive(visible);

            if (btnInteractIcon == null)
                return;

            if (visible && icon != null)
                btnInteractIcon.sprite = icon;
            else if (!visible && _defaultInteractIcon != null)
                btnInteractIcon.sprite = _defaultInteractIcon;
        }

        public void SetInteractButtonVisible(bool visible) => SetInteractPrompt(null, visible);

        void EnsureCoinFlyEffect()
        {
            var target = coinFlyTarget;
            if (target == null)
            {
                var icon = transform.Find("TopLeft/Coin/Icon");
                if (icon != null)
                    target = icon as RectTransform;
            }

            if (target == null)
                return;

            var effect = UICoinFlyEffect.Instance;
            if (effect == null)
                effect = GetComponent<UICoinFlyEffect>();
            if (effect == null)
                effect = gameObject.AddComponent<UICoinFlyEffect>();

            effect.RegisterDefaultTarget(target, priority: 10);
        }

        void WireButtons()
        {
            btnSetting?.onClick.AddListener(OnSettingsClicked);
            coinsPlusButton?.onClick.AddListener(OnEconomyMoneyClicked);
            gemsPlusButton?.onClick.AddListener(OnEconomyDiamondClicked);
            btnDoubleCoins?.onClick.AddListener(OnMoneyBuffClicked);
            btnSignIn?.onClick.AddListener(OnSignInClicked);
            btnLuckyWheel?.onClick.AddListener(OnLuckyWheelClicked);
            btnOnlineReward?.onClick.AddListener(OnOnlineRewardClicked);
            routerWing?.onClick.AddListener(OnWingShopClicked);
            routerTrail?.onClick.AddListener(OnTrailShopClicked);
            routerPetBackpack?.onClick.AddListener(OnPetBackpackClicked);
            routerPetHatch?.onClick.AddListener(OnPetHatchClicked);
            routerCharacter?.onClick.AddListener(OnCharacterShopClicked);
            btnJump?.onClick.AddListener(OnJumpClicked);

            btnMapSelect?.onClick.AddListener(OnMapSelectClicked);
            btnDoubleSpeed?.onClick.AddListener(OnSpeedBuffClicked);
            btnAutoClimb?.onClick.AddListener(OnAutoClimbClicked);

            btnStrength?.onClick.AddListener(OnStrengthClicked);
            routerRodShop?.onClick.AddListener(OnRodShopClicked);
            routerFishCodex?.onClick.AddListener(OnFishCodexClicked);
            btnFishingAction?.onClick.AddListener(OnFishingActionClicked);
            btnEndFishing?.onClick.AddListener(OnEndFishingClicked);
            btnTenCatchReward?.onClick.AddListener(OnTenCatchRewardClicked);
            btnAutoFishing?.onClick.AddListener(OnAutoFishingClicked);
            EnsureWormDodgeStaminaButtonWired();
        }

        /// <summary>仅在引用为空时查找一次，禁止在 Update/刷新 HUD 中重复调用。</summary>
        void EnsureFishingRefsResolved()
        {
            if (jumpInput == null)
                jumpInput = GetComponentInParent<LegacyPlayerInput>()
                            ?? UnityObjectFinder.FindFirst<LegacyPlayerInput>();

            if (_fishing == null)
                _fishing = GetComponentInParent<PlayerFishingController>()
                           ?? UnityObjectFinder.FindFirst<PlayerFishingController>();

            if (fishingBridge == null && _fishing != null)
            {
                fishingBridge = _fishing.GetComponent<FishingHudInputBridge>();
                if (fishingBridge == null && GameplayModuleContext.IsModuleActive(GameplayModule.Fishing))
                    fishingBridge = _fishing.gameObject.AddComponent<FishingHudInputBridge>();
            }

            if (fishingBridge == null)
                fishingBridge = GetComponent<FishingHudInputBridge>()
                                ?? UnityObjectFinder.FindFirst<FishingHudInputBridge>();

            if (fishingBridge != null && _fishing == null)
                _fishing = fishingBridge.Fishing;

            if (fishingStats == null && _fishing != null)
                fishingStats = _fishing.GetComponent<FishingStatsAggregator>();
        }

        void WireFishingEvents()
        {
            if (_fishing == null || _fishingEventsWired)
                return;

            _fishing.SessionStarted += OnFishingSessionStarted;
            _fishing.GameplayStarted += OnFishingGameplayStarted;
            _fishing.SessionCancelled += OnFishingSessionCancelled;
            _fishing.ReelApplied += OnReelApplied;
            _fishing.CatchCompleted += OnCatchCompleted;
            _fishingEventsWired = true;
            WireCatchPresentationHudEvents();
        }

        void UnwireFishingEvents()
        {
            UnwireCatchPresentationHudEvents();

            if (_fishing == null)
            {
                _fishingEventsWired = false;
                return;
            }

            _fishing.SessionStarted -= OnFishingSessionStarted;
            _fishing.GameplayStarted -= OnFishingGameplayStarted;
            _fishing.SessionCancelled -= OnFishingSessionCancelled;
            _fishing.ReelApplied -= OnReelApplied;
            _fishing.CatchCompleted -= OnCatchCompleted;
            _fishingEventsWired = false;
        }

        void WireCatchPresentationHudEvents()
        {
            if (_catchHudEventsWired || _fishing == null)
                return;

            _catchNotificationPresenter = _fishing.GetComponent<FishingCatchNotificationPresenter>();
            if (_catchNotificationPresenter != null)
                _catchNotificationPresenter.CatchRewardToastDisplayed += OnCatchRewardToastDisplayed;

            _fishingLineDriver = _fishing.GetComponent<PlayerFishingLineDriver>();
            if (_fishingLineDriver != null)
                _fishingLineDriver.PostCatchPresentationEnded += OnPostCatchPresentationEndedForHud;

            _catchHudEventsWired = true;
        }

        void UnwireCatchPresentationHudEvents()
        {
            if (_catchNotificationPresenter != null)
                _catchNotificationPresenter.CatchRewardToastDisplayed -= OnCatchRewardToastDisplayed;

            if (_fishingLineDriver != null)
                _fishingLineDriver.PostCatchPresentationEnded -= OnPostCatchPresentationEndedForHud;

            _catchNotificationPresenter = null;
            _fishingLineDriver = null;
            _catchHudEventsWired = false;
        }

        void OnCatchRewardToastDisplayed()
        {
            _blockFishingActionUntilCatchToast = false;
            RefreshFishingHud();
        }

        void OnPostCatchPresentationEndedForHud(bool naturalComplete)
        {
            if (naturalComplete)
                return;

            _blockFishingActionUntilCatchToast = false;
            RefreshFishingHud();
        }

        void ResolveClimbRefOnce()
        {
            if (_climb == null)
                _climb = UnityObjectFinder.FindFirst<ClimbController>();
        }

        public void ApplyProductModeVisibility()
        {
            var fusion = GameplayModuleContext.IsFusionProduct();
            var active = GameplayModuleContext.ActiveModule;
            var hud = GameModeRuntimeConfig.ResolveHudVisibility();

            var tower = fusion ? active == GameplayModule.Tower : GameProductSettings.IsTowerContentEnabled;
            var fish = fusion ? active == GameplayModule.Fishing : GameProductSettings.IsFishingContentEnabled;
            var destroyCity = fusion ? active == GameplayModule.DestroyCity : GameProductSettings.IsDestroyCityContentEnabled;
            var wormDodge = fusion ? active == GameplayModule.WormDodge : GameProductSettings.IsWormDodgeContentEnabled;
            var supermarket = fusion ? active == GameplayModule.SupermarketOrganize : GameProductSettings.IsSupermarketOrganizeContentEnabled;

            if (towerSectionRoot != null) towerSectionRoot.SetActive(tower);
            if (fishingSectionRoot != null) fishingSectionRoot.SetActive(fish);
            if (medalsRoot != null) medalsRoot.SetActive(tower);
            if (btnMapSelect != null) btnMapSelect.gameObject.SetActive(tower);
            if (btnDoubleSpeed != null) btnDoubleSpeed.gameObject.SetActive(tower);
            if (btnDoubleCoins != null) btnDoubleCoins.gameObject.SetActive(tower);

            SetHudButtonVisible(btnSignIn, hud.signIn);
            SetHudButtonVisible(btnLuckyWheel, hud.luckyWheel);
            SetHudButtonVisible(btnOnlineReward, hud.onlineReward);
            SetHudButtonVisible(routerPetBackpack, hud.petBackpack);
            SetHudButtonVisible(routerPetHatch, hud.petHatch);
            SetHudButtonVisible(routerCharacter, hud.character);
            SetHudButtonVisible(routerTrail, hud.trail);
            SetHudButtonVisible(routerWing, hud.wing);
            SetHudButtonVisible(routerRodShop, hud.rodShop);
            SetHudButtonVisible(routerFishCodex, hud.fishCodex);

            var destroyHud = GetComponentInChildren<DestroyCity.DestroyCityHudSection>(true);
            if (destroyHud != null)
                destroyHud.gameObject.SetActive(destroyCity);

            var wormHud = GetComponentInChildren<WormDodge.WormDodgeHudSection>(true);
            if (wormHud != null)
                wormHud.gameObject.SetActive(wormDodge);

            var supermarketHud = GetComponentInChildren<SupermarketOrganize.SupermarketOrganizeHudSection>(true);
            if (supermarketHud != null)
                supermarketHud.gameObject.SetActive(supermarket);

            var supermarketSkillHud = UnityEngine.Object.FindObjectOfType<SupermarketOrganize.SupermarketOrganizeSkillHud>(true);
            if (supermarketSkillHud != null)
                supermarketSkillHud.gameObject.SetActive(supermarket);

            if (wormDodgeStaminaRoot != null)
                wormDodgeStaminaRoot.SetActive(wormDodge);

            if (btnWormDodgeStamina != null)
                btnWormDodgeStamina.gameObject.SetActive(wormDodge);

            GameplayHudSection.RefreshAllInScene();

            if (wormDodge)
                RefreshWormDodgeStaminaDisplay();
        }

        static void SetHudButtonVisible(Button button, bool visible)
        {
            if (button != null)
                button.gameObject.SetActive(visible);
        }

        public void RefreshWormDodgeStaminaDisplay()
        {
            if (!GameProductSettings.IsWormDodgeContentEnabled)
                return;

            TryResolveWormDodgeStaminaButton();
            EnsureWormDodgeStaminaButtonWired();
            WormDodge.WormDodgeStaminaService.RefreshRegen();
            if (wormDodgeStaminaAmountText != null)
                wormDodgeStaminaAmountText.text =
                    $"{WormDodge.WormDodgeStaminaService.Current}/{WormDodge.WormDodgeStaminaService.MaxStamina}";

            if (wormDodgeStaminaRegenText != null)
            {
                if (WormDodge.WormDodgeStaminaService.Current >= WormDodge.WormDodgeStaminaService.MaxStamina)
                    wormDodgeStaminaRegenText.text = "体力已满";
                else
                {
                    var wait = WormDodge.WormDodgeStaminaService.TimeUntilNextRegen;
                    wormDodgeStaminaRegenText.text = $"{wait.Minutes:D2}:{wait.Seconds:D2}";
                }
            }

            RefreshWormDodgeStaminaButton();
        }

        void RefreshWormDodgeStaminaButton()
        {
            ResolveWormDodgeStaminaLabel();
            var full = WormDodge.WormDodgeStaminaService.IsFull;
            if (btnWormDodgeStamina != null)
                btnWormDodgeStamina.interactable = !full;

            if (wormDodgeStaminaLabel != null)
                wormDodgeStaminaLabel.text = $"体力+{WormDodge.WormDodgeStaminaService.FreeStaminaReward}";
        }

        void SuppressFguiIfConfigured()
        {
            var panel = suppressFguiGameWnd != null ? suppressFguiGameWnd : HudController.ResolveDefaultGamePanel();
            if (panel == null)
                return;
            panel.gameObject.SetActive(false);
        }

        static void OnMapRuntimeChanged() => UiCoordinator.Hud?.RefreshEconomyDisplay();

        public void SetEconomyDisplay(long coins, long gems, long medals, bool immediate = false)
        {
            CacheRollingTexts();
            if (_coinsRolling != null)
                _coinsRolling.AnimateTo(coins, immediate);
            else if (coinsText != null)
                coinsText.text = EconomyDisplayFormat.Format(coins);

            if (_gemsRolling != null)
                _gemsRolling.AnimateTo(gems, immediate);
            else if (gemsText != null)
                gemsText.text = EconomyDisplayFormat.Format(gems);

            if (medalsText != null)
                medalsText.text = EconomyDisplayFormat.Format(medals);
            RefreshMapOrZoneLabel();
        }

        public void RefreshEconomyFromSave(bool immediate = false)
        {
            var d = SaveService.Data;
            if (d == null) return;
            SetEconomyDisplay(d.coins, d.gems, d.medals, immediate);
        }

        /// <summary>金币飞入逐枚到账时刷新顶栏（不重复写存档）。</summary>
        public void AddCoinsToDisplay(long delta, bool immediate = true)
        {
            if (delta <= 0 || SaveService.Data == null)
                return;

            CacheRollingTexts();
            if (_coinsRolling != null)
                _coinsRolling.AnimateTo(SaveService.Data.coins, immediate);
            else if (coinsText != null)
                coinsText.text = EconomyDisplayFormat.Format(SaveService.Data.coins);
        }

        public void RefreshHudMapShortcutLabel() => RefreshMapOrZoneLabel();

        void RefreshMapOrZoneLabel()
        {
            if (GameProductSettings.IsFishingContentEnabled && fishingZoneLabel != null)
            {
                var zone = _fishing?.CurrentZoneDef;
                fishingZoneLabel.text = zone != null ? zone.displayName ?? zone.zoneId : "";
                return;
            }

            if (!GameProductSettings.IsTowerContentEnabled || mapTitleText == null)
                return;

            var id = SaveService.Data?.currentMapId;
            if (ConfigRegistry.Maps != null && ConfigRegistry.Maps.TryGet(id, out var m) && m != null)
                mapTitleText.text = m.displayName ?? id ?? "";
            else
                mapTitleText.text = id ?? "";
        }

        public void SetHeight(float heightMeters)
        {
            if (heightText == null) return;
            heightText.text = $"当前: {Mathf.Max(0f, heightMeters):0}米";
        }

        public void SetStamina(float current, float max)
        {
            var n = max <= 0.0001f ? 0f : Mathf.Clamp01(current / max);
            if (staminaSlider != null) staminaSlider.value = n;
            if (staminaLabel != null)
            {
                var curInt = Mathf.Max(0, Mathf.RoundToInt(current));
                var maxInt = Mathf.Max(0, Mathf.RoundToInt(max));
                staminaLabel.text = $"{curInt}/{maxInt}";
            }
        }

        public void SetClimbOnlyHudVisible(bool visible)
        {
            if (heightText != null) heightText.gameObject.SetActive(visible);
            if (staminaSlider != null) staminaSlider.gameObject.SetActive(visible);
            if (staminaLabel != null) staminaLabel.gameObject.SetActive(visible);
            if (btnDoubleSpeed != null) btnDoubleSpeed.gameObject.SetActive(visible);
            if (btnAutoClimb != null) btnAutoClimb.gameObject.SetActive(visible);
            if (visible)
            {
                RefreshAutoClimbHud();
                TickBuffDisplays();
            }
            else
                ResetAutoClimbHudPresentation();
        }

        public void TickBuffDisplays()
        {
            var d = SaveService.Data;
            if (doubleSpeedTimerText != null)
            {
                var climbBuff = RewardBuffService.IsClimbBuffActive();
                doubleSpeedTimerText.text = climbBuff && d != null
                    ? RewardBuffService.FormatRemainingMmSs(d.climbBuffEndUtcTicks)
                    : RewardBuffService.HintDurationMmSs;
            }

            if (doubleCoinsTimerText != null)
            {
                var coinOn = RewardBuffService.IsCoinsBuffActive();
                doubleCoinsTimerText.text = coinOn && d != null
                    ? RewardBuffService.FormatRemainingMmSs(d.coinsBuffEndUtcTicks)
                    : RewardBuffService.HintDurationMmSs;
            }

            TickAutoFishingTimerDisplay();
        }

        void TickAutoFishingTimerDisplay()
        {
            if (!GameProductSettings.IsFishingContentEnabled)
                return;

            var active = AutoFishingService.IsPassActive;
            if (autoFishingTimerRoot != null)
                autoFishingTimerRoot.SetActive(active);

            if (autoFishingTimerText != null)
            {
                autoFishingTimerText.text = active
                    ? AutoFishingService.FormatRemainingMmSs(showHintWhenInactive: false)
                    : AutoFishingService.FormatRemainingMmSs(showHintWhenInactive: true);
            }
        }

        void OnAutoFishingPassChanged() => TickAutoFishingTimerDisplay();

        void OnAutoFishingModeChanged() => RefreshFishingHud();

        void RefreshAutoClimbHud()
        {
            _climb ??= UnityObjectFinder.FindFirst<ClimbController>();
            var on = _climb != null &&
                     _climb.State != ClimbController.ClimbState.Inactive &&
                     _climb.AutoAscendEnabled;
            if (autoClimbLabel != null)
                autoClimbLabel.text = on ? "自动攀爬中..." : "自动攀爬";
            if (autoClimbCheckmark != null)
                autoClimbCheckmark.SetActive(on);
        }

        void ResetAutoClimbHudPresentation()
        {
            if (autoClimbLabel != null) autoClimbLabel.text = "自动攀爬";
            if (autoClimbCheckmark != null) autoClimbCheckmark.SetActive(false);
        }

        public void RefreshStrengthDisplay()
        {
            if (strengthText == null && _strengthRolling == null)
                return;

            fishingStats?.Recompute();
            var power = fishingStats != null
                ? fishingStats.Current.totalPower
                : FishingStatsAggregator.ComputeTotalPower(0f, 0f);
            var powerInt = Mathf.Max(0, Mathf.RoundToInt(power));
            if (powerInt == _lastStrengthValue)
                return;

            _lastStrengthValue = powerInt;

            CacheRollingTexts();
            if (_strengthRolling != null)
                _strengthRolling.SetImmediate(powerInt);
            else if (strengthText != null)
                strengthText.text = EconomyDisplayFormat.Format(powerInt);
        }

        void RefreshFishingHud()
        {
            if (!GameProductSettings.IsFishingContentEnabled)
                return;

            if (_fishing == null)
            {
                EnsureFishingRefsResolved();
                WireFishingEvents();
            }

            var inZone = _fishing != null && _fishing.IsInsideFishingZone;
            var session = _fishing != null && _fishing.IsSessionActive;
            var gameplay = _fishing != null && _fishing.IsGameplayActive;

            if (btnFishingAction != null)
            {
                var showActionButton = inZone
                                       && !_blockFishingActionUntilCatchToast
                                       && (!session || CanShowFishingActionButton(session, gameplay));
                btnFishingAction.gameObject.SetActive(showActionButton);
            }

            if (fishingActiveRoot != null)
            {
                if (!gameplay && fishingActiveRoot.activeSelf)
                    ClearReelProgressPopups();
                fishingActiveRoot.SetActive(gameplay);
            }

            if (btnAutoFishing != null)
                btnAutoFishing.gameObject.SetActive(inZone);

            if (btnEndFishing != null)
                btnEndFishing.gameObject.SetActive(session);

            if (btnFishingAction != null && fishingActionLabel != null)
            {
                if (!session)
                    fishingActionLabel.text = "开始钓鱼";
                else if (gameplay)
                    fishingActionLabel.text = "抽杆";
                else
                    fishingActionLabel.text = "放杆中...";
            }

            RefreshReelHintVisibility(gameplay);

            if (gameplay && fishingProgressSlider != null)
            {
                var max = Mathf.Max(1f, _fishing.ProgressMax);
                _displayedProgressMax = max;
                ApplyFishingProgressDisplay(max);
            }

            if (autoFishingLabel != null)
            {
                if (!AutoFishingService.IsPassActive)
                    autoFishingLabel.text = "自动钓鱼";
                else if (AutoFishingService.IsModeEnabled)
                    autoFishingLabel.text = "自动钓鱼中...";
                else
                    autoFishingLabel.text = "自动钓鱼";
            }

            ResolveAutoFishingRewardIcon();
            if (autoFishingRewardIcon != null)
                autoFishingRewardIcon.SetActive(!AutoFishingService.IsModeEnabled);

            RefreshMapOrZoneLabel();
            RefreshStrengthOfferLabel();
        }

        void RefreshStrengthOfferLabel()
        {
            if (!GameProductSettings.IsFishingContentEnabled)
                return;

            ResolveStrengthLabel();
            if (strengthLabel == null)
                return;

            var bonus = Mathf.RoundToInt(FishingWorldAreaRewardService.ResolveStrengthBonus());
            if (bonus == _lastStrengthOfferValue)
                return;

            _lastStrengthOfferValue = bonus;
            strengthLabel.text = $"力量+{EconomyDisplayFormat.Format(bonus)}";
        }

        void TickFishingProgressDisplay()
        {
            if (_fishing == null || !_fishing.IsGameplayActive)
                return;

            var target = _fishing.ProgressCurrent;
            var max = Mathf.Max(1f, _fishing.ProgressMax);
            _displayedProgressMax = max;

            if (target < _displayedProgressCurrent - 0.01f)
                _displayedProgressCurrent = target;
            else if (target > _displayedProgressCurrent + 0.01f)
            {
                var speed = fishingProgressAnimUnitsPerSecond > 0f
                    ? fishingProgressAnimUnitsPerSecond
                    : max * 2.5f;
                _displayedProgressCurrent = Mathf.MoveTowards(
                    _displayedProgressCurrent,
                    target,
                    speed * Time.deltaTime);
            }

            ApplyFishingProgressDisplay(max);
        }

        void ApplyFishingProgressDisplay(float max)
        {
            if (fishingProgressSlider != null)
                fishingProgressSlider.value = Mathf.Clamp01(_displayedProgressCurrent / max);

            if (fishingProgressText != null)
            {
                var cur = Mathf.RoundToInt(_displayedProgressCurrent);
                var mx = Mathf.RoundToInt(max);
                fishingProgressText.text = $"{EconomyDisplayFormat.Format(cur)}/{EconomyDisplayFormat.Format(mx)}";
            }
        }

        void ResetFishingProgressDisplay()
        {
            _displayedProgressCurrent = 0f;
            _displayedProgressMax = 1f;
        }

        bool CanShowFishingActionButton(bool session, bool gameplay)
        {
            if (!session)
                return true;

            if (!gameplay)
                return false;

            return _fishing != null && _fishing.CanShowReelButton();
        }

        void RefreshReelHintVisibility(bool gameplay)
        {
            var hintRoot = ResolveReelHintRoot();
            if (hintRoot == null)
                return;

            var show = gameplay
                       && _fishing != null
                       && _fishing.CanShowReelButton()
                       && _fishing.ProgressCurrent <= 0.0001f;
            hintRoot.SetActive(show);

            if (show && fishingHintText != null && string.IsNullOrEmpty(fishingHintText.text))
                fishingHintText.text = "点击抽杆!";
        }

        void OnFishingSessionStarted(FishingSessionState _)
        {
            _blockFishingActionUntilCatchToast = false;
            ClearReelProgressPopups();
            ResetFishingProgressDisplay();
            RefreshFishingHud();
        }

        void OnFishingGameplayStarted()
        {
            ResetFishingProgressDisplay();
            RefreshFishingHud();
        }

        void OnFishingSessionCancelled()
        {
            PauseAutoFishingMode();
            ClearReelProgressPopups();
            ResetFishingProgressDisplay();
            RefreshFishingHud();
        }
        void OnReelApplied(FishingService.ReelResult reel)
        {
            if (reel.progressDelta > 0.0001f)
                AudioGameplayFeedback.PlayFishingProgressGain();
            ShowReelProgressPopup(reel.progressDelta);
            RefreshFishingHud();
        }

        void OnCatchCompleted(FishingService.CatchResult result)
        {
            ClearReelProgressPopups();
            ResetFishingProgressDisplay();
            if (result.success && result.fish != null)
                _blockFishingActionUntilCatchToast = true;
            RefreshFishingHud();
        }

        void ClearReelProgressPopups()
        {
            reelProgressFloater?.ClearAll();

            var anchor = ResolveReelFloatAnchor();
            if (anchor == null)
                return;

            for (var i = anchor.childCount - 1; i >= 0; i--)
            {
                var child = anchor.GetChild(i);
                if (child != null && child.GetComponent<FishingReelProgressPopupView>() != null)
                    Destroy(child.gameObject);
            }
        }

        void ShowReelProgressPopup(float delta)
        {
            if (delta <= 0.0001f)
                return;

            if (reelProgressFloater != null)
            {
                reelProgressFloater.SpawnProgressDelta(delta);
                return;
            }

            var anchor = ResolveReelFloatAnchor();
            var prefab = reelProgressPopupPrefab;
            if (anchor == null || prefab == null)
                return;

            var inst = Instantiate(prefab, anchor);
            var rt = inst.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(
                    UnityEngine.Random.Range(reelFloatOffsetMin.x, reelFloatOffsetMax.x),
                    UnityEngine.Random.Range(reelFloatOffsetMin.y, reelFloatOffsetMax.y));
                rt.localScale = Vector3.one;
            }

            inst.ShowDelta(delta);
        }

        RectTransform ResolveReelFloatAnchor()
        {
            if (reelProgressFloatAnchor != null)
                return reelProgressFloatAnchor;

            if (fishingProgressSlider != null)
                return fishingProgressSlider.transform.parent as RectTransform
                       ?? fishingProgressSlider.GetComponent<RectTransform>();

            return null;
        }

        #region Button handlers

        static bool IsMapAndEquipmentNavigationBlocked() =>
            GameplayNavigationBlock.BlocksMapAndEquipmentShops();

        static void ShowBlockedHint() => GameplayNavigationBlock.ShowShopNavigationBlockedHint();

        void OnSettingsClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenSettings();
        }

        void OnEconomyMoneyClicked()
        {
            if (GameplayNavigationBlock.ShouldBlockOpeningFreeCurrencyFromHud())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenCoinsHub();
        }

        void OnEconomyDiamondClicked()
        {
            if (GameplayNavigationBlock.ShouldBlockOpeningFreeCurrencyFromHud())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenFreeDiamondsOffer();
        }

        void OnMoneyBuffClicked()
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenDoubleSettlementCoinsOffer();
        }

        void OnSpeedBuffClicked()
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenDoubleClimbSpeedOffer();
        }

        void OnSignInClicked()
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenSignIn();
        }

        void OnLuckyWheelClicked()
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenLuckyWheel();
        }

        void OnOnlineRewardClicked()
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenOnlineReward();
        }

        void OnMapSelectClicked()
        {
            if (IsMapAndEquipmentNavigationBlocked())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenMapSelect();
        }

        void OnWingShopClicked() => OpenShopIfAllowed(UiRouter.OpenWingShop);
        void OnTrailShopClicked() => OpenShopIfAllowed(UiRouter.OpenTrailShop);
        void OnPetBackpackClicked() => OpenShopIfAllowed(UiRouter.OpenPet);
        void OnPetHatchClicked() => OpenShopIfAllowed(UiRouter.OpenPetHatch);
        void OnCharacterShopClicked() => OpenShopIfAllowed(UiRouter.OpenCharacterShop);
        void OnRodShopClicked() => OpenShopIfAllowed(UiRouter.OpenRodShop);
        void OnFishCodexClicked() => OpenShopIfAllowed(UiRouter.OpenFishCodex);

        static void OpenShopIfAllowed(Action open)
        {
            if (IsMapAndEquipmentNavigationBlocked())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            open?.Invoke();
        }

        void OnJumpClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            jumpInput?.QueueGlidePressFromUi();
        }

        void OnAutoClimbClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            ResolveClimbRefOnce();
            if (_climb == null || _climb.State == ClimbController.ClimbState.Inactive)
                return;
            _climb.AutoAscendEnabled = !_climb.AutoAscendEnabled;
            RefreshAutoClimbHud();
        }

        void OnFishingActionClicked()
        {
            EnsureFishingRefsResolved();
            WireFishingEvents();
            var gameplay = _fishing != null && _fishing.IsGameplayActive;
            if (!gameplay)
                AudioGameplayFeedback.PlayUiButton();
            fishingBridge?.OnFishingButtonPressed();
            RefreshFishingHud();
        }

        void OnEndFishingClicked()
        {
            EnsureFishingRefsResolved();
            AudioGameplayFeedback.PlayUiButton();
            PauseAutoFishingMode();
            fishingBridge?.OnCancelFishingPressed();
            RefreshFishingHud();
        }

        void OnTenCatchRewardClicked()
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            if (_fishing == null || !_fishing.IsGameplayActive)
                return;
            _fishing.SetCatchCoinMultiplier(10);
        }

        void ShowStrengthGain(float delta)
        {
            if (delta <= 0.0001f)
                return;

            ResolveStrengthFloater();
            strengthGainFloater?.AddGain(delta);
        }

        void OnStrengthClicked()
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                ShowBlockedHint();
                return;
            }
            AudioGameplayFeedback.PlayUiButton();
            FreeRewardGate.Grant(() =>
            {
                FishingStrengthRewardService.TryGrantStrength();
            });
        }

        void OnWormDodgeStaminaClicked()
        {
            if (!GameProductSettings.IsWormDodgeContentEnabled)
                return;

            AudioGameplayFeedback.PlayUiButton();
            if (WormDodge.WormDodgeStaminaService.IsFull)
            {
                AudioGameplayFeedback.PlayUiError();
                return;
            }

            FreeRewardGate.Grant(() =>
            {
                if (WormDodge.WormDodgeStaminaService.TryGrantStamina())
                    AudioGameplayFeedback.PlayResourceCollect();
                else
                    AudioGameplayFeedback.PlayUiError();

                RefreshWormDodgeStaminaDisplay();
            });
        }

        void OnAutoFishingClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            EnsureFishingRefsResolved();

            if (_fishing == null || !_fishing.IsInsideFishingZone)
                return;

            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                ShowBlockedHint();
                return;
            }

            if (!AutoFishingService.IsPassActive)
            {
                UiRouter.OpenAutoFishingOffer();
                return;
            }

            var enable = !AutoFishingService.IsModeEnabled;
            AutoFishingService.SetModeEnabled(enable);
            RefreshFishingHud();
        }

        static void PauseAutoFishingMode() => AutoFishingService.PauseMode();

        #endregion
    }
}
